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
                AWSConfigReader.ProcessConfigMessages();
                return;
                int writeTopology = 0;
                int readFlowLogs = 0;
                int flowLogDurationType = 0;
                int ttl = 5;
                int.TryParse(ConfigurationManager.AppSettings["RedisKeysTTLDays"], out ttl);
                Common.SetRedisTTL(ttl);

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
                IDatabase db = Common.GetRedisDatabase();
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

            var dataKey = Common.GetDataKey(accountNumber, regionEndPoint);            
            db.SetAdd("TS", dataKey);            
            db.StringSet(string.Format("LATESTTS-{0}-{1}", accountNumber, regionEndPoint.SystemName), dataKey);

            WriteVpcs(ec2, dataKey, db);
            WriteVpcPeeringConnections(ec2, dataKey, db);
            WriteVpcEndPoints(ec2, dataKey, db);
            var subnetResponse = WriteSubnets(ec2, dataKey, db);
            WriteRouteTables(ec2, dataKey, db);
            var igResponse = WriteInternetGateways(ec2, dataKey, db);
            var vgResponse = WriteVpnGateways(ec2, dataKey, db);
            WriteVpnConnections(ec2, dataKey, db);
            WriteEnis(ec2, dataKey, db);
            WriteEbs(ec2, dataKey, db);
            WriteSnapshots(accountNumber, ec2, dataKey, db);
            WriteRds(regionEndPoint, dataKey, db);
            WriteContainers(regionEndPoint, dataKey, db);
            WriteInstances(ec2, dataKey, db);
            WriteAsgs(regionEndPoint, dataKey, db);
            WriteElbs(regionEndPoint, dataKey, db);
            WriteSecurityGroups(ec2, dataKey, db);
                        
            Log.InfoFormat("End writing data to redis ({0})", regionEndPoint.SystemName);
        }

        

        private static void WriteSecurityGroups(IAmazonEC2 ec2, string dataKey, IDatabase db)
        {
            var sgResponse = ec2.DescribeSecurityGroups();
            foreach (var sg in sgResponse.SecurityGroups)
            {
                var sgJson = JsonConvert.SerializeObject(sg);                
                Common.AddSetToRedisWithExpiry(string.Format("{0}-sgs", dataKey), string.Format("sg-{0}", sg.GroupId), db);
                Common.AddToRedisWithExpiry(string.Format("{0}-sg-{1}", dataKey, sg.GroupName), sgJson, db);
            }
        }

        private static void WriteElbs(RegionEndpoint regionEndPoint, string dataKey, IDatabase db)
        {
            var elbc = new AmazonElasticLoadBalancingClient(regionEndPoint);
            var elbResponse = elbc.DescribeLoadBalancers();
            foreach (var elb in elbResponse.LoadBalancerDescriptions)
            {                
                Common.AddSetToRedisWithExpiry(string.Format("{0}-lbs", dataKey), string.Format("lbg-{0}", elb.LoadBalancerName), db);
                var elbJson = JsonConvert.SerializeObject(elb);
                Common.AddToRedisWithExpiry(string.Format("{0}-lb-{1}", dataKey, elb.LoadBalancerName), elbJson, db);
                //code to add elb name to the instance details
                foreach (var instance in elb.Instances)
                {
                    //load instance data from redis
                    var instanceJson = db.StringGet(string.Format("{0}-ins-{1}", dataKey, instance.InstanceId));
                    if (instanceJson.HasValue)
                    {
                        var dataInstance = JsonConvert.DeserializeObject<Data.Instance>(instanceJson);
                        //add elb name to instance
                        dataInstance.ElbKeyName = string.Format("{0}-lb-{1}", dataKey, elb.LoadBalancerName);
                        //add the instance data with asg name back to redis
                        instanceJson = JsonConvert.SerializeObject(dataInstance);
                        Common.AddToRedisWithExpiry(string.Format("{0}-ins-{1}", dataKey, instance.InstanceId), instanceJson, db);
                    }
                }
            }
        }

        private static void WriteAsgs(RegionEndpoint regionEndPoint, string dataKey, IDatabase db)
        {
            var asc = new AmazonAutoScalingClient(regionEndPoint);
            DescribeAutoScalingGroupsResponse asgResponse = asc.DescribeAutoScalingGroups();
            DescribeAutoScalingInstancesResponse asgInstanceResponse = asc.DescribeAutoScalingInstances();
            foreach (var asGroup in asgResponse.AutoScalingGroups)
            {
                Common.AddSetToRedisWithExpiry(string.Format("{0}-asgs", dataKey), string.Format("asg-{0}", asGroup.AutoScalingGroupName), db);
                var asgJson = JsonConvert.SerializeObject(asGroup);
                Common.AddToRedisWithExpiry(string.Format("{0}-asg-{1}", dataKey, asGroup.AutoScalingGroupName), asgJson, db);
                //code to add asg name to the instance details
                foreach (var instance in asGroup.Instances)
                {
                    //load instance data from redis
                    var instanceJson = db.StringGet(string.Format("{0}-ins-{1}", dataKey, instance.InstanceId));
                    if (instanceJson.HasValue)
                    {
                        var dataInstance = JsonConvert.DeserializeObject<Data.Instance>(instanceJson);
                        //add asg name to instance
                        dataInstance.AsgKeyName = string.Format("{0}-asg-{1}", dataKey, asGroup.AutoScalingGroupName);
                        //add the instance data with asg name back to redis
                        instanceJson = JsonConvert.SerializeObject(dataInstance);
                        Common.AddToRedisWithExpiry(string.Format("{0}-ins-{1}", dataKey, instance.InstanceId), instanceJson, db);
                    }
                }
            }
        }

        private static void WriteInstances(IAmazonEC2 ec2, string dataKey, IDatabase db)
        {
            DescribeInstancesResponse instanceResponse = ec2.DescribeInstances();
            var dKey = string.Empty;
            foreach (var reservation in instanceResponse.Reservations)
            {
                foreach (var rInstance in reservation.Instances)
                {
                    var instance = AutoMapper.Mapper.Map<Data.Instance>(rInstance);
                    instance.Size = new Random().Next(1, 32);
                    string instanceJson = JsonConvert.SerializeObject(instance);
                    Common.AddToRedisWithExpiry(string.Format("{0}-ins-{1}", dataKey, instance.InstanceId), instanceJson, db);
                    Common.AddSetToRedisWithExpiry(string.Format("{0}-vpcinstances-{1}", dataKey, instance.VpcId), string.Format("{0}-ins-{1}", dataKey, instance.InstanceId), db);
                    Common.AddSetToRedisWithExpiry(string.Format("{0}-subnetinstances-{1}", dataKey, instance.SubnetId), string.Format("{0}-ins-{1}", dataKey, instance.InstanceId), db);
                    dKey = string.Format("{0}-{1}", dataKey, instance.SubnetId);
                }
            }
        }

        private static void WriteContainers(RegionEndpoint regionEndPoint, string dataKey, IDatabase db)
        {
            try
            {
                var ecsClient = new AmazonECSClient(regionEndPoint);
                var listClusterResponse = ecsClient.ListClusters(new ListClustersRequest { MaxResults = 100 });
                foreach (var cluster in listClusterResponse.ClusterArns)
                {
                    var ecsResponse = ecsClient.DescribeContainerInstances(new DescribeContainerInstancesRequest { Cluster = cluster });
                    foreach (var ecs in ecsResponse.ContainerInstances)
                    {
                        string ecsJson = JsonConvert.SerializeObject(ecs);
                        Common.AddToRedisWithExpiry(string.Format("{0}-ecs-{1}", dataKey, ecs.Ec2InstanceId), ecsJson, db);
                    }
                }
            }
            catch (Exception ex)
            {
                //Log.Error("Exception occured while reading containers", ex);
                Log.InfoFormat("Error reading containers: {0}", ex.Message);
            }
        }

        private static void WriteRds(RegionEndpoint regionEndPoint, string dataKey, IDatabase db)
        {
            var rdsClient = new AmazonRDSClient(regionEndPoint);
            var rdsInstanceResponse = rdsClient.DescribeDBInstances();
            foreach (var dbInstance in rdsInstanceResponse.DBInstances)
            {
                string dbJson = JsonConvert.SerializeObject(dbInstance);
                Common.AddToRedisWithExpiry(string.Format("{0}-rds-{1}", dataKey, dbInstance.DBInstanceIdentifier), dbJson, db);
            }
        }

        private static void WriteSnapshots(string accountNumber, IAmazonEC2 ec2, string dataKey, IDatabase db)
        {
            var describeSnapshotRequest = new DescribeSnapshotsRequest();
            var filters = new List<Filter>();
            var filter = new Filter();
            filter.Name = "owner-id";
            filter.Values = new List<string> { accountNumber };
            filters.Add(filter);
            describeSnapshotRequest.Filters = filters;
            var ssResponse = ec2.DescribeSnapshots(describeSnapshotRequest);
            foreach (var snapshot in ssResponse.Snapshots)
            {
                string snapshotJson = JsonConvert.SerializeObject(snapshot);
                Common.AddToRedisWithExpiry(string.Format("{0}-ss-{1}", dataKey, snapshot.SnapshotId), snapshotJson, db);
            }
        }

        private static void WriteEbs(IAmazonEC2 ec2, string dataKey, IDatabase db)
        {
            var ebsResponse = ec2.DescribeVolumes();
            foreach (var volume in ebsResponse.Volumes)
            {
                string volumeJson = JsonConvert.SerializeObject(volume);
                Common.AddToRedisWithExpiry(string.Format("{0}-ebs-{1}", dataKey, volume.VolumeId), volumeJson, db);
            }
        }

        private static void WriteEnis(IAmazonEC2 ec2, string dataKey, IDatabase db)
        {
            var eniResponse = ec2.DescribeNetworkInterfaces();
            foreach (var ni in eniResponse.NetworkInterfaces)
            {
                string niJson = JsonConvert.SerializeObject(ni);
                Common.AddToRedisWithExpiry(string.Format("{0}-eni-{1}", dataKey, ni.NetworkInterfaceId), niJson, db);
                Common.AddToRedisWithExpiry(string.Format("{0}-eni-{1}-{2}", dataKey, ni.VpcId, ni.PrivateIpAddress), niJson, db);
                if (ni.Association != null && !string.IsNullOrEmpty(ni.Association.PublicIp))
                {
                    Common.AddToRedisWithExpiry(string.Format("{0}-eni-{1}-{2}", dataKey, ni.VpcId, ni.Association.PublicIp), niJson, db);
                }
            }
        }

        private static void WriteVpnConnections(IAmazonEC2 ec2, string dataKey, IDatabase db)
        {
            var vcResponse = ec2.DescribeVpnConnections();
            foreach (var vc in vcResponse.VpnConnections)
            {
                string vcJson = JsonConvert.SerializeObject(vc);
                Common.AddToRedisWithExpiry(string.Format("{0}-vc-{1}", dataKey, vc.VpnConnectionId), vcJson, db);
            }
        }

        private static DescribeVpnGatewaysResponse WriteVpnGateways(IAmazonEC2 ec2, string dataKey, IDatabase db)
        {
            var vgResponse = ec2.DescribeVpnGateways();
            foreach (var vg in vgResponse.VpnGateways)
            {
                string vgJson = JsonConvert.SerializeObject(vg);
                Common.AddToRedisWithExpiry(string.Format("{0}-vg-{1}", dataKey, vg.VpnGatewayId), vgJson, db);
            }
            return vgResponse;
        }

        private static DescribeInternetGatewaysResponse WriteInternetGateways(IAmazonEC2 ec2, string dataKey, IDatabase db)
        {
            var igResponse = ec2.DescribeInternetGateways();
            foreach (var ig in igResponse.InternetGateways)
            {
                string igJson = JsonConvert.SerializeObject(ig);
                Common.AddToRedisWithExpiry(string.Format("{0}-ig-{1}", dataKey, ig.InternetGatewayId), igJson, db);
            }
            return igResponse;
        }

        private static void WriteRouteTables(IAmazonEC2 ec2, string dataKey, IDatabase db)
        {
            var rtResponse = ec2.DescribeRouteTables();
            foreach (var rt in rtResponse.RouteTables)
            {
                string rtJson = JsonConvert.SerializeObject(rt);
                Common.AddToRedisWithExpiry(string.Format("{0}-rt-{1}", dataKey, rt.RouteTableId), rtJson, db);
            }
        }

        private static DescribeSubnetsResponse WriteSubnets(IAmazonEC2 ec2, string dataKey, IDatabase db)
        {
            var subnetResponse = ec2.DescribeSubnets();
            foreach (var subnet in subnetResponse.Subnets)
            {
                var topologySubnet = AutoMapper.Mapper.Map<Data.Subnet>(subnet);
                if (subnet.Tags.Find(t => t.Key == "Name") != null)
                    topologySubnet.Name = subnet.Tags.Find(t => t.Key == "Name").Value;
                else
                    topologySubnet.Name = subnet.SubnetId;
                string subnetJson = JsonConvert.SerializeObject(topologySubnet);
                Common.AddToRedisWithExpiry(string.Format("{0}-subnet-{1}", dataKey, subnet.SubnetId), subnetJson, db);
                Common.AddSetToRedisWithExpiry(string.Format("{0}-vpcsubnets-{1}", dataKey, subnet.VpcId), string.Format("{0}-subnet-{1}", dataKey, subnet.SubnetId), db);
            }
            return subnetResponse;
        }

        private static void WriteVpcEndPoints(IAmazonEC2 ec2, string dataKey, IDatabase db)
        {
            var vpcEndPointsRequest = new DescribeVpcEndpointsRequest();
            try
            {
                var vpcEndPointsResponse = ec2.DescribeVpcEndpoints(vpcEndPointsRequest);
                foreach (var vpcEndPoint in vpcEndPointsResponse.VpcEndpoints)
                {
                    string vpcEndPointJson = JsonConvert.SerializeObject(vpcEndPoint);
                    Common.AddToRedisWithExpiry(string.Format("{0}-vpcep-{1}", dataKey, vpcEndPoint.VpcEndpointId), vpcEndPointJson, db);
                }
            }
            catch (Exception ex)
            {
                Log.InfoFormat("Error reading vpc endpoints: {0}", ex.Message);
                //Log.Error("Error reading vpc endpoints", ex);
            }
        }

        private static void WriteVpcPeeringConnections(IAmazonEC2 ec2, string dataKey, IDatabase db)
        {
            DescribeVpcPeeringConnectionsResponse vpcPeeringResponses = ec2.DescribeVpcPeeringConnections();
            foreach (var vpcPeer in vpcPeeringResponses.VpcPeeringConnections)
            {
                string vpcPeerJson = JsonConvert.SerializeObject(vpcPeer);
                Common.AddToRedisWithExpiry(string.Format("{0}-vpcpc-{1}", dataKey, vpcPeer.VpcPeeringConnectionId), vpcPeerJson, db);
            }
        }

        private static void WriteVpcs(IAmazonEC2 ec2, string dataKey, IDatabase db)
        {
            DescribeVpcsResponse vpcResponse = ec2.DescribeVpcs();
            foreach (Vpc vpc in vpcResponse.Vpcs)
            {
                var topologyVPC = new Data.Vpc()
                {
                    Id = vpc.VpcId
                };
                var nameTag = vpc.Tags.Find(t => t.Key == "Name");
                if (nameTag != null)
                {
                    topologyVPC.Name = nameTag.Value;
                }
                else
                {
                    topologyVPC.Name = vpc.VpcId;
                }
                topologyVPC.CidrBlock = vpc.CidrBlock;
                string vpcJson = JsonConvert.SerializeObject(topologyVPC);
                //var vpcJson = JsonConvert.SerializeObject(vpc);
                Common.AddToRedisWithExpiry(string.Format("{0}-vpc-{1}", dataKey, vpc.VpcId), vpcJson, db);
            }
        }
    }
}