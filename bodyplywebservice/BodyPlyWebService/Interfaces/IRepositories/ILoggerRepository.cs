using BodyPlyWebService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BodyPlyWebService.Interfaces.IRepositories
{
    public interface ILoggerRepository
    {
        void AddLogsData(LogInfo logInfo);
    }
}
