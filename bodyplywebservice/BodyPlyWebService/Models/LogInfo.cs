using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BodyPlyWebService.Models
{
    public class LogInfo
    {
        public string Schema { get; set; }
        public string TransactionID { get; set; }
        public string EquipmentId { get; set; }
        public string LogType { get; set; }
        public string LogCode { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
        public string MethodInfo { get; set; }
        public string FolderName { get; set; }
        public string DateTime { get; set; }
    }
}