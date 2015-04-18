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
        public void GetMetrics()
        {            
            var client = new Amazon.CloudWatch.AmazonCloudWatchClient();
            var cMetrics = client.GetMetricStatistics(new GetMetricStatisticsRequest
            {
                Namespace = "AWS/EC2",
                MetricName = "CPUUtilization",
                StartTime = DateTime.Now.AddDays(-10),
                EndTime = DateTime.Now,
                Period = 3000,
                Statistics = new List<string> { "Average" },
                Dimensions = new List<Dimension> { new Dimension { Name = "InstanceId", Value = "i-90e7459d" } },
                Unit = StandardUnit.Count
            });
            var metrics = client.ListMetrics();
        }        
    }
}
