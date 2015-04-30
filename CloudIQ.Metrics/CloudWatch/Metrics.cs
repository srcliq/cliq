using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Amazon;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.EC2.Model;
using System.Configuration;

namespace CloudWatch
{
    class Metrics
    {
        public void GetMetrics()
        {
            var clientCW = new Amazon.CloudWatch.AmazonCloudWatchClient();
            var instanceList = GetInstanceList();
            var availableMetrics = clientCW.ListMetrics();            
            
            foreach (var metric in availableMetrics.Metrics)
            {                                   
                var statisticTypes = ConfigurationManager.AppSettings["MetricStatisticTypes"];
                var statisticTypeList = statisticTypes.Split(';').ToList();
                int metricPeriod = Convert.ToInt32(ConfigurationManager.AppSettings["MetricPeriod"]);                        
                var cwMetrics = GetCWMetrics(metric, statisticTypeList, metricPeriod);
                Instance instance = (metric.Dimensions.Count > 0) ? GetInstance(instanceList, metric.Dimensions[0].Value) : null;
                DBManager.SaveCWMetrics(metric, cwMetrics, instance, statisticTypeList);
                FileManager.SaveCWMetrics(metric, cwMetrics, instance, statisticTypeList);                      
            }
            S3Manager.UploadMetricFile();  
        }

        private static GetMetricStatisticsResponse GetCWMetrics(Metric metric, List<string> statisticTypeList, int metricPeriod)
        {
            var clientCW = new Amazon.CloudWatch.AmazonCloudWatchClient();            
            var cwMetrics = clientCW.GetMetricStatistics(new GetMetricStatisticsRequest
            {
                Namespace = metric.Namespace,
                MetricName = metric.MetricName,
                StartTime = DateTime.UtcNow.AddDays(-1),
                EndTime = DateTime.UtcNow,
                Period = metricPeriod,
                Statistics = statisticTypeList,
                Dimensions = metric.Dimensions
            });
            return cwMetrics;            
        }

        public static Instance GetInstance(List<Instance> instanceList, string instanceId)
        {
            foreach(var instance in instanceList)
            {
                if(instance.InstanceId == instanceId)
                {
                    return instance;
                }
            }
            return null;
        }

        public List<Instance> GetInstanceList()
        {
            var instanceList = new List<Instance>();
            var clientEC2 = new Amazon.EC2.AmazonEC2Client();
            var describeInstancesResponse = clientEC2.DescribeInstances();
            foreach (var reservation in describeInstancesResponse.Reservations)
            {
                instanceList.AddRange(reservation.Instances);                
            }
            return instanceList;
        }
    }
}
