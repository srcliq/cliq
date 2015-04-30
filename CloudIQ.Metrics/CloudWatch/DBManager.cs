using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Linq;
using System.Text;

using Amazon;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.EC2.Model;

namespace CloudWatch
{
    static class DBManager
    {
        public static void SaveCWMetrics(Metric metric, GetMetricStatisticsResponse cwMetrics, Instance instance, List<string> statisticTypeList)
        {
            DataSet ds = new DataSet();
            DataTable dt = new DataTable();

            var server = "cloudiq-dw-dev.cne8vdl5tict.us-west-2.redshift.amazonaws.com";
            var port = "5439";
            var masterUsername = "cloudiq";
            var masterUserPassword = "Cloudiq123";
            var DBName = "cloudiqdwdev";
            var instanceId = (instance != null) ? instance.InstanceId : null;
            var instanceType = (instance != null) ? instance.InstanceType : null;
            var query = new StringBuilder("insert into factusagemetrics(instanceid, namekey, value, metricstimestamp, createddate, instancetype, unit, statistic, namespace) values ");
            foreach (var dataPoint in cwMetrics.Datapoints)
            {
                foreach(var statisticType in statisticTypeList)
                {
                    query.Append(string.Format("('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}'),", instanceId, metric.MetricName, Convert.ToInt32(Common.GetPropertyValue(dataPoint, statisticType)), dataPoint.Timestamp, DateTime.UtcNow, instanceType, dataPoint.Unit.Value, statisticType, metric.Namespace));                
                }                
            }
            try
            {
                if (cwMetrics.Datapoints.Count > 0)
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
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.ReadKey();
            }
        }
    }
}
