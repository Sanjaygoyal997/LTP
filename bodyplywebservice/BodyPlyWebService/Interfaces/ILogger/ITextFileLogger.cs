using BodyPlyWebService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BodyPlyWebService.Interfaces.ILogger
{
    public interface ITextFileLogger
    {
        Task LogIntoFileAsync(LogInfo logInfo);
    }
}
