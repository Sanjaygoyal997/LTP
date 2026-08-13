-- =============================================================================
-- bodyply.addupdatebodyplyconsumptioni_production
--
-- Records what a finished body ply production consumed, and decrements the live
-- quantity on the rolls it came from.
--
-- Consumption is grouped by material and feeder:
--
--   the same material on different feeders is consumed in PARALLEL, meaning
--   each feeder carries its own demand and never draws from a shared pool
--
--   several lots of the same material on one feeder are consumed in SERIES,
--   meaning those lots share one demand, oldest first by sequence_no, and the
--   next roll only starts once the previous is exhausted
--
-- The demand for a feeder is worked out in one place, marked DEMAND below, from
-- the feeder and the consumed item code. It is the production length as
-- recorded until the bill of materials formula is added there.
--
-- Input (_json):
--   productionID              the production being recorded
--   itemCode                  the recipe read from the PLC, matched to plcbomname
--   productionQuantityLength  length produced
--   progressWidth             width, for the formula
--   userName, syncTime, mHECode, remark, winder, lastRemark
--   machinename               matched against master.equipment_master.name
--
-- Output (result):
--   status   success | partial | fail | skipped | error
--   code     machine readable reason
--   consumed  what was taken, one entry per lot
--   shortfall what could not be covered, one entry per feeder
-- =============================================================================
CREATE OR REPLACE PROCEDURE bodyply.addupdatebodyplyconsumptioni_production(
	IN _json text,
	OUT result json)
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    -- Inputs
    p_productionid             text;
    p_recipename               text;
    p_productionquantitylength text;
    p_productionwidth          text;
    p_username                 text;
    p_mhecode                  text;
    p_remark                   text;
    p_winder                   text;
    p_lastremark               text;
    p_machinename              text;
    p_synctime                 timestamp;

    -- Machine
    -- Two ids are held. i_material.equipment_id is written from
    -- local_equipment_id, so lots are matched on that. Consumption rows keep
    -- carrying id, which is what this procedure has always written.
    v_equipmentid       int;
    v_localequipmentid  int;

    -- Recipe
    v_mesrecipename  text;
    v_produceitem    text;

    -- Working
    v_materialid   text;
    v_demand       numeric(18,6);
    v_remaining    numeric(18,6);
    v_take         numeric(18,6);
    v_consumed     jsonb := '[]'::jsonb;
    v_shortfall    jsonb := '[]'::jsonb;
    v_anyconsumed  boolean := false;

    r_material record;
    r_feeder   record;
    r_lot      record;
BEGIN
    -------------------------------------------------------------------------
    -- Inputs
    -------------------------------------------------------------------------
    p_productionid             := (_json::json ->> 'productionID');
    p_recipename               := (_json::json ->> 'itemCode');
    p_productionquantitylength := (_json::json ->> 'productionQuantityLength');
    p_productionwidth          := (_json::json ->> 'progressWidth');
    p_username                 := (_json::json ->> 'userName');
    p_mhecode                  := (_json::json ->> 'mHECode');
    p_remark                   := (_json::json ->> 'remark');
    p_winder                   := (_json::json ->> 'winder');
    p_lastremark               := (_json::json ->> 'lastRemark');
    p_machinename              := (_json::json ->> 'machinename');
    p_synctime                 := (_json::json ->> 'syncTime')::timestamp;

    -------------------------------------------------------------------------
    -- The machine
    --
    -- Reported rather than defaulted. A default meant a name that did not
    -- resolve recorded the whole production against another machine.
    -------------------------------------------------------------------------
    SELECT e.id, e.local_equipment_id
      INTO v_equipmentid, v_localequipmentid
      FROM master.equipment_master e
     WHERE e.name = p_machinename
     LIMIT 1;

    IF v_equipmentid IS NULL THEN
        result := json_build_object(
            'status',  'fail',
            'code',    'machine_not_found',
            'message', 'Machine ' || coalesce(p_machinename, '') || ' is not in the equipment master.'
        );
        RETURN;
    END IF;

    -------------------------------------------------------------------------
    -- The recipe
    -------------------------------------------------------------------------
    SELECT b.formulacode, b.materialcode_o
      INTO v_mesrecipename, v_produceitem
      FROM dbo.bom b
     WHERE b.plcbomname = p_recipename
     LIMIT 1;

    IF v_mesrecipename IS NULL THEN
        result := json_build_object(
            'status',  'fail',
            'code',    'recipe_not_found',
            'message', 'Recipe ' || coalesce(p_recipename, '') || ' is not available in the bill of materials.'
        );
        RETURN;
    END IF;

    -------------------------------------------------------------------------
    -- Already recorded
    --
    -- A production resubmitted after a timeout used to consume a second time.
    -------------------------------------------------------------------------
    IF EXISTS (SELECT 1 FROM bodyply.i_production_consumption
                WHERE production_id = p_productionid) THEN
        result := json_build_object(
            'status',  'skipped',
            'code',    'already_recorded',
            'message', 'Consumption for this production is already recorded.'
        );
        RETURN;
    END IF;

    -------------------------------------------------------------------------
    -- Every input material on the recipe
    -------------------------------------------------------------------------
    FOR r_material IN
        SELECT DISTINCT b.materialcodegroup_in AS itemcode
          FROM dbo.bom b
         WHERE b.formulacode = v_mesrecipename
           AND b.materialcodegroup_in IS NOT NULL
    LOOP
        SELECT i.itemid
          INTO v_materialid
          FROM dbo.tblitemmaster i
         WHERE i.itemcode = r_material.itemcode
           AND i.iscurrent = 1
         LIMIT 1;

        --An item with no current master row is reported, not turned into
        --material 1, which is what the previous coalesce did.
        IF v_materialid IS NULL THEN
            v_shortfall := v_shortfall || jsonb_build_object(
                'itemcode', r_material.itemcode,
                'feeder',   null,
                'reason',   'item_master_missing');
            CONTINUE;
        END IF;

        ---------------------------------------------------------------------
        -- PARALLEL: each feeder carrying this material has its own demand
        ---------------------------------------------------------------------
        FOR r_feeder IN
            SELECT DISTINCT m.feeder_code
              FROM bodyply.i_material m
             WHERE m.equipment_id = v_localequipmentid
               AND m.material_id  = v_materialid
               AND m.sequence_no  > 0
             ORDER BY m.feeder_code
        LOOP
            -----------------------------------------------------------------
            -- DEMAND
            --
            -- Worked out for this feeder and this consumed item code, and
            -- nowhere else. The bill of materials formula goes here. In scope
            -- for it: r_feeder.feeder_code, r_material.itemcode,
            -- v_mesrecipename, p_productionquantitylength, p_productionwidth.
            --
            -- Until then the demand is the production length as recorded,
            -- which is what the procedure has always consumed.
            -----------------------------------------------------------------
            v_demand := coalesce(p_productionquantitylength, '0')::numeric;

            v_remaining := v_demand;

            -----------------------------------------------------------------
            -- SERIES: the lots on this feeder share that demand, in the order
            -- they were loaded. sequence_no is the queue position assigned
            -- when the roll was scanned, so it is what puts them in order.
            -- Ordering by modified_dtandtime sent the roll just consumed to
            -- the back and left every roll partly used.
            -----------------------------------------------------------------
            FOR r_lot IN
                SELECT m.id, m.lot_id, m.production_id, m.quantity, m.live_quantity,
                       m.sequence_no, m.dtandtime, m.producedmachinename,
                       m.produceddatetime, m.producedusername
                  FROM bodyply.i_material m
                 WHERE m.equipment_id = v_localequipmentid
                   AND m.material_id  = v_materialid
                   AND m.feeder_code  = r_feeder.feeder_code
                   AND m.sequence_no  > 0
                 ORDER BY m.sequence_no ASC
                 FOR UPDATE
            LOOP
                EXIT WHEN v_remaining <= 0;

                v_take := LEAST(v_remaining, r_lot.live_quantity);
                CONTINUE WHEN v_take IS NULL OR v_take <= 0;

                INSERT INTO bodyply.i_production_consumption(
                    equipment_id, production_id, consumption_production_id,
                    consumption_material_id, consumed_qty, uom, validation_mode, dtandtime)
                VALUES (
                    v_equipmentid, p_productionid, r_lot.lot_id,
                    v_materialid, v_take, 'M', 'true', p_synctime);

                --The five consume columns are filled from the lot being taken.
                --They were left null because the table meant to carry them was
                --created and never written to.
                INSERT INTO dbo.mi_transactions(
                    productionid, itemcode, itempqty, itempuom,
                    itemsqty, itemsuom, locationcode, machinename,
                    dtandtime, consumeitemcode, consumeproductionid, consumepqty,
                    consumepuom, consumesqty, consumesuom, itemstatus,
                    remark, qastatus, username, consumelotqty, consumemachinename,
                    consumematerialproductiondate, consumematerialusername, scanmaterialdatetime)
                VALUES (
                    p_productionid,
                    v_mesrecipename,
                    coalesce(p_productionquantitylength, '0')::numeric,
                    'M',
                    coalesce(p_productionquantitylength, '0')::numeric,
                    'M',
                    'BodyPly',
                    p_machinename,
                    p_synctime,
                    r_material.itemcode,
                    r_lot.lot_id,
                    v_take,
                    'M',
                    v_take,
                    'M',
                    0,
                    coalesce(p_remark, ''),
                    '',
                    p_username,
                    r_lot.quantity,
                    r_lot.producedmachinename,
                    r_lot.produceddatetime,
                    r_lot.producedusername,
                    r_lot.dtandtime);

                --A roll is finished at or below zero. Testing for exactly zero
                --left a residue live and unusable, since validation already
                --treats a small remainder as consumed.
                UPDATE bodyply.i_material
                   SET live_quantity      = live_quantity - v_take,
                       modified_dtandtime = now(),
                       sequence_no        = CASE WHEN live_quantity - v_take <= 0
                                                 THEN 0 ELSE sequence_no END
                 WHERE id = r_lot.id;

                v_consumed := v_consumed || jsonb_build_object(
                    'itemcode', r_material.itemcode,
                    'feeder',   r_feeder.feeder_code,
                    'lot',      r_lot.lot_id,
                    'qty',      v_take);

                v_anyconsumed := true;
                v_remaining   := v_remaining - v_take;
            END LOOP;

            --What the loaded rolls could not cover is reported rather than
            --dropped, which is what happened when only one lot was consulted.
            IF v_remaining > 0 THEN
                v_shortfall := v_shortfall || jsonb_build_object(
                    'itemcode', r_material.itemcode,
                    'feeder',   r_feeder.feeder_code,
                    'qty',      v_remaining,
                    'reason',   'insufficient_stock');
            END IF;
        END LOOP;
    END LOOP;

    -------------------------------------------------------------------------
    -- Outcome
    -------------------------------------------------------------------------
    IF NOT v_anyconsumed AND jsonb_array_length(v_shortfall) = 0 THEN
        result := json_build_object(
            'status',       'fail',
            'code',         'no_material_loaded',
            'message',      'No material is loaded against this machine for the recipe.',
            'productionid', p_productionid,
            'timestamp',    now());
    ELSIF jsonb_array_length(v_shortfall) > 0 THEN
        result := json_build_object(
            'status',       'partial',
            'code',         'shortfall',
            'message',      'Some feeders could not be covered by the loaded material.',
            'productionid', p_productionid,
            'consumed',     v_consumed,
            'shortfall',    v_shortfall,
            'timestamp',    now());
    ELSE
        result := json_build_object(
            'status',       'success',
            'code',         'consumed',
            'productionid', p_productionid,
            'consumed',     v_consumed,
            'timestamp',    now());
    END IF;

EXCEPTION
    WHEN OTHERS THEN
        result := json_build_object(
            'status',  'error',
            'code',    'exception',
            'message', SQLERRM);
END;
$BODY$;

ALTER PROCEDURE bodyply.addupdatebodyplyconsumptioni_production(text)
    OWNER TO postgres;
