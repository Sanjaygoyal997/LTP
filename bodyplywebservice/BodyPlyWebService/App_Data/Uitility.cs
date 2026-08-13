using System;
using System.Data;
using System.Configuration;
using System.Linq;
using System.Web;

using System.Xml.Linq;
using System.IO;
using System.Net.Http;

namespace SmartMIS
{
    public class Uitility
    {
        public static void LogError(Exception ex)
        {
            string message = string.Format("Time: {0}", DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss tt"));

            message += Environment.NewLine;
            message += "-----------------------------------------------------------";
            message += Environment.NewLine;
            message += string.Format("Message: {0}", ex.Message);
            message += Environment.NewLine;
            message += string.Format("StackTrace: {0}", ex.StackTrace);
            message += Environment.NewLine;
            message += string.Format("Source: {0}", ex.Source);
            message += Environment.NewLine;
            message += string.Format("TargetSite: {0}", ex.TargetSite.ToString());
            message += Environment.NewLine;

            message += "-----------------------------------------------------------";
            message += Environment.NewLine;
            string date = DateTime.Now.ToString("ddMMMyyyy");
            // string path=  System.IO.Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + @"Errorlogfiles\" + (date+".txt"));
            //  System.IO.Directory.CreateDirectory(buildingMachine);
            //  string path = HttpContent.Current.Server.MapPath(@"~/Errorlogfiles/" + date + ".txt");
            string path = AppDomain.CurrentDomain.BaseDirectory + @"Errorlog\" + date + ".txt";
            using (StreamWriter writer = new StreamWriter(path, true))
            {
                writer.WriteLine(message);
                writer.Close();
            }
        }
        public static void LogEvent(string logevent)
        {
            string message = DateTime.Now.ToString() + "\t" + logevent;


            // string path = Application.StartupPath+ @"\EventLog\ErrorLog.txt";
            string date = DateTime.Now.ToString("ddMMMyyyy");
            // System.IO.Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + @"EventLog\" + (buildingMachine));
            string path = AppDomain.CurrentDomain.BaseDirectory + @"EventLog\" + date + ".txt";
            using (StreamWriter writer = new StreamWriter(path, true))
            {
                writer.WriteLine(message);
                writer.Close();
            }
        }

        public static void LogEventDetails(string logevent, string flag)
        {
            if (flag == "1")
            {
                string message = DateTime.Now.ToString() + "\t" + logevent;


                message += Environment.NewLine;
                message += "-----------------------------------------------------------";
                message += Environment.NewLine;
                message += string.Format("Message: {0}", logevent);

                message += "-----------------------------------------------------------";
                message += Environment.NewLine;

                // string path = Application.StartupPath+ @"\EventLog\ErrorLog.txt";
                string date = DateTime.Now.ToString("ddMMMyyyy");
                // System.IO.Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + @"EventLog\" + (buildingMachine));
                string path = AppDomain.CurrentDomain.BaseDirectory + @"EventLogTag\" + date + ".txt";
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine(message);
                    writer.Close();
                }
            }
        }
    }
}

