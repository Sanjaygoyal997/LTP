using BodyPlyWebService.Interfaces.IApp_Data;
using BodyPlyWebService.Interfaces.ILogger;
using BodyPlyWebService.Models;
using Newtonsoft.Json.Linq;
using Npgsql;
using SmartMIS;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;

namespace BodyPlyWebService.App_Data
{
    public class DBOperations : IDBOperations
    {

        private readonly string _connection = "Host=10.200.233.13; Port=5432; Database=smartmes; Username=postgres; Password=smartAdmin";
        private readonly LogInfo _logInfo;
        private IDBLogger _dBLogger;

        private NpgsqlConnection connection;
        private readonly string _transaction_Id;

        public DBOperations(LogInfo loginfo, IDBLogger dBLogger, string transaction_Id)
        {
            _logInfo = loginfo;
            _dBLogger = dBLogger;
            _transaction_Id = transaction_Id;
        }

        public void SetLogger(IDBLogger dBLogger)
        {
            if (dBLogger != null)
                _dBLogger = dBLogger;
        }
        public NpgsqlConnection OpenConnection()
        {
            try
            {
                connection = new NpgsqlConnection(_connection);
                connection.Open();
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("DBOperations", "NpgsqlConnection", exc, _transaction_Id);
            }
            return connection;
        }
        public void CloseConnection(NpgsqlConnection _connection)
        {
            try
            {
                if (_connection != null && _connection.State == ConnectionState.Open)
                {
                    _connection.Close();
                }
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("DBOperations", "CloseConnection", exc, _transaction_Id);
            }
        }

        public bool ItemExistsInDatabase(string query)
        {
            bool result = false;
            try
            {
                using (var connection = OpenConnection())
                using (var command = new NpgsqlCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            result = true;
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("DBOperations", "ItemExistsInDatabase", exc, _transaction_Id);
            }
            return result;
        }


        public DataTable OprationWithDB(String commandType, string QueryOrProcedureName, String parametervalue)
        {
            DataTable dtResult = new DataTable();
            string parameterName = "_json";
            // string constr = ConfigurationManager.ConnectionStrings["Connection"].ConnectionString;

            var resultJsonParam = new NpgsqlParameter("result", NpgsqlTypes.NpgsqlDbType.Json)
            {
                Direction = ParameterDirection.Output
            };

            using (var con = new NpgsqlConnection(_connection))
            {
                using (var cmd = new NpgsqlCommand(QueryOrProcedureName, con))
                {
                    if (commandType == "StoredProcedure")
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue(parameterName, parametervalue);
                        cmd.Parameters.Add(resultJsonParam);
                    }

                    try
                    {
                        con.Open();

                        if (commandType == "StoredProcedure")
                        {
                            cmd.ExecuteNonQuery();
                            string resultProc = resultJsonParam.Value?.ToString();
                            if (!string.IsNullOrEmpty(resultProc))
                            {
                                // Parse JSON into JArray
                                JArray array;

                                // Detect object vs array
                                if (resultProc.TrimStart().StartsWith("["))
                                {
                                    array = JArray.Parse(resultProc);
                                }
                                else
                                {
                                    var obj = JObject.Parse(resultProc);
                                    array = new JArray(obj);  // wrap single object in array
                                }

                                if (array.Count > 0)
                                {
                                    // Create columns based on first object
                                    foreach (JProperty column in ((JObject)array[0]).Properties())
                                    {
                                        dtResult.Columns.Add(column.Name, typeof(string)); // or infer type
                                    }

                                    // Fill rows
                                    foreach (JObject obj in array)
                                    {
                                        DataRow row = dtResult.NewRow();
                                        foreach (JProperty prop in obj.Properties())
                                        {
                                            row[prop.Name] = prop.Value.ToString();
                                        }
                                        dtResult.Rows.Add(row);
                                    }
                                }
                            }
                        }
                        else
                        {
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    dtResult.Load(reader);
                                }
                            }

                        }
                    }
                    catch (Exception exc)
                    {
                        Uitility.LogEvent("Error OprationWithDB :" + exc.ToString());
                        dtResult.Columns.Add("Message");
                        DataRow dr = dtResult.NewRow();
                        dr[0] = exc.Message;
                        dtResult.Rows.Add(dr);
                        // Log unexpected error in catch block and log into DB
                        //LogError("DBOperations", "OprationWithDB", exc, _transaction_Id);
                    }
                }
            }
            return dtResult;
        }
        public DataTable OprationWith_DB(string commandType, string QueryOrProcedureName, string parametervalue)
        {
            DataTable dtResult = new DataTable();
            string parameterName = "_json";

            using (var con = new NpgsqlConnection(_connection))
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = con;

                try
                {
                    con.Open();

                    if (commandType == "StoredProcedure")
                    {
                        // Call procedure with OUT params
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandText = $"CALL {QueryOrProcedureName}(@{parameterName})";
                        cmd.Parameters.AddWithValue(parameterName, parametervalue);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string resultProc = reader["json_result"]?.ToString();
                                if (!string.IsNullOrEmpty(resultProc))
                                {
                                    JArray array;

                                    // Detect object vs array
                                    if (resultProc.TrimStart().StartsWith("["))
                                        array = JArray.Parse(resultProc);
                                    else
                                        array = new JArray(JObject.Parse(resultProc));

                                    // Create columns
                                    foreach (JProperty column in ((JObject)array[0]).Properties())
                                        dtResult.Columns.Add(column.Name, typeof(string));

                                    // Fill rows
                                    foreach (JObject obj in array)
                                    {
                                        DataRow row = dtResult.NewRow();
                                        foreach (JProperty prop in obj.Properties())
                                            row[prop.Name] = prop.Value.ToString();
                                        dtResult.Rows.Add(row);
                                    }
                                }
                            }
                        }
                    }
                    else // Regular query
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandText = QueryOrProcedureName;

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.HasRows)
                                dtResult.Load(reader);
                        }
                    }
                }
                catch (Exception exc)
                {
                    dtResult.Columns.Add("Message");
                    DataRow dr = dtResult.NewRow();
                    dr[0] = exc.Message;
                    dtResult.Rows.Add(dr);
                    LogError("DBOperations", "OprationWithDB", exc, _transaction_Id);
                }
            }

            return dtResult;
        }
        public IDataReader ExecuteReader()
        {
            IDataReader reader = null;
            try
            {
                using (var connection = new NpgsqlConnection(_connection))
                using (var command = new NpgsqlCommand("", connection))
                {
                    connection.Open();
                    command.CommandType = CommandType.StoredProcedure;
                    reader = command.ExecuteReader(CommandBehavior.CloseConnection);
                }
            }
            catch (Exception ex)
            {

                throw ex;

            }

            return reader;
        }
        public IDataReader ExecuteReader(string commandtext)
        {
            IDataReader reader = null;
            try
            {
                using (var connection = new NpgsqlConnection(_connection))
                using (var command = new NpgsqlCommand("", connection))
                {
                    connection.Open();
                    command.CommandText = commandtext;
                    reader = this.ExecuteReader();
                }
            }
            catch (Exception ex)
            {

                throw ex;

            }

            return reader;
        }
        public object ExecuteScalar()
        {
            object obj = null;
            try
            {
                using (var connection = new NpgsqlConnection(_connection))
                using (var command = new NpgsqlCommand("", connection))
                {
                    connection.Open();
                    obj = command.ExecuteScalar();
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return obj;
        }
        public object ExecuteScalar(string commandtext)
        {
            object obj = null;
            try
            {
                using (var connection = new NpgsqlConnection(_connection))
                using (var command = new NpgsqlCommand("", connection))
                {
                    connection.Open();
                    command.CommandText = commandtext;
                    obj = this.ExecuteScalar();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return obj;
        }
        public int ExecuteNonQuery()
        {
            int flag = -1;
            try
            {
                using (var connection = new NpgsqlConnection(_connection))
                using (var command = new NpgsqlCommand("", connection))
                {
                    connection.Open();
                    flag = command.ExecuteNonQuery();
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return flag;
        }
        public int ExecuteNonQuery(string commandtext)
        {
            int flag = -1;
            try
            {
                using (var connection = new NpgsqlConnection(_connection))
                using (var command = new NpgsqlCommand("", connection))
                {
                    connection.Open();
                    command.CommandText = commandtext;
                    flag = this.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return flag;
        }
        public DataSet ExecuteDataSet()
        {
            NpgsqlDataAdapter da = null;
            DataSet ds = null;
            try
            {
                using (var connection = new NpgsqlConnection(_connection))
                using (var command = new NpgsqlCommand("", connection))
                {
                    da = new NpgsqlDataAdapter();
                    da.SelectCommand = (NpgsqlCommand)command;
                    ds = new DataSet();
                    da.Fill(ds);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return ds;
        }
        public DataTable ExecuteDataTable()
        {
            NpgsqlDataAdapter da = null;
            DataTable dt = null;
            try
            {
                using (var connection = new NpgsqlConnection(_connection))
                using (var command = new NpgsqlCommand("", connection))
                {
                    da = new NpgsqlDataAdapter();
                    da.SelectCommand = (NpgsqlCommand)command;
                    dt = new DataTable();
                    da.Fill(dt);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return dt;
        }
        public DataTable ExecuteDataTable(string commandtext)
        {
            DataTable dt = null;
            try
            {
                using (var connection = new NpgsqlConnection(_connection))
                using (var command = new NpgsqlCommand("", connection))
                {
                    command.CommandText = commandtext;
                    dt = this.ExecuteDataTable();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return dt;
        }
        public DataSet ExecuteDataSet(string commandtext)
        {
            DataSet ds = null;
            try
            {
                using (var connection = new NpgsqlConnection(_connection))
                using (var command = new NpgsqlCommand("", connection))
                {
                    command.CommandText = commandtext;
                    ds = this.ExecuteDataSet();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return ds;
        }
        public void AddParameter(string paramname, object paramvalue)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connection))
                using (var command = new NpgsqlCommand("", connection))
                {
                    connection.Open();
                    NpgsqlParameter param = new NpgsqlParameter(paramname, paramvalue);
                    command.Parameters.Add(param);
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public void AddParameter(IDataParameter param)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connection))
                using (var command = new NpgsqlCommand("", connection))
                {
                    command.Parameters.Add(param);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        //Method for Logging Error
        private void LogError(string folderName, string methodInfo, Exception exc, string transactionId)
        {
            _dBLogger.LogIntoDB(new LogInfo
            {
                EquipmentId = "DBOperation",
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
                EquipmentId = "InputMaterialRepository",
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