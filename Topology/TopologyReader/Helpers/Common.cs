using Amazon;
using Amazon.IdentityManagement;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TopologyReader.Data;

namespace TopologyReader.Helpers
{
    public static class Common
    {
        

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
            return string.Empty; //"arn:aws:iam::990008671661:user/xyz"
        }

        public static string GetDataKey(string accountNumber, RegionEndpoint regionEndPoint)
        {
            var dataKey = GetDataKey(accountNumber, regionEndPoint.SystemName);
            return dataKey;
        }

        public static string GetDataKey(string accountNumber, string regionName)
        {
            return GetDataKey(DateTime.UtcNow, accountNumber, regionName);
        }

        public static string GetDataKey(DateTime date, string accountNumber, string regionName)
        {
            var dateString = date.ToString("yyyyMMddHHmmss");
            var dataKey = string.Format("{0}-{1}-{2}", dateString, accountNumber, regionName);
            return dataKey;
        }

        public static string GetDataKey(string dateString, string accountNumber, string regionName)
        {            
            var dataKey = string.Format("{0}-{1}-{2}", dateString, accountNumber, regionName);
            return dataKey;
        }

        internal static void UpdateTopology(ConfigurationItem configuration, string entityIdentifier, string entityId, string entityJson, string changeType)
        {
            var captureTimeString = configuration.ConfigurationItemCaptureTime;
            DateTime captureTime;
            if (!DateTime.TryParse(captureTimeString, out captureTime))
            {
                captureTime = DateTime.UtcNow;
            }
            UpdateTopology(captureTime, configuration.AWSAccountId, configuration.AWSRegion, entityIdentifier, entityId, entityJson, changeType);
            //var latestDataKey = Common.GetDataKey("latest", configuration.AWSAccountId, configuration.AWSRegion);
            //var newDataKey = Common.GetDataKey(captureTime, configuration.AWSAccountId, configuration.AWSRegion);

            //var latestEntitySetKey = string.Format("{0}-{1}set", latestDataKey, entityIdentifier);
            //var newEntitySetKey = string.Format("{0}-{1}set", newDataKey, entityIdentifier);

            //var entityTimelineSetKey = string.Format("timeline-{0}-{1}-{2}", configuration.AWSAccountId, configuration.AWSRegion, entityIdentifier);

            //var dataKey = Common.GetDataKey(configuration.AWSAccountId, configuration.AWSRegion);
            //var entityKey = string.Format("{0}-{1}-{2}", dataKey, entityIdentifier, entityId);
            //var db = RedisManager.GetRedisDatabase();
            //RedisManager.AddWithExpiry(entityKey, entityJson, db);
            //if (RedisManager.GetSet(latestEntitySetKey, db).Length > 0)
            //{
            //    switch (changeType)
            //    {
            //        case "CREATE":
            //            RedisManager.AddSetWithExpiry(latestEntitySetKey, entityKey, db);
            //            break;
            //        case "UPDATE":
            //            RedisManager.RemoveSetMember(latestEntitySetKey, entityKey, db);
            //            RedisManager.AddSetWithExpiry(latestEntitySetKey, entityKey, db);
            //            break;
            //        case "DELETE":
            //            RedisManager.RemoveSetMember(latestEntitySetKey, entityKey, db);
            //            break;
            //        default:
            //            break;
            //    }
            //}
            //RedisManager.CopySetAndStore(newEntitySetKey, latestEntitySetKey, db);
            //RedisManager.AddSortedSet(entityTimelineSetKey, newEntitySetKey, db);
        }

        internal static void UpdateTopology(DateTime captureTime, string accountId, string region, string entityIdentifier, string entityId, string entityJson, string changeType)
        {
            var latestDataKey = Common.GetDataKey("latest", accountId, region);
            var newDataKey = Common.GetDataKey(captureTime, accountId, region);

            var latestEntitySetKey = string.Format("{0}-{1}set", latestDataKey, entityIdentifier);
            var newEntitySetKey = string.Format("{0}-{1}set", newDataKey, entityIdentifier);

            var entityTimelineSetKey = string.Format("timeline-{0}-{1}-{2}", accountId, region, entityIdentifier);

            //var dataKey = Common.GetDataKey(accountId, region);
            var entityKey = string.Format("{0}-{1}-{2}", newDataKey, entityIdentifier, entityId);
            var db = RedisManager.GetRedisDatabase();
            RedisManager.AddWithExpiry(entityKey, entityJson, db);
            //if (RedisManager.GetSet(latestEntitySetKey, db).Length > 0)
            {
                if (string.IsNullOrEmpty(changeType))
                {
                    RedisManager.AddSetWithExpiry(latestEntitySetKey, entityKey, db);
                }
                else
                {
                    switch (changeType)
                    {
                        case "CREATE":
                            RedisManager.AddSetWithExpiry(latestEntitySetKey, entityKey, db);
                            break;
                        case "UPDATE":
                            RedisManager.RemoveSetMember(latestEntitySetKey, entityKey, db);
                            RedisManager.AddSetWithExpiry(latestEntitySetKey, entityKey, db);
                            break;
                        case "DELETE":
                            RedisManager.RemoveSetMember(latestEntitySetKey, entityKey, db);
                            break;
                    }
                }                
            }
            RedisManager.CopySetAndStore(newEntitySetKey, latestEntitySetKey, db);
            RedisManager.AddSortedSet(entityTimelineSetKey, newEntitySetKey, db);
        }

        internal static void UpdateTopologySet(DateTime captureTime, string accountId, string region, string entityIdentifier, string entityId, string memberKey, string changeType)
        {
            var latestDataKey = Common.GetDataKey("latest", accountId, region);
            var newDataKey = Common.GetDataKey(captureTime, accountId, region);

            var latestEntitySetKey = string.Format("{0}-{1}set", latestDataKey, entityIdentifier);
            var newEntitySetKey = string.Format("{0}-{1}set", newDataKey, entityIdentifier);

            var entityTimelineSetKey = string.Format("timeline-{0}-{1}-{2}", accountId, region, entityIdentifier);

            //var dataKey = Common.GetDataKey(accountId, region);
            var entityKey = string.Format("{0}-{1}-{2}", newDataKey, entityIdentifier, entityId);
            var db = RedisManager.GetRedisDatabase();
            RedisManager.AddSetWithExpiry(entityKey, memberKey, db);
            //if (RedisManager.GetSet(latestEntitySetKey, db).Length > 0)
            {
                if (string.IsNullOrEmpty(changeType))
                {
                    RedisManager.AddSetWithExpiry(latestEntitySetKey, entityKey, db);
                }
                else
                {
                    switch (changeType)
                    {
                        case "CREATE":
                            RedisManager.AddSetWithExpiry(latestEntitySetKey, entityKey, db);
                            break;
                        case "UPDATE":
                            RedisManager.RemoveSetMember(latestEntitySetKey, entityKey, db);
                            RedisManager.AddSetWithExpiry(latestEntitySetKey, entityKey, db);
                            break;
                        case "DELETE":
                            RedisManager.RemoveSetMember(latestEntitySetKey, entityKey, db);
                            break;
                    }
                }
            }
            RedisManager.CopySetAndStore(newEntitySetKey, latestEntitySetKey, db);
            RedisManager.AddSortedSet(entityTimelineSetKey, newEntitySetKey, db);
        }

    }
}
