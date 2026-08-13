-- =============================================================================
-- dbo.bomextrudermapping.sourceschema
--
-- Names the schema that holds the o_production record for material consumed on
-- an extruder. The let off takes calendered roll produced on the four roll
-- calender, the strip feeders take slitted material produced on the multi
-- slitter, and the production record therefore lives in a different schema
-- depending on the feeder.
--
-- Procedures read this instead of naming a schema, so a further source machine
-- is a row in this table rather than a change to the procedures.
--
-- The table holds one row per bom, consumed item and extruder, so every row for
-- the same extruder has to carry the same value. The seed below sets them all
-- through the extruder, and the check at the end reports any extruder whose
-- rows disagree.
-- =============================================================================

ALTER TABLE dbo.bomextrudermapping
    ADD COLUMN IF NOT EXISTS sourceschema character varying(50);

COMMENT ON COLUMN dbo.bomextrudermapping.sourceschema IS
    'Schema holding o_production for material consumed on this extruder, e.g. frc or multislitter.';

-- -----------------------------------------------------------------------------
-- Seed from the extruder, so every row for one extruder gets the same value
-- -----------------------------------------------------------------------------
UPDATE dbo.bomextrudermapping b
   SET sourceschema = 'frc'
  FROM dbo.master_extruder m
 WHERE m.id = b.extruderid
   AND m.name IN ('LetOff01');

UPDATE dbo.bomextrudermapping b
   SET sourceschema = 'multislitter'
  FROM dbo.master_extruder m
 WHERE m.id = b.extruderid
   AND m.name IN ('GumStrip', 'GumStrip(L)', 'GumStrip(R)',
                  'WideStrip', 'WideStrip(L)', 'WideStrip(R)');

-- -----------------------------------------------------------------------------
-- Rows still without a source, these will fall back to searching every schema
-- -----------------------------------------------------------------------------
SELECT b.equipmentid, b.sequenceno, m.name AS extrudername
  FROM dbo.bomextrudermapping b
  JOIN dbo.master_extruder m ON m.id = b.extruderid
 WHERE b.isactive = true
   AND (b.sourceschema IS NULL OR b.sourceschema = '')
 GROUP BY b.equipmentid, b.sequenceno, m.name
 ORDER BY b.equipmentid, b.sequenceno;

-- -----------------------------------------------------------------------------
-- Extruders whose rows disagree with each other, which must be corrected
-- -----------------------------------------------------------------------------
SELECT b.equipmentid, m.name AS extrudername,
       count(DISTINCT b.sourceschema) AS distinct_sources,
       string_agg(DISTINCT b.sourceschema, ', ') AS sources
  FROM dbo.bomextrudermapping b
  JOIN dbo.master_extruder m ON m.id = b.extruderid
 WHERE b.isactive = true
 GROUP BY b.equipmentid, m.name
HAVING count(DISTINCT b.sourceschema) > 1
 ORDER BY b.equipmentid, m.name;
