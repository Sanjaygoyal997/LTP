using BodyPlyWebService.Interfaces.ILogger;
using BodyPlyWebService.Interfaces.IRepositories;
using BodyPlyWebService.Models;
using SmartLogic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Windows.Forms;

namespace BodyPlyWebService.Repositories
{
    public class OPCManagerRepository : IOPCManagerRepository
    {
        private readonly LogInfo _logInfo;
        private readonly IDBLogger _dBLogger;
        private readonly string _transaction_Id;

        loginformation inf = new loginformation();
        public bool opcstarted = false;
        static string FilePath = HttpContext.Current.Server.MapPath(@"~/Configuration/config.ini");
        INIFile INIFile = new INIFile(FilePath);
        public AutoCompleteStringCollection WcName;
        SmartLog smartLog = new SmartLog(INIFile.Read("Path", "EventLog"), INIFile.Read("Path", "ErrorLog"));

        SmartOPC opcManager;
        string configFile = "";
        public string OK = "";
        public string NOTOK = "";
        public string Start = "";
        public string Stop = "";
        public string Receipe = "";

        public OPCManagerRepository(LogInfo loginfo, IDBLogger dBLogger, string transaction_Id)
        {
            _logInfo = loginfo;
            _dBLogger = dBLogger;
            _transaction_Id = transaction_Id;
            InitializeOPCManagerRepository();
        }
        public void InitializeOPCManagerRepository()
        {
            
            try
            {
                //maintmr = new System.Timers.Timer(1000);
                //maintmr.Enabled = false;
                opcManager = new SmartOPC(INIFile.Read("OPC Server", "opcServer"), smartLog);

                WcName = new AutoCompleteStringCollection();
                inf.opcItemID = new AutoCompleteStringCollection();
                inf.opcItem = new AutoCompleteStringCollection();
                inf.opcvalue = new AutoCompleteStringCollection();
                configFile = INIFile.Read("Path", "TagConfigPath");
                try
                {
                    LoadConfiguration();
                    opcManager.Initialize(inf.opcItemID, inf.opcItem);
                }
                catch (Exception exc)
                {
                    LogError("OPCManagerRepository", "OPCManagerRepository", exc, _transaction_Id);
                }
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("OPCManagerRepository", "OPCManagerRepository", exc, _transaction_Id);
            }
        }
        public SmartOPC TBMHMI()
        {
            return opcManager;
        }
        public loginformation TBMInf()
        {
            return inf;
        }
        public void Startopc()
        {
            opcManager.StartData();
            opcstarted = true;
        }
        public void Stopopc()
        {
            opcManager.StopData();

            opcstarted = false;
        }
        public int OPCStatus()
        {
            int flag = 0;

            if (opcManager.opcRunningState() == true)
            {

                flag = 1;
            }

            return flag;
        }
        public void OKCode()
        {
            opcManager.WriteData(1, inf.opcItemID[inf.opcItemID.IndexOf("Ok")]);
        }
        public void NOKCode()
        {
            opcManager.WriteData(1, inf.opcItemID[inf.opcItemID.IndexOf("Nok")]);
        }
        public void LoadConfiguration()
        {
            try
            {
                inf.csvReader = new StreamReader(configFile);

                inf.csvReader.ReadLine();

                inf.content = inf.csvReader.ReadLine();

                int i = 0;

                while (inf.content != null)
                {
                    SetFilePath(inf.content);

                    inf.content = inf.csvReader.ReadLine();

                    i++;
                }

                inf.csvReader.Close();

                inf.metadata = EventID.Load_Configuration + "#" + DateTime.Now.ToString() + "#" + configFile + "#" + "Loaded successfully" + "#" + "";
                inf.args = inf.metadata.Split(new char[] { '#' });
                smartLog.WriteEventMessage(inf.args);
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("OPCManagerRepository", "LoadConfiguration", exc, _transaction_Id);
            }
        }
        public void SetFilePath(string args)
        {
            try
            {
                string[] metadata = args.Split(new char[] { ',' });

                for (int i = 1; i < metadata.Length; i = i + 2)
                {
                    inf.opcvalue.Add("0");
                    WcName.Add(metadata[0]);
                    inf.opcItemID.Add(metadata[i].ToLower());
                    inf.opcItem.Add(metadata[i + 1].ToLower());
                    //  inf.opcItem.Add(metadata[i + 1].Split(new char[] { '\\' })[1]);
                }
            }
            catch (Exception ex)
            {
                //Uitility.LogError(ex);
            }
        }
        private void LogError(string folderName, string methodInfo, Exception exc, string transactionId)
        {
            _dBLogger.LogIntoDB(new LogInfo
            {
                EquipmentId = "OPCManagerRepository",
                LogType = "Error",
                LogCode = "500",
                Message = $"Exception: {exc.Message}, Inner Exception: {exc.InnerException?.Message}",
                FolderName = $"{folderName}",
                MethodInfo = methodInfo,
                DateTime = DateTime.Now.ToString(),
                TransactionID = transactionId,
                Data = "NA"
            });
        }

        //Method for Logging Event
        private void LogEvent(string folderName, string methodInfo, string message, string transactionId, object data)
        {
            _dBLogger.LogIntoDB(new LogInfo
            {
                EquipmentId = "OPCManagerRepository",
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
    }
}