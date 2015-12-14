﻿using Amazon.EC2;
using Amazon.EC2.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopologyReader.Helpers
{
    public static class TopologyWriter
    {
        internal static void WriteSecurityGroups(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
        {
            var sgResponse = ec2.DescribeSecurityGroups();
            foreach (var sg in sgResponse.SecurityGroups)
            {
                var sgJson = JsonConvert.SerializeObject(sg);
                Common.UpdateTopology(captureTime, accountId, region, "sg", sg.GroupId, sgJson, "UPDATE");
            }
        }

        private static void WriteElbs(RegionEndpoint regionEndPoint, string dataKey, IDatabase db)
        {
            var elbc = new AmazonElasticLoadBalancingClient(regionEndPoint);
            var elbResponse = elbc.DescribeLoadBalancers();
            foreach (var elb in elbResponse.LoadBalancerDescriptions)
            {
                RedisManager.AddSetWithExpiry(string.Format("{0}-lbs", dataKey), string.Format("lbg-{0}", elb.LoadBalancerName), db);
                var elbJson = JsonConvert.SerializeObject(elb);
                RedisManager.AddWithExpiry(string.Format("{0}-lb-{1}", dataKey, elb.LoadBalancerName), elbJson, db);
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
                        RedisManager.AddWithExpiry(string.Format("{0}-ins-{1}", dataKey, instance.InstanceId), instanceJson, db);
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
                RedisManager.AddSetWithExpiry(string.Format("{0}-asgs", dataKey), string.Format("asg-{0}", asGroup.AutoScalingGroupName), db);
                var asgJson = JsonConvert.SerializeObject(asGroup);
                RedisManager.AddWithExpiry(string.Format("{0}-asg-{1}", dataKey, asGroup.AutoScalingGroupName), asgJson, db);
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
                        RedisManager.AddWithExpiry(string.Format("{0}-ins-{1}", dataKey, instance.InstanceId), instanceJson, db);
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
                    RedisManager.AddWithExpiry(string.Format("{0}-ins-{1}", dataKey, instance.InstanceId), instanceJson, db);
                    RedisManager.AddSetWithExpiry(string.Format("{0}-vpcinstances-{1}", dataKey, instance.VpcId), string.Format("{0}-ins-{1}", dataKey, instance.InstanceId), db);
                    RedisManager.AddSetWithExpiry(string.Format("{0}-subnetinstances-{1}", dataKey, instance.SubnetId), string.Format("{0}-ins-{1}", dataKey, instance.InstanceId), db);
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
                        RedisManager.AddWithExpiry(string.Format("{0}-ecs-{1}", dataKey, ecs.Ec2InstanceId), ecsJson, db);
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
                RedisManager.AddWithExpiry(string.Format("{0}-rds-{1}", dataKey, dbInstance.DBInstanceIdentifier), dbJson, db);
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
                RedisManager.AddWithExpiry(string.Format("{0}-ss-{1}", dataKey, snapshot.SnapshotId), snapshotJson, db);
            }
        }

        private static void WriteEbs(IAmazonEC2 ec2, string dataKey, IDatabase db)
        {
            var ebsResponse = ec2.DescribeVolumes();
            foreach (var volume in ebsResponse.Volumes)
            {
                string volumeJson = JsonConvert.SerializeObject(volume);
                RedisManager.AddWithExpiry(string.Format("{0}-ebs-{1}", dataKey, volume.VolumeId), volumeJson, db);
            }
        }

        private static void WriteEnis(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
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

        private static void WriteVpnConnections(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
        {
            var vcResponse = ec2.DescribeVpnConnections();
            foreach (var vc in vcResponse.VpnConnections)
            {
                string vcJson = JsonConvert.SerializeObject(vc);                
                Common.UpdateTopology(captureTime, accountId, region, "vc", vc.VpnConnectionId, vcJson, "UPDATE");
            }
        }

        private static DescribeVpnGatewaysResponse WriteVpnGateways(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
        {
            var vgResponse = ec2.DescribeVpnGateways();
            foreach (var vg in vgResponse.VpnGateways)
            {
                string vgJson = JsonConvert.SerializeObject(vg);                
                Common.UpdateTopology(captureTime, accountId, region, "vg", vg.VpnGatewayId, vgJson, "UPDATE");
            }
            return vgResponse;
        }

        private static DescribeInternetGatewaysResponse WriteInternetGateways(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
        {
            var igResponse = ec2.DescribeInternetGateways();
            foreach (var ig in igResponse.InternetGateways)
            {
                string igJson = JsonConvert.SerializeObject(ig);                
                Common.UpdateTopology(captureTime, accountId, region, "ig", ig.InternetGatewayId, igJson, "UPDATE");
            }
            return igResponse;
        }

        private static void WriteRouteTables(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
        {
            var rtResponse = ec2.DescribeRouteTables();
            foreach (var rt in rtResponse.RouteTables)
            {
                string rtJson = JsonConvert.SerializeObject(rt);                
                Common.UpdateTopology(captureTime, accountId, region, "rt", rt.RouteTableId, rtJson, "UPDATE");
            }
        }

        private static DescribeSubnetsResponse WriteSubnets(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
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
                //to do:
                //RedisManager.AddSetWithExpiry(string.Format("{0}-vpcsubnets-{1}", dataKey, subnet.VpcId), string.Format("{0}-subnet-{1}", dataKey, subnet.SubnetId), db);
                Common.UpdateTopology(captureTime, accountId, region, "subnet", subnet.SubnetId, subnetJson, "UPDATE");
            }
            return subnetResponse;
        }

        private static void WriteVpcEndPoints(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
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

        private static void WriteVpcPeeringConnections(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
        {
            DescribeVpcPeeringConnectionsResponse vpcPeeringResponses = ec2.DescribeVpcPeeringConnections();
            foreach (var vpcPeer in vpcPeeringResponses.VpcPeeringConnections)
            {
                string vpcPeerJson = JsonConvert.SerializeObject(vpcPeer);                
                Common.UpdateTopology(captureTime, accountId, region, "vpcpc", vpcPeer.VpcPeeringConnectionId, vpcPeerJson, "UPDATE");
            }
        }

        private static void WriteVpcs(IAmazonEC2 ec2, DateTime captureTime, string accountId, string region)
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
