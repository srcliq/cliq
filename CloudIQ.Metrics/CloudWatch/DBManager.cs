using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Linq;
using System.Text;

using Amazon;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;

namespace CloudWatch
{
    static class DBManager
    {
        public static void SaveCPUUtilizationMetrics(List<Datapoint> cpuMetricDataPoints, string instanceId)
        {
            DataSet ds = new DataSet();
            DataTable dt = new DataTable();

            string server = "cloudiq-dw-dev.cne8vdl5tict.us-west-2.redshift.amazonaws.com";
            string port = "5439";
            string masterUsername = "cloudiq";
            string masterUserPassword = "Cloudiq123";
            string DBName = "cloudiqdwdev";
            StringBuilder query = new StringBuilder("insert into factcpuutilization values ");
            foreach(var dataPoint in cpuMetricDataPoints)
            {
                query.Append(string.Format("('{0}','{1}','{2}'),", instanceId, Convert.ToInt32(dataPoint.Average), dataPoint.Timestamp));
            }
            try
            {
                // Create the ODBC connection string.
                //Redshift ODBC Driver - 64 bits                
                string connString = "Driver={Amazon Redshift (x64)};" +
                    String.Format("Server={0};Database={1};" +
                    "UID={2};PWD={3};Port={4};SSL=true;Sslmode=Require",
                    server, DBName, masterUsername,
                    masterUserPassword, port);                

                //Redshift ODBC Driver - 32 bits
                //string connString = "Driver={Amazon Redshift (x86)};" +
                //    String.Format("Server={0};Database={1};" +
                //    "UID={2};PWD={3};Port={4};SSL=true;Sslmode=Require",
                //    server, DBName, masterUsername,
                //    masterUserPassword, port);

                // Make a connection using the psqlODBC provider.
                OdbcConnection conn = new OdbcConnection(connString);
                conn.Open();

                // Try a simple query.
                string sql = query.ToString().TrimEnd(',');
                OdbcDataAdapter da = new OdbcDataAdapter();
                da.InsertCommand = new OdbcCommand(sql, conn);
                var recordCount = da.InsertCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.ReadKey();
            }
        }
    }
}
