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
    class CPUUtilization
    {
        public void GetCPUMetrics()
        {
            var clientCW = new Amazon.CloudWatch.AmazonCloudWatchClient();
            var instanceList = GetInstanceList();
            var availableMetrics = clientCW.ListMetrics();
            var requiredMetrics = ConfigurationManager.AppSettings["EC2Metrics"];
            var requiredMetricsList = requiredMetrics.Split(';').ToList();
            var metricNamespaces = ConfigurationManager.AppSettings["MetricNamespaces"];
            var metricNamespacesList = metricNamespaces.Split(';').ToList();
            
            foreach (var instance in instanceList)
            {
                foreach (var metric in availableMetrics.Metrics)
                {
                    if (requiredMetricsList.Contains(metric.MetricName) && metricNamespacesList.Contains(metric.Namespace) && metric.Dimensions[0].Value == instance.InstanceId)
                    {
                        var statisticTypes = ConfigurationManager.AppSettings[metric.MetricName + "StatisticTypes"];
                        var statisticTypeList = statisticTypes.Split(';').ToList();
                        int metricPeriod = Convert.ToInt32(ConfigurationManager.AppSettings["MetricPeriod"]);
                        string metricUnit = ConfigurationManager.AppSettings[metric.MetricName + "MetricUnit"];
                        var standardMetricUnit = StandardUnit.FindValue(metricUnit);
                        var cwMetrics = GetCWMetrics(instance, metric.MetricName, metric.Namespace, statisticTypeList, metricPeriod, standardMetricUnit);
                        DBManager.SaveCWMetrics(metric, cwMetrics, instance, statisticTypeList, standardMetricUnit);                        
                    }
                }
            }
        }

        private static GetMetricStatisticsResponse GetCWMetrics(Instance instance, string metricName, string metricNamespace, List<string> statisticTypeList, int metricPeriod, string standardMetricUnit)
        {
            var clientCW = new Amazon.CloudWatch.AmazonCloudWatchClient();
            
            var cwMetrics = clientCW.GetMetricStatistics(new GetMetricStatisticsRequest
            {
                Namespace = metricNamespace,
                MetricName = metricName,
                StartTime = DateTime.UtcNow.AddDays(-1),
                EndTime = DateTime.UtcNow,
                Period = metricPeriod,
                Statistics = statisticTypeList,
                Dimensions = new List<Dimension> { new Dimension { Name = "InstanceId", Value = instance.InstanceId } },
                Unit = standardMetricUnit
            });
            return cwMetrics;            
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
