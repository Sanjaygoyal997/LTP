-- =============================================================================
-- bodyply.add_update_i_material
--
-- Records a scanned material against a feeder.
--
-- Input (_json):
--   productionId  the scanned lot / production id
--   qty           quantity
--   itemCode      the scanned item code
--   feeder        the extruder name, written to i_material.feeder_code
--   machineName   machine the scan was made on
--   machineCode   matched against master.equipment_master.name
--   userId        matched against dbo.userdetails.username
--   syncStatus    sync flag
--   equipmentId   optional, the machine the scan was made on
--   sequenceNo    optional, the feeder position the scan was made at
--
-- The production record lives in a different schema depending on where the
-- material was made: the let off takes calendered roll from the four roll
-- calender, the strip feeders take slitted material from the multi slitter.
-- The schema is read from dbo.bomextrudermapping.schemaname using equipmentId
-- and sequenceNo, so no feeder name is named here.
--
-- equipmentId and sequenceNo are optional. Without them, or without a schema
-- recorded on the mapping, every known schema is searched instead, because the
-- lot exists in exactly one of them.
--
-- Output (result):
--   status   1 on success, 0 when the lot was not found
--   message  text for the operator
-- =============================================================================
CREATE OR REPLACE PROCEDURE bodyply.add_update_i_material(
	IN _json text,
	OUT result json)
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    _productionid   varchar(300);
    _qty            varchar(300);
    _itemcode       varchar(300);
    _feedercode     varchar(300);
    _machinename    varchar(300);
    _machinecode    varchar(300);
    _userid         varchar(300);
    _syncstatus     varchar(300);
    _equipmentid    text;
    _sequenceno     text;

    _count int;
    _countfrom int := 0;
    _countto int := 0;
    _lono varchar(100);
    _producedusername varchar(100);
    _producedmachinename varchar(100);
    _produceddatetime timestamp;

    _equipment_id int := 0;
    _user_id int := 0;
    _schemaname text;
    v_materialId text;
BEGIN
    -- Extract parameters from JSON
    _productionid   := (_json::json ->> 'productionId');
    _qty            := (_json::json ->> 'qty');
    _itemcode       := (_json::json ->> 'itemCode');
    _feedercode     := (_json::json ->> 'feeder');
    _machinename    := (_json::json ->> 'machineName');
    _machinecode    := (_json::json ->> 'machineCode');
    _userid         := (_json::json ->> 'userId');
    _syncstatus     := (_json::json ->> 'syncStatus');
    _equipmentid    := (_json::json ->> 'equipmentId');
    _sequenceno     := (_json::json ->> 'sequenceNo');

    --Dropped first, so a second call inside one transaction does not fail on a
    --table the previous call left behind.
    DROP TABLE IF EXISTS temp_tbl;

    CREATE TEMP TABLE temp_tbl (
        id serial PRIMARY KEY,
        productionid varchar(100),
        qty varchar(30),
        itemcode varchar(100),
        machinename varchar(100),
        dtandtime timestamp,
        username varchar(30),
        producedusername varchar(100),
        producedmachinename varchar(100),
        produceddatetime timestamp
    ) ON COMMIT DROP;

    SELECT itemid INTO v_materialId
      FROM dbo.tblitemmaster
     WHERE itemcode = _itemcode AND iscurrent = 1
     LIMIT 1;

    -------------------------------------------------------------------------
    -- Which schema holds the production record for this feeder
    --
    -- Read from the mapping rather than decided from the feeder name. A name
    -- outside the list the previous version carried silently recorded nothing
    -- and still reported success.
    -------------------------------------------------------------------------
    IF _equipmentid IS NOT NULL AND _sequenceno IS NOT NULL
       AND _equipmentid ~ '^[0-9]+$' AND _sequenceno ~ '^[0-9]+$' THEN

        SELECT b.schemaname
          INTO _schemaname
          FROM dbo.bomextrudermapping b
         WHERE b.equipmentid = _equipmentid::int
           AND b.sequenceno  = _sequenceno::int
           AND b.isactive = true
           AND b.schemaname IS NOT NULL
           AND b.schemaname <> ''
         LIMIT 1;
    END IF;

    -------------------------------------------------------------------------
    -- Collect every production record making up the lot
    --
    -- The identifier goes through format with %I, so a value that is not a
    -- schema fails to resolve rather than being executed, and the lot and item
    -- stay bound parameters.
    -------------------------------------------------------------------------
    IF _schemaname IS NOT NULL THEN
        EXECUTE format(
            'SELECT lot_id FROM %I.o_production WHERE production_id = $1 LIMIT 1',
            _schemaname)
           INTO _lono
          USING _productionid;

        EXECUTE format(
            'INSERT INTO temp_tbl (productionid, qty, itemcode, machinename, dtandtime, '
            '                      username, producedmachinename, produceddatetime, producedusername) '
            'SELECT p.production_id, p.quantity, $1, em.name, now(), '
            '       p.user_id1, em.name, p.dtandtime, p.user_id1 '
            '  FROM %I.o_production p '
            '  INNER JOIN master.equipment_master em ON p.equipment_id = em.local_equipment_id '
            ' WHERE p.lot_id = $2', _schemaname)
          USING _itemcode, _lono;
    ELSE
        SELECT p.lot_id
          INTO _lono
          FROM ( SELECT production_id, lot_id FROM frc.o_production
                 UNION ALL
                 SELECT production_id, lot_id FROM multislitter.o_production ) p
         WHERE p.production_id = _productionid
         LIMIT 1;

        INSERT INTO temp_tbl (productionid, qty, itemcode, machinename, dtandtime,
                              username, producedmachinename, produceddatetime, producedusername)
        SELECT p.production_id, p.quantity, _itemcode, em.name, now(),
               p.user_id1, em.name, p.dtandtime, p.user_id1
          FROM ( SELECT production_id, lot_id, quantity, dtandtime, user_id1, equipment_id
                   FROM frc.o_production
                  UNION ALL
                 SELECT production_id, lot_id, quantity, dtandtime, user_id1, equipment_id
                   FROM multislitter.o_production ) p
          INNER JOIN master.equipment_master em ON p.equipment_id = em.local_equipment_id
         WHERE p.lot_id = _lono;
    END IF;

    SELECT count(*) INTO _countto FROM temp_tbl;

    --Nothing to record is reported rather than passed off as a success, which
    --is what hid a feeder name that matched no branch.
    IF _countto = 0 THEN
        result := json_agg(json_build_object(
            'status',  '0',
            'message', 'No production record found for this lot.'
        ));
        RETURN;
    END IF;

    SELECT local_equipment_id INTO _equipment_id
      FROM master.equipment_master WHERE name = _machinecode LIMIT 1;

    SELECT userid INTO _user_id
      FROM dbo.userdetails WHERE username = _userid LIMIT 1;

    -------------------------------------------------------------------------
    -- Record each one
    -------------------------------------------------------------------------
    WHILE (_countfrom < _countto) LOOP
        SELECT productionid, qty, itemcode, producedusername, producedmachinename, produceddatetime
          INTO _productionid, _qty, _itemcode, _producedusername, _producedmachinename, _produceddatetime
          FROM temp_tbl WHERE id = _countfrom + 1;

        --Scoped to the equipment, because the update below is. Testing without
        --it meant a lot already recorded on one machine took the update branch
        --on the other, where it matched nothing and wrote nothing.
        IF NOT EXISTS (SELECT 1 FROM bodyply.i_material
                        WHERE lot_id = _productionid
                          AND equipment_id = _equipment_id) THEN

            SELECT COALESCE(MAX(sequence_no), 0) + 1 INTO _count
              FROM bodyply.i_material
             WHERE material_id = v_materialId AND live_quantity > 0;

            INSERT INTO bodyply.i_material (
                equipment_id, feeder_code, production_id, lot_id, material_id, quantity,
                live_quantity, uom, sequence_no, user_id, dtandtime, modified_dtandtime,
                islive, load_status, blending_percentage, producedmachinename,
                produceddatetime, producedusername)
            VALUES (
                _equipment_id, _feedercode, _productionid, _productionid, v_materialId,
                cast(_qty as numeric), cast(_qty as numeric), 'm', _count,
                _user_id, now(), now(), 'true', 1, '50', _producedmachinename,
                _produceddatetime, _producedusername);
        ELSE
            SELECT COALESCE(MAX(sequence_no), 0) + 1 INTO _count
              FROM bodyply.i_material
             WHERE material_id = v_materialId AND live_quantity > 0 AND sequence_no > 0;

            UPDATE bodyply.i_material
               SET modified_dtandtime = now(),
                   sequence_no = _count,
                   user_id = _user_id,
                   feeder_code = _feedercode
             WHERE lot_id ILIKE _productionid
               AND equipment_id = _equipment_id;
        END IF;

        _countfrom := _countfrom + 1;
    END LOOP;

    result := json_agg(json_build_object(
        'status',  '1',
        'message', 'Material Inserted successfully'
    ));
END;
$BODY$;

ALTER PROCEDURE bodyply.add_update_i_material(text)
    OWNER TO postgres;
