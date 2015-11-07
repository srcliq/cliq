using Amazon.EC2.Model;
using log4net;
using Nest;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopologyReader
{    
    public static class FlowLogManager
    {
        private static readonly ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static string sumOfPacketsAggKey = "sumOfPackets";
        private static string sumOfBytesAggKey = "sumOfBytes";
        private static string actionAggKey = "Action";
        private static string protocolAggKey = "Protocol";
        private static string dstPortAggKey = "DstPort";

        public static void ReadES(IDatabase db, string dataKey, int durationType, List<Subnet> subnets, List<VpnGateway> vpnGateways, List<InternetGateway> internetGateways)
        {
            List<AggregationParameter> parameters = new List<AggregationParameter>();
            int logDurationinMin = 15;
            int.TryParse(ConfigurationManager.AppSettings["FlowLogDurationMin"], out logDurationinMin);
            foreach (var subnet in subnets)
            {
                foreach (var otherSubnet in subnets)
                {
                    if (subnet.SubnetId != otherSubnet.SubnetId)
                    {
                        AggregationParameter parameter = new AggregationParameter();
                        parameter.AggregationIdentifier = string.Format("{0}-traffic-s2s-{1}-{2}", dataKey, subnet.SubnetId, otherSubnet.SubnetId);
                        parameter.SourceAggExpression = new Expression { Field = "src_subnetid", Values = new List<string> { subnet.SubnetId.Replace("-", "") } };
                        parameter.DestAggExpression = new Expression { Field = "dest_subnetid", Values = new List<string> { otherSubnet.SubnetId.Replace("-", "") } };
                        parameters.Add(parameter);
                    }
                }
            }

            foreach (var subnet in subnets)
            {
                foreach (var vpnGateway in vpnGateways)
                {
                    AggregationParameter parameterST = new AggregationParameter();
                    parameterST.AggregationIdentifier = string.Format("{0}-traffic-s2v-{1}-{2}", dataKey, subnet.SubnetId, vpnGateway.VpnGatewayId);
                    parameterST.SourceAggExpression = new Expression { Field = "src_subnetid", Values = new List<string> { subnet.SubnetId.Replace("-", "") } };
                    parameterST.DestAggExpression = new Expression { Field = "gatewayid", Values = new List<string> { vpnGateway.VpnGatewayId.Replace("-", "") } };
                    parameters.Add(parameterST);

                    AggregationParameter parameterVG = new AggregationParameter();
                    parameterVG.AggregationIdentifier = string.Format("{0}-traffic-v2s-{1}-{2}", dataKey, vpnGateway.VpnGatewayId, subnet.SubnetId);
                    parameterVG.SourceAggExpression = new Expression { Field = "gatewayid", Values = new List<string> { vpnGateway.VpnGatewayId.Replace("-", "") } };
                    parameterVG.DestAggExpression = new Expression { Field = "dest_subnetid", Values = new List<string> { subnet.SubnetId.Replace("-", "") } };
                    parameters.Add(parameterVG);
                }

                foreach (var igGateway in internetGateways)
                {
                    AggregationParameter parameterST = new AggregationParameter();
                    parameterST.AggregationIdentifier = string.Format("{0}-traffic-s2i-{1}-{2}", dataKey, subnet.SubnetId, igGateway.InternetGatewayId);
                    parameterST.SourceAggExpression = new Expression { Field = "src_subnetid", Values = new List<string> { subnet.SubnetId.Replace("-", "") } };
                    parameterST.DestAggExpression = new Expression { Field = "gatewayid", Values = new List<string> { igGateway.InternetGatewayId.Replace("-", "") } };
                    parameters.Add(parameterST);

                    AggregationParameter parameterIG = new AggregationParameter();
                    parameterIG.AggregationIdentifier = string.Format("{0}-traffic-i2s-{1}-{2}", dataKey, igGateway.InternetGatewayId, subnet.SubnetId);
                    parameterIG.SourceAggExpression = new Expression { Field = "gatewayid", Values = new List<string> { igGateway.InternetGatewayId.Replace("-", "") } };
                    parameterIG.DestAggExpression = new Expression { Field = "dest_subnetid", Values = new List<string> { subnet.SubnetId.Replace("-", "") } };
                    parameters.Add(parameterIG);
                }
            }

            var flowLogAggregations = new List<FlowLogAggregation>();
            if (durationType == 0)
            {
                flowLogAggregations = ReadES(parameters, logDurationinMin);
            }
            
            foreach (var aggregation in flowLogAggregations)
            {                
                if(aggregation.SumOfBytes > 0)
                {                    
                    Log.Debug(aggregation);
                    aggregation.SourceType = "";
                    aggregation.SourceId = "";
                    aggregation.DestinationType = "";
                    aggregation.DestinationId = "";
                    var aggregationJson = JsonConvert.SerializeObject(aggregation);
                    Reader.AddToRedisWithExpiry(string.Format("{0}-{1}-{2}-{3}", aggregation.AggregationIdentifier, aggregation.Action, aggregation.Protocol, aggregation.DstPort), aggregationJson, db);
                }                
            }
        }

        public static List<FlowLogAggregation> ReadES(List<AggregationParameter> parameters, int logDurationInMin)
        {
            //string index = "cwl-2015.10.25";
            string index = ConfigurationManager.AppSettings["ESIndexPrefix"] + DateTime.UtcNow.ToString("yyyy.MM.dd"); //2015.09.18
            //string type = "CloudTrail/Flowlogs";
            string type = ConfigurationManager.AppSettings["ESIndexType"];

            var node = new Uri(ConfigurationManager.AppSettings["ESEndPoint"]);

            var settings = new ConnectionSettings(node);
            //settings.SetBasicAuthentication("admin", "admin");
            var currentUnixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds; ;
            var aggStart = currentUnixTimestamp - (logDurationInMin*60);
            var aggEnd = currentUnixTimestamp;
            var esClient = new ElasticClient(settings);

            var result = esClient.Search<FlowLog>(s => s.Index(index).Type(type)
                //end of flowlog capture window should fall between the timeframe we are aggregating on
                .Query(q => q.Filtered(filtered => 
                    filtered.Filter(filter => filter.And(expr1 => expr1.Range(r => r.GreaterOrEquals(aggStart).OnField(f => f.end)),
                                                                  expr2 => expr2.Range(r => r.LowerOrEquals(aggEnd).OnField(f => f.end))
                                                                  //,
                                                                  //expr3 => expr3.Term(p => p.action, "accept")
                                                                  ))
                      ))
                .Aggregations(a => GetAggregationDescriptor(a, parameters)));

            if (result != null && result.Aggregations != null && result.Aggregations.Any())
            {
                var flowLogAggregations = ParseAggregation(result.Aggregations);
                return flowLogAggregations;                
            }
            return null;
        }

        #region GetAggregationDescriptor
        private static AggregationDescriptor<FlowLog> GetAggregationDescriptor(AggregationDescriptor<FlowLog> descriptor, List<AggregationParameter> parameters)
        {
            if (parameters != null && parameters.Any())
            {
                foreach (var parameter in parameters)
                {
                    if (parameter.SourceAggExpression != null)
                    {
                        descriptor.Filters(parameter.AggregationIdentifier, f => f.Filters(filter =>
                        {
                            FilterContainer container = new FilterContainer();
                            container &= filter.Query(t => t.Terms(parameter.SourceAggExpression.Field, parameter.SourceAggExpression.Values));
                            if (parameter.DestAggExpression != null)
                            {
                                container &= filter.Query(t => t.Terms(parameter.DestAggExpression.Field, parameter.DestAggExpression.Values));
                            }

                            return container;
                        }
                            ).Aggregations(ag1 => ag1.Terms(actionAggKey, t1 => t1.Field(f1 => f1.action).
                                Aggregations(ag2 => ag2.Terms(protocolAggKey, t2 => t2.Field(f2 => f2.protocol).
                                Aggregations(ag3 => ag3.Terms(dstPortAggKey, t3 => t3.Field(f3 => f3.dstport).
                                Aggregations(a => a.Sum(sumOfBytesAggKey, byField => byField.Field(fieldName => fieldName.bytes))
                                                    .Sum(sumOfPacketsAggKey, byField => byField.Field(fieldName => fieldName.packets)))))))))

                             );

                    }
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
                        if (srcSubnetAgg.Aggregations.ContainsKey(actionAggKey))
                        {
                            var actionAgg = srcSubnetAgg.Aggregations[actionAggKey] as Nest.Bucket;

                            if (actionAgg != null && actionAgg.Items.Any())
                            {
                                foreach (KeyItem actionAggItem in actionAgg.Items)
                                {
                                    if (actionAggItem.Aggregations != null && actionAggItem.Aggregations.Any())
                                    {
                                        var protocolAgg = actionAggItem.Aggregations[protocolAggKey] as Nest.Bucket;

                                        if (protocolAgg != null && protocolAgg.Items.Any())
                                        {
                                            foreach (KeyItem protocolAggItem in protocolAgg.Items)
                                            {
                                                if (protocolAggItem.Aggregations != null && protocolAggItem.Aggregations.Any())
                                                {
                                                    if (protocolAggItem.Aggregations.ContainsKey(dstPortAggKey))
                                                    {
                                                        var dstPortAgg = protocolAggItem.Aggregations[dstPortAggKey] as Nest.Bucket;

                                                        if (dstPortAgg != null && dstPortAgg.Items.Any())
                                                        {
                                                            foreach (KeyItem dstPortAggItem in dstPortAgg.Items)
                                                            {
                                                                var flowLogAggregation = new FlowLogAggregation
                                                                {
                                                                    AggregationIdentifier = srcSubnetKey,
                                                                    FlowLogsCount = dstPortAggItem.DocCount,
                                                                    Action = actionAggItem.Key,
                                                                    Protocol = protocolAggItem.Key,
                                                                    DstPort = dstPortAggItem.Key
                                                                };

                                                                if (dstPortAggItem.Aggregations.ContainsKey(sumOfBytesAggKey))
                                                                {
                                                                    var sumMetric = dstPortAggItem.Aggregations[sumOfBytesAggKey] as Nest.ValueMetric;
                                                                    if (sumMetric != null && sumMetric.Value.HasValue)
                                                                    {
                                                                        flowLogAggregation.SumOfBytes = Convert.ToInt64(sumMetric.Value.Value);
                                                                    }
                                                                }

                                                                if (dstPortAggItem.Aggregations.ContainsKey(sumOfPacketsAggKey))
                                                                {
                                                                    var sumMetric = dstPortAggItem.Aggregations[sumOfPacketsAggKey] as Nest.ValueMetric;
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
                                    }
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
        public string source_vpcid { get; set; }
        public long protocol { get; set; }
        public double account_id { get; set; }
        public long bytes { get; set; }
        public string source_subnetid { get; set; }
        public string dest_vpcid { get; set; }
        public string interface_id { get; set; }
        public long packets { get; set; }
        public string seondnewfield { get; set; }
        public long dstport { get; set; }
        public long srcport { get; set; }
        public string log_status { get; set; }
        public long version { get; set; }
        public string action { get; set; }
        public string dstaddr { get; set; }
        public double start { get; set; }
        public string dest_subnetid { get; set; }
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
        [JsonIgnore]
        public string AggregationIdentifier { get; set; }
        public string SourceType { get; set; }
        public string DestinationType { get; set; }
        public string SourceId { get; set; }
        public string DestinationId { get; set; }
        public string Action { get; set; }
        public string Protocol { get; set; }
        public string DstPort { get; set; }
        public long FlowLogsCount { get; set; }
        public long SumOfBytes { get; set; }
        public long SumOfPackets { get; set; }

        public override string ToString()
        {
            return string.Format("Aggregation={0}, Action={1}, Protocol={2}, DstPort={3}, Count={4}, Bytes={5}, Packets={6}", AggregationIdentifier, Action, Protocol, DstPort, FlowLogsCount, SumOfBytes, SumOfPackets);
        }
    }
    #endregion

    #region AggregationParameter
    public class AggregationParameter
    {
        public string AggregationIdentifier { get; set; }
        public Expression SourceAggExpression { get; set; }
        public Expression DestAggExpression { get; set; }
    }
    #endregion

    #region Expression
    public class Expression
    {
        public string Field { get; set; }
        public List<string> Values { get; set; }        
    }    
    #endregion
}
