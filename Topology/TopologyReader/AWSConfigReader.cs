using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using TopologyReader.Data;
using TopologyReader.Helpers;
using Instance = Amazon.EC2.Model.Instance;

namespace TopologyReader
{
    static class AWSConfigReader
    {
        private static readonly ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static AmazonSQSClient sqsClient = new AmazonSQSClient(RegionEndpoint.USWest2);

        public static void ProcessConfigMessages()
        {
            Log.Info("Start processing queue messages");
            var configQueueUrl = sqsClient.GetQueueUrl("config-queue").QueueUrl;
            var result = sqsClient.ReceiveMessage(new ReceiveMessageRequest
            {
                QueueUrl = configQueueUrl,
                WaitTimeSeconds = 20,
                MaxNumberOfMessages = 10
            });
            Log.InfoFormat("Number of the messages to process = {0}", result.Messages.Count);
            if (result.Messages.Count != 0)
            {
                for (int messageIndex = 0; messageIndex < result.Messages.Count; messageIndex++)
                {
                    try
                    {
                        if (result.Messages[messageIndex].Body != "")
                        {
                            var messageBody = result.Messages[messageIndex].Body;//.Replace("\\", "");
                            var m = JObject.Parse(messageBody);
                            var configNotification = JsonConvert.DeserializeObject<ConfigNotification>(m.ToString());
                            var c = JObject.Parse(configNotification.Message);
                            var message = JsonConvert.DeserializeObject<ConfigMessage>(c.ToString());
                            if (message.messageType == "ConfigurationItemChangeNotification")
                            {
                                var ci = JObject.Parse(message.configurationItem.ToString());
                                var ciDiff = JObject.Parse(message.configurationItemDiff.ToString());
                                var configurationItemDiff = JsonConvert.DeserializeObject<ConfigurationItemDiff>(ciDiff.ToString());
                                var entityIdentifier = string.Empty;
                                var entityId = string.Empty;
                                var entityJson = string.Empty;
                                var configuration = JsonConvert.DeserializeObject<ConfigurationItem>(ci.ToString());
                                if(configuration.configuration != null)
                                {
                                    switch (configuration.ResourceType)
                                    {
                                        case "AWS::EC2::Volume":
                                            var volume = JsonConvert.DeserializeObject<Amazon.EC2.Model.Volume>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                            var volumeJson = JsonConvert.SerializeObject(volume);
                                            entityIdentifier = "volume";
                                            entityId = volume.VolumeId;
                                            entityJson = volumeJson;
                                            break;
                                        case "AWS::EC2::Host":
                                            var host = JsonConvert.DeserializeObject<Amazon.EC2.Model.Host>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                            var hostJson = JsonConvert.SerializeObject(host);
                                            entityIdentifier = "host";
                                            entityId = host.HostId;
                                            entityJson = hostJson;
                                            break;
                                        case "AWS::EC2::EIP":
                                            var eip = JsonConvert.DeserializeObject<Amazon.EC2.Model.Address>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                            var eipJson = JsonConvert.SerializeObject(eip);
                                            entityIdentifier = "eip";
                                            entityId = eip.AllocationId;
                                            entityJson = eipJson;
                                            break;
                                        case "AWS::EC2::Instance":
                                            var instance = JsonConvert.DeserializeObject<Amazon.EC2.Model.Instance>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                            var instanceJson = JsonConvert.SerializeObject(instance);
                                            entityIdentifier = "instance";
                                            entityId = instance.InstanceId;
                                            entityJson = instanceJson;
                                            break;
                                        case "AWS::EC2::NetworkInterface":
                                            var eni = JsonConvert.DeserializeObject<Amazon.EC2.Model.NetworkInterface>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                            var eniJson = JsonConvert.SerializeObject(eni);
                                            entityIdentifier = "eni";
                                            entityId = eni.NetworkInterfaceId;
                                            entityJson = eniJson;
                                            break;
                                        case "AWS::EC2::SecurityGroup":
                                            var sg = JsonConvert.DeserializeObject<Amazon.EC2.Model.SecurityGroup>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                            var sgJson = JsonConvert.SerializeObject(sg);
                                            entityIdentifier = "sg";
                                            entityId = sg.GroupId;
                                            entityJson = sgJson;
                                            break;
                                        case "AWS::EC2::CustomerGateway":
                                            var cg = JsonConvert.DeserializeObject<Amazon.EC2.Model.CustomerGateway>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                            var cgJson = JsonConvert.SerializeObject(cg);
                                            entityIdentifier = "cg";
                                            entityId = cg.CustomerGatewayId;
                                            entityJson = cgJson;
                                            break;
                                        case "AWS::EC2::InternetGateway":
                                            var ig = JsonConvert.DeserializeObject<Amazon.EC2.Model.InternetGateway>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                            var igJson = JsonConvert.SerializeObject(ig);
                                            entityIdentifier = "ig";
                                            entityId = ig.InternetGatewayId;
                                            entityJson = igJson;
                                            break;
                                        case "AWS::EC2::NetworkAcl":
                                            var nacl = JsonConvert.DeserializeObject<Amazon.EC2.Model.NetworkAcl>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                            var naclJson = JsonConvert.SerializeObject(nacl);
                                            entityIdentifier = "nacl";
                                            entityId = nacl.NetworkAclId;
                                            entityJson = naclJson;
                                            break;
                                        case "AWS::EC2::RouteTable":
                                            var rt = JsonConvert.DeserializeObject<Amazon.EC2.Model.RouteTable>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                            var rtJson = JsonConvert.SerializeObject(rt);
                                            entityIdentifier = "rt";
                                            entityId = rt.RouteTableId;
                                            entityJson = rtJson;
                                            break;
                                        case "AWS::EC2::Subnet":
                                            var subnet = JsonConvert.DeserializeObject<Amazon.EC2.Model.Subnet>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                            var subnetJson = JsonConvert.SerializeObject(subnet);
                                            entityIdentifier = "subnet";
                                            entityId = subnet.SubnetId;
                                            entityJson = subnetJson;
                                            break;
                                        case "AWS::EC2::VPC":
                                            var vpc = JsonConvert.DeserializeObject<Amazon.EC2.Model.SecurityGroup>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                            var vpcJson = JsonConvert.SerializeObject(vpc);
                                            entityIdentifier = "vpc";
                                            entityId = vpc.VpcId;
                                            entityJson = vpcJson;
                                            break;
                                        case "AWS::EC2::VPNConnection":
                                            var vpn = JsonConvert.DeserializeObject<Amazon.EC2.Model.VpnConnection>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                            var vpnJson = JsonConvert.SerializeObject(vpn);
                                            entityIdentifier = "vpn";
                                            entityId = vpn.VpnConnectionId;
                                            entityJson = vpnJson;
                                            break;
                                        case "AWS::EC2::VPNGateway":
                                            var vg = JsonConvert.DeserializeObject<Amazon.EC2.Model.VpnGateway>(configuration.configuration.ToString(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                                            var vgJson = JsonConvert.SerializeObject(vg);
                                            entityIdentifier = "vg";
                                            entityId = vg.VpnGatewayId;
                                            entityJson = vgJson;
                                            break;
                                        default:
                                            break;
                                    }
                                }                                
                                if (!string.IsNullOrEmpty(entityId))
                                {
                                    Common.UpdateTopology(configuration, entityIdentifier, entityId, entityJson, configurationItemDiff.ChangeType);
                                }
                            }


                            //Dictionary<string, string> cnAttributes = JsonConvert.DeserializeObject<Dictionary<string, string>>(result.Messages[i].Body);
                            ////var x = JsonConvert.DeserializeAnonymousType(result.Messages[i].Body);
                            //Dictionary<string, string> messageAttributes = JsonConvert.DeserializeObject<Dictionary<string, string>>(cnAttributes["Message"].Replace("\\", ""));
                            var receiptHandle = result.Messages[messageIndex].ReceiptHandle;

                            var deleteMessageRequest = new DeleteMessageRequest();

                            deleteMessageRequest.QueueUrl = configQueueUrl;
                            deleteMessageRequest.ReceiptHandle = receiptHandle;

                            var response = sqsClient.DeleteMessage(deleteMessageRequest);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error occurred while processing config message", ex);
                    }                    
                }
            }
            Log.Info("End processing queue messages");
        }

        
    }
}
