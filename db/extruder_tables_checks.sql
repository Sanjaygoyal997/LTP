-- =============================================================================
-- Extruder tables, checks after the data is entered
--
-- The catalogue and the machine layout are maintained by hand. These four
-- queries report the states that do not raise an error but change what the
-- machine does, so they are worth reading after any edit.
--
--   dbo.master_extruder            one row per extruder, a fixed key used by
--                                  every machine, carrying the three OPC tag
--                                  names.
--
--   dbo.equipment_extruder_lookup  which machine carries that extruder, at
--                                  which feeder position, and the schema its
--                                  material was produced in.
--
--   dbo.bomextrudermapping         which consumed item is valid on which
--                                  extruder for a given bom.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. The feeder list each machine will show, in the order it will show it
--
-- This is exactly what GetEquipmentExtruder returns, so it is what appears on
-- the HMI and what ResetExtruderMaterial writes to the PLC. Check the names,
-- the order, and that every tag column is filled.
-- -----------------------------------------------------------------------------
SELECT l.equipmentid, l.sequenceno, m.name AS extrudername, l.schemaname,
       m.mesitemcount, m.extruderscanok, m.extruderhooter
  FROM dbo.equipment_extruder_lookup l
  JOIN dbo.master_extruder m ON m.id = l.extruderid
 WHERE l.isdeleted = false AND m.isdeleted = false
 ORDER BY l.equipmentid, l.sequenceno;

-- -----------------------------------------------------------------------------
-- 2. Catalogue rows with a tag missing
--
-- A missing tag name is logged and skipped rather than failing, so the feeder
-- appears on the HMI while its count, hooter or scan ok is never written.
-- -----------------------------------------------------------------------------
SELECT id, name, mesitemcount, extruderscanok, extruderhooter
  FROM dbo.master_extruder
 WHERE isdeleted = false
   AND (mesitemcount IS NULL OR mesitemcount = ''
     OR extruderscanok IS NULL OR extruderscanok = ''
     OR extruderhooter IS NULL OR extruderhooter = '');

-- -----------------------------------------------------------------------------
-- 3. A tag name used by more than one extruder
--
-- Two extruders sharing a scan ok or a hooter means one feeder's state
-- overwrites the other's on every poll.
-- -----------------------------------------------------------------------------
SELECT tagname, tagrole, count(*) AS extruders, string_agg(name, ', ') AS used_by
  FROM (
        SELECT name, mesitemcount   AS tagname, 'mesitemcount'   AS tagrole FROM dbo.master_extruder WHERE isdeleted = false
        UNION ALL
        SELECT name, extruderscanok AS tagname, 'extruderscanok' AS tagrole FROM dbo.master_extruder WHERE isdeleted = false
        UNION ALL
        SELECT name, extruderhooter AS tagname, 'extruderhooter' AS tagrole FROM dbo.master_extruder WHERE isdeleted = false
       ) t
 WHERE tagname IS NOT NULL AND tagname <> ''
 GROUP BY tagname, tagrole
HAVING count(*) > 1;

-- -----------------------------------------------------------------------------
-- 4. Positions that repeat on one machine
--
-- Two feeders answering to one button. A scan at that position lands on
-- whichever row the database returns first.
-- -----------------------------------------------------------------------------
SELECT l.equipmentid, l.sequenceno, count(*) AS rows,
       string_agg(m.name, ', ') AS extruders
  FROM dbo.equipment_extruder_lookup l
  JOIN dbo.master_extruder m ON m.id = l.extruderid
 WHERE l.isdeleted = false AND m.isdeleted = false
 GROUP BY l.equipmentid, l.sequenceno
HAVING count(*) > 1;

-- -----------------------------------------------------------------------------
-- 5. Mapping rows for an extruder the machine does not carry
--
-- The recipe allows the item on an extruder that is not in the machine layout,
-- so there is no feeder position to scan it at and validation will refuse it.
-- -----------------------------------------------------------------------------
SELECT DISTINCT b.equipmentid, b.bomcode, b.consumeitemcode, b.extruderid,
       m.name AS extrudername
  FROM dbo.bomextrudermapping b
  LEFT JOIN dbo.equipment_extruder_lookup l
         ON l.equipmentid = b.equipmentid AND l.extruderid = b.extruderid
        AND l.isdeleted = false
  LEFT JOIN dbo.master_extruder m ON m.id = b.extruderid
 WHERE b.isactive = true AND l.id IS NULL;

-- -----------------------------------------------------------------------------
-- 6. Feeders with no schema recorded
--
-- Not an error. The procedures fall back to searching every known schema, so
-- these still work, just less directly.
-- -----------------------------------------------------------------------------
SELECT l.equipmentid, l.sequenceno, m.name AS extrudername
  FROM dbo.equipment_extruder_lookup l
  JOIN dbo.master_extruder m ON m.id = l.extruderid
 WHERE l.isdeleted = false AND m.isdeleted = false
   AND (l.schemaname IS NULL OR l.schemaname = '')
 ORDER BY l.equipmentid, l.sequenceno;
