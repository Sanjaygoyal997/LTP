using BodyPlyWebService.Interfaces.ILogger;
using BodyPlyWebService.Interfaces.IRepositories;
using BodyPlyWebService.Interfaces.IServices;
using BodyPlyWebService.Logger;
using BodyPlyWebService.Models;
using BodyPlyWebService.Repositories;
using BodyPlyWebService.Services;
using BodyPlyWebService.App_Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Script.Services;
using System.Web.Services;
namespace BodyPlyWebService
{
    /// <summary>
    /// Summary description for BodyPly2
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    [ScriptService]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    // [System.Web.Script.Services.ScriptService]
    public class BodyPly2 : System.Web.Services.WebService
    {
        private readonly LogInfo _logInfo;
        private readonly IDBLogger _dBLogger;
        private string _transaction_Id;
        public static IBodyPlyASPXService _bodyplyRollCalenderASPXService;
        string machineName = "Bodyply02";
        int equipment_id = 231;
        static BodyPly2()
        {
            // Static constructor: runs once for the AppDomain lifetime
            var logInfo = new LogInfo();
            var textFileLogger = new TextFileLogger();

            // Step 1: create temporary DBOperations without logger (pass nulls first)
            DBOperations dbOperations = null;
            LoggerRepository loggerRepository = null;
            DBLogger dBLogger = null;

            // Step 2: create LoggerRepository and DBLogger after DBOperations is ready
            dbOperations = new DBOperations(logInfo, null, Guid.NewGuid().ToString());
            loggerRepository = new LoggerRepository(dbOperations, textFileLogger);
            dBLogger = new DBLogger(loggerRepository);

            // Step 3: now link DBOperations back with proper logger
            dbOperations.SetLogger(dBLogger); // you’ll need to add this setter (explained below)

            // Step 4: continue your logic
            IBodyPlyRepository calenderRepository = new BodyPlyRepository(logInfo, dBLogger, dbOperations, Guid.NewGuid().ToString());
            _bodyplyRollCalenderASPXService = new BodyPlyASPXService(null, calenderRepository, logInfo, dBLogger, Guid.NewGuid().ToString());
        }
        public BodyPly2()
        {
            _transaction_Id = Guid.NewGuid().ToString();
            _logInfo = new LogInfo();

            var textFileLogger = new TextFileLogger();

            DBOperations dbOperations = null;
            LoggerRepository loggerRepository = null;
            DBLogger dBLogger = null;

            dbOperations = new DBOperations(_logInfo, null, _transaction_Id);
            loggerRepository = new LoggerRepository(dbOperations, textFileLogger);
            dBLogger = new DBLogger(loggerRepository);

            dbOperations.SetLogger(dBLogger); // add this method in DBOperations

            _dBLogger = dBLogger;
        }

        [WebMethod]
        public string HelloWorld()
        {
            return "Hello World";
        }

        //Clears the cached extruder/feeder configuration so changes to
        //dbo.bomextrudermapping are picked up without recycling the application pool.
        [WebMethod]
        [ScriptMethod(UseHttpGet = true, ResponseFormat = ResponseFormat.Json)]
        public string ReloadExtruderConfig()
        {
            string result = string.Empty;
            try
            {
                ExtruderConfigProvider.Clear();
                result = "OK";
            }
            catch (Exception exc)
            {
                _dBLogger.LogIntoDB(new LogInfo
                {
                    EquipmentId = "BodyPly",
                    LogType = "Error",
                    LogCode = "500",
                    Message = $"Exception: {exc.Message}, Inner Exception: {exc.InnerException?.Message}",
                    FolderName = "BodyPly",
                    MethodInfo = "ReloadExtruderConfig",
                    DateTime = DateTime.Now.ToString(),
                    TransactionID = _transaction_Id
                });
            }
            return result;
        }

        [WebMethod]
        [ScriptMethod(UseHttpGet = true, ResponseFormat = ResponseFormat.Json)]
        //[XmlInclude(typeof(ResponceData))]
        public string UpdateHMI()
        {
            string result = string.Empty;
            try
            {
                result = _bodyplyRollCalenderASPXService.UpdateHMIService(machineName, equipment_id, _transaction_Id);
            }
            catch (Exception exc)
            {
                _dBLogger.LogIntoDB(new LogInfo
                {
                    EquipmentId = "BodyPly",
                    LogType = "Error",
                    LogCode = "500",
                    Message = $"Exception: {exc.Message}, Inner Exception: {exc.InnerException?.Message}",
                    FolderName = "BodyPly",
                    MethodInfo = "UpdateHMI",
                    DateTime = DateTime.Now.ToString(),
                    TransactionID = _transaction_Id
                });
            }
            return result;
        }

        [WebMethod]
        [ScriptMethod(UseHttpGet = true, ResponseFormat = ResponseFormat.Json)]
        //[XmlInclude(typeof(ResponceData))]
        public string GetRecipeList()
        {
            string result = string.Empty;
            try
            {
                result = _bodyplyRollCalenderASPXService.GetRecipeListService(_transaction_Id);
            }
            catch (Exception exc)
            {
                _dBLogger.LogIntoDB(new LogInfo
                {
                    EquipmentId = "BodyPly",
                    LogType = "Error",
                    LogCode = "500",
                    Message = $"Exception: {exc.Message}, Inner Exception: {exc.InnerException?.Message}",
                    FolderName = "BodyPly",
                    MethodInfo = "GetRecipeList",
                    DateTime = DateTime.Now.ToString(),
                    TransactionID = _transaction_Id
                });
            }
            return result;
        }

        [WebMethod]
        [ScriptMethod(UseHttpGet = true, ResponseFormat = ResponseFormat.Json)]
        //[XmlInclude(typeof(ResponceData))]
        public string GetTodayProductionDetails()
        {
            string result = string.Empty;
            try
            {
                result = _bodyplyRollCalenderASPXService.GetTodayProductionDetailsService(machineName,_transaction_Id);
            }
            catch (Exception exc)
            {
                _dBLogger.LogIntoDB(new LogInfo
                {
                    EquipmentId = "BodyPly",
                    LogType = "Error",
                    LogCode = "500",
                    Message = $"Exception: {exc.Message}, Inner Exception: {exc.InnerException?.Message}",
                    FolderName = "BodyPly",
                    MethodInfo = "GetRecipeList",
                    DateTime = DateTime.Now.ToString(),
                    TransactionID = _transaction_Id
                });
            }
            return result;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string ScanningQrcode(string QrCode, string Feeder, string numberofscan, string itemnumber, string isManual, string UserName)
        {
            string result = string.Empty;
            try
            {
                result = _bodyplyRollCalenderASPXService.ScanningQrcodeService(QrCode, Feeder, numberofscan, itemnumber, isManual, UserName, _transaction_Id,machineName);
               // _bodyplyRollCalenderASPXService.ResetExtruderMaterial(equipment_id);
            }
            catch (Exception exc)
            {
                _dBLogger.LogIntoDB(new LogInfo
                {
                    EquipmentId = "BodyPly",
                    LogType = "Error",
                    LogCode = "500",
                    Message = $"Exception: {exc.Message}, Inner Exception: {exc.InnerException?.Message}",
                    FolderName = "BodyPly",
                    MethodInfo = "ScanningQrcode",
                    DateTime = DateTime.Now.ToString(),
                    TransactionID = _transaction_Id
                });
            }
            return result;
        }

        [WebMethod]
        //[XmlInclude(typeof(ResponceData))]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string removeMaterial(string qrcode, string userID, string isManual, string Feeder)
        {
            string result = string.Empty;
            try
            {
                result = _bodyplyRollCalenderASPXService.removeMaterialService(qrcode, userID, isManual, Feeder);
            }
            catch (Exception exc)
            {
                _dBLogger.LogIntoDB(new LogInfo
                {
                    EquipmentId = "BodyPly",
                    LogType = "Error",
                    LogCode = "500",
                    Message = $"Exception: {exc.Message}, Inner Exception: {exc.InnerException?.Message}",
                    FolderName = "BodyPly",
                    MethodInfo = "removeMaterial",
                    DateTime = DateTime.Now.ToString(),
                    TransactionID = _transaction_Id
                });
            }
            return result;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string MHEScanning(string qrCode)
        {
            string result = string.Empty;
            try
            {
                result = _bodyplyRollCalenderASPXService.MHEScanningService(qrCode);
            }
            catch (Exception exc)
            {
                _dBLogger.LogIntoDB(new LogInfo
                {
                    EquipmentId = "BodyPly",
                    LogType = "Error",
                    LogCode = "500",
                    Message = $"Exception: {exc.Message}, Inner Exception: {exc.InnerException?.Message}",
                    FolderName = "BodyPly",
                    MethodInfo = "MHEScanning",
                    DateTime = DateTime.Now.ToString(),
                    TransactionID = _transaction_Id
                });
            }
            return result;
        }

        [WebMethod]
        [ScriptMethod(UseHttpGet = true, ResponseFormat = ResponseFormat.Json)]
        //[XmlInclude(typeof(ResponceData))]
        public string GetBom()
        {
            string result = string.Empty;
            try
            {
                result = _bodyplyRollCalenderASPXService.GetBOMService(_transaction_Id);
            }
            catch (Exception exc)
            {
                _dBLogger.LogIntoDB(new LogInfo
                {
                    EquipmentId = "BodyPly",
                    LogType = "Error",
                    LogCode = "500",
                    Message = $"Exception: {exc.Message}, Inner Exception: {exc.InnerException?.Message}",
                    FolderName = "BodyPly",
                    MethodInfo = "GetBOM",
                    DateTime = DateTime.Now.ToString(),
                    TransactionID = _transaction_Id
                });
            }
            return result;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string RecipeChangeConfirmation(Boolean RecipeChange)
        {
            string result = string.Empty;
            try
            {
                result = _bodyplyRollCalenderASPXService.RecipeChangeConfirmationService(RecipeChange);
            }
            catch (Exception exc)
            {
                _dBLogger.LogIntoDB(new LogInfo
                {
                    EquipmentId = "BodyPly",
                    LogType = "Error",
                    LogCode = "500",
                    Message = $"Exception: {exc.Message}, Inner Exception: {exc.InnerException?.Message}",
                    FolderName = "BodyPly",
                    MethodInfo = "RecipeChangeConfirmation",
                    DateTime = DateTime.Now.ToString(),
                    TransactionID = _transaction_Id
                });
            }
            return result;
        }

        [WebMethod]
        //[XmlInclude(typeof(ResponceData))]
        [ScriptMethod(UseHttpGet = true, ResponseFormat = ResponseFormat.Json)]
        public string RecipeChange()
        {
            string result = string.Empty;
            try
            {
                result = _bodyplyRollCalenderASPXService.RecipeChangeService();
            }
            catch (Exception exc)
            {
                _dBLogger.LogIntoDB(new LogInfo
                {
                    EquipmentId = "BodyPly",
                    LogType = "Error",
                    LogCode = "500",
                    Message = $"Exception: {exc.Message}, Inner Exception: {exc.InnerException?.Message}",
                    FolderName = "BodyPly",
                    MethodInfo = "RecipeChange",
                    DateTime = DateTime.Now.ToString(),
                    TransactionID = _transaction_Id
                });
            }
            return result;
        }

        [WebMethod]
        //[XmlInclude(typeof(ResponceData))]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string MESRecipeChange(string recipeName)
        {
            string result = string.Empty;
            try
            {
                result = _bodyplyRollCalenderASPXService.MESRecipeChangeService(recipeName);
            }
            catch (Exception exc)
            {
                _dBLogger.LogIntoDB(new LogInfo
                {
                    EquipmentId = "BodyPly",
                    LogType = "Error",
                    LogCode = "500",
                    Message = $"Exception: {exc.Message}, Inner Exception: {exc.InnerException?.Message}",
                    FolderName = "BodyPly",
                    MethodInfo = "MESRecipeChange",
                    DateTime = DateTime.Now.ToString(),
                    TransactionID = _transaction_Id
                });
            }
            return result;
        }

        [WebMethod]
        //[XmlInclude(typeof(ResponceData))]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetFeederScanMaterial(string FeederName)
        {
            string result = string.Empty;
            try
            {
                result = _bodyplyRollCalenderASPXService.GetFeederScanMaterialService(FeederName, equipment_id);
            }
            catch (Exception exc)
            {
                _dBLogger.LogIntoDB(new LogInfo
                {
                    EquipmentId = "BodyPly",
                    LogType = "Error",
                    LogCode = "500",
                    Message = $"Exception: {exc.Message}, Inner Exception: {exc.InnerException?.Message}",
                    FolderName = "BodyPly",
                    MethodInfo = "GetFeederScanMaterial",
                    DateTime = DateTime.Now.ToString(),
                    TransactionID = _transaction_Id
                });
            }
            return result;
        }

        [WebMethod]
        //[XmlInclude(typeof(PrintTag))]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string PrintTagAndSaveData(string UserName, string RecipeName, string ProgressLength, string ProgressWidth)
        {
            string result = string.Empty;
            try
            {
                result = _bodyplyRollCalenderASPXService.PrintTagAndSaveDataService(UserName, RecipeName, ProgressLength, ProgressWidth, machineName);
            }
            catch (Exception exc)
            {
                _dBLogger.LogIntoDB(new LogInfo
                {
                    EquipmentId = "BodyPly",
                    LogType = "Error",
                    LogCode = "500",
                    Message = $"Exception: {exc.Message}, Inner Exception: {exc.InnerException?.Message}",
                    FolderName = "BodyPly",
                    MethodInfo = "PrintTagAndSaveData",
                    DateTime = DateTime.Now.ToString(),
                    TransactionID = _transaction_Id
                });
            }
            return result;
        }

        [WebMethod]
        //[XmlInclude(typeof(PrintTag))]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string PrintTagPRNDublicate(string ProductionId)
        {
            string result = string.Empty;
            try
            {
                result = _bodyplyRollCalenderASPXService.PrintTagPRNDublicateService(ProductionId, machineName);
            }
            catch (Exception exc)
            {
                _dBLogger.LogIntoDB(new LogInfo
                {
                    EquipmentId = "BodyPly",
                    LogType = "Error",
                    LogCode = "500",
                    Message = $"Exception: {exc.Message}, Inner Exception: {exc.InnerException?.Message}",
                    FolderName = "BodyPly",
                    MethodInfo = "PrintTagPRNDublicate",
                    DateTime = DateTime.Now.ToString(),
                    TransactionID = _transaction_Id
                });
            }
            return result;
        }
    }
}
