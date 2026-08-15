-- =============================================================================
-- Extruder tables restructure
--
-- Three concerns are separated:
--
--   dbo.master_extruder             the catalogue. An extruder's fixed key, its
--                                   name and its three OPC tag names. One row
--                                   per extruder, the same key on every machine.
--
--   dbo.equipment_extruder_lookup   which machine carries that extruder, at
--                                   which feeder position, and which schema its
--                                   material is produced in.
--
--   dbo.bomextrudermapping          which consumed item is valid on which
--                                   extruder for a given bom.
--
-- The feeder list comes from the lookup alone, so a machine shows every extruder
-- it carries whichever recipe is running. The mapping decides only which of them
-- the running recipe requires a scan on.
--
-- Run the sections in order and read the verification output before section 5.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Catalogue gains the tag names
-- -----------------------------------------------------------------------------
ALTER TABLE dbo.master_extruder
    ADD COLUMN IF NOT EXISTS mesitemcount   character varying(50),
    ADD COLUMN IF NOT EXISTS extruderhooter character varying(50),
    ADD COLUMN IF NOT EXISTS extruderscanok character varying(50);

COMMENT ON COLUMN dbo.master_extruder.mesitemcount IS
    'OPC friendly name carrying the scanned item count for this extruder.';
COMMENT ON COLUMN dbo.master_extruder.extruderhooter IS
    'OPC friendly name of this extruder''s hooter.';
COMMENT ON COLUMN dbo.master_extruder.extruderscanok IS
    'OPC friendly name of this extruder''s scan ok bit.';

-- -----------------------------------------------------------------------------
-- 2. Carry the tag names over from the mapping
--
-- Taken as the value most rows agree on, since the mapping holds one row per bom
-- and consumed item. The GumStrip pair is then corrected: the scan ok tags were
-- exchanged between left and right, so each row's scan ok belonged to the other
-- feeder while its hooter and item count were its own.
-- -----------------------------------------------------------------------------
UPDATE dbo.master_extruder m
   SET mesitemcount   = agreed.mesitemcount,
       extruderhooter = agreed.extruderhooter,
       extruderscanok = agreed.extruderscanok
  FROM (
        SELECT b.extruderid,
               mode() WITHIN GROUP (ORDER BY b.mesitemcount)   AS mesitemcount,
               mode() WITHIN GROUP (ORDER BY b.extruderhooter) AS extruderhooter,
               mode() WITHIN GROUP (ORDER BY b.extruderscanok) AS extruderscanok
          FROM dbo.bomextrudermapping b
         WHERE b.isactive = true
         GROUP BY b.extruderid
       ) agreed
 WHERE m.id = agreed.extruderid;

UPDATE dbo.master_extruder SET extruderscanok = 'inputmaterial_scanok2' WHERE name = 'GumStrip(L)';
UPDATE dbo.master_extruder SET extruderscanok = 'inputmaterial_scanok3' WHERE name = 'GumStrip(R)';

-- -----------------------------------------------------------------------------
-- 3. Which machine carries which extruder, where, and from which schema
--
-- The position comes from the mapping. The schema is the four roll calender for
-- the let off and the multi slitter for every strip feeder.
-- -----------------------------------------------------------------------------
INSERT INTO dbo.equipment_extruder_lookup (equipmentid, extruderid, sequenceno, schemaname)
SELECT b.equipmentid,
       b.extruderid,
       mode() WITHIN GROUP (ORDER BY b.sequenceno) AS sequenceno,
       CASE WHEN m.name ILIKE 'LetOff%' THEN 'frc' ELSE 'multislitter' END
  FROM dbo.bomextrudermapping b
  JOIN dbo.master_extruder m ON m.id = b.extruderid
 WHERE b.isactive = true
   AND NOT EXISTS (
        SELECT 1 FROM dbo.equipment_extruder_lookup l
         WHERE l.equipmentid = b.equipmentid AND l.extruderid = b.extruderid)
 GROUP BY b.equipmentid, b.extruderid, m.name;

-- -----------------------------------------------------------------------------
-- 4. Verification. Read all four before going on.
-- -----------------------------------------------------------------------------

-- 4a. The catalogue, as the machines will now read it
SELECT id, name, mesitemcount, extruderscanok, extruderhooter
  FROM dbo.master_extruder
 WHERE isdeleted = false
 ORDER BY name;

-- 4b. The feeder list each machine will show
SELECT l.equipmentid, l.sequenceno, m.name AS extrudername, l.schemaname,
       m.mesitemcount, m.extruderscanok, m.extruderhooter
  FROM dbo.equipment_extruder_lookup l
  JOIN dbo.master_extruder m ON m.id = l.extruderid
 WHERE l.isdeleted = false AND m.isdeleted = false
 ORDER BY l.equipmentid, l.sequenceno;

-- 4c. Positions that repeat on one machine, which would make two feeders
--     answer to the same button
SELECT equipmentid, sequenceno, count(*) AS rows
  FROM dbo.equipment_extruder_lookup
 WHERE isdeleted = false
 GROUP BY equipmentid, sequenceno
HAVING count(*) > 1;

-- 4d. Mapping rows for an extruder the machine does not carry. These items can
--     never be scanned, because there is no feeder position to scan them at.
SELECT DISTINCT b.equipmentid, b.bomcode, b.consumeitemcode, b.extruderid,
       m.name AS extrudername
  FROM dbo.bomextrudermapping b
  LEFT JOIN dbo.equipment_extruder_lookup l
         ON l.equipmentid = b.equipmentid AND l.extruderid = b.extruderid
  LEFT JOIN dbo.master_extruder m ON m.id = b.extruderid
 WHERE b.isactive = true AND l.id IS NULL;

-- -----------------------------------------------------------------------------
-- 5. Only once the above reads correctly, and the service is running the
--    rebuilt queries, drop what has moved.
-- -----------------------------------------------------------------------------
-- ALTER TABLE dbo.bomextrudermapping
--     DROP COLUMN IF EXISTS mesitemcount,
--     DROP COLUMN IF EXISTS extruderhooter,
--     DROP COLUMN IF EXISTS extruderscanok,
--     DROP COLUMN IF EXISTS sequenceno,
--     DROP COLUMN IF EXISTS schemaname;
