using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Amazon;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;

namespace CloudWatch
{
    class CPUUtilization
    {
        public void GetCPUMetrics()
        {
            var clientCW = new Amazon.CloudWatch.AmazonCloudWatchClient();
            var instanceIdList = GetInstanceIdList();
            var availableMetrics = clientCW.ListMetrics();
            foreach (var instanceId in instanceIdList)
            {
                foreach (var metric in availableMetrics.Metrics)
                {
                    if (metric.MetricName == "CPUUtilization" && metric.Namespace == "AWS/EC2" && metric.Dimensions[0].Value == instanceId)
                    {
                        var cMetrics = clientCW.GetMetricStatistics(new GetMetricStatisticsRequest
                        {
                            Namespace = "AWS/EC2",
                            MetricName = "CPUUtilization",
                            StartTime = DateTime.UtcNow.AddDays(-1),
                            EndTime = DateTime.UtcNow,
                            Period = 300,
                            Statistics = new List<string> { "Average" },
                            Dimensions = new List<Dimension> { new Dimension { Name = "InstanceId", Value = instanceId } },
                            Unit = StandardUnit.Percent
                        });
                        DBManager.SaveCPUUtilizationMetrics(cMetrics.Datapoints, instanceId);
                    }
                }
            }
        }

        public List<string> GetInstanceIdList()
        {
            var instanceIdList = new List<string>();
            var clientEC2 = new Amazon.EC2.AmazonEC2Client();
            var describeInstancesResponse = clientEC2.DescribeInstances();
            foreach (var reservation in describeInstancesResponse.Reservations)
            {
                foreach (var instance in reservation.Instances)
                {
                    instanceIdList.Add(instance.InstanceId);
                }
            }
            return instanceIdList;
        }
    }
}
