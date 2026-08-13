using BodyPlyWebService.Interfaces.IApp_Data;
using BodyPlyWebService.Interfaces.ILogger;
using BodyPlyWebService.Interfaces.IRepositories;
using BodyPlyWebService.Models;
using BodyPlyWebService.App_Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;

namespace BodyPlyWebService.Repositories
{
    public class LoggerRepository: ILoggerRepository
    {
        private readonly IDBOperations _dBOperations;
        private readonly ITextFileLogger _textFileLogger;

        public LoggerRepository(IDBOperations dbOperatios, ITextFileLogger textFileLogger)
        {
            _dBOperations = dbOperatios;
            _textFileLogger = textFileLogger;
        }

        public void AddLogsData(LogInfo logInfo)
        {
            if (_dBOperations == null)
            {
                throw new NullReferenceException("_dBOperations is null in LoggerRepository.AddLogsData");
            }

            if (_textFileLogger == null)
            {
                throw new NullReferenceException("_textFileLogger is null in LoggerRepository.AddLogsData");
            }

            if (logInfo == null)
            {
                throw new ArgumentNullException(nameof(logInfo));
            }

            string dtandtime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            //procedure name to save log into db
            string procedureName = ConstantData.LOG_PROCEDURE_NAME;
            try
            {
                //execute procedure and store response in datatable
                DataTable dt = _dBOperations.OprationWithDB("StoredProcedure", procedureName, JsonConvert.SerializeObject(new { logInfo, dtandtime }));

                //logged into textfile if logs are not saved in DB
                if (!dt.Rows[0][0].ToString().Contains("logged saved successfully"))
                {
                    //log information store into textfile
                    _textFileLogger.LogIntoFileAsync(logInfo);
                }
            }
            catch (Exception ex)
            {
                //log information and store into textfile
                logInfo.LogType = "Error"; logInfo.LogCode = "500"; logInfo.Message = "Error in logging into Database or text file" + ex.Message; logInfo.Data = ex.ToString(); logInfo.FolderName = "LoggerRepository_AddlogInfo"; logInfo.Data = JsonConvert.SerializeObject(logInfo);
                _textFileLogger.LogIntoFileAsync(logInfo);
            }
        }
    }
}