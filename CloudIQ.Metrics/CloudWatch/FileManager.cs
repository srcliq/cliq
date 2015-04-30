using Amazon.CloudWatch.Model;
using Amazon.EC2.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CloudWatch
{
    class FileManager
    {
        public static void SaveCWMetrics(Metric metric, GetMetricStatisticsResponse cwMetrics, Instance instance, List<string> statisticTypeList)
        {
            var instanceId = (instance != null) ? instance.InstanceId : null;
            var instanceType = (instance != null) ? instance.InstanceType : null;
            string path = string.Format("CWMetrics{0}.csv", DateTime.Now.ToString("MMddyyyy"));
            if (!File.Exists(path))
            {
                using (StreamWriter sw = File.CreateText(path))
                {
                    var header = "instanceid,namekey,value,metricstimestamp,createddate,instancetype,unit,statistic,namespace";
                    sw.WriteLine(header);
                    sw.Flush();
                }
            }
            using(var sw = File.AppendText(path))
            {                
                foreach (var dataPoint in cwMetrics.Datapoints)
                {
                    foreach (var statisticType in statisticTypeList)
                    {
                        var line = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}", instanceId, metric.MetricName, Convert.ToInt32(Common.GetPropertyValue(dataPoint, statisticType)), dataPoint.Timestamp, DateTime.UtcNow, instanceType, dataPoint.Unit.Value, statisticType, metric.Namespace);
                        sw.WriteLine(line);
                        sw.Flush();
                    }
                }
            }                       
        }
    }
}
