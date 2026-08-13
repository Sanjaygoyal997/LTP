using BodyPlyWebService.Interfaces.ILogger;
using BodyPlyWebService.Interfaces.IRepositories;
using BodyPlyWebService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BodyPlyWebService.Logger
{
    public class DBLogger : IDBLogger
    {
        private readonly ILoggerRepository _loggerRepository;

         public DBLogger(ILoggerRepository loggerRepository)
        {
            _loggerRepository = loggerRepository;
        }
        public void LogIntoDB(LogInfo logInfo)
        {
            _loggerRepository.AddLogsData(logInfo);
        }
    }
}