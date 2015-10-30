using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nest;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Configuration;

namespace TopologyReader
{
    public static class FlowLogManager
    {
        //private static Dictionary<string, string> subnets;
        private static string sumOfPacketsAggKey = "sumOfPackets";
        private static string sumOfBytesAggKey = "sumOfBytes";
        private static string anonymousSubnet = "unknown";
        private static char ipSplitChar = ',';

        public static void ReadES(Dictionary<string, string> subnets, IDatabase db, string dateKey)
        {
            Console.WriteLine("Start of flowlog aggregation");
            //string index = "cwl-2015.09.18";
            //string type = "CloudTrail/Flowlogs";
            string index = ConfigurationManager.AppSettings["ESIndex"];
            string type = ConfigurationManager.AppSettings["ESIndexType"];

            //subnets = GetSubnets();

            //var node = new Uri("http://52.25.80.139:9200");
            var node = new Uri(ConfigurationManager.AppSettings["ESEndPoint"]);
            var settings = new ConnectionSettings(node);
            var esClient = new ElasticClient(settings);

            var result = esClient.Search<FlowLog>(s => s.Index(index).Type(type).Aggregations(a => GetAggregationDescriptor(a, subnets)));
            var dKey = string.Empty;
            if (result != null && result.Aggregations != null && result.Aggregations.Any())
            {
                var flowLogAggregations = ParseAggregation(result.Aggregations);
                foreach (var aggregation in flowLogAggregations)
                {
                    //Console.WriteLine(aggregation);
                    string aggregationJson = JsonConvert.SerializeObject(aggregation);
                    dKey = string.Format("{0}-subnettraffic", aggregation.SourceSubnet);
                    db.SetAdd(dKey, aggregationJson);                    
                }
            }

            Console.WriteLine("End of flowlog aggregation");
        }

        #region GetSubnets
        //private static Dictionary<string, string> GetSubnets()
        //{
        //    subnets = new Dictionary<string, string>();

        //    subnets.Add("subnet1", "172.31.10.115,172.31.24.161,66.155.40.249");

        //    subnets.Add("subnet2", "203.178.148.19,114.43.14.109,178.71.248.224");

        //    subnets.Add("subnet3", "198.55.111.50,52.25.149.105,58.218.213.208");

        //    subnets.Add("subnet4", "199.203.59.121,111.123.180.44,172.31.18.125");

        //    return subnets;
        //}
        #endregion

        #region GetAggregationDescriptor
        private static AggregationDescriptor<FlowLog> GetAggregationDescriptor(AggregationDescriptor<FlowLog> descriptor, Dictionary<string, string> subnets)
        {
            if (subnets.Any())
            {
                var ipList = new List<string>();
                foreach (var subnet in subnets)
                {
                    var subnetIPs = subnet.Value.Split(ipSplitChar);
                    ipList.AddRange(subnetIPs);

                    descriptor.Filters(subnet.Key, f => f.Filters(t => t.Terms(field => field.srcaddr, subnetIPs))
                            .Aggregations(a => GetNestedAggregationDescriptor(a, subnets, subnet.Key)));
                }
                if (ipList.Any())
                {
                    descriptor.Filters(anonymousSubnet, f => f.Filters(t => t.Not(n => n.Terms(field => field.srcaddr, ipList)))
                                .Aggregations(a => GetNestedAggregationDescriptor(a, subnets, anonymousSubnet)));
                }
            }
            return descriptor;
        }
        #endregion

        #region GetNestedAggregationDescriptor
        private static AggregationDescriptor<FlowLog> GetNestedAggregationDescriptor(AggregationDescriptor<FlowLog> descriptor, Dictionary<string, string> subnets, string sourceSubnetKey)
        {
            if (subnets.Any())
            {
                var ipList = new List<string>();
                foreach (var subnet in subnets)
                {
                    if (subnet.Key != sourceSubnetKey) //Not sure if we need to filter this
                    {
                        var subnetIPs = subnet.Value.Split(ipSplitChar);
                        ipList.AddRange(subnetIPs);
                        descriptor.Filters(subnet.Key, f => f.Filters(t => t.Terms(field => field.dstaddr, subnetIPs))
                                .Aggregations(a => a.Sum(sumOfBytesAggKey, byField => byField.Field(fieldName => fieldName.bytes))
                                                    .Sum(sumOfPacketsAggKey, byField => byField.Field(fieldName => fieldName.packets))));
                    }
                }
                if (ipList.Any())
                {
                    descriptor.Filters(anonymousSubnet, f => f.Filters(t => t.Not(n => n.Terms(field => field.dstaddr, ipList)))
                                .Aggregations(a => a.Sum(sumOfBytesAggKey, byField => byField.Field(fieldName => fieldName.bytes))
                                                    .Sum(sumOfPacketsAggKey, byField => byField.Field(fieldName => fieldName.packets))));
                }
            }
            return descriptor;
        }
        #endregion

        #region ParseAggregation
        private static List<FlowLogAggregation> ParseAggregation(IDictionary<string, IAggregation> aggregations)
        {
            List<FlowLogAggregation> parsedAggregations = new List<FlowLogAggregation>();

            foreach (string srcSubnetKey in aggregations.Keys)
            {
                var srcSubnetAggBucket = aggregations[srcSubnetKey] as Nest.Bucket;
                if (srcSubnetAggBucket != null && srcSubnetAggBucket.Items != null)
                {
                    var srcSubnetAgg = srcSubnetAggBucket.Items.FirstOrDefault() as Nest.SingleBucket;
                    if (srcSubnetAgg != null && srcSubnetAgg.Aggregations != null && srcSubnetAgg.Aggregations.Any())
                    {
                        foreach (var dstSubnetKey in srcSubnetAgg.Aggregations.Keys)
                        {
                            var dstSubnetAggBucket = srcSubnetAgg.Aggregations[dstSubnetKey] as Nest.Bucket;
                            if (dstSubnetAggBucket != null && dstSubnetAggBucket.Items != null)
                            {
                                var dstSubnetAgg = dstSubnetAggBucket.Items.FirstOrDefault() as Nest.SingleBucket;
                                if (dstSubnetAgg != null)
                                {
                                    var flowLogAggregation = new FlowLogAggregation
                                    {
                                        SourceSubnet = srcSubnetKey,
                                        DestinationSubnet = dstSubnetKey,
                                        IsUnknownSourceSubnet = srcSubnetKey == anonymousSubnet,
                                        IsUnknownDestinationSubnet = dstSubnetKey == anonymousSubnet,
                                        NumberOfFlowLogs = dstSubnetAgg.DocCount
                                    };

                                    if (dstSubnetAgg.Aggregations.ContainsKey(sumOfBytesAggKey))
                                    {
                                        var sumMetric = dstSubnetAgg.Aggregations[sumOfBytesAggKey] as Nest.ValueMetric;
                                        if (sumMetric != null && sumMetric.Value.HasValue)
                                        {
                                            flowLogAggregation.SumOfBytes = Convert.ToInt64(sumMetric.Value.Value);
                                        }
                                    }

                                    if (dstSubnetAgg.Aggregations.ContainsKey(sumOfPacketsAggKey))
                                    {
                                        var sumMetric = dstSubnetAgg.Aggregations[sumOfPacketsAggKey] as Nest.ValueMetric;
                                        if (sumMetric != null && sumMetric.Value.HasValue)
                                        {
                                            flowLogAggregation.SumOfPackets = Convert.ToInt64(sumMetric.Value.Value);
                                        }
                                    }

                                    parsedAggregations.Add(flowLogAggregation);
                                }
                            }
                        }
                    }
                }
            }

            return parsedAggregations;
        }
        #endregion

    }

    #region FlowLog
    public class FlowLog
    {
        public long protocol { get; set; }
        public double account_id { get; set; }
        public long bytes { get; set; }
        public string interface_id { get; set; }
        public long packets { get; set; }
        public long dstport { get; set; }
        public long srcport { get; set; }
        public string log_status { get; set; }
        public long version { get; set; }
        public string action { get; set; }
        public string dstaddr { get; set; }
        public double start { get; set; }
        public double end { get; set; }
        public string srcaddr { get; set; }

        public override string ToString()
        {
            return string.Format("protocol:{0}\naccount_Id:{1}\npackets:{2}\ndstport:{3}\nsrcport:{4}\nlog_status:{5}\nversion:{6}\naction:{7}\ndstaddr:{8}\nstart:{9}\nend:{10}\nsrcaddr:{11}",
                protocol, account_id, packets, dstport, srcport, log_status, version, action, dstaddr, start, end, srcaddr);
        }
    }
    #endregion

    #region FlowLogAggregation
    public class FlowLogAggregation
    {
        public string SourceSubnet { get; set; }
        public string DestinationSubnet { get; set; }
        public bool IsUnknownSourceSubnet { get; set; }
        public bool IsUnknownDestinationSubnet { get; set; }
        public long NumberOfFlowLogs { get; set; }
        public long SumOfBytes { get; set; }
        public long SumOfPackets { get; set; }


        public override string ToString()
        {
            return string.Format("Source: {0}, Destination: {1}, Count: {2}, Bytes: {3},  Packets: {4}"
                                            , SourceSubnet, DestinationSubnet, NumberOfFlowLogs, SumOfBytes, SumOfPackets);
        }
    }
    #endregion
}
