using BodyPlyWebService.Interfaces.IRepositories;
using BodyPlyWebService.Models;
using SmartMIS;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;

namespace BodyPlyWebService.App_Data
{
    /// <summary>
    /// Caches the extruder/feeder configuration per equipment.
    /// UpdateHMI is polled continuously, so this must not hit the database on every call.
    /// Call Clear() (exposed as the ReloadExtruderConfig web method) after changing the
    /// configuration rows so the change is picked up without recycling the application pool.
    /// </summary>
    public static class ExtruderConfigProvider
    {
        private static readonly ConcurrentDictionary<string, List<ExtruderConfig>> _cache
            = new ConcurrentDictionary<string, List<ExtruderConfig>>();

        public static List<ExtruderConfig> Get(string equipmentId, IBodyPlyRepository bodyPlyRepository)
        {
            List<ExtruderConfig> cached;
            if (_cache.TryGetValue(equipmentId, out cached))
            {
                return cached;
            }

            List<ExtruderConfig> configuredExtruders = new List<ExtruderConfig>();

            try
            {
                DataTable dt = bodyPlyRepository.GetEquipmentExtruder(equipmentId);

                //DBOperations returns a single "Message" column when the call fails,
                //so the expected columns are verified before the rows are read.
                if (dt != null && dt.Rows.Count > 0
                    && dt.Columns.Contains("extrudername")
                    && dt.Columns.Contains("mesitemcount"))
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        configuredExtruders.Add(new ExtruderConfig
                        {
                            ExtruderName = ColumnValue(row, "extrudername"),
                            SequenceNo = SequenceValue(row),
                            MesItemCountTag = ColumnValue(row, "mesitemcount"),
                            ExtruderScanOkTag = ColumnValue(row, "extruderscanok"),
                            ExtruderHooterTag = ColumnValue(row, "extruderhooter")
                        });
                    }
                }
            }
            catch (Exception exc)
            {
                Uitility.LogEvent("ExtruderConfigProvider Get :" + exc.ToString());
                configuredExtruders.Clear();
            }

            //A failed or empty load is not cached, so the next poll retries and the
            //caller keeps using the existing hardcoded feeder list in the meantime.
            if (configuredExtruders.Count > 0)
            {
                _cache[equipmentId] = configuredExtruders;
            }

            return configuredExtruders;
        }

        public static void Clear()
        {
            _cache.Clear();
        }

        private static string ColumnValue(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
            {
                return "";
            }
            return Convert.ToString(row[columnName]);
        }

        private static int SequenceValue(DataRow row)
        {
            try
            {
                if (!row.Table.Columns.Contains("sequenceno") || row["sequenceno"] == DBNull.Value)
                {
                    return 0;
                }
                return Convert.ToInt32(row["sequenceno"]);
            }
            catch
            {
                return 0;
            }
        }
    }
}
