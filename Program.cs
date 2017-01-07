using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESFlowLogAggregation
{    
    class Program
    {        
        private static string sumOfPacketsAggKey = "sumOfPackets";
        private static string sumOfBytesAggKey = "sumOfBytes";
        private static string actionAggKey = "Action";
        private static string protocolAggKey = "Protocol";
        private static string dstPortAggKey = "DstPort";
                
        static void Main(string[] args)
        {
            string index = "cwl-2015.11.06";
            string type = "CloudTrail/Flowlogs";
                        
            var parameters = GetAggregationParameters();
            var node = new Uri("http://54.191.199.66:9200");
            
            var settings = new ConnectionSettings(node);            
            //settings.SetBasicAuthentication("admin", "admin");

            var currentUnixTimestamp = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

            var esClient = new ElasticClient(settings);
            
            var result = esClient.Search<FlowLog>(s => s.Index(index).Type(type)
                            .Query(q => q.Filtered(filtered => filtered.Filter(filter => filter.And(expr1 => expr1.Range(r => r.GreaterOrEquals(currentUnixTimestamp).OnField(f => f.start)),
                                                                                              expr2 => expr2.Range(r => r.LowerOrEquals(currentUnixTimestamp).OnField(f => f.end))))))
                            .Aggregations(a => GetAggregationDescriptor(a, parameters)));

            if (result != null && result.Aggregations != null && result.Aggregations.Any())
            {
                var flowLogAggregations = ParseAggregation(result.Aggregations);
                foreach (var aggregation in flowLogAggregations)
                {
                    Console.WriteLine(aggregation);
                }
            }

            Console.Read();
        }

        #region GetAggregationParameters
        private static List<AggregationParameter> GetAggregationParameters()
        {
            List<AggregationParameter> parameters = new List<AggregationParameter>();
            AggregationParameter parameter = new AggregationParameter();
            parameter.AggregationIdentifier = "dummy123";
            parameter.SourceAggExpression = new Expression() { Field = "dest_subnetid", Values = new List<string> { "subnet4d64263a" } };
            //parameter.SourceAggExpression = new Expression { Field = "dstaddr", Values = new List<string> { "172.31.24.161", "177.54.146.100" } };

            AggregationParameter parameter1 = new AggregationParameter();
            parameter1.AggregationIdentifier = "dummy124";
            parameter1.SourceAggExpression = new Expression() { Field = "dest_subnetid", Values = new List<string> { "subnetec64269b" } };
            //parameter1.DestAggExpression = new Expression { Field = "dstaddr", Values = new List<string> { "172.31.18.125" } };

            parameters.Add(parameter);
            //parameters.Add(parameter1);
            
            return parameters;
        }
        #endregion

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
                                container &= filter.Query(t=>t.Terms(parameter.SourceAggExpression.Field, parameter.SourceAggExpression.Values));
                                if(parameter.DestAggExpression != null)
                                {
                                    container &= filter.Query(t=>t.Terms(parameter.DestAggExpression.Field, parameter.DestAggExpression.Values));
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
        public double account_id { get; set; }
        public string action { get; set; }
        public long bytes { get; set; }
        public string dest_A { get; set; }
        public string dest_B { get; set; }
        public string dest_C { get; set; }
        public string dest_D { get; set; }
        public string dest_subnetid { get; set; }
        public string dest_vpcid { get; set; }
        public string dstaddr { get; set; }
        public long dstport { get; set; }
        public double end { get; set; }
        public string interface_id { get; set; }
        public string is_originating { get; set; }
        public string is_platformservice { get; set; }
        public string is_reservedip { get; set; }
        public string latestts { get; set; }
        public string log_status { get; set; }
        public long packets { get; set; }
        public long protocol { get; set; }
        public string src_A { get; set; }
        public string src_B { get; set; }
        public string src_C { get; set; }
        public string src_D { get; set; }
        public string src_subnetid { get; set; }
        public string src_vpcid { get; set; }
        public string srcaddr { get; set; }
        public long srcport { get; set; }
        public double start { get; set; }
        public long version { get; set; }
        
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
        public string AggregationIdentifier { get; set; }
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
