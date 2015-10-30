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
using DescribeVolumesResponse = Amazon.EC2.Model.DescribeVolumesResponse;
using Filter = Amazon.EC2.Model.Filter;
using Tag = Amazon.EC2.Model.Tag;
using Vpc = Amazon.EC2.Model.Vpc;
using Amazon.RDS;
using System.Text.RegularExpressions;
using Amazon.ECS;
using Amazon.ECS.Model;
using Amazon.ElasticMapReduce;

namespace TopologyReader
{
    internal class Reader
    {
        public static void Main(string[] args)
        {
            //ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("54.173.136.7");
            //IDatabase db = redis.GetDatabase();
            //string value = db.StringGet("Test");
            //Console.Write(GetServiceOutput());
            //GetTopology3();
            AutoMapper.Mapper.CreateMap<Amazon.EC2.Model.Subnet, TopologyReader.Data.Subnet>();
            AutoMapper.Mapper.CreateMap<Amazon.EC2.Model.Instance, TopologyReader.Data.Instance>();
            //GetAccountNumber();
                        
            var accountNumber = GetAccountNumber();
            foreach(var endPoint in RegionEndpoint.EnumerableAllRegions){
                //WriteTopology(accountNumber, RegionEndpoint.USGovCloudWest1);
                WriteTopology(accountNumber, endPoint); 
            }
             
            //GetSubnetFlow();
            //Console.WriteLine("Press any key to continue...");
            //Console.Read();
        }

        public static void GetSubnetFlow()
        {
            var dataKey = DateTime.Now.ToString("MMddyyyHHmmss");
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("172.21.16.155");
            IDatabase db = redis.GetDatabase();
            var ec2 = new Amazon.EC2.AmazonEC2Client();
            DescribeInstancesResponse instanceResponse = ec2.DescribeInstances();
            var subnetIpSet = new Dictionary<string, string>();
            var dKey = string.Empty;
            foreach (var reservation in instanceResponse.Reservations)
            {
                foreach (var rInstance in reservation.Instances)
                {
                    var instance = AutoMapper.Mapper.Map<Data.Instance>(rInstance);
                    instance.Size = new Random().Next(1, 32);
                    string instanceJson = JsonConvert.SerializeObject(instance);
                    //db.StringSet(string.Format("{0}-ins-{1}", dataKey, instance.InstanceId), instanceJson);
                    //db.SetAdd(string.Format("{0}-vpcinstances-{1}", dataKey, instance.VpcId), string.Format("{0}-ins-{1}", dataKey, instance.InstanceId));
                    //db.SetAdd(string.Format("{0}-subnetinstances-{1}", dataKey, instance.SubnetId), string.Format("{0}-ins-{1}", dataKey, instance.InstanceId));
                    dKey = string.Format("{0}-{1}", dataKey, instance.SubnetId);
                    if (!subnetIpSet.ContainsKey(dKey))
                    {
                        subnetIpSet.Add(dKey, instance.PrivateIpAddress);
                    }
                    else
                    {
                        subnetIpSet[dKey] += ("," + instance.PrivateIpAddress);
                    }
                }
            }
            //FlowlogManager.ReadES(subnetIpSet, db, dataKey);
        }

        public static string GetAccountNumber()
        {
            var iamClient = new AmazonIdentityManagementServiceClient();
            var x = iamClient.GetUser();
            Regex regex = new Regex(@"\d+");
            Match match = regex.Match(x.User.Arn);
            if (match.Success)
            {
                return match.Value;
            }
            return string.Empty; //"arn:aws:iam::990008671661:user/snigdha"
        }

        public static void WriteTopology(string accountNumber, RegionEndpoint regionEndPoint)
        {
            Console.WriteLine("Start writing data to redis ({0})", regionEndPoint.SystemName);
            
            if (string.IsNullOrEmpty(accountNumber))
            {
                Console.WriteLine("Unable to read the account number");
            }
            var dateString = DateTime.Now.ToString("MMddyyyHHmmss");
            var dataKey = string.Format("{0}-{1}-{2}", dateString, accountNumber, regionEndPoint.SystemName);
            //ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("52.1.198.130");
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(ConfigurationManager.AppSettings["RedisEndPoint"]);
            IDatabase db = redis.GetDatabase();

            //IAmazonEC2 ec2x = new Amazon.EC2.AmazonEC2Client(RegionEndpoint.USWest2);
            //var subnetResponsex = ec2x.DescribeSubnets();
            //var vgResponsex = ec2x.DescribeVpnGateways();
            //var igResponsex = ec2x.DescribeInternetGateways();
            //FlowLogManager.ReadES(db, dataKey, subnetResponsex.Subnets, vgResponsex.VpnGateways, igResponsex.InternetGateways);
            //return;

            db.SetAdd("TS", dataKey);

            IAmazonEC2 ec2 = new Amazon.EC2.AmazonEC2Client(regionEndPoint);
            try
            {
                ec2.DescribeVpcs();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }
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
                db.StringSet(string.Format("{0}-vpc-{1}", dataKey, vpc.VpcId), vpcJson);
            }

            DescribeVpcPeeringConnectionsResponse vpcPeeringResponses = ec2.DescribeVpcPeeringConnections();
            foreach(var vpcPeer in vpcPeeringResponses.VpcPeeringConnections)
            {
                string vpcPeerJson = JsonConvert.SerializeObject(vpcPeer);
                db.StringSet(string.Format("{0}-vpcpc-{1}", dataKey, vpcPeer.VpcPeeringConnectionId), vpcPeerJson);
            }

            var vpcEndPointsRequest = new DescribeVpcEndpointsRequest();
            try
            {
                var vpcEndPointsResponse = ec2.DescribeVpcEndpoints(vpcEndPointsRequest);
                foreach (var vpcEndPoint in vpcEndPointsResponse.VpcEndpoints)
                {
                    string vpcEndPointJson = JsonConvert.SerializeObject(vpcEndPoint);
                    db.StringSet(string.Format("{0}-vpcep-{1}", dataKey, vpcEndPoint.VpcEndpointId), vpcEndPointJson);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }                        

            var subnetResponse = ec2.DescribeSubnets();
            foreach (var subnet in subnetResponse.Subnets)
            {
                var topologySubnet = AutoMapper.Mapper.Map<Data.Subnet>(subnet);
                if (subnet.Tags.Find(t => t.Key == "Name") != null)
                    topologySubnet.Name = subnet.Tags.Find(t => t.Key == "Name").Value;
                else
                    topologySubnet.Name = subnet.SubnetId;
                string subnetJson = JsonConvert.SerializeObject(topologySubnet);
                db.StringSet(string.Format("{0}-subnet-{1}", dataKey, subnet.SubnetId), subnetJson);
                db.SetAdd(string.Format("{0}-vpcsubnets-{1}", dataKey, subnet.VpcId), string.Format("{0}-subnet-{1}", dataKey, subnet.SubnetId));
            }

            var rtResponse = ec2.DescribeRouteTables();
            foreach (var rt in rtResponse.RouteTables)
            {
                string rtJson = JsonConvert.SerializeObject(rt);
                db.StringSet(string.Format("{0}-rt-{1}", dataKey, rt.RouteTableId), rtJson);
            }

            var igResponse = ec2.DescribeInternetGateways();
            foreach (var ig in igResponse.InternetGateways)
            {
                string igJson = JsonConvert.SerializeObject(ig);
                db.StringSet(string.Format("{0}-ig-{1}", dataKey, ig.InternetGatewayId), igJson);
            }

            var vgResponse = ec2.DescribeVpnGateways();
            foreach (var vg in vgResponse.VpnGateways)
            {
                string vgJson = JsonConvert.SerializeObject(vg);
                db.StringSet(string.Format("{0}-vg-{1}", dataKey, vg.VpnGatewayId), vgJson);
            }

            var eniResponse = ec2.DescribeNetworkInterfaces();
            foreach (var ni in eniResponse.NetworkInterfaces)
            {
                string niJson = JsonConvert.SerializeObject(ni);
                db.StringSet(string.Format("{0}-eni-{1}", dataKey, ni.NetworkInterfaceId), niJson);
                db.StringSet(string.Format("{0}-eni-{1}-{2}", dataKey, ni.VpcId, ni.PrivateIpAddress), niJson);
                if (ni.Association != null && !string.IsNullOrEmpty(ni.Association.PublicIp))
                {
                    db.StringSet(string.Format("{0}-eni-{1}-{2}", dataKey, ni.VpcId, ni.Association.PublicIp), niJson);
                }
            }

            var ebsResponse = ec2.DescribeVolumes();
            foreach (var volume in ebsResponse.Volumes)
            {
                string volumeJson = JsonConvert.SerializeObject(volume);
                db.StringSet(string.Format("{0}-ebs-{1}", dataKey, volume.VolumeId), volumeJson);                
            }

            var describeSnapshotRequest = new DescribeSnapshotsRequest();
            var filters = new List<Filter>();
            var filter = new Filter();
            filter.Name = "owner-id";
            filter.Values = new List<string>{ accountNumber };
            filters.Add(filter);
            describeSnapshotRequest.Filters = filters;
            var ssResponse = ec2.DescribeSnapshots(describeSnapshotRequest);
            foreach (var snapshot in ssResponse.Snapshots)
            {
                string snapshotJson = JsonConvert.SerializeObject(snapshot);
                db.StringSet(string.Format("{0}-ss-{1}", dataKey, snapshot.SnapshotId), snapshotJson);
            }

            var rdsClient = new AmazonRDSClient(regionEndPoint);
            var rdsInstanceResponse = rdsClient.DescribeDBInstances();
            foreach (var dbInstance in rdsInstanceResponse.DBInstances)
            {
                string dbJson = JsonConvert.SerializeObject(dbInstance);
                db.StringSet(string.Format("{0}-rds-{1}", dataKey, dbInstance.DBInstanceIdentifier), dbJson);
            }

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
                        db.StringSet(string.Format("{0}-ecs-{1}", dataKey, ecs.Ec2InstanceId), ecsJson);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occured while reading containers.");
                Console.WriteLine(ex.Message);
            }
            
            //var emrClient = new AmazonElasticMapReduceClient(regionEndPoint);
            //var emrResponse = emrClient.DescribeCluster();            

            DescribeInstancesResponse instanceResponse = ec2.DescribeInstances();
            var subnetIpSet = new Dictionary<string, string>();
            var dKey = string.Empty;
            foreach (var reservation in instanceResponse.Reservations)
            {
                foreach (var rInstance in reservation.Instances)
                {
                    var instance = AutoMapper.Mapper.Map<Data.Instance>(rInstance);
                    instance.Size = new Random().Next(1, 32);                    
                    string instanceJson = JsonConvert.SerializeObject(instance);
                    db.StringSet(string.Format("{0}-ins-{1}", dataKey, instance.InstanceId), instanceJson);
                    db.SetAdd(string.Format("{0}-vpcinstances-{1}", dataKey, instance.VpcId), string.Format("{0}-ins-{1}", dataKey, instance.InstanceId));
                    db.SetAdd(string.Format("{0}-subnetinstances-{1}", dataKey, instance.SubnetId), string.Format("{0}-ins-{1}", dataKey, instance.InstanceId));
                    dKey = string.Format("{0}-{1}", dataKey, instance.SubnetId);
                    if (!subnetIpSet.ContainsKey(dKey))
                    {
                        subnetIpSet.Add(dKey, instance.PrivateIpAddress);
                    }
                    else
                    {
                        subnetIpSet[dKey] += ("," + instance.PrivateIpAddress);
                    }
                }
            }
            //FlowLogManager.ReadES(subnetIpSet, db, dataKey);

            var asc = new AmazonAutoScalingClient(regionEndPoint);
            DescribeAutoScalingGroupsResponse asgResponse = asc.DescribeAutoScalingGroups();
            DescribeAutoScalingInstancesResponse asgInstanceResponse = asc.DescribeAutoScalingInstances();
            foreach (var asGroup in asgResponse.AutoScalingGroups)
            {
                db.SetAdd(string.Format("{0}-asgs", dataKey), string.Format("asg-{0}", asGroup.AutoScalingGroupName));
                var asgJson = JsonConvert.SerializeObject(asGroup);
                db.StringSet(string.Format("{0}-asg-{1}", dataKey, asGroup.AutoScalingGroupName), asgJson);
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
                        db.StringSet(string.Format("{0}-ins-{1}", dataKey, instance.InstanceId), instanceJson);
                    }                    
                }
            }

            var elbc = new AmazonElasticLoadBalancingClient(regionEndPoint);
            var elbResponse = elbc.DescribeLoadBalancers();
            foreach (var elb in elbResponse.LoadBalancerDescriptions)
            {
                db.SetAdd(string.Format("{0}-lbs", dataKey), string.Format("lbg-{0}", elb.LoadBalancerName));
                var elbJson = JsonConvert.SerializeObject(elb);
                db.StringSet(string.Format("{0}-lb-{1}", dataKey, elb.LoadBalancerName), elbJson);
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
                        db.StringSet(string.Format("{0}-ins-{1}", dataKey, instance.InstanceId), instanceJson);
                    }                    
                }
            }

            var sgResponse = ec2.DescribeSecurityGroups();
            foreach (var sg in sgResponse.SecurityGroups)
            {
                var sgJson = JsonConvert.SerializeObject(sg);
                db.SetAdd(string.Format("{0}-sgs", dataKey), string.Format("sg-{0}", sg.GroupName));
                db.StringSet(string.Format("{0}-sg-{1}", dataKey, sg.GroupName), sgJson);
            }

            Console.WriteLine("Start reading flowlogs and writing traffic data to redis ({0})", regionEndPoint.SystemName);
            try
            {
                FlowLogManager.ReadES(db, dataKey, subnetResponse.Subnets, vgResponse.VpnGateways, igResponse.InternetGateways);
                Console.WriteLine("End reading flowlogs and writing traffic data to redis ({0})", regionEndPoint.SystemName);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading flowlogs and writing traffic data to redis ({0}): {1}", regionEndPoint.SystemName, ex.Message);
            }            
            Console.WriteLine("End writing data to redis ({0})", regionEndPoint.SystemName);
        }

        //public static string GetServiceOutput()
        //{
        //    StringBuilder sb = new StringBuilder(1024);
        //    using (StringWriter sr = new StringWriter(sb))
        //    {
        //        sr.WriteLine("===========================================");
        //        sr.WriteLine("Welcome to the AWS .NET SDK!");
        //        sr.WriteLine("===========================================");

        //        // Print the number of Amazon EC2 instances.
        //        var ec2 = new AmazonEC2Client();
        //        DescribeVpcsRequest vpcRequest = new DescribeVpcsRequest();

        //        try
        //        {
        //            DescribeVpcsResponse ec2Response = ec2.DescribeVpcs(vpcRequest);
        //            int numVpcs = 0;
        //            numVpcs = ec2Response.Vpcs.Count;
        //            sr.WriteLine(string.Format("You have {0} Amazon VPCs setup in the {1} region.",
        //                numVpcs, ConfigurationManager.AppSettings["AWSRegion"]));
        //        }
        //        catch (AmazonEC2Exception ex)
        //        {
        //            if (ex.ErrorCode != null && ex.ErrorCode.Equals("AuthFailure"))
        //            {
        //                sr.WriteLine("The account you are using is not signed up for Amazon EC2.");
        //                sr.WriteLine("You can sign up for Amazon EC2 at http://aws.amazon.com/ec2");
        //            }
        //            else
        //            {
        //                sr.WriteLine("Caught Exception: " + ex.Message);
        //                sr.WriteLine("Response Status Code: " + ex.StatusCode);
        //                sr.WriteLine("Error Code: " + ex.ErrorCode);
        //                sr.WriteLine("Error Type: " + ex.ErrorType);
        //                sr.WriteLine("Request ID: " + ex.RequestId);
        //            }
        //        }

        //        DescribeSubnetsRequest subnetRequest = new DescribeSubnetsRequest();
        //        //subnetRequest.Filters.Add(new Filter("vpc-id", new List<string>() { "vpc-6207ff07" }));
        //        try
        //        {
        //            DescribeSubnetsResponse ec2Response = ec2.DescribeSubnets(subnetRequest);
        //            int numSubnets = 0;
        //            numSubnets = ec2Response.Subnets.Count;
        //            sr.WriteLine(string.Format("You have {0} Amazon subnets setup in the {1} region.",
        //                numSubnets, ConfigurationManager.AppSettings["AWSRegion"]));
        //        }
        //        catch (AmazonEC2Exception ex)
        //        {
        //            if (ex.ErrorCode != null && ex.ErrorCode.Equals("AuthFailure"))
        //            {
        //                sr.WriteLine("The account you are using is not signed up for Amazon EC2.");
        //                sr.WriteLine("You can sign up for Amazon EC2 at http://aws.amazon.com/ec2");
        //            }
        //            else
        //            {
        //                sr.WriteLine("Caught Exception: " + ex.Message);
        //                sr.WriteLine("Response Status Code: " + ex.StatusCode);
        //                sr.WriteLine("Error Code: " + ex.ErrorCode);
        //                sr.WriteLine("Error Type: " + ex.ErrorType);
        //                sr.WriteLine("Request ID: " + ex.RequestId);
        //            }
        //        }

        //        // Print the number of Amazon EC2 instances.
        //        //IAmazonEC2 ec2 = AWSClientFactory.CreateAmazonEC2Client();
        //        DescribeInstancesRequest ec2Request = new DescribeInstancesRequest();
        //        ec2Request.Filters.Add(new Filter("subnet-id", new List<string>() {"subnet-d67baeb3"}));
        //        try
        //        {
        //            DescribeInstancesResponse ec2Response = ec2.DescribeInstances(ec2Request);
        //            int numInstances = 0;
        //            numInstances = ec2Response.Reservations.Count;
        //            sr.WriteLine(string.Format("You have {0} Amazon EC2 instance(s) running in the {1} region.",
        //                numInstances, ConfigurationManager.AppSettings["AWSRegion"]));
        //        }
        //        catch (AmazonEC2Exception ex)
        //        {
        //            if (ex.ErrorCode != null && ex.ErrorCode.Equals("AuthFailure"))
        //            {
        //                sr.WriteLine("The account you are using is not signed up for Amazon EC2.");
        //                sr.WriteLine("You can sign up for Amazon EC2 at http://aws.amazon.com/ec2");
        //            }
        //            else
        //            {
        //                sr.WriteLine("Caught Exception: " + ex.Message);
        //                sr.WriteLine("Response Status Code: " + ex.StatusCode);
        //                sr.WriteLine("Error Code: " + ex.ErrorCode);
        //                sr.WriteLine("Error Type: " + ex.ErrorType);
        //                sr.WriteLine("Request ID: " + ex.RequestId);
        //            }
        //        }

        //        sr.WriteLine("Press any key to continue...");
        //    }
        //    return sb.ToString();
        //}

        //public static string GetTopology()
        //{
        //    //var topology = new TopologyHierarchy();
        //    var company = new Company();
        //    company.name = "CloudIQ";
        //    //topology.children = company;
        //    var account = new TopologyReader.Account();
        //    account.name = "Non-prod";
        //    company.children = new[]{account};

        //    IAmazonEC2 ec2 = new AmazonEC2Client();

        //    var vpcRequest = new DescribeVpcsRequest();
        //    try
        //    {
        //        DescribeVpcsResponse vpcResponse = ec2.DescribeVpcs(vpcRequest);
        //        var vpcList = new List<VPC>(); 
        //        foreach (Vpc vpc in vpcResponse.Vpcs)
        //        {
        //            var topologyVPC = new VPC() { name = vpc.Tags.Find(t => t.Key == "Name").Value };
        //            var subnetRequest = new DescribeSubnetsRequest();
        //            subnetRequest.Filters.Add(new Filter("vpc-id", new List<string>() {vpc.VpcId}));
        //            DescribeSubnetsResponse subnetResponse = ec2.DescribeSubnets(subnetRequest);
        //            var subnetList = new List<Subnet>(); 
        //            foreach (var subnet in subnetResponse.Subnets)
        //            {
        //                var topologySubnet = new Subnet() { name = subnet.Tags.Find(t => t.Key == "Name").Value };
        //                var ec2Request = new DescribeInstancesRequest();
        //                ec2Request.Filters.Add(new Filter("subnet-id", new List<string>() {subnet.SubnetId}));
        //                DescribeInstancesResponse ec2Response = ec2.DescribeInstances(ec2Request);
        //                var instanceList = new List<Instance>();
        //                foreach (var ec2Instance in ec2Response.Reservations)
        //                {
        //                    var instance = ec2Instance.Instances[0];
        //                    if (instance != null)
        //                    {
        //                        var topologyInstance = new Instance()
        //                        {
        //                            name = instance.InstanceId,//instance.Tags.Find(t => t.Key == "Name").Value,
        //                            size = new Random().Next(1,10),
        //                            instanceState = instance.State.Name.Value,
        //                            instanceType = instance.InstanceType.Value,
        //                            launchTime = instance.LaunchTime
        //                        };
        //                        instanceList.Add(topologyInstance);
        //                    }                           
        //                }
        //                topologySubnet.children = instanceList.ToArray();
        //                subnetList.Add(topologySubnet);
        //            }
        //            topologyVPC.children = subnetList.ToArray();
        //            vpcList.Add(topologyVPC);
        //        }
        //        account.children = vpcList.ToArray();
        //        string json = JsonConvert.SerializeObject(company, Formatting.Indented);
        //    }
        //    catch (AmazonEC2Exception ex)
        //    {

        //    }
        //    return string.Empty;
        //}

        //public static string GetTopology2()
        //{
        //    //var topology = new TopologyHierarchy();
        //    var company = new CompanySG();
        //    company.name = "CloudIQ";
        //    //topology.children = company;
        //    var account = new TopologyReader.AccountSG();
        //    account.name = "Non-prod";
        //    company.children = new[] { account };

        //    IAmazonEC2 ec2 = new AmazonEC2Client();

        //    var vpcRequest = new DescribeVpcsRequest();
        //    try
        //    {
        //        DescribeVpcsResponse vpcResponse = ec2.DescribeVpcs(vpcRequest);                

        //        var vpcList = new List<VPCSG>();
        //        foreach (Vpc vpc in vpcResponse.Vpcs)
        //        {
        //            var topologyVPC = new VPCSG() { name = vpc.Tags.Find(t => t.Key == "Name").Value };
        //            var sgRequest = new DescribeSecurityGroupsRequest();
        //            sgRequest.Filters.Add(new Filter("vpc-id", new List<string>() { vpc.VpcId }));
        //            var sgResponse = ec2.DescribeSecurityGroups(sgRequest);
        //            var sgList = new List<SecurityGroup>();
        //            foreach (var sg in sgResponse.SecurityGroups)
        //            {
        //                var topologySG = new SecurityGroup() { name = sg.GroupName };
        //                var ec2Request = new DescribeInstancesRequest();
        //                ec2Request.Filters.Add(new Filter("instance.group-id", new List<string>() { sg.GroupId }));
        //                DescribeInstancesResponse ec2Response = ec2.DescribeInstances(ec2Request);
        //                var instanceList = new List<Instance>();
        //                foreach (var ec2Instance in ec2Response.Reservations)
        //                {
        //                    var instance = ec2Instance.Instances[0];
        //                    if (instance != null)
        //                    {
        //                        var topologyInstance = new Instance()
        //                        {
        //                            name = instance.InstanceId,//instance.Tags.Find(t => t.Key == "Name").Value,
        //                            size = new Random().Next(1, 10),
        //                            instanceState = instance.State.Name.Value,
        //                            instanceType = instance.InstanceType.Value,
        //                            launchTime = instance.LaunchTime
        //                        };
        //                        instanceList.Add(topologyInstance);
        //                    }
        //                }
        //                topologySG.children = instanceList.ToArray();
        //                sgList.Add(topologySG);
        //            }
        //            topologyVPC.children = sgList.ToArray();
        //            vpcList.Add(topologyVPC);
        //        }
        //        account.children = vpcList.ToArray();
        //        string json = JsonConvert.SerializeObject(company, Formatting.Indented);
        //    }
        //    catch (AmazonEC2Exception ex)
        //    {

        //    }
        //    return string.Empty;
        //}

        //public static void GetTopology3()
        //{
        //    var ec2 = new AmazonEC2Client();
        //    var asc = new Amazon.AutoScaling.AmazonAutoScalingClient();
        //    IAmazonElasticLoadBalancing elb = new AmazonElasticLoadBalancingClient();

        //    //var vpcRequest = new DescribeVpcsRequest();
        //    //var subnetRequest = new DescribeSubnetsRequest();
        //    //var sgRequest = new DescribeSecurityGroupsRequest();
        //    //var asgRequest = new DescribeAutoScalingGroupsRequest();
        //    //var asgInstanceRequest = new DescribeAutoScalingInstancesRequest();
        //    try
        //    {
        //        DescribeVpcsResponse vpcResponse = ec2.DescribeVpcs();
        //        DescribeSubnetsResponse subnetResponse = ec2.DescribeSubnets();
        //        DescribeSecurityGroupsResponse sgResponse = ec2.DescribeSecurityGroups();
        //        DescribeAutoScalingGroupsResponse asgResponse = asc.DescribeAutoScalingGroups();
        //        DescribeAutoScalingInstancesResponse asgInstanceResponse = asc.DescribeAutoScalingInstances();                
                    
        //        WriteFile("vpcs.csv", vpcResponse.Vpcs);
        //        WriteFile("subnets.csv", subnetResponse.Subnets);
        //        WriteFile("securityGroups.csv", sgResponse.SecurityGroups);
        //        WriteFile("asgs.csv", asgResponse.AutoScalingGroups);
        //        WriteFile("asgInstances.csv", asgInstanceResponse.AutoScalingInstances);

        //        DescribeInstancesResponse instanceResponse = ec2.DescribeInstances();
        //        var reservationIndex = 0;
        //        foreach (var reservation in instanceResponse.Reservations)
        //        {
        //            if (reservationIndex == 0)
        //                WriteFile("instances.csv", reservation.Instances);
        //            else
        //                AppendFile("instances.csv", reservation.Instances);
        //            reservationIndex++;
        //        }

        //        DescribeNetworkAclsResponse naclResponse = ec2.DescribeNetworkAcls();
        //        WriteFile("nacls.csv", naclResponse.NetworkAcls);

        //        Amazon.EC2.Model.DescribeTagsResponse tagsResponse = ec2.DescribeTags();
        //        WriteFile("tags.csv", tagsResponse.Tags);

        //        DescribeVolumesResponse volumesResponse = ec2.DescribeVolumes();
        //        WriteFile("volumes.csv", volumesResponse.Volumes);

        //        //var elbResponse = elb.DescribeLoadBalancers();
        //        //WriteFile("elbs.csv", elbResponse.LoadBalancerDescriptions);

        //        DescribeInternetGatewaysResponse igResponse = ec2.DescribeInternetGateways();
        //        WriteFile("igs.csv", igResponse.InternetGateways);
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex.Message);
        //    }
        //}

        //public static void WriteFile(string filePath, IEnumerable records)
        //{
        //    using (var w = new CsvWriter(new StreamWriter(filePath)))
        //    {
        //        w.WriteRecords(records);
        //    }
        //}

        public static void WriteFile(string filename, IEnumerable records)
        {
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Replace(@"file:\", "") + @"\" + filename;
            using (var w = new CsvWriter(new StreamWriter(path)))
            {
                w.WriteRecords(records);
            }
        }

        public static void AppendFile(string filename, IEnumerable records)
        {
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Replace(@"file:\", "") + @"\" + filename;
            using (var w = new CsvWriter(new StreamWriter(path, true)))
            {
                w.Configuration.HasHeaderRecord = false;
                w.WriteRecords(records);
            }
        }

    }
}