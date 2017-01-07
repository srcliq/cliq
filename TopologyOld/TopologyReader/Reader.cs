using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;

using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.SimpleDB;
using Amazon.SimpleDB.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json;
using Tag = Amazon.EC2.Model.Tag;

namespace TopologyReader
{
    internal class Reader
    {
        public static void Main(string[] args)
        {
            //Console.Write(GetServiceOutput());
            GetTopology2();
            Console.Read();
        }

        public static string GetServiceOutput()
        {
            StringBuilder sb = new StringBuilder(1024);
            using (StringWriter sr = new StringWriter(sb))
            {
                sr.WriteLine("===========================================");
                sr.WriteLine("Welcome to the AWS .NET SDK!");
                sr.WriteLine("===========================================");

                // Print the number of Amazon EC2 instances.
                IAmazonEC2 ec2 = AWSClientFactory.CreateAmazonEC2Client();
                DescribeVpcsRequest vpcRequest = new DescribeVpcsRequest();

                try
                {
                    DescribeVpcsResponse ec2Response = ec2.DescribeVpcs(vpcRequest);
                    int numVpcs = 0;
                    numVpcs = ec2Response.Vpcs.Count;
                    sr.WriteLine(string.Format("You have {0} Amazon VPCs setup in the {1} region.",
                        numVpcs, ConfigurationManager.AppSettings["AWSRegion"]));
                }
                catch (AmazonEC2Exception ex)
                {
                    if (ex.ErrorCode != null && ex.ErrorCode.Equals("AuthFailure"))
                    {
                        sr.WriteLine("The account you are using is not signed up for Amazon EC2.");
                        sr.WriteLine("You can sign up for Amazon EC2 at http://aws.amazon.com/ec2");
                    }
                    else
                    {
                        sr.WriteLine("Caught Exception: " + ex.Message);
                        sr.WriteLine("Response Status Code: " + ex.StatusCode);
                        sr.WriteLine("Error Code: " + ex.ErrorCode);
                        sr.WriteLine("Error Type: " + ex.ErrorType);
                        sr.WriteLine("Request ID: " + ex.RequestId);
                    }
                }

                DescribeSubnetsRequest subnetRequest = new DescribeSubnetsRequest();
                //subnetRequest.Filters.Add(new Filter("vpc-id", new List<string>() { "vpc-6207ff07" }));
                try
                {
                    DescribeSubnetsResponse ec2Response = ec2.DescribeSubnets(subnetRequest);
                    int numSubnets = 0;
                    numSubnets = ec2Response.Subnets.Count;
                    sr.WriteLine(string.Format("You have {0} Amazon subnets setup in the {1} region.",
                        numSubnets, ConfigurationManager.AppSettings["AWSRegion"]));
                }
                catch (AmazonEC2Exception ex)
                {
                    if (ex.ErrorCode != null && ex.ErrorCode.Equals("AuthFailure"))
                    {
                        sr.WriteLine("The account you are using is not signed up for Amazon EC2.");
                        sr.WriteLine("You can sign up for Amazon EC2 at http://aws.amazon.com/ec2");
                    }
                    else
                    {
                        sr.WriteLine("Caught Exception: " + ex.Message);
                        sr.WriteLine("Response Status Code: " + ex.StatusCode);
                        sr.WriteLine("Error Code: " + ex.ErrorCode);
                        sr.WriteLine("Error Type: " + ex.ErrorType);
                        sr.WriteLine("Request ID: " + ex.RequestId);
                    }
                }

                // Print the number of Amazon EC2 instances.
                //IAmazonEC2 ec2 = AWSClientFactory.CreateAmazonEC2Client();
                DescribeInstancesRequest ec2Request = new DescribeInstancesRequest();
                ec2Request.Filters.Add(new Filter("subnet-id", new List<string>() {"subnet-d67baeb3"}));
                try
                {
                    DescribeInstancesResponse ec2Response = ec2.DescribeInstances(ec2Request);
                    int numInstances = 0;
                    numInstances = ec2Response.Reservations.Count;
                    sr.WriteLine(string.Format("You have {0} Amazon EC2 instance(s) running in the {1} region.",
                        numInstances, ConfigurationManager.AppSettings["AWSRegion"]));
                }
                catch (AmazonEC2Exception ex)
                {
                    if (ex.ErrorCode != null && ex.ErrorCode.Equals("AuthFailure"))
                    {
                        sr.WriteLine("The account you are using is not signed up for Amazon EC2.");
                        sr.WriteLine("You can sign up for Amazon EC2 at http://aws.amazon.com/ec2");
                    }
                    else
                    {
                        sr.WriteLine("Caught Exception: " + ex.Message);
                        sr.WriteLine("Response Status Code: " + ex.StatusCode);
                        sr.WriteLine("Error Code: " + ex.ErrorCode);
                        sr.WriteLine("Error Type: " + ex.ErrorType);
                        sr.WriteLine("Request ID: " + ex.RequestId);
                    }
                }

                sr.WriteLine("Press any key to continue...");
            }
            return sb.ToString();
        }

        public static string GetTopology()
        {
            //var topology = new TopologyHierarchy();
            var company = new Company();
            company.name = "CloudIQ";
            //topology.children = company;
            var account = new TopologyReader.Account();
            account.name = "Non-prod";
            company.children = new[]{account};

            IAmazonEC2 ec2 = AWSClientFactory.CreateAmazonEC2Client();

            var vpcRequest = new DescribeVpcsRequest();
            try
            {
                DescribeVpcsResponse vpcResponse = ec2.DescribeVpcs(vpcRequest);
                var vpcList = new List<VPC>(); 
                foreach (Vpc vpc in vpcResponse.Vpcs)
                {
                    var topologyVPC = new VPC() { name = vpc.Tags.Find(t => t.Key == "Name").Value };
                    var subnetRequest = new DescribeSubnetsRequest();
                    subnetRequest.Filters.Add(new Filter("vpc-id", new List<string>() {vpc.VpcId}));
                    DescribeSubnetsResponse subnetResponse = ec2.DescribeSubnets(subnetRequest);
                    var subnetList = new List<Subnet>(); 
                    foreach (var subnet in subnetResponse.Subnets)
                    {
                        var topologySubnet = new Subnet() { name = subnet.Tags.Find(t => t.Key == "Name").Value };
                        var ec2Request = new DescribeInstancesRequest();
                        ec2Request.Filters.Add(new Filter("subnet-id", new List<string>() {subnet.SubnetId}));
                        DescribeInstancesResponse ec2Response = ec2.DescribeInstances(ec2Request);
                        var instanceList = new List<Instance>();
                        foreach (var ec2Instance in ec2Response.Reservations)
                        {
                            var instance = ec2Instance.Instances[0];
                            if (instance != null)
                            {
                                var topologyInstance = new Instance()
                                {
                                    name = instance.InstanceId,//instance.Tags.Find(t => t.Key == "Name").Value,
                                    size = new Random().Next(1,10),
                                    instanceState = instance.State.Name.Value,
                                    instanceType = instance.InstanceType.Value,
                                    launchTime = instance.LaunchTime
                                };
                                instanceList.Add(topologyInstance);
                            }                           
                        }
                        topologySubnet.children = instanceList.ToArray();
                        subnetList.Add(topologySubnet);
                    }
                    topologyVPC.children = subnetList.ToArray();
                    vpcList.Add(topologyVPC);
                }
                account.children = vpcList.ToArray();
                string json = JsonConvert.SerializeObject(company, Formatting.Indented);
            }
            catch (AmazonEC2Exception ex)
            {

            }
            return string.Empty;
        }

        public static string GetTopology2()
        {
            //var topology = new TopologyHierarchy();
            var company = new CompanySG();
            company.name = "CloudIQ";
            //topology.children = company;
            var account = new TopologyReader.AccountSG();
            account.name = "Non-prod";
            company.children = new[] { account };

            IAmazonEC2 ec2 = AWSClientFactory.CreateAmazonEC2Client();

            var vpcRequest = new DescribeVpcsRequest();
            try
            {
                DescribeVpcsResponse vpcResponse = ec2.DescribeVpcs(vpcRequest);
                var vpcList = new List<VPCSG>();
                foreach (Vpc vpc in vpcResponse.Vpcs)
                {
                    var topologyVPC = new VPCSG() { name = vpc.Tags.Find(t => t.Key == "Name").Value };
                    var sgRequest = new DescribeSecurityGroupsRequest();
                    sgRequest.Filters.Add(new Filter("vpc-id", new List<string>() { vpc.VpcId }));
                    var sgResponse = ec2.DescribeSecurityGroups(sgRequest);
                    var sgList = new List<SecurityGroup>();
                    foreach (var sg in sgResponse.SecurityGroups)
                    {
                        var topologySG = new SecurityGroup() { name = sg.GroupName };
                        var ec2Request = new DescribeInstancesRequest();
                        ec2Request.Filters.Add(new Filter("instance.group-id", new List<string>() { sg.GroupId }));
                        DescribeInstancesResponse ec2Response = ec2.DescribeInstances(ec2Request);
                        var instanceList = new List<Instance>();
                        foreach (var ec2Instance in ec2Response.Reservations)
                        {
                            var instance = ec2Instance.Instances[0];
                            if (instance != null)
                            {
                                var topologyInstance = new Instance()
                                {
                                    name = instance.InstanceId,//instance.Tags.Find(t => t.Key == "Name").Value,
                                    size = new Random().Next(1, 10),
                                    instanceState = instance.State.Name.Value,
                                    instanceType = instance.InstanceType.Value,
                                    launchTime = instance.LaunchTime
                                };
                                instanceList.Add(topologyInstance);
                            }
                        }
                        topologySG.children = instanceList.ToArray();
                        sgList.Add(topologySG);
                    }
                    topologyVPC.children = sgList.ToArray();
                    vpcList.Add(topologyVPC);
                }
                account.children = vpcList.ToArray();
                string json = JsonConvert.SerializeObject(company, Formatting.Indented);
            }
            catch (AmazonEC2Exception ex)
            {

            }
            return string.Empty;
        }
    }
}