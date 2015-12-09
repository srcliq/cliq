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

namespace TopologyReader.Helpers
{
    public static class Common
    {
        private static int redisTTL = 5;
        private static ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(ConfigurationManager.AppSettings["RedisEndPoint"]);
        private static IDatabase redisDb = null;

        internal static IDatabase GetRedisDatabase(){
            if (redisDb == null)
            {
                redisDb = redis.GetDatabase();
            }
            return redisDb;
        }

        internal static void SetRedisTTL(int ttl)
        {
            redisTTL = ttl;
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
            var dateString = date.ToString("MMddyyyHHmmss");
            var dataKey = string.Format("{0}-{1}-{2}", dateString, accountNumber, regionName);
            return dataKey;
        }

        public static string GetDataKey(string dateString, string accountNumber, string regionName)
        {            
            var dataKey = string.Format("{0}-{1}-{2}", dateString, accountNumber, regionName);
            return dataKey;
        }

        internal static void AddToRedisWithExpiry(string key, string value, IDatabase db)
        {
            db.StringSet(key, value);
            db.KeyExpire(key, new TimeSpan(redisTTL, 0, 0, 0));
        }

        internal static void AddSetToRedisWithExpiry(string key, string value, IDatabase db)
        {
            db.SetAdd(key, value);
            db.KeyExpire(key, new TimeSpan(redisTTL, 0, 0, 0));
        }

        internal static RedisValue[] GetSet(string key, IDatabase db)
        {
            return db.SetMembers(key);
        }

        internal static bool RemoveSetMember(string key, string value, IDatabase db)
        {
            var members = db.SetMembers(key);
            foreach (var member in members)
            {
                if (member.ToString().Contains(value))
                {
                    return db.SetRemove(key, member);
                }
            }
            return false;
        }

        internal static void CopySetAndStore(RedisKey destinationSetKey, RedisKey sourceSetKey, IDatabase db)
        {
            db.SetCombineAndStore(SetOperation.Union, destinationSetKey, new RedisKey[]{sourceSetKey});
        }
    }
}
