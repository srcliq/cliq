using Amazon;
using Amazon.AutoScaling;
using Amazon.AutoScaling.Model;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.ECS;
using Amazon.ECS.Model;
using Amazon.ElasticLoadBalancing;
using Amazon.RDS;
using ConnectionSettings = Amazon.ElasticLoadBalancing.Model.ConnectionSettings;
using DescribeInstancesRequest = Amazon.EC2.Model.DescribeInstancesRequest;
using DescribeInstancesResponse = Amazon.EC2.Model.DescribeInstancesResponse;
using Filter = Amazon.EC2.Model.Filter;
using Tag = Amazon.EC2.Model.Tag;
using Vpc = Amazon.EC2.Model.Vpc;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace TopologyReader.Helpers
{
    public static class TopologyWriter
    {
        internal static readonly ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        internal static void WriteSecurityGroups(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
        {
            var sgResponse = ec2.DescribeSecurityGroups();
            foreach (var sg in sgResponse.SecurityGroups)
            {
                var sgJson = JsonConvert.SerializeObject(sg);
                Common.UpdateTopology(captureTime, accountId, region, "sg", sg.GroupId, sgJson, "UPDATE");
            }
        }

        internal static void WriteElbs(DateTime captureTime, string accountId, RegionEndpoint regionEndPoint)
        {
            var elbc = new AmazonElasticLoadBalancingClient(regionEndPoint);
            var elbResponse = elbc.DescribeLoadBalancers();
            var db = RedisManager.GetRedisDatabase();
            foreach (var elb in elbResponse.LoadBalancerDescriptions)
            {
                //to do: may not be required.
                //RedisManager.AddSetWithExpiry(string.Format("{0}-lbs", dataKey), string.Format("lbg-{0}", elb.LoadBalancerName), db);
                
                var elbJson = JsonConvert.SerializeObject(elb);
                //RedisManager.AddWithExpiry(string.Format("{0}-lb-{1}", dataKey, elb.LoadBalancerName), elbJson, db);
                var newDataKey = Common.GetDataKey(captureTime, accountId, regionEndPoint.SystemName);
                var entityKey = string.Format("{0}-{1}-{2}", newDataKey, "lb", elb.LoadBalancerName);
                Common.UpdateTopology(captureTime, accountId, regionEndPoint.SystemName, "lb", elb.LoadBalancerName, elbJson, "UPDATE");
                //code to add elb name to the instance details
                var latestDataKey = Common.GetDataKey("latest", accountId, regionEndPoint.SystemName);
                var latestEntitySetKey = string.Format("{0}-{1}set", latestDataKey, "ins");
                foreach (var instance in elb.Instances)
                {
                    //load instance data from redis
                    var latestInstanceKey = RedisManager.GetSetMember(latestEntitySetKey, instance.InstanceId, db);
                    var instanceJson = db.StringGet(latestInstanceKey.ToString());
                    if (instanceJson.HasValue)
                    {
                        var dataInstance = JsonConvert.DeserializeObject<Data.Instance>(instanceJson);
                        //add elb name to instance
                        dataInstance.ElbKeyName = entityKey;
                        //add the instance data with asg name back to redis
                        instanceJson = JsonConvert.SerializeObject(dataInstance);
                        RedisManager.AddWithExpiry(latestInstanceKey, instanceJson, db);
                    }
                }
            }
        }

        internal static void WriteAsgs(DateTime captureTime, string accountId, RegionEndpoint regionEndPoint)
        {
            var asc = new AmazonAutoScalingClient(regionEndPoint);
            DescribeAutoScalingGroupsResponse asgResponse = asc.DescribeAutoScalingGroups();
            DescribeAutoScalingInstancesResponse asgInstanceResponse = asc.DescribeAutoScalingInstances();
            var db = RedisManager.GetRedisDatabase();
            foreach (var asGroup in asgResponse.AutoScalingGroups)
            {
                //to do: may not be required.
                //RedisManager.AddSetWithExpiry(string.Format("{0}-asgs", dataKey), string.Format("asg-{0}", asGroup.AutoScalingGroupName), db);
                var asgJson = JsonConvert.SerializeObject(asGroup);
                //RedisManager.AddWithExpiry(string.Format("{0}-asg-{1}", dataKey, asGroup.AutoScalingGroupName), asgJson, db);
                var newDataKey = Common.GetDataKey(captureTime, accountId, regionEndPoint.SystemName);
                var entityKey = string.Format("{0}-{1}-{2}", newDataKey, "asg", asGroup.AutoScalingGroupName);
                Common.UpdateTopology(captureTime, accountId, regionEndPoint.SystemName, "asg", asGroup.AutoScalingGroupName, asgJson, "UPDATE");
                //code to add asg name to the instance details
                var latestDataKey = Common.GetDataKey("latest", accountId, regionEndPoint.SystemName);
                var latestEntitySetKey = string.Format("{0}-{1}set", latestDataKey, "ins");
                foreach (var instance in asGroup.Instances)
                {
                    //load instance data from redis
                    var latestInstanceKey = RedisManager.GetSetMember(latestEntitySetKey, instance.InstanceId, db);
                    var instanceJson = db.StringGet(latestInstanceKey.ToString());
                    if (instanceJson.HasValue)
                    {
                        var dataInstance = JsonConvert.DeserializeObject<Data.Instance>(instanceJson);
                        //add elb name to instance
                        dataInstance.AsgKeyName = entityKey;
                        //add the instance data with asg name back to redis
                        instanceJson = JsonConvert.SerializeObject(dataInstance);
                        RedisManager.AddWithExpiry(latestInstanceKey, instanceJson, db);
                    }
                }
            }
        }
        
        internal static void WriteInstances(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
        {
            DescribeInstancesResponse instanceResponse = ec2.DescribeInstances();
            Common.GetDataKey(captureTime, accountId, region);
            RedisManager.GetRedisDatabase();
            var newDataKey = Common.GetDataKey(captureTime, accountId, region);
            foreach (var reservation in instanceResponse.Reservations)
            {
                foreach (var rInstance in reservation.Instances)
                {
                    var instance = AutoMapper.Mapper.Map<Data.Instance>(rInstance);
                    instance.Size = new Random().Next(1, 32);
                    string instanceJson = JsonConvert.SerializeObject(instance);
                    //RedisManager.AddWithExpiry(string.Format("{0}-ins-{1}", dataKey, instance.InstanceId), instanceJson, db);
                    Common.UpdateTopology(captureTime, accountId, region, "ins", instance.InstanceId, instanceJson, "UPDATE");
                    var entityKey = string.Format("{0}-{1}-{2}", newDataKey, "ins", instance.InstanceId);
                    Common.UpdateTopologySet(captureTime, accountId, region, "vpcinstances", instance.VpcId, entityKey, "UPDATE");
                    Common.UpdateTopologySet(captureTime, accountId, region, "subnetinstances", instance.SubnetId, entityKey, "UPDATE");
                    //RedisManager.AddSetWithExpiry(string.Format("{0}-vpcinstances-{1}", dataKey, instance.VpcId), string.Format("{0}-ins-{1}", dataKey, instance.InstanceId), db);
                    //RedisManager.AddSetWithExpiry(string.Format("{0}-subnetinstances-{1}", dataKey, instance.SubnetId), string.Format("{0}-ins-{1}", dataKey, instance.InstanceId), db);
                }
            }
        }

        internal static void WriteContainers(DateTime captureTime, string accountId, RegionEndpoint regionEndPoint)
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
                        Common.UpdateTopology(captureTime, accountId, regionEndPoint.SystemName, "ecs", ecs.Ec2InstanceId, ecsJson, "UPDATE");
                    }
                }
            }
            catch (Exception ex)
            {
                //Log.Error("Exception occured while reading containers", ex);
                Log.InfoFormat("Error reading containers: {0}", ex.Message);
            }
        }

        internal static void WriteRds(DateTime captureTime, string accountId, RegionEndpoint regionEndPoint)
        {
            var rdsClient = new AmazonRDSClient(regionEndPoint);
            var rdsInstanceResponse = rdsClient.DescribeDBInstances();
            foreach (var dbInstance in rdsInstanceResponse.DBInstances)
            {
                string dbJson = JsonConvert.SerializeObject(dbInstance);                
                Common.UpdateTopology(captureTime, accountId, regionEndPoint.SystemName, "rds", dbInstance.DBInstanceIdentifier, dbJson, "UPDATE");
            }
        }

        internal static void WriteSnapshots(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
        {
            var describeSnapshotRequest = new DescribeSnapshotsRequest();
            var filters = new List<Filter>();
            var filter = new Filter();
            filter.Name = "owner-id";
            filter.Values = new List<string> { accountId };
            filters.Add(filter);
            describeSnapshotRequest.Filters = filters;
            var ssResponse = ec2.DescribeSnapshots(describeSnapshotRequest);
            foreach (var snapshot in ssResponse.Snapshots)
            {
                string snapshotJson = JsonConvert.SerializeObject(snapshot);                
                Common.UpdateTopology(captureTime, accountId, region, "ss", snapshot.SnapshotId, snapshotJson, "UPDATE");
            }
        }

        internal static void WriteEbs(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
        {
            var ebsResponse = ec2.DescribeVolumes();
            foreach (var volume in ebsResponse.Volumes)
            {
                string volumeJson = JsonConvert.SerializeObject(volume);                
                Common.UpdateTopology(captureTime, accountId, region, "ebs", volume.VolumeId, volumeJson, "UPDATE");
            }
        }

        internal static void WriteEnis(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
        {
            var eniResponse = ec2.DescribeNetworkInterfaces();
            foreach (var ni in eniResponse.NetworkInterfaces)
            {
                string niJson = JsonConvert.SerializeObject(ni);
                Common.UpdateTopology(captureTime, accountId, region, "eni", ni.NetworkInterfaceId, niJson, "UPDATE");
                Common.UpdateTopology(captureTime, accountId, region, "eni", string.Format("{0}-{1}", ni.VpcId, ni.PrivateIpAddress), niJson, "UPDATE");                
                if (ni.Association != null && !string.IsNullOrEmpty(ni.Association.PublicIp))
                {
                    Common.UpdateTopology(captureTime, accountId, region, "eni", string.Format("{0}-{1}", ni.VpcId, ni.Association.PublicIp), niJson, "UPDATE");                    
                }
            }
        }

        internal static void WriteVpnConnections(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
        {
            var vcResponse = ec2.DescribeVpnConnections();
            foreach (var vc in vcResponse.VpnConnections)
            {
                string vcJson = JsonConvert.SerializeObject(vc);                
                Common.UpdateTopology(captureTime, accountId, region, "vc", vc.VpnConnectionId, vcJson, "UPDATE");
            }
        }

        internal static DescribeVpnGatewaysResponse WriteVpnGateways(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
        {
            var vgResponse = ec2.DescribeVpnGateways();
            foreach (var vg in vgResponse.VpnGateways)
            {
                string vgJson = JsonConvert.SerializeObject(vg);                
                Common.UpdateTopology(captureTime, accountId, region, "vg", vg.VpnGatewayId, vgJson, "UPDATE");
            }
            return vgResponse;
        }

        internal static DescribeInternetGatewaysResponse WriteInternetGateways(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
        {
            var igResponse = ec2.DescribeInternetGateways();
            foreach (var ig in igResponse.InternetGateways)
            {
                string igJson = JsonConvert.SerializeObject(ig);                
                Common.UpdateTopology(captureTime, accountId, region, "ig", ig.InternetGatewayId, igJson, "UPDATE");
            }
            return igResponse;
        }

        internal static void WriteRouteTables(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
        {
            var rtResponse = ec2.DescribeRouteTables();
            foreach (var rt in rtResponse.RouteTables)
            {
                string rtJson = JsonConvert.SerializeObject(rt);                
                Common.UpdateTopology(captureTime, accountId, region, "rt", rt.RouteTableId, rtJson, "UPDATE");
            }
        }

        internal static DescribeSubnetsResponse WriteSubnets(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
        {
            var subnetResponse = ec2.DescribeSubnets();
            var newDataKey = Common.GetDataKey(captureTime, accountId, region);
            foreach (var subnet in subnetResponse.Subnets)
            {
                var topologySubnet = AutoMapper.Mapper.Map<Data.Subnet>(subnet);
                if (subnet.Tags.Find(t => t.Key == "Name") != null)
                    topologySubnet.Name = subnet.Tags.Find(t => t.Key == "Name").Value;
                else
                    topologySubnet.Name = subnet.SubnetId;
                string subnetJson = JsonConvert.SerializeObject(topologySubnet);
                //to do:
                //RedisManager.AddSetWithExpiry(string.Format("{0}-vpcsubnets-{1}", dataKey, subnet.VpcId), string.Format("{0}-subnet-{1}", dataKey, subnet.SubnetId), db);                
                var entityKey = string.Format("{0}-{1}-{2}", newDataKey, "subnet", subnet.SubnetId);
                Common.UpdateTopology(captureTime, accountId, region, "subnet", subnet.SubnetId, subnetJson, "UPDATE");
                Common.UpdateTopologySet(captureTime, accountId, region, "vpcsubnets", subnet.VpcId, entityKey, "UPDATE");                
            }
            return subnetResponse;
        }

        internal static void WriteVpcEndPoints(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
        {
            var vpcEndPointsRequest = new DescribeVpcEndpointsRequest();
            try
            {
                var vpcEndPointsResponse = ec2.DescribeVpcEndpoints(vpcEndPointsRequest);
                foreach (var vpcEndPoint in vpcEndPointsResponse.VpcEndpoints)
                {
                    string vpcEndPointJson = JsonConvert.SerializeObject(vpcEndPoint);                    
                    Common.UpdateTopology(captureTime, accountId, region, "vpcep", vpcEndPoint.VpcEndpointId, vpcEndPointJson, "UPDATE");
                }
            }
            catch (Exception ex)
            {
                Log.InfoFormat("Error reading vpc endpoints: {0}", ex.Message);
                //Log.Error("Error reading vpc endpoints", ex);
            }
        }

        internal static void WriteVpcPeeringConnections(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
        {
            DescribeVpcPeeringConnectionsResponse vpcPeeringResponses = ec2.DescribeVpcPeeringConnections();
            foreach (var vpcPeer in vpcPeeringResponses.VpcPeeringConnections)
            {
                string vpcPeerJson = JsonConvert.SerializeObject(vpcPeer);                
                Common.UpdateTopology(captureTime, accountId, region, "vpcpc", vpcPeer.VpcPeeringConnectionId, vpcPeerJson, "UPDATE");
            }
        }

        internal static void WriteVpcs(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
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
                Common.UpdateTopology(captureTime, accountId, region, "vpc", vpc.VpcId, vpcJson, "UPDATE");
            }
        }
    }
}
