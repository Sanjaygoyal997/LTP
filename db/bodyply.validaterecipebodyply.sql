-- =============================================================================
-- bodyply.validaterecipebodyply
--
-- Validates a scanned material before it is recorded against a feeder.
--
-- Input (_json):
--   recipe        the recipe read from the PLC, matched to dbo.bom.plcbomname
--   itemName      the scanned item code
--   productionId  the scanned lot / production id
--   equipmentId   optional, the machine the scan was made on
--   sequenceNo    optional, the feeder position the scan was made at
--
-- equipmentId and sequenceNo are optional so that callers which do not send
-- them keep working unchanged. When both are present the scanned material is
-- also checked against dbo.bomextrudermapping, which is what decides whether
-- the material belongs on that particular feeder.
--
-- Output (result):
--   status   success | fail | confirm
--   code     machine readable reason, present on every response
--   message  text for the operator
--
-- Callers should read status and code. The message is for display only and is
-- expected to change.
-- =============================================================================
CREATE OR REPLACE PROCEDURE bodyply.validaterecipebodyply(
	IN _json text,
	OUT result json)
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    -- Inputs
    _recipename    text := (_json::json ->> 'recipe');
    _itemname      text := (_json::json ->> 'itemName');
    _productionid  text := (_json::json ->> 'productionId');
    _equipmentid   text := (_json::json ->> 'equipmentId');
    _sequenceno    text := (_json::json ->> 'sequenceNo');

    -- Working vars
    _mesrecipename     text;
    _materialgroup_in  text;
    _count             int  := 0;

    _maxaging      int;
    _maxagingunit  int;
    _minaging      int;
    _minagingunit  int;
    _addedage      int  := 0;
    _minaddedage   int  := 0;
    _itemmasterrow int  := 0;

    _productiondate timestamp;
    _expdate        timestamp;
    _now            timestamp := now();
BEGIN
    -------------------------------------------------------------------------
    -- Recipe must exist in the bill of materials
    --
    -- The PLC carries plcbomname while the rest of the system keys on
    -- formulacode, so the name is translated once here and the translated
    -- value is used from this point on.
    -------------------------------------------------------------------------
    SELECT formulacode
      INTO _mesrecipename
      FROM dbo.bom
     WHERE plcbomname = _recipename
     LIMIT 1;

    IF _mesrecipename IS NULL THEN
        result := json_build_object(
            'status',  'fail',
            'code',    'recipe_not_found',
            'message', 'Recipe not available in BOM or not released.'
        );
        RETURN;
    END IF;

    -------------------------------------------------------------------------
    -- Scanned material must belong to the recipe
    --
    -- The material group is captured in the same pass, because the lab check
    -- further down needs it. Leaving it unset is what stopped that check from
    -- ever running.
    -------------------------------------------------------------------------
    SELECT count(*), max(materialgroup_in)
      INTO _count, _materialgroup_in
      FROM dbo.bom
     WHERE plcbomname = _recipename
       AND materialcodegroup_in = _itemname;

    IF _count = 0 THEN
        result := json_build_object(
            'status',  'fail',
            'code',    'item_not_in_recipe',
            'message', 'Scanned material does not belong to the selected recipe.'
        );
        RETURN;
    END IF;

    -------------------------------------------------------------------------
    -- Scanned material must belong to this feeder
    --
    -- Only applied when the caller states where the scan was made. The
    -- mapping is matched on either bom code so a row that stores the plc name
    -- rather than the formula code is still found.
    -------------------------------------------------------------------------
    IF _equipmentid IS NOT NULL AND _sequenceno IS NOT NULL
       AND _sequenceno ~ '^[0-9]+$' THEN

        SELECT count(*)
          INTO _count
          FROM dbo.bomextrudermapping b
         WHERE b.equipmentid      = _equipmentid
           AND b.sequenceno       = _sequenceno::int
           AND b.consumeitemcode  = _itemname
           AND (b.bomcode = _mesrecipename OR b.bomcode = _recipename)
           AND b.isactive = true;

        IF _count = 0 THEN
            result := json_build_object(
                'status',  'fail',
                'code',    'wrong_feeder',
                'message', 'Scanned material is not mapped to this input feeder.'
            );
            RETURN;
        END IF;
    END IF;

    -------------------------------------------------------------------------
    -- Material already consumed
    -------------------------------------------------------------------------
    SELECT count(*)
      INTO _count
      FROM bodyply.i_material
     WHERE lot_id = _productionid
       AND live_quantity <= 5;

    IF _count > 0 THEN
        result := json_build_object(
            'status',  'fail',
            'code',    'already_consumed',
            'message', 'This Material Already Consume!'
        );
        RETURN;
    END IF;

    -------------------------------------------------------------------------
    -- The production record carries the date every aging rule is measured
    -- from. Without it the interval comparisons evaluate to NULL and both
    -- aging checks pass without saying anything, so it is required here.
    -------------------------------------------------------------------------
    SELECT dtandtime
      INTO _productiondate
      FROM frc.o_production
     WHERE production_id = _productionid
     LIMIT 1;

    IF _productiondate IS NULL THEN
        result := json_build_object(
            'status',  'fail',
            'code',    'production_not_found',
            'message', 'Production record not found for this lot, aging cannot be checked.'
        );
        RETURN;
    END IF;

    -------------------------------------------------------------------------
    -- Item master must describe the material before it can be aged
    --
    -- A missing row used to leave maxaging at zero, which put the expiry date
    -- on the production date itself and reported every such material as
    -- expired. The absence is now reported for what it is.
    -------------------------------------------------------------------------
    SELECT count(*)
      INTO _itemmasterrow
      FROM dbo.tblitemmaster
     WHERE itemcode = _itemname
       AND iscurrent = 1;

    IF _itemmasterrow = 0 THEN
        result := json_build_object(
            'status',  'fail',
            'code',    'item_master_missing',
            'message', 'This Material is not configured in item master.'
        );
        RETURN;
    END IF;

    -------------------------------------------------------------------------
    -- Expiry, from max aging plus any age added against the lot
    --
    -- A max aging of zero or null means no shelf life is configured for the
    -- item, and the check is skipped rather than treated as immediate expiry.
    -------------------------------------------------------------------------
    SELECT maxaging, maxagingunitid
      INTO _maxaging, _maxagingunit
      FROM dbo.tblitemmaster
     WHERE itemcode = _itemname
       AND iscurrent = 1
     LIMIT 1;

    SELECT coalesce(added_age, 0)
      INTO _addedage
      FROM dbo.materialageupdate
     WHERE lot_no = _productionid
     ORDER BY dtandtime DESC
     LIMIT 1;

    _maxaging := coalesce(_maxaging, 0) + coalesce(_addedage, 0);

    IF _maxaging > 0 THEN
        IF _maxagingunit = 13 THEN        -- months
            _expdate := _productiondate + make_interval(months => _maxaging);
        ELSIF _maxagingunit = 10 THEN     -- days
            _expdate := _productiondate + make_interval(days   => _maxaging);
        ELSIF _maxagingunit = 11 THEN     -- hours
            _expdate := _productiondate + make_interval(hours  => _maxaging);
        ELSE                              -- default days
            _expdate := _productiondate + make_interval(days   => _maxaging);
        END IF;

        IF _expdate < _now THEN
            result := json_build_object(
                'status',  'fail',
                'code',    'expired',
                'message', 'This Material is expired on ' || to_char(_expdate,'DD-MM-YYYY') || ' date!'
            );
            RETURN;
        END IF;
    END IF;

    -------------------------------------------------------------------------
    -- Minimum aging, the material is not ready before it
    --
    -- A minimum of zero or null means no minimum is configured, and the check
    -- is skipped.
    -------------------------------------------------------------------------
    SELECT minaging, minagingunitid
      INTO _minaging, _minagingunit
      FROM dbo.tblitemmaster
     WHERE itemcode = _itemname
       AND iscurrent = 1
     LIMIT 1;

    _minaging := coalesce(_minaging, 0) - coalesce(_minaddedage, 0);

    IF _minaging > 0 THEN
        IF _minagingunit = 13 THEN        -- months
            _expdate := _productiondate + make_interval(months => _minaging);
        ELSIF _minagingunit = 10 THEN     -- days
            _expdate := _productiondate + make_interval(days   => _minaging);
        ELSIF _minagingunit = 11 THEN     -- hours
            _expdate := _productiondate + make_interval(hours  => _minaging);
        ELSE                              -- default hours, kept as it was
            _expdate := _productiondate + make_interval(hours  => _minaging);
        END IF;

        IF _expdate > _now THEN
            result := json_build_object(
                'status',  'fail',
                'code',    'not_ready',
                'message', 'This Material is not ready for use on ' ||
                           to_char(_expdate,'DD-Mon-YYYY HH12:MI:SS AM') || ' date!'
            );
            RETURN;
        END IF;
    END IF;

    -------------------------------------------------------------------------
    -- Lab decision, calendered roll only
    --
    -- The group is matched on a pattern because the bill of materials spells
    -- it CALANDARED ROLL, which an equality test against 'Calendar Roll' can
    -- never match.
    -------------------------------------------------------------------------
    IF upper(coalesce(_materialgroup_in, '')) LIKE '%CAL%ND%R%' THEN
        SELECT count(*)
          INTO _count
          FROM frc.o_production
         WHERE production_id = _productionid
           AND quality_status IN ('1','3','5');

        IF _count = 0 THEN
            result := json_build_object(
                'status',  'fail',
                'code',    'lab_pending',
                'message', 'This Material Decision Pending in LAB, not ready to use!'
            );
            RETURN;
        END IF;
    END IF;

    -------------------------------------------------------------------------
    -- Material against the released bill of materials
    -------------------------------------------------------------------------
    SELECT count(*)
      INTO _count
      FROM dbo.bom
     WHERE formulacode = _mesrecipename
       AND materialcodegroup_in = _itemname;

    IF _count = 0 THEN
        result := json_build_object(
            'status',  'fail',
            'code',    'item_not_in_released_bom',
            'message', 'Material Code Not Validate With Recipe ' || _recipename
        );
        RETURN;
    END IF;

    -------------------------------------------------------------------------
    -- Material already open against this lot, ask about reversal
    -------------------------------------------------------------------------
    SELECT count(*)
      INTO _count
      FROM bodyply.i_material
     WHERE production_id = _productionid
       AND live_quantity > 0
       AND sequence_no > 0;

    IF _count > 0 THEN
        result := json_build_object(
            'status',  'confirm',
            'code',    'reversal_required',
            'message', 'Are you want to Reversal this Lot No.!'
        );
        RETURN;
    END IF;

    -------------------------------------------------------------------------
    -- Every check passed
    -------------------------------------------------------------------------
    result := json_build_object(
        'status',  'success',
        'code',    'validated',
        'message', 'Material Validation Successfully!'
    );
END;
$BODY$;

ALTER PROCEDURE bodyply.validaterecipebodyply(text)
    OWNER TO postgres;
