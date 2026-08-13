-- =============================================================================
-- dbo.bomextrudermapping.schemaname
--
-- Names the schema that holds the o_production record for material consumed on
-- an extruder. The let off takes calendered roll produced on the four roll
-- calender, the strip feeders take slitted material produced on the multi
-- slitter, and the production record therefore lives in a different schema
-- depending on the feeder.
--
-- validaterecipebodyply reads this instead of naming a schema, so a further
-- source machine is a row in this table rather than a change to the procedure.
-- A row with no value falls back to searching every known schema, so seeding
-- this is safe to do before or after the procedure is applied.
--
-- The table holds one row per bom, consumed item and extruder, so every row for
-- the same extruder has to carry the same value. The seed below sets them all
-- through the extruder, and the last query reports any extruder whose rows
-- disagree.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Seed from the extruder, so every row for one extruder gets the same value
-- -----------------------------------------------------------------------------
UPDATE dbo.bomextrudermapping b
   SET schemaname = 'frc'
  FROM dbo.master_extruder m
 WHERE m.id = b.extruderid
   AND m.name IN ('LetOff01');

UPDATE dbo.bomextrudermapping b
   SET schemaname = 'multislitter'
  FROM dbo.master_extruder m
 WHERE m.id = b.extruderid
   AND m.name IN ('GumStrip', 'GumStrip(L)', 'GumStrip(R)',
                  'WideStrip', 'WideStrip(L)', 'WideStrip(R)');

-- -----------------------------------------------------------------------------
-- What the mapping now says
-- -----------------------------------------------------------------------------
SELECT b.equipmentid, b.sequenceno, m.name AS extrudername, b.schemaname
  FROM dbo.bomextrudermapping b
  JOIN dbo.master_extruder m ON m.id = b.extruderid
 WHERE b.isactive = true
 GROUP BY b.equipmentid, b.sequenceno, m.name, b.schemaname
 ORDER BY b.equipmentid, b.sequenceno;

-- -----------------------------------------------------------------------------
-- Rows still without a schema, these fall back to searching every schema
--
-- An extruder named something outside the two lists above lands here, which is
-- also how a name that does not match master_extruder shows itself.
-- -----------------------------------------------------------------------------
SELECT b.equipmentid, b.sequenceno, m.name AS extrudername
  FROM dbo.bomextrudermapping b
  JOIN dbo.master_extruder m ON m.id = b.extruderid
 WHERE b.isactive = true
   AND (b.schemaname IS NULL OR b.schemaname = '')
 GROUP BY b.equipmentid, b.sequenceno, m.name
 ORDER BY b.equipmentid, b.sequenceno;

-- -----------------------------------------------------------------------------
-- Extruders whose rows disagree with each other, which must be corrected
-- -----------------------------------------------------------------------------
SELECT b.equipmentid, m.name AS extrudername,
       count(DISTINCT b.schemaname) AS distinct_schemas,
       string_agg(DISTINCT b.schemaname, ', ') AS schemas
  FROM dbo.bomextrudermapping b
  JOIN dbo.master_extruder m ON m.id = b.extruderid
 WHERE b.isactive = true
 GROUP BY b.equipmentid, m.name
HAVING count(DISTINCT b.schemaname) > 1
 ORDER BY b.equipmentid, m.name;
