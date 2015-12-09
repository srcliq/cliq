using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using TopologyReader.Data;
using TopologyReader.Helpers;

namespace TopologyReader
{
    static class AWSConfigReader
    {
        private static AmazonSQSClient sqsClient = new AmazonSQSClient(RegionEndpoint.USWest2);

        public static void ProcessConfigMessages()
        {
            var configQueueUrl = sqsClient.GetQueueUrl("config-queue").QueueUrl;
            var result = sqsClient.ReceiveMessage(new ReceiveMessageRequest
            {
                QueueUrl = configQueueUrl,
                WaitTimeSeconds = 20,
                MaxNumberOfMessages = 10
            });

            if (result.Messages.Count != 0)
            {
                for (int messageIndex = 0; messageIndex < result.Messages.Count; messageIndex++)
                {
                    if (result.Messages[messageIndex].Body != "")
                    {
                        var messageBody = result.Messages[messageIndex].Body;//.Replace("\\", "");
                        var m = JObject.Parse(messageBody);
                        var configNotification = JsonConvert.DeserializeObject<ConfigNotification>(m.ToString());
                        var c = JObject.Parse(configNotification.Message);
                        var message = JsonConvert.DeserializeObject<ConfigMessage>(c.ToString());
                        if (message.messageType != "ConfigurationItemChangeNotification")
                        {
                            return;
                        }
                        var ci = JObject.Parse(message.configurationItem.ToString());
                        var ciDiff = JObject.Parse(message.configurationItemDiff.ToString());
                        var configurationItemDiff = JsonConvert.DeserializeObject<ConfigurationItemDiff>(ciDiff.ToString());
                        var configuration = JsonConvert.DeserializeObject<ConfigurationItem>(ci.ToString());
                        switch (configuration.ResourceType)
                        {
                            case "AWS::EC2::Volume":
                                var volume = JsonConvert.DeserializeObject<Amazon.EC2.Model.Volume>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                var volumeJson = JsonConvert.SerializeObject(volume);
                                break;
                            case "AWS::EC2::Host":
                                var host = JsonConvert.DeserializeObject<Amazon.EC2.Model.Host>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                var hostJson = JsonConvert.SerializeObject(host);
                                break;
                            case "AWS::EC2::EIP":
                                var eip = JsonConvert.DeserializeObject<Amazon.EC2.Model.Address>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                var eipJson = JsonConvert.SerializeObject(eip);
                                break;
                            case "AWS::EC2::Instance":
                                var instance = JsonConvert.DeserializeObject<Amazon.EC2.Model.Instance>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                var instanceJson = JsonConvert.SerializeObject(instance);
                                var captureTimeString = configuration.ConfigurationItemCaptureTime;
                                var captureTime = DateTime.UtcNow;
                                DateTime.TryParse(captureTimeString, out captureTime);
                                var latestDataKey = Common.GetDataKey("latest", configuration.AWSAccountId, configuration.AWSRegion);
                                var latestInstanceSetKey = string.Format("{0}-ins", latestDataKey);
                                var newDataKey = Common.GetDataKey(captureTime, configuration.AWSAccountId, configuration.AWSRegion);
                                var dataKey = Common.GetDataKey(configuration.AWSAccountId, configuration.AWSRegion);
                                var instanceKey = string.Format("{0}-ins-{1}", dataKey, instance.InstanceId);
                                var db = RedisManager.GetRedisDatabase();
                                RedisManager.AddWithExpiry(instanceKey, instanceJson, db);
                                if (RedisManager.GetSet(latestInstanceSetKey, db).Length > 0)
                                {
                                    switch (configurationItemDiff.ChangeType)
                                    {
                                        case "CREATE":
                                            RedisManager.AddSetWithExpiry(latestInstanceSetKey, instanceKey, db);
                                            break;
                                        case "UPDATE":
                                            RedisManager.RemoveSetMember(latestInstanceSetKey, instanceKey, db);
                                            RedisManager.AddSetWithExpiry(latestInstanceSetKey, instanceKey, db);
                                            break;
                                        case "DELETE":
                                            RedisManager.RemoveSetMember(latestInstanceSetKey, instanceKey, db);
                                            break;
                                        default:
                                            break;
                                    }                                                     
                                }
                                var newInstanceSetKey = string.Format("{0}-ins", newDataKey);
                                RedisManager.CopySetAndStore(newInstanceSetKey, latestInstanceSetKey, db);
                                RedisManager.AddSortedSet(string.Format("timeline-{0}-{1}-ins", configuration.AWSAccountId, configuration.AWSRegion), newInstanceSetKey, db);
                                break;
                            case "AWS::EC2::NetworkInterface":
                                var eni = JsonConvert.DeserializeObject<Amazon.EC2.Model.NetworkInterface>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                var eniJson = JsonConvert.SerializeObject(eni);
                                break;
                            case "AWS::EC2::SecurityGroup":
                                var sg = JsonConvert.DeserializeObject<Amazon.EC2.Model.SecurityGroup>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                var sgJson = JsonConvert.SerializeObject(sg);
                                break;
                            case "AWS::EC2::CustomerGateway":
                                var cg = JsonConvert.DeserializeObject<Amazon.EC2.Model.CustomerGateway>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                var cgJson = JsonConvert.SerializeObject(cg);
                                break;
                            case "AWS::EC2::InternetGateway":
                                var ig = JsonConvert.DeserializeObject<Amazon.EC2.Model.InternetGateway>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                var igJson = JsonConvert.SerializeObject(ig);
                                break;
                            case "AWS::EC2::NetworkAcl":
                                var nacl = JsonConvert.DeserializeObject<Amazon.EC2.Model.NetworkAcl>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                var naclJson = JsonConvert.SerializeObject(nacl);
                                break;
                            case "AWS::EC2::RouteTable":
                                var rt = JsonConvert.DeserializeObject<Amazon.EC2.Model.RouteTable>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                var rtJson = JsonConvert.SerializeObject(rt);
                                break;
                            case "AWS::EC2::Subnet":
                                var subnet = JsonConvert.DeserializeObject<Amazon.EC2.Model.Subnet>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                var subnetJson = JsonConvert.SerializeObject(subnet);
                                break;
                            case "AWS::EC2::VPC":
                                var vpc = JsonConvert.DeserializeObject<Amazon.EC2.Model.SecurityGroup>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                var vpcJson = JsonConvert.SerializeObject(vpc);
                                //var dataKey = Common.GetDataKey(configuration.AWSAccountId, configuration.AWSRegion);
                                //var db = Common.GetRedisDatabase();
                                //Common.AddToRedisWithExpiry(string.Format("{0}-vpc-{1}", dataKey, vpc.VpcId), vpcJson, db);
                                break;
                            case "AWS::EC2::VPNConnection":
                                var vpn = JsonConvert.DeserializeObject<Amazon.EC2.Model.VpnConnection>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                var vpnJson = JsonConvert.SerializeObject(vpn);
                                break;
                            case "AWS::EC2::VPNGateway":
                                var vg = JsonConvert.DeserializeObject<Amazon.EC2.Model.SecurityGroup>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                var vgJson = JsonConvert.SerializeObject(vg);
                                break;
                            default:
                                break;
                        }
                        //Dictionary<string, string> cnAttributes = JsonConvert.DeserializeObject<Dictionary<string, string>>(result.Messages[i].Body);
                        ////var x = JsonConvert.DeserializeAnonymousType(result.Messages[i].Body);
                        //Dictionary<string, string> messageAttributes = JsonConvert.DeserializeObject<Dictionary<string, string>>(cnAttributes["Message"].Replace("\\", ""));
                        //var receiptHandle = result.Messages[i].ReceiptHandle;

                        //var deleteMessageRequest = new DeleteMessageRequest();

                        //deleteMessageRequest.QueueUrl = configQueueUrl;
                        //deleteMessageRequest.ReceiptHandle = receiptHandle;

                        //var response = sqsClient.DeleteMessage(deleteMessageRequest);
                    }
                }
            }
        }
    }
}
