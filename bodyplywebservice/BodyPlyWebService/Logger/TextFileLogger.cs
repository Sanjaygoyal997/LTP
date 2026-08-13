using BodyPlyWebService.Interfaces.ILogger;
using BodyPlyWebService.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace BodyPlyWebService.Logger
{
    public class TextFileLogger : ITextFileLogger
    {
        private readonly string baseLogPath = AppDomain.CurrentDomain.BaseDirectory + @"Log";

        private async Task<string> GetLogFilePathAsync(string logType, string equipmentID)
        {
            string logDirectory = Path.Combine(baseLogPath, logType, equipmentID);
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
            string filePath = Path.Combine(logDirectory, $"Log_{DateTime.UtcNow:yyyyMMdd}.txt");
            return await Task.FromResult(filePath);
        }
        public async Task LogIntoFileAsync(LogInfo logInfo)
        {
            string filePath = "";
            if (logInfo.LogType.ToLower() == "event")
            {
                filePath = await GetLogFilePathAsync("EventLogs", logInfo.EquipmentId);
            }
            else
            {
                filePath = await GetLogFilePathAsync("ErrorLogs", logInfo.EquipmentId);
            }

            //string logPath = Path.Combine(filePath, $"Log_{DateTime.UtcNow:yyyyMMdd}.txt");

            string logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [TransactionID: {logInfo.TransactionID}] [EquipmentId: {logInfo.EquipmentId}] [LogType: {logInfo.LogType}] [LogCode: {logInfo.LogCode}] [FolderName: {logInfo.FolderName}]\nMessage: {logInfo.Message}\nData: {logInfo.Data}\n---\n";

            using (var writer = new StreamWriter(filePath, append: true))
            {
                await writer.WriteLineAsync(logEntry);
            }
        }
    }
}