using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using TcsData = CloudIQ.ES.API.Data;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using CloudIQ.ES.API.Data;
using System.Collections;
using Amazon.AutoScaling;
using Amazon.ElasticLoadBalancing;
using Amazon.ElasticLoadBalancing.Model;

namespace CloudIQ.ES.API.Helpers
{
    public static class Common
    {
        public static string ReadFile(string fileName)
        {
            using (StreamReader r = new StreamReader(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Replace(@"file:\", "") + @"\" + fileName))
            //using (StreamReader r = new StreamReader(@".\" + fileName))
            {
                string json = r.ReadToEnd();
                return json;
            }
        }

        public static List<Account> GetAccounts()
        {
            var accounts = new List<Account>();
            var account = new Account 
                            { 
                                AccountId = "549000045678",
                                AccountName = "Non-prod",
                                AvailableVolumes = 100,
                                BillingContact = "Billing Manager",
                                CommonContact = "General Manger",
                                CustomWarnings = 3,
                                InUseVolumes = 80,
                                L2mCost = 200000,
                                MtdCost = 50000,
                                OperationsContact = "Operations Manager",
                                ReservedInstances = 70,
                                RunningInstances = 80,
                                SecurityContact = "Security Manager",
                                StoppedInstances = 20,
                                TAWarnings = 5
                            };
            accounts.Add(account);
            account = new Account
                            {
                                AccountId = "549000045929",
                                AccountName = "Prod",
                                AvailableVolumes = 40,
                                BillingContact = "Billing Manager",
                                CommonContact = "General Manger",
                                CustomWarnings = 5,
                                InUseVolumes = 40,
                                L2mCost = 100000,
                                MtdCost = 25000,
                                OperationsContact = "Operations Manager",
                                ReservedInstances = 30,
                                RunningInstances = 20,
                                SecurityContact = "Security Manager",
                                StoppedInstances = 5,
                                TAWarnings = 3
                            };
            accounts.Add(account);
            return accounts;
        }

        public static List<TcsData.Vpc> GetVpcs()
        {
            IAmazonEC2 ec2 = AWSClientFactory.CreateAmazonEC2Client();
            DescribeVpcsRequest vpcRequest = new DescribeVpcsRequest();
            var vpcList = new List<TcsData.Vpc>();
            try
            {
                DescribeVpcsResponse ec2Response = ec2.DescribeVpcs(vpcRequest);
                int numVpcs = 0;
                numVpcs = ec2Response.Vpcs.Count;
                foreach(var vpc in ec2Response.Vpcs)
                {
                    var tcsVpc = new TcsData.Vpc();
                    //tcsVpc.AvailableVolumes = vpc.
                }
            }
            catch (AmazonEC2Exception ex)
            {

            }
            return vpcList;
        }

        public static Stream GetAccountCsvFile()
        {
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Replace(@"file:\", "") + @"\" + "accounts.csv";
            using (var w = new CsvWriter(new StreamWriter(path)))
            {                
                w.WriteRecords(GetAccounts());                
            }            
            var stream = new FileStream(path, FileMode.Open);
            return stream;
        }

        public static Stream GetVpcCsvFile()
        {
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Replace(@"file:\", "") + @"\" + "vpcs.csv";
            using (var w = new CsvWriter(new StreamWriter(path)))
            {
                w.WriteRecords(GetVpcs());
            }
            var stream = new FileStream(path, FileMode.Open);
            return stream;
        }

        public static void GetTopology()
        {
            IAmazonEC2 ec2 = AWSClientFactory.CreateAmazonEC2Client();
            IAmazonAutoScaling asg = AWSClientFactory.CreateAmazonAutoScalingClient();
            IAmazonElasticLoadBalancing elb = AWSClientFactory.CreateAmazonElasticLoadBalancingClient();

            DescribeVpcsResponse vpcResponse = ec2.DescribeVpcs();
            WriteFile("vpcs.csv", vpcResponse.Vpcs);

            DescribeInstancesResponse instanceResponse = ec2.DescribeInstances();
            var reservationIndex = 0;
            foreach(var reservation in instanceResponse.Reservations)
            {        
                if(reservationIndex == 0)
                    WriteFile("instances.csv", reservation.Instances);
                else
                    AppendFile("instances.csv", reservation.Instances);
                reservationIndex++;
            }

            DescribeNetworkAclsResponse naclResponse = ec2.DescribeNetworkAcls();
            WriteFile("nacls.csv", naclResponse.NetworkAcls);

            Amazon.EC2.Model.DescribeTagsResponse tagsResponse = ec2.DescribeTags();
            WriteFile("tags.csv", tagsResponse.Tags);

            DescribeVolumesResponse volumesResponse = ec2.DescribeVolumes();
            WriteFile("volumes.csv", volumesResponse.Volumes);

            DescribeLoadBalancersResponse elbResponse = elb.DescribeLoadBalancers();
            WriteFile("elbs.csv", elbResponse.LoadBalancerDescriptions);

            DescribeInternetGatewaysResponse igResponse = ec2.DescribeInternetGateways();
            WriteFile("igs.csv", igResponse.InternetGateways);
        }

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