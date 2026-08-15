-- =============================================================================
-- Extruder tables seed
--
-- The three tables already exist in their separated form, so there is nothing
-- to migrate out of dbo.bomextrudermapping. This fills the catalogue and the
-- machine layout directly.
--
--   dbo.master_extruder            one row per extruder, a fixed key used by
--                                  every machine, carrying the three OPC tag
--                                  names.
--
--   dbo.equipment_extruder_lookup  which machine carries that extruder, at
--                                  which feeder position, and the schema its
--                                  material was produced in.
--
-- Values for equipment 230 are taken from what dbo.bomextrudermapping held
-- before the columns moved. The GumStrip scan ok tags are corrected on the way:
-- they were exchanged between left and right, so each row's scan ok belonged to
-- the other feeder while its hooter and item count were its own.
--
-- Rows are only inserted when absent and tags are set by name, so the script
-- can be run more than once.
--
-- Sections 3 and 4 are for equipment 231, which has no data at all. Fill in the
-- tag names from that machine's BodyPlyConfig.csv before running them.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Catalogue rows, created only if the extruder is not already there
-- -----------------------------------------------------------------------------
INSERT INTO dbo.master_extruder (name)
SELECT v.name
  FROM (VALUES ('LetOff01'), ('GumStrip(L)'), ('GumStrip(R)'),
               ('WideStrip(L)'), ('WideStrip(R)')) AS v(name)
 WHERE NOT EXISTS (
        SELECT 1 FROM dbo.master_extruder m
         WHERE m.name = v.name AND m.isdeleted = false);

-- -----------------------------------------------------------------------------
-- 2. Tag names for the extruders equipment 230 carries
--
-- The scan ok values for the two GumStrips are the corrected ones: each now
-- matches the hooter and item count already on its own row.
-- -----------------------------------------------------------------------------
UPDATE dbo.master_extruder SET
    mesitemcount   = 'mescompounditem',
    extruderscanok = 'inputmaterial_scanok1',
    extruderhooter = 'inputmaterial_scanok1hooter'
 WHERE name = 'LetOff01' AND isdeleted = false;

UPDATE dbo.master_extruder SET
    mesitemcount   = 'mescompounditem1',
    extruderscanok = 'inputmaterial_scanok2',
    extruderhooter = 'inputmaterial_scanok2hooter'
 WHERE name = 'GumStrip(L)' AND isdeleted = false;

UPDATE dbo.master_extruder SET
    mesitemcount   = 'mescompounditem2',
    extruderscanok = 'inputmaterial_scanok3',
    extruderhooter = 'inputmaterial_scanok3hooter'
 WHERE name = 'GumStrip(R)' AND isdeleted = false;

UPDATE dbo.master_extruder SET
    mesitemcount   = 'mescompounditem3',
    extruderscanok = 'inputmaterial_scanok4',
    extruderhooter = 'inputmaterial_scanok4hooter'
 WHERE name = 'WideStrip(L)' AND isdeleted = false;

UPDATE dbo.master_extruder SET
    mesitemcount   = 'mescompounditem4',
    extruderscanok = 'inputmaterial_scanok5',
    extruderhooter = 'inputmaterial_scanok5hooter'
 WHERE name = 'WideStrip(R)' AND isdeleted = false;

-- -----------------------------------------------------------------------------
-- 3. What equipment 230 carries, and where
--
-- The positions are as the mapping held them, so GumStrip(R) stands at 2 and
-- GumStrip(L) at 3. Confirm that against the physical HMI buttons before the
-- machine runs: the position is what a scan is matched on, so a swap here sends
-- the scan to the wrong extruder.
--
-- The let off takes calendered roll from the four roll calender, every strip
-- feeder takes slitted material from the multi slitter.
-- -----------------------------------------------------------------------------
INSERT INTO dbo.equipment_extruder_lookup (equipmentid, extruderid, sequenceno, schemaname)
SELECT 230, m.id, v.sequenceno, v.schemaname
  FROM (VALUES ('LetOff01',     1, 'frc'),
               ('GumStrip(R)',  2, 'multislitter'),
               ('GumStrip(L)',  3, 'multislitter'),
               ('WideStrip(L)', 4, 'multislitter'),
               ('WideStrip(R)', 5, 'multislitter')) AS v(name, sequenceno, schemaname)
  JOIN dbo.master_extruder m ON m.name = v.name AND m.isdeleted = false
 WHERE NOT EXISTS (
        SELECT 1 FROM dbo.equipment_extruder_lookup l
         WHERE l.equipmentid = 230 AND l.extruderid = m.id AND l.isdeleted = false);

-- -----------------------------------------------------------------------------
-- 4. Equipment 231, which has nothing today
--
-- LetOff01 is the same catalogue row and needs no second entry. GumStrip and
-- WideStrip are their own extruders with their own tag names, which have to
-- come from that machine's BodyPlyConfig.csv. Replace the placeholders and
-- uncomment.
-- -----------------------------------------------------------------------------
-- INSERT INTO dbo.master_extruder (name)
-- SELECT v.name
--   FROM (VALUES ('GumStrip'), ('WideStrip')) AS v(name)
--  WHERE NOT EXISTS (
--         SELECT 1 FROM dbo.master_extruder m
--          WHERE m.name = v.name AND m.isdeleted = false);
--
-- UPDATE dbo.master_extruder SET
--     mesitemcount   = '<ItemName from Bodyply02 config>',
--     extruderscanok = '<ItemName from Bodyply02 config>',
--     extruderhooter = '<ItemName from Bodyply02 config>'
--  WHERE name = 'GumStrip' AND isdeleted = false;
--
-- UPDATE dbo.master_extruder SET
--     mesitemcount   = '<ItemName from Bodyply02 config>',
--     extruderscanok = '<ItemName from Bodyply02 config>',
--     extruderhooter = '<ItemName from Bodyply02 config>'
--  WHERE name = 'WideStrip' AND isdeleted = false;
--
-- INSERT INTO dbo.equipment_extruder_lookup (equipmentid, extruderid, sequenceno, schemaname)
-- SELECT 231, m.id, v.sequenceno, v.schemaname
--   FROM (VALUES ('LetOff01',  1, 'frc'),
--                ('GumStrip',  2, 'multislitter'),
--                ('WideStrip', 3, 'multislitter')) AS v(name, sequenceno, schemaname)
--   JOIN dbo.master_extruder m ON m.name = v.name AND m.isdeleted = false
--  WHERE NOT EXISTS (
--         SELECT 1 FROM dbo.equipment_extruder_lookup l
--          WHERE l.equipmentid = 231 AND l.extruderid = m.id AND l.isdeleted = false);

-- -----------------------------------------------------------------------------
-- 5. Verification. Read all four.
-- -----------------------------------------------------------------------------

-- 5a. The feeder list each machine will show, in the order it will show it
SELECT l.equipmentid, l.sequenceno, m.name AS extrudername, l.schemaname,
       m.mesitemcount, m.extruderscanok, m.extruderhooter
  FROM dbo.equipment_extruder_lookup l
  JOIN dbo.master_extruder m ON m.id = l.extruderid
 WHERE l.isdeleted = false AND m.isdeleted = false
 ORDER BY l.equipmentid, l.sequenceno;

-- 5b. Catalogue rows with no tags, which write nothing and log a line
SELECT id, name FROM dbo.master_extruder
 WHERE isdeleted = false
   AND (mesitemcount IS NULL OR extruderscanok IS NULL OR extruderhooter IS NULL);

-- 5c. Positions that repeat on one machine. Two feeders answering to one
--     button, and a scan lands on whichever row is returned first.
SELECT equipmentid, sequenceno, count(*) AS rows
  FROM dbo.equipment_extruder_lookup
 WHERE isdeleted = false
 GROUP BY equipmentid, sequenceno
HAVING count(*) > 1;

-- 5d. Mapping rows for an extruder the machine does not carry. Those items can
--     never be scanned, because there is no feeder position to scan them at.
SELECT DISTINCT b.equipmentid, b.bomcode, b.consumeitemcode, b.extruderid,
       m.name AS extrudername
  FROM dbo.bomextrudermapping b
  LEFT JOIN dbo.equipment_extruder_lookup l
         ON l.equipmentid = b.equipmentid AND l.extruderid = b.extruderid
  LEFT JOIN dbo.master_extruder m ON m.id = b.extruderid
 WHERE b.isactive = true AND l.id IS NULL;
