using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace TopologyReader.Helpers
{
    public static class RedisManager
    {
        private static int redisTTL = 5;
        private static readonly ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(ConfigurationManager.AppSettings["RedisEndPoint"]);
        private static IDatabase redisDb = null;

        internal static IDatabase GetRedisDatabase()
        {
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

        internal static void AddWithExpiry(string key, string value, IDatabase db)
        {
            db.StringSet(key, value);
            db.KeyExpire(key, new TimeSpan(redisTTL, 0, 0, 0));
        }

        internal static void AddSetWithExpiry(string key, string value, IDatabase db)
        {
            db.SetAdd(key, value);
            db.KeyExpire(key, new TimeSpan(redisTTL, 0, 0, 0));
        }

        internal static void AddSet(string key, string value, IDatabase db)
        {
            db.SetAdd(key, value);
        }

        internal static void AddSortedSet(string key, string value, IDatabase db)
        {
            db.SortedSetAdd(key, value, 0);
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
                if (value != null && member.ToString().Contains(value))
                {
                    return db.SetRemove(key, member);
                }
            }
            return false;
        }

        internal static RedisValue? GetSetMember(string key, string value, IDatabase db)
        {
            var members = db.SetMembers(key);
            foreach (var member in members)
            {
                if (member.ToString().Contains(value))
                {
                    return member;
                }
            }
            return null;
        }

        internal static void CopySetAndStore(RedisKey destinationSetKey, RedisKey sourceSetKey, IDatabase db)
        {
            db.SetCombineAndStore(SetOperation.Union, destinationSetKey, new RedisKey[] { sourceSetKey });
        }
    }
}
