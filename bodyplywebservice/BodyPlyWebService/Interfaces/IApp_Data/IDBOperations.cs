using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BodyPlyWebService.Interfaces.IApp_Data
{
    public interface IDBOperations
    {
        NpgsqlConnection OpenConnection();
        void CloseConnection(NpgsqlConnection _connection);
        bool ItemExistsInDatabase(string query);
        DataTable OprationWithDB(String commandType, string QueryOrProcedureName, String parametervalue);
        DataTable OprationWith_DB(string commandType, string QueryOrProcedureName, string parametervalue);
        IDataReader ExecuteReader();

        IDataReader ExecuteReader(string commandtext);

        object ExecuteScalar();

        object ExecuteScalar(string commandtext);

        int ExecuteNonQuery();

        int ExecuteNonQuery(string commandtext);

        DataSet ExecuteDataSet();

        DataTable ExecuteDataTable();

        DataTable ExecuteDataTable(string commandtext);

        DataSet ExecuteDataSet(string commandtext);

        void AddParameter(string paramname, object paramvalue);

        void AddParameter(IDataParameter param);
    }
}
