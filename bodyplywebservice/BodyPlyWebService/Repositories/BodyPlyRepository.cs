using BodyPlyWebService.Interfaces.IApp_Data;
using BodyPlyWebService.Interfaces.ILogger;
using BodyPlyWebService.Interfaces.IRepositories;
using BodyPlyWebService.Models;
using Newtonsoft.Json;
using SmartMIS;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;

namespace BodyPlyWebService.Repositories
{
    public class BodyPlyRepository : IBodyPlyRepository
    {
        private readonly LogInfo _logInfo;
        private readonly IDBLogger _dBLogger;
        private readonly IDBOperations _dBOperations;
        private readonly string _transaction_Id;

        public string RecipeName { get; set; }
        public string PorkChop1recipe { get; set; }
        public string PorkChop2recipe { get; set; }
        public string ScadaRecipe { get; set; }
        public string BufferRecipe { get; set; }
        public string ItemName { get; set; }
        public string ProductionID { get; set; }
        public string ProgressWidth { get; set; }
        public string Qty { get; set; }
        public string ItemCode { get; set; }
        public string ProductionQuantityLength { get; set; }
        public string FromDate { get; set; }
        public string Todate { get; set; }
        public string FeederCode { get; set; }
        public string MachineName { get; set; }
        public string MachineCode { get; set; }
        public string UserId { get; set; }
        public string SyncStatus { get; set; }
        public string EquipmentName { get; set; }
        public string PrinterName { get; set; } = "BodyPly";
        public BodyPlyRepository(LogInfo loginfo, IDBLogger dBLogger, IDBOperations dBOperations, string transaction_Id)
        {
            _logInfo = loginfo;
            _dBLogger = dBLogger;
            _transaction_Id = transaction_Id;
            _dBOperations = dBOperations;
        }

        public string AddUpdateI_Material(string lot_No, string quantity, string item_Code, string feederName, string machine_Name, string machine_Code, string user_Id, string sync_Status)
        {
            DataTable dt = new DataTable();
            try
            {
                var paramObj = new { productionId = lot_No, qty = quantity, itemCode = item_Code, feeder = feederName, machineName = machine_Name, machineCode = machine_Code, userId = user_Id, syncStatus = sync_Status };

                string jsonParams = JsonConvert.SerializeObject(paramObj);

                dt = _dBOperations.OprationWithDB("StoredProcedure", "bodyply.add_update_i_material", jsonParams);
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyRepository", "AddUpdateI_Material", exc, _transaction_Id);
            }
            return JsonConvert.SerializeObject(dt);
        }

        public string AddUpdateI_Production()
        {
            throw new NotImplementedException();
        }

        public DataTable CheckMaterailAvailable()
        {
            throw new NotImplementedException();
        }

        public DataTable CheckMaterialQty()
        {
            throw new NotImplementedException();
        }

        public DataTable GetBom(string recipe)
        {
            DataTable dt = new DataTable();
            try
            {
                dt = _dBOperations.OprationWithDB("StoredProcedure", "bodyply.getbom", recipe);
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyRepository", "GetBom", exc, _transaction_Id);
            }
            return dt;
        }

        public DataTable GetFollowRemark()
        {
            throw new NotImplementedException();
        }

        public DataTable GetBodyPlyPRNDublicateTag(string ProductionId)
        {
            DataTable dt = new DataTable();
            try
            {
                dt = _dBOperations.OprationWithDB("StoredProcedure", "bodyply.getbodyplyprndublicatetag", ProductionId);
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyRepository", "GetBodyPlyPRNDublicateTag", exc, _transaction_Id);
            }
            return dt;
        }

        public DataTable GetBodyPlyShiftDataCount(int equipmentId)
        {

            DataTable dt = new DataTable();
            try
            {
                dt = _dBOperations.OprationWithDB("StoredProcedure", "bodyply.getbodyplyshiftdatacount", equipmentId.ToString());
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyRepository", "GetBodyPlyPRNDublicateTag", exc, _transaction_Id);
            }
            return dt;
        }

        public DataTable GetProduceItem(string recipe)
        {
            DataTable dt = new DataTable();
            try
            {
                dt = _dBOperations.OprationWithDB("StoredProcedure", "bodyply.getproduceitem", recipe);
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyRepository", "GetProduceItem", exc, _transaction_Id);
            }
            return dt;
        }
        public DataTable GetProductionDetails(string from_date, string to_date, string machineName)
        {
            DataTable dt = new DataTable();
            try
            {
                var paramObj = new { fromdate = Convert.ToDateTime(from_date).ToString("yyyy-MM-dd HH:mm:ss"), todate = Convert.ToDateTime(to_date).ToString("yyyy-MM-dd HH:mm:ss"),machinename= machineName };

                string jsonParams = JsonConvert.SerializeObject(paramObj);

                dt = _dBOperations.OprationWithDB("StoredProcedure", "bodyply.getbodyplyproduction", jsonParams);
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyRepository", "GetRecipe", exc, _transaction_Id);
            }
            return dt;
        }

        public DataTable GetRawandCompoundProductionDetailbyProductionId(string qrCode)
        {
            DataTable dt = new DataTable();
            try
            {
                dt = _dBOperations.OprationWithDB("StoredProcedure", "bodyply.get_raw_and_compound_production_detail_by_productionid", qrCode);
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyRepository", "GetRawandCompoundProductionDetailbyProductionId", exc, _transaction_Id);
            }
            return dt;
        }

        public DataTable GetRecipe(string equipmentName)
        {
            DataTable dt = new DataTable();
            try
            {
                dt = _dBOperations.OprationWithDB("StoredProcedure", "bodyply.getrecipe", equipmentName);
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyRepository", "GetRecipe", exc, _transaction_Id);
            }
            return dt;
        }

        public DataTable GetMachineConfig(string equipmentName)
        {
            DataTable dt = new DataTable();
            try
            {
                dt = _dBOperations.OprationWithDB("StoredProcedure", "dbo.get_machine_config", equipmentName);
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyRepository", "GetMachineConfig", exc, _transaction_Id);
            }
            return dt;
        }

        public DataTable GetProduceItembom(string recipe)
        {
            DataTable dt = new DataTable();
            try
            {
                dt = _dBOperations.OprationWithDB("StoredProcedure", "bodyply.getproduceitembom", recipe);
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyRepository", "GetProduceItembom", exc, _transaction_Id);
            }
            return dt;
        }

        public DataTable GetShift()
        {
            DataTable dt = new DataTable();
            try
            {
                dt = _dBOperations.OprationWithDB("StoredProcedure", "bodyply.getshift", "");
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyRepository", "GetShift", exc, _transaction_Id);
            }
            return dt;
        }

        //public DataTable GetTotalBodyPlyScan(string feederName)
        //{
        //    DataTable dt = new DataTable();
        //    try
        //    {
        //        dt = _dBOperations.OprationWithDB("StoredProcedure", "bodyply.get_total_bodyply_scan", feederName);
        //    }
        //    catch (Exception exc)
        //    {
        //        // Log unexpected error in catch block and log into DB
        //        LogError("BodyPlyRepository", "GetTotalBodyPlyScan", exc, _transaction_Id);
        //    }
        //    return dt;
        //}

        public DataTable GetTotalBodyPlyScan(string feederName,string equipment_id)
        {
            DataTable dt = new DataTable();
            try
            {
                var paramObj = new { feedername = feederName, equipment_id = equipment_id.ToString() };

                string jsonParams = JsonConvert.SerializeObject(paramObj);
                dt = _dBOperations.OprationWithDB("StoredProcedure", "bodyply.get_total_bodyply_byfeederscan", jsonParams);
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyRepository", "GetTotalBodyPlyScan", exc, _transaction_Id);
            }
            return dt;
        }

        public string ReverceLotno(string lotNo)
        {
            DataTable dt = new DataTable();
            try
            {
                dt = _dBOperations.OprationWithDB("StoredProcedure", "bodyply.reverce_lotno_bodyply", lotNo);
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyRepository", "ReverceLotno", exc, _transaction_Id);
            }
            return JsonConvert.SerializeObject(dt); ;
        }

        public DataTable SelectPrinterDetailbyName(string machineName)
        {
            DataTable dt = new DataTable();
            try
            {
                dt = _dBOperations.OprationWithDB("StoredProcedure", "dbo.selectprinterdetailbyname", machineName);
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyRepository", "SelectPrinterDetailbyName", exc, _transaction_Id);
            }
            return dt;
        }

        public string UpdateBodyPlyI_Material()
        {
            throw new NotImplementedException();
        }

        public string ValidateRecipe(string item_Name, string recipe_Name, string production_Id)
        {
            DataTable dt = new DataTable();
            try
            {
                var paramObj = new { itemName = item_Name, recipe = recipe_Name, productionId = production_Id, };

                string jsonParams = JsonConvert.SerializeObject(paramObj);
                Uitility.LogEvent("bodyply.validaterecipebodyply:"+ jsonParams);
                dt = _dBOperations.OprationWithDB("StoredProcedure", "bodyply.validaterecipebodyply", jsonParams);
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyRepository", "GetRecipe", exc, _transaction_Id);
            }
            return JsonConvert.SerializeObject(dt);
        }

        public DataTable VerifyRecipeExist(string RecipeName)
        {
            DataTable dt = new DataTable();
            try
            {
                dt = _dBOperations.OprationWithDB("StoredProcedure", "bodyply.verifyrecipeexist", RecipeName);
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyRepository", "VerifyRecipeExist", exc, _transaction_Id);
            }
            return dt;
        }
        
        //Method for Logging Error
        private void LogError(string folderName, string methodInfo, Exception exc, string transactionId)
        {
            _dBLogger.LogIntoDB(new LogInfo
            {
                EquipmentId = "BodyPlyRepository",
                LogType = "Error",
                LogCode = "500",
                Message = $"Exception: {exc.Message}, Inner Exception: {exc.InnerException?.Message}",
                FolderName = $"{folderName}",
                MethodInfo = methodInfo,
                DateTime = DateTime.Now.ToString(),
                TransactionID = transactionId
            });
        }

        //Method for Logging Event
        private void LogEvent(string folderName, string methodInfo, string message, string transactionId, object data)
        {
            _dBLogger.LogIntoDB(new LogInfo
            {
                EquipmentId = "BodyPlyRepository",
                LogType = "Event",
                LogCode = "200",
                Message = message,
                Data = data,
                FolderName = $"{folderName}",
                MethodInfo = methodInfo,
                DateTime = DateTime.Now.ToString(),
                TransactionID = transactionId
            });
        }

        public void ResetExtruderMaterial(int equipmentId)
        {
            throw new NotImplementedException();
        }
    }
}