using MySqlConnector;
using Newtonsoft.Json;
using Sample.Models.DAO;
using StackExchange.Redis;

namespace Sample.Common
{
    public class RedisManager
    {
        private static readonly NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

        private const string lastUDIDName = "udid:";
        public static ConnectionMultiplexer? redisClient;

        public IDatabase? GetRedis(int db)
        {
            if (redisClient == null || redisClient.IsConnected == false)
            {
                log.Error("redis 연결이 끊어졌습니다.");
                return null;
            }

            return redisClient.GetDatabase(db);
        }

        public async Task<bool> StringSetAsync<T>(int db, string key, T value, double ttlTime)
        {
            bool result = false;
            try
            {
                string data = JsonConvert.SerializeObject(value);
                IDatabase? redis = GetRedis(db);
                if (redis == null)
                    return false;
                result = await redis.StringSetAsync(key, data).ConfigureAwait(false);
                if (result == false)
                {
                    log.Debug($"RedisManager.StringSetAsync | StringSetAsync is failed | {key}");
                    return false;
                }

                if (ttlTime > 0d)
                {
                    result = await redis.KeyExpireAsync(key, TimeSpan.FromSeconds(ttlTime)).ConfigureAwait(false);
                    if (result == false)
                    {
                        log.Debug($"RedisManager.StringSetAsync | ttl is failed | {key}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.ToSimpleString());
                return false;
            }

            return result;
        }

        public async Task<T?> StringGetAsync<T>(int db, string key, double ttlTime)
        {
            IDatabase? redis = GetRedis(db);
            if (redis == null)
                return default(T?);
            string? value = await redis.StringGetAsync(key).ConfigureAwait(false);
            if (value == null)
                return default(T?);

            T? data;

            try
            {
                data = JsonConvert.DeserializeObject<T>(value);
            }
            catch (Exception ex)
            {
                log.Error(ex.ToSimpleString());
                return default(T?);
            }

            bool result = await redis.KeyExpireAsync(key, TimeSpan.FromSeconds(ttlTime)).ConfigureAwait(false);
            if (result == false)
            {
                log.Debug($"RedisManager.StringGetAsync | ttl is failed | {key}");
                return default(T?);
            }

            return data;
        }

        public async Task<bool> SetAddAsync(int db, string key, string value)
        {
            IDatabase? redis = GetRedis(db);
            if (redis == null)
                return false;
            bool result = await redis.SetAddAsync(key, value).ConfigureAwait(false);
            return result;
        }

        public async Task<bool> SortedSetAddAsync(int db, string key, string value, double score)
        {
            IDatabase? redis = GetRedis(db);
            if (redis == null)
                return false;
            bool result = await redis.SortedSetAddAsync(key, value, score).ConfigureAwait(false);
            return result;
        }

        public async Task<(bool, double?)> SortedSetScoreAsync(int db, string key, string value)
        {
            IDatabase? redis = GetRedis(db);
            if (redis == null)
                return (false, 0d);
            double? currentScore = await redis.SortedSetScoreAsync(key, value).ConfigureAwait(false);
            return (false, currentScore);
        }

        //public async Task<SortedSetEntry[]?> SortedSetRangeByRankWithScoresAsync(string key, long minimum, long maximum, StackExchange.Redis.Order order)
        //{
        //    SortedSetEntry[] entries = await redis.SortedSetRangeByRankWithScoresAsync(key, minimum, maximum - 1, order).ConfigureAwait(CommonDefine.CaptureContext);
        //    if (entries == null)
        //        return null;
        //    else
        //    {
        //        List rvTuples = new List<(int rank, int uid, long score)>(len);
        //        return entries;
        //    }
        //}

        public async Task<bool> HashSetAsync<T1, T2>(int db, string key, Dictionary<T1, T2> directory) where T1 : notnull
        {
            try
            {
                HashEntry[] Entries = new HashEntry[directory.Count];
                int i = 0;
                foreach (var Item in directory)
                {
                    string jsonData = JsonConvert.SerializeObject(Item.Value);
                    Entries[i++] = new HashEntry(Item.Key.ToString(), jsonData);
                }

                IDatabase? redis = GetRedis(db);
                if (redis == null)
                    return false;
                await redis.HashSetAsync(key, Entries).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.Error(ex.ToSimpleString());
                return false;
            }

            return true;
        }

        public async Task<T?> HashGetAsync<T>(int db, string key, string hashField)
        {
            IDatabase? redis = GetRedis(db);
            if (redis == null)
                return default(T);
            string? value = await redis.HashGetAsync(key, hashField).ConfigureAwait(false);
            if (value == null)
            {
                log.Debug("RedisManager.HashGetAsync | HashGetAsync() value is null");
                return default(T);
            }

            T? data;
            try
            {
                data = JsonConvert.DeserializeObject<T>(value);
            }
            catch (Exception ex)
            {
                log.Error(ex.ToSimpleString());
                return default(T);
            }

            return data;
        }

        public async Task<Dictionary<string, string>?> HashGetAllAsync(int db, string key)
        {
            IDatabase? redis = GetRedis(db);
            if (redis == null)
                return null;
            HashEntry[] Entries = await redis.HashGetAllAsync(key).ConfigureAwait(false);
            if (Entries == null)
            {
                log.Debug("RedisManager.HashGetAllAsync | HashGetAllAsync() Entries is null");
                return null;
            }

            Dictionary<string, string> dict = new Dictionary<string, string>();
            foreach (var Item in Entries)
            {
                dict.Add(Item.Name, Item.Value);
            }

            return dict;
        }

        public async Task<bool> KeyDeleteAsync<T>(int db, string key)
        {
            IDatabase? redis = GetRedis(db);
            if (redis == null)
                return false;
            bool result = await redis.KeyExistsAsync(key).ConfigureAwait(false);
            if (result == false)
                return true;

            result = await redis.KeyDeleteAsync(key).ConfigureAwait(false);
            return result;
        }

        // 사용자정의 함수
        public async Task<bool> SetSession(MySqlConnection? shardConnection, int db, string lastUDID, Session session)
        {
            // 세션 저장
            if (shardConnection != null)
            {
                string query = $"UPDATE tbl_user SET state = {session.State}, stateValue = {session.StateValue} WHERE userIdx = {session.Uid};";
                using (MySqlCommand cmd = new MySqlCommand(query, shardConnection))
                {
                    int affectedRows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    if (affectedRows <= 0)
                        log.Debug("RedisManager.SetSession | affectedRows: " + affectedRows.ToString());
                }
            }

            string totalUDID = lastUDIDName + lastUDID;
            bool result = await StringSetAsync<Session>(db, totalUDID, session, 10).ConfigureAwait(false);
            if (result == false)
            {
                log.Debug($"RedisManager.SetSession | totalUDID is failed | {totalUDID}");
                return false;
            }

            return true;
        }

        public async Task<Session?> GetSession(int db, string lastUDID, double ttlTime)
        {
            string totalUDID = lastUDIDName + lastUDID;
            Session? session = await StringGetAsync<Session>(db, totalUDID, ttlTime).ConfigureAwait(false);
            if (session == null)
            {
                log.Debug($"RedisManager.GetSession | session is null | {totalUDID}");
                return null;
            }

            return session;
        }
    }
}

// ttl 알아내기
//TimeSpan? ttl = await RedisManager.Instance.GetRedis.KeyTimeToLiveAsync(lastUDID).ConfigureAwait(CommonDefine.CaptureContext);
//if (ttl == null)
//{
//    new NLogManager().ErrorLog("UserController.Login | ttl is null");
//    return Ok("Error");
//}
