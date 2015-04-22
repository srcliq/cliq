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
            StringBuilder query = new StringBuilder("insert into factusagemetrics(instanceid, namekey, value, metricstimestamp, createddate) values ");
            foreach(var dataPoint in cpuMetricDataPoints)
            {
                query.Append(string.Format("('{0}','{1}','{2}','{3}','{4}'),", instanceId, "CPUUtilMinPercent", Convert.ToInt32(dataPoint.Minimum), dataPoint.Timestamp, DateTime.UtcNow));
                query.Append(string.Format("('{0}','{1}','{2}','{3}','{4}'),", instanceId, "CPUUtilMaxPercent", Convert.ToInt32(dataPoint.Maximum), dataPoint.Timestamp, DateTime.UtcNow));
                query.Append(string.Format("('{0}','{1}','{2}','{3}','{4}'),", instanceId, "CPUUtilAvgPercent", Convert.ToInt32(dataPoint.Average), dataPoint.Timestamp, DateTime.UtcNow));
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
