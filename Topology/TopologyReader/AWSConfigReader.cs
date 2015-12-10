﻿using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
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
                        var entityIdentifier = string.Empty;
                        var entityId = string.Empty;
                        var entityJson = string.Empty;
                        var configuration = JsonConvert.DeserializeObject<ConfigurationItem>(ci.ToString());
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
                        if (!string.IsNullOrEmpty(entityId))
                        {
                            UpdateTopology(configuration, entityIdentifier, entityId, entityJson, configurationItemDiff.ChangeType);
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

        private static void UpdateTopology(ConfigurationItem configuration, string entityIdentifier, string entityId, string entityJson, string changeType)
        {
            var captureTimeString = configuration.ConfigurationItemCaptureTime;
            DateTime captureTime;
            if (!DateTime.TryParse(captureTimeString, out captureTime))
            {
                captureTime = DateTime.UtcNow;
            }
            var latestDataKey = Common.GetDataKey("latest", configuration.AWSAccountId, configuration.AWSRegion);
            var newDataKey = Common.GetDataKey(captureTime, configuration.AWSAccountId, configuration.AWSRegion);

            var latestEntitySetKey = string.Format("{0}-{1}set", latestDataKey, entityIdentifier);
            var newEntitySetKey = string.Format("{0}-{1}set", newDataKey, entityIdentifier);

            var entityTimelineSetKey = string.Format("timeline-{0}-{1}-{2}", configuration.AWSAccountId, configuration.AWSRegion, entityIdentifier);

            var dataKey = Common.GetDataKey(configuration.AWSAccountId, configuration.AWSRegion);
            var instanceKey = string.Format("{0}-{1}-{2}", dataKey, entityIdentifier, entityId);
            var db = RedisManager.GetRedisDatabase();
            RedisManager.AddWithExpiry(instanceKey, entityJson, db);
            if (RedisManager.GetSet(latestEntitySetKey, db).Length > 0)
            {
                switch (changeType)
                {
                    case "CREATE":
                        RedisManager.AddSetWithExpiry(latestEntitySetKey, instanceKey, db);
                        break;
                    case "UPDATE":
                        RedisManager.RemoveSetMember(latestEntitySetKey, instanceKey, db);
                        RedisManager.AddSetWithExpiry(latestEntitySetKey, instanceKey, db);
                        break;
                    case "DELETE":
                        RedisManager.RemoveSetMember(latestEntitySetKey, instanceKey, db);
                        break;
                    default:
                        break;
                }
            }
            RedisManager.CopySetAndStore(newEntitySetKey, latestEntitySetKey, db);
            RedisManager.AddSortedSet(entityTimelineSetKey, newEntitySetKey, db);
        }
    }
}
