-- PROCEDURE: bodyply.addupdatebodyplyconsumptioni_production(text)

-- DROP PROCEDURE IF EXISTS bodyply.addupdatebodyplyconsumptioni_production(text);

-- Consumption is grouped by material and feeder.
--   Same material on different feeders  -> PARALLEL, each feeder its own demand.
--   Several lots on the same feeder     -> SERIES, one demand shared, in the
--                                          order the rolls were loaded.
-- The demand is worked out in one place, marked DEMAND below, from the feeder
-- and the consumed item code. The BOM formula goes there.

CREATE OR REPLACE PROCEDURE bodyply.addupdatebodyplyconsumptioni_production(
	IN _json text,
	OUT result json)
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    -- Input parameters parsed from JSON
    p_productionid   text;
    p_recipename     text;
    p_quantitylength text;
    p_productionwidth text;
    p_username       text;
    p_mhecode        text;
    p_remark         text;
    p_winder         text;
    p_lastremark     text;
    p_machinename    text;
    p_synctime       timestamp;

    -- Machine
    v_equipmentid      int;
    v_localequipmentid int;

    -- Recipe
    mesrecipename   text;
    produceitemname text;

    -- Current material / feeder / lot
    p_itemname    text;
    v_materialid  text;
    v_feedercode  text;

    v_lotrowid    int;
    v_lotid       text;
    v_lotqty      numeric(18,6);
    v_liveqty     numeric(18,6);
    v_scandate    timestamp;
    v_prodmachine text;
    v_proddate    timestamp;
    v_produser    text;

    -- Quantities
    v_demand     numeric(18,6);
    v_remaining  numeric(18,6);
    v_take       numeric(18,6);

    -- Counters
    cntmaterial     int := 0;
    cntmaterialstop int := 0;
    cntfeeder       int := 0;
    cntfeederstop   int := 0;
    cntlot          int := 0;
    cntlotstop      int := 0;

    consumedcount  int := 0;
    shortfallcount int := 0;
    shortfalltext  text := '';
BEGIN
    -- Extract parameters from JSON
    p_productionid    := (_json::json ->> 'productionID');
    p_recipename      := (_json::json ->> 'itemCode');
    p_quantitylength  := (_json::json ->> 'productionQuantityLength');
    p_productionwidth := (_json::json ->> 'progressWidth');
    p_username        := (_json::json ->> 'userName');
    p_mhecode         := (_json::json ->> 'mHECode');
    p_remark          := (_json::json ->> 'remark');
    p_winder          := (_json::json ->> 'winder');
    p_lastremark      := (_json::json ->> 'lastRemark');
    p_machinename     := (_json::json ->> 'machinename');
    p_synctime        := (_json::json ->> 'syncTime')::timestamp;

    -- Temporary tables
    DROP TABLE IF EXISTS temp_tbl;
    CREATE TEMP TABLE temp_tbl(
        id serial primary key,
        item text
    ) ON COMMIT DROP;

    DROP TABLE IF EXISTS temp_tbl1;
    CREATE TEMP TABLE temp_tbl1(
        id serial primary key,
        feeder_code text
    ) ON COMMIT DROP;

    DROP TABLE IF EXISTS temp_tbl2;
    CREATE TEMP TABLE temp_tbl2(
        id serial primary key,
        lotrowid int,
        lot_id text,
        quantity numeric(18,6),
        live_quantity numeric(18,6),
        sequence_no int,
        dtandtime timestamp,
        producedmachinename text,
        produceddatetime timestamp,
        producedusername text
    ) ON COMMIT DROP;

    -------------------------------------------------------------------------
    -- Find Equipment Id
    -- i_material.equipment_id is written from local_equipment_id, so lots are
    -- matched on that. Consumption rows keep carrying id, as before.
    -------------------------------------------------------------------------
    SELECT e.id, e.local_equipment_id
      INTO v_equipmentid, v_localequipmentid
      FROM master.equipment_master e
     WHERE e.name = p_machinename
     LIMIT 1;

    RAISE NOTICE 'machine % equipmentid % localequipmentid %',
                 p_machinename, v_equipmentid, v_localequipmentid;

    IF v_equipmentid IS NULL THEN
        result := json_build_object(
            'status',  'fail',
            'code',    'machine_not_found',
            'message', 'Machine not available in equipment master.'
        );
        RETURN;
    END IF;

    -------------------------------------------------------------------------
    -- Get BOM mapping
    -------------------------------------------------------------------------
    SELECT b.formulacode, b.materialcode_o
      INTO mesrecipename, produceitemname
      FROM dbo.bom b
     WHERE b.plcbomname = p_recipename
     LIMIT 1;

    IF mesrecipename IS NULL THEN
        result := json_build_object(
            'status',  'fail',
            'code',    'recipe_not_found',
            'message', 'Recipe not available in BOM or not released.'
        );
        RETURN;
    END IF;

    -------------------------------------------------------------------------
    -- Already recorded for this production
    -------------------------------------------------------------------------
    SELECT COUNT(*) INTO cntmaterialstop
      FROM bodyply.i_production_consumption
     WHERE production_id = p_productionid;

    IF cntmaterialstop > 0 THEN
        result := json_build_object(
            'status',  'skipped',
            'code',    'already_recorded',
            'message', 'Consumption for this production is already recorded.'
        );
        RETURN;
    END IF;

    -------------------------------------------------------------------------
    -- Insert material group items
    -------------------------------------------------------------------------
    INSERT INTO temp_tbl(item)
    SELECT DISTINCT b.materialcodegroup_in
      FROM dbo.bom b
     WHERE b.formulacode = mesrecipename
       AND b.materialcodegroup_in IS NOT NULL;

    SELECT COUNT(*) INTO cntmaterialstop FROM temp_tbl;
    RAISE NOTICE 'input materials %', cntmaterialstop;

    cntmaterial := 0;

    -------------------------------------------------------------------------
    -- Loop 1 : every input material on the recipe
    -------------------------------------------------------------------------
    WHILE (cntmaterial < cntmaterialstop) LOOP

        SELECT t.item INTO p_itemname
          FROM temp_tbl t WHERE t.id = cntmaterial + 1;

        -- Get Material id against p_itemname
        SELECT i.itemid INTO v_materialid
          FROM dbo.tblitemmaster i
         WHERE i.itemcode = p_itemname AND i.iscurrent = 1
         LIMIT 1;

        RAISE NOTICE 'material % materialid %', p_itemname, v_materialid;

        IF v_materialid IS NULL THEN
            shortfallcount := shortfallcount + 1;
            shortfalltext  := shortfalltext || p_itemname || ' not in item master; ';
        ELSE
            -----------------------------------------------------------------
            -- Feeders carrying this material on this machine
            -----------------------------------------------------------------
            TRUNCATE temp_tbl1 RESTART IDENTITY;

            INSERT INTO temp_tbl1(feeder_code)
            SELECT s.feeder_code FROM (
                SELECT DISTINCT m.feeder_code
                  FROM bodyply.i_material m
                 WHERE m.equipment_id = v_localequipmentid
                   AND m.material_id  = v_materialid
                   AND m.sequence_no  > 0
                 ORDER BY m.feeder_code
            ) s;

            SELECT COUNT(*) INTO cntfeederstop FROM temp_tbl1;
            RAISE NOTICE 'feeders for % : %', p_itemname, cntfeederstop;

            cntfeeder := 0;

            -------------------------------------------------------------
            -- Loop 2 : PARALLEL, each feeder carries its own demand
            -------------------------------------------------------------
            WHILE (cntfeeder < cntfeederstop) LOOP

                SELECT f.feeder_code INTO v_feedercode
                  FROM temp_tbl1 f WHERE f.id = cntfeeder + 1;

                ---------------------------------------------------------
                -- DEMAND
                --
                -- Worked out for this feeder and this consumed item code,
                -- and nowhere else. The BOM formula goes here. Available:
                --   v_feedercode      the feeder
                --   p_itemname        the consumed item code
                --   mesrecipename     the formula code
                --   p_quantitylength  the length produced
                --   p_productionwidth the width
                --
                -- Until the formula is added it is the produced length.
                ---------------------------------------------------------
                v_demand := COALESCE(p_quantitylength, '0')::numeric;

                v_remaining := v_demand;
                RAISE NOTICE 'feeder % demand %', v_feedercode, v_demand;

                ---------------------------------------------------------
                -- Lots on this feeder, in the order they were loaded
                ---------------------------------------------------------
                TRUNCATE temp_tbl2 RESTART IDENTITY;

                INSERT INTO temp_tbl2(lotrowid, lot_id, quantity, live_quantity,
                                      sequence_no, dtandtime, producedmachinename,
                                      produceddatetime, producedusername)
                SELECT s.id, s.lot_id, s.quantity, s.live_quantity,
                       s.sequence_no, s.dtandtime, s.producedmachinename,
                       s.produceddatetime, s.producedusername
                  FROM (
                    SELECT m.id, m.lot_id, m.quantity, m.live_quantity,
                           m.sequence_no, m.dtandtime, m.producedmachinename,
                           m.produceddatetime, m.producedusername
                      FROM bodyply.i_material m
                     WHERE m.equipment_id = v_localequipmentid
                       AND m.material_id  = v_materialid
                       AND m.feeder_code  = v_feedercode
                       AND m.sequence_no  > 0
                     ORDER BY m.sequence_no ASC
                  ) s;

                SELECT COUNT(*) INTO cntlotstop FROM temp_tbl2;
                cntlot := 0;

                -----------------------------------------------------
                -- Loop 3 : SERIES, the lots share this one demand
                -----------------------------------------------------
                WHILE (cntlot < cntlotstop AND v_remaining > 0) LOOP

                    SELECT l.lotrowid, l.lot_id, l.quantity, l.live_quantity,
                           l.dtandtime, l.producedmachinename,
                           l.produceddatetime, l.producedusername
                      INTO v_lotrowid, v_lotid, v_lotqty, v_liveqty,
                           v_scandate, v_prodmachine, v_proddate, v_produser
                      FROM temp_tbl2 l WHERE l.id = cntlot + 1;

                    v_take := LEAST(v_remaining, v_liveqty);

                    IF v_take IS NOT NULL AND v_take > 0 THEN

                        RAISE NOTICE 'lot % take % of remaining %',
                                     v_lotid, v_take, v_remaining;

                        INSERT INTO bodyply.i_production_consumption(
                            equipment_id, production_id, consumption_production_id,
                            consumption_material_id, consumed_qty, uom,
                            validation_mode, dtandtime)
                        VALUES(
                            v_equipmentid, p_productionid, v_lotid,
                            v_materialid, v_take, 'M',
                            'true', p_synctime);

                        INSERT INTO dbo.mi_transactions(
                            productionid, itemcode, itempqty, itempuom,
                            itemsqty, itemsuom, locationcode, machinename,
                            dtandtime, consumeitemcode, consumeproductionid, consumepqty,
                            consumepuom, consumesqty, consumesuom, itemstatus,
                            remark, qastatus, username, consumelotqty, consumemachinename,
                            consumematerialproductiondate, consumematerialusername,
                            scanmaterialdatetime)
                        VALUES(
                            p_productionid,
                            mesrecipename,
                            COALESCE(p_quantitylength, '0')::numeric,
                            'M',
                            COALESCE(p_quantitylength, '0')::numeric,
                            'M',
                            'BodyPly',
                            p_machinename,
                            p_synctime,
                            p_itemname,
                            v_lotid,
                            v_take,
                            'M',
                            v_take,
                            'M',
                            0,
                            COALESCE(p_remark, ''),
                            '',
                            p_username,
                            v_lotqty,
                            v_prodmachine,
                            v_proddate,
                            v_produser,
                            v_scandate);

                        UPDATE bodyply.i_material
                           SET live_quantity      = live_quantity - v_take,
                               modified_dtandtime = NOW(),
                               sequence_no        = CASE
                                                      WHEN live_quantity - v_take <= 0
                                                      THEN 0 ELSE sequence_no
                                                    END
                         WHERE id = v_lotrowid;

                        consumedcount := consumedcount + 1;
                        v_remaining   := v_remaining - v_take;
                    END IF;

                    cntlot := cntlot + 1;
                END LOOP;

                -- What the loaded rolls could not cover
                IF v_remaining > 0 THEN
                    shortfallcount := shortfallcount + 1;
                    shortfalltext  := shortfalltext || p_itemname || ' on ' ||
                                      v_feedercode || ' short by ' ||
                                      v_remaining::text || '; ';
                    RAISE NOTICE 'shortfall % on % : %',
                                 p_itemname, v_feedercode, v_remaining;
                END IF;

                cntfeeder := cntfeeder + 1;
            END LOOP;
        END IF;

        cntmaterial := cntmaterial + 1;
    END LOOP;

    -------------------------------------------------------------------------
    -- Return result JSON
    -------------------------------------------------------------------------
    IF consumedcount = 0 AND shortfallcount = 0 THEN
        result := json_build_object(
            'status',       'fail',
            'code',         'no_material_loaded',
            'message',      'No material is loaded against this machine for the recipe.',
            'productionid', p_productionid,
            'timestamp',    now()
        );
    ELSIF shortfallcount > 0 THEN
        result := json_build_object(
            'status',        'partial',
            'code',          'shortfall',
            'message',       shortfalltext,
            'productionid',  p_productionid,
            'consumedrows',  consumedcount,
            'shortfallrows', shortfallcount,
            'timestamp',     now()
        );
    ELSE
        result := json_build_object(
            'status',       'success',
            'code',         'consumed',
            'message',      'Material consumed successfully',
            'productionid', p_productionid,
            'consumedrows', consumedcount,
            'timestamp',    now()
        );
    END IF;

EXCEPTION
    WHEN OTHERS THEN
        result := json_build_object(
            'status',  'error',
            'code',    'exception',
            'message', SQLERRM
        );
END;
$BODY$;
ALTER PROCEDURE bodyply.addupdatebodyplyconsumptioni_production(text)
    OWNER TO postgres;
