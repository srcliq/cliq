using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using Amazon;
using Amazon.AutoScaling;
using Amazon.AutoScaling.Model;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.ElasticLoadBalancing;
using Amazon.ElasticLoadBalancing.Model;
using Amazon.IdentityManagement;
using CsvHelper;
using Nest;
using Newtonsoft.Json;
using StackExchange.Redis;
using TopologyReader.Data;
using ConnectionSettings = Amazon.ElasticLoadBalancing.Model.ConnectionSettings;
using DescribeInstancesRequest = Amazon.EC2.Model.DescribeInstancesRequest;
using DescribeInstancesResponse = Amazon.EC2.Model.DescribeInstancesResponse;
using Filter = Amazon.EC2.Model.Filter;
using Tag = Amazon.EC2.Model.Tag;
using Vpc = Amazon.EC2.Model.Vpc;
using Amazon.RDS;
using System.Text.RegularExpressions;
using Amazon.ECS;
using Amazon.ECS.Model;
using Amazon.ElasticMapReduce;
using log4net;
using TopologyReader.Helpers;

namespace TopologyReader
{
    internal class Reader
    {
        private static readonly ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
            
        public static void Main(string[] args)
        {
            try
            {
                //AWSConfigReader.ProcessConfigMessages();
                //return;
                int writeTopology = 0;
                int readFlowLogs = 0;
                int flowLogDurationType = 0;
                int ttl = 5;
                int.TryParse(ConfigurationManager.AppSettings["RedisKeysTTLDays"], out ttl);
                RedisManager.SetRedisTTL(ttl);

                GetInputs(args, ref writeTopology, ref readFlowLogs, ref flowLogDurationType);

                AutoMapper.Mapper.CreateMap<Amazon.EC2.Model.Subnet, TopologyReader.Data.Subnet>();
                AutoMapper.Mapper.CreateMap<Amazon.EC2.Model.Instance, TopologyReader.Data.Instance>();

                var accountNumber = Common.GetAccountNumber();
                if (string.IsNullOrEmpty(accountNumber))
                {
                    Log.Error("Unable to read the account number");
                    return;
                }
                //ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(ConfigurationManager.AppSettings["RedisEndPoint"]);
                IDatabase db = RedisManager.GetRedisDatabase();
                foreach (var endPoint in RegionEndpoint.EnumerableAllRegions)
                {
                    if (writeTopology == 1)
                    {
                        WriteTopology(accountNumber, endPoint, db);
                    }                    
                    if (readFlowLogs == 1)
                    {
                        ReadFlowLogs(accountNumber, endPoint, flowLogDurationType, db);
                    }
                }
                if (writeTopology == 2)
                {
                    AWSConfigReader.ProcessConfigMessages();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Exception occurred.", ex);
            }                                    
        }

        private static void GetInputs(string[] args, ref int writeTopology, ref int readFlowLogs, ref int flowLogDurationType)
        {
            switch (args.Length)
            {
                case 0:
                    writeTopology = 1;
                    break;
                case 1:
                    if (!int.TryParse(args[0], out writeTopology))
                    {
                        Log.Error("Invalid arguments");
                    }
                    break;
                case 2:
                    if (!int.TryParse(args[0], out writeTopology) || !int.TryParse(args[1], out readFlowLogs))
                    {
                        Log.Error("Invalid arguments");
                    }
                    break;
                case 3:
                    if (!int.TryParse(args[0], out writeTopology) || !int.TryParse(args[1], out readFlowLogs) || !int.TryParse(args[2], out flowLogDurationType))
                    {
                        Log.Error("Invalid arguments");
                    }
                    break;
                default:
                    break;
            }
        }

        

        public static void ReadFlowLogs(string accountNumber, RegionEndpoint regionEndPoint, int durationType, IDatabase db)
        {
            Log.InfoFormat("Start reading flowlogs and writing traffic data to redis ({0})", regionEndPoint.SystemName);
            IAmazonEC2 ec2 = new Amazon.EC2.AmazonEC2Client(regionEndPoint);
            try
            {
                ec2.DescribeSubnets();
            }
            catch (Exception ex)
            {
                Log.InfoFormat("Unable to read subnets: {0}", ex.Message);
                return;
            }

            var dataKey = Common.GetDataKey(accountNumber, regionEndPoint);
            db.SetAdd("TST", dataKey);
            db.StringSet(string.Format("LATESTTST-{0}-{1}", accountNumber, regionEndPoint.SystemName), dataKey);

            var subnetResponse = ec2.DescribeSubnets();
            var vgResponse = ec2.DescribeVpnGateways();
            var igResponse = ec2.DescribeInternetGateways();
                        
            try
            {
                FlowLogManager.ReadES(db, dataKey, durationType, subnetResponse.Subnets, vgResponse.VpnGateways, igResponse.InternetGateways);
                Log.InfoFormat("End reading flowlogs and writing traffic data to redis ({0})", regionEndPoint.SystemName);
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("Error reading flowlogs and writing traffic data to redis ({0}): {1}", regionEndPoint.SystemName, ex.Message);
            }
        }

        public static void WriteTopology(string accountNumber, RegionEndpoint regionEndPoint, IDatabase db)
        {            
            Log.InfoFormat("Start writing data to redis ({0})", regionEndPoint.SystemName);
            
            IAmazonEC2 ec2 = new Amazon.EC2.AmazonEC2Client(regionEndPoint);
            try
            {
                ec2.DescribeVpcs();
            }
            catch (Exception ex)
            {                
                Log.InfoFormat("Unable to read Vpcs: {0}", ex.Message);
                return;
            }

            var currentDateTime = DateTime.UtcNow;
            //var dataKey = Common.GetDataKey(currentDateTime, accountNumber, regionEndPoint.SystemName);            
            //db.SetAdd("TS", dataKey);            
            //db.StringSet(string.Format("LATESTTS-{0}-{1}", accountNumber, regionEndPoint.SystemName), dataKey);


            //WriteVpcs(ec2, dataKey, db);
            //WriteVpcPeeringConnections(ec2, dataKey, db);
            //WriteVpcEndPoints(ec2, dataKey, db);
            //var subnetResponse = WriteSubnets(ec2, dataKey, db);
            //WriteRouteTables(ec2, dataKey, db);
            //var igResponse = WriteInternetGateways(ec2, dataKey, db);
            //var vgResponse = WriteVpnGateways(ec2, dataKey, db);
            //WriteVpnConnections(ec2, dataKey, db);
            //WriteEnis(ec2, dataKey, db);
            //WriteEbs(ec2, dataKey, db);
            //WriteSnapshots(accountNumber, ec2, dataKey, db);
            //WriteRds(regionEndPoint, dataKey, db);
            //WriteContainers(regionEndPoint, dataKey, db);
            //WriteInstances(ec2, dataKey, db);
            //WriteAsgs(regionEndPoint, dataKey, db);
            //WriteElbs(regionEndPoint, dataKey, db);
            //WriteSecurityGroups(ec2, dataKey, db);

            TopologyWriter.WriteVpcs(ec2, currentDateTime, accountNumber, regionEndPoint.SystemName);
            TopologyWriter.WriteVpcPeeringConnections(ec2, currentDateTime, accountNumber, regionEndPoint.SystemName);
            TopologyWriter.WriteVpcEndPoints(ec2, currentDateTime, accountNumber, regionEndPoint.SystemName);
            TopologyWriter.WriteSubnets(ec2, currentDateTime, accountNumber, regionEndPoint.SystemName);
            TopologyWriter.WriteSecurityGroups(ec2, currentDateTime, accountNumber, regionEndPoint.SystemName);
            TopologyWriter.WriteInstances(ec2, currentDateTime, accountNumber, regionEndPoint.SystemName);
            TopologyWriter.WriteRouteTables(ec2, currentDateTime, accountNumber, regionEndPoint.SystemName);
            TopologyWriter.WriteInternetGateways(ec2, currentDateTime, accountNumber, regionEndPoint.SystemName);
            TopologyWriter.WriteVpnGateways(ec2, currentDateTime, accountNumber, regionEndPoint.SystemName);
            TopologyWriter.WriteVpnConnections(ec2, currentDateTime, accountNumber, regionEndPoint.SystemName);
            TopologyWriter.WriteEnis(ec2, currentDateTime, accountNumber, regionEndPoint.SystemName);
            TopologyWriter.WriteEbs(ec2, currentDateTime, accountNumber, regionEndPoint.SystemName);
            TopologyWriter.WriteSnapshots(ec2, currentDateTime, accountNumber, regionEndPoint.SystemName);
            TopologyWriter.WriteTags(ec2, currentDateTime, accountNumber, regionEndPoint.SystemName);
            TopologyWriter.WriteRds(currentDateTime, accountNumber, regionEndPoint);
            TopologyWriter.WriteContainers(currentDateTime, accountNumber, regionEndPoint);            
            TopologyWriter.WriteAsgs(currentDateTime, accountNumber, regionEndPoint);
            TopologyWriter.WriteElbs(currentDateTime, accountNumber, regionEndPoint);            

            Log.InfoFormat("End writing data to redis ({0})", regionEndPoint.SystemName);
        }        
    }
}