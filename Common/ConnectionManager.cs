using MySqlConnector;
using Sample.Models.DAO;

namespace Sample.Common
{
    public class ConnectionManager : IAsyncDisposable
    {
        private static readonly NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

        private bool disposed = false;
        private const int MAX_SHARD_COUNT = 2;

        private MySqlConnection? masterDB;
        private MySqlConnection[]? shardDB;
        private RedisManager redisManager;

        public RedisManager GetRedis
        {
            get
            {
                return redisManager;
            }
        }

        public ConnectionManager()
        {
            shardDB = new MySqlConnection[MAX_SHARD_COUNT];
            redisManager = new RedisManager();
        }

        ~ConnectionManager()
        {
            Dispose(false);
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsync(true);
            GC.SuppressFinalize(this);
        }

        protected virtual async Task DisposeAsync(bool disposing)
        {
            if (!disposed)
            {
                // 관리 리소스 해제
                //if (disposing)
                //{
                //}

                // 비관리 리소스 해제
                await CloseAll();

                disposed = true;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                // 관리 리소스 해제
                //if (disposing)
                //{
                //}

                // 비관리 리소스 해제
                masterDB?.Dispose();
                masterDB = null;

                if (shardDB != null)
                {
                    for (int i = 0; i < shardDB.Length; i++)
                    {
                        shardDB[i]?.Dispose();
                        shardDB[i] = null;
                    }

                    shardDB = null;
                }

                disposed = true;
            }
        }

        public async Task CloseAll()
        {
            if (masterDB != null)
            {
                await Close(masterDB);
                masterDB = null;
            }

            if (shardDB != null)
            {
                for (int i = 0; i < shardDB.Length; i++)
                {
                    if (shardDB[i] != null)
                        await Close(shardDB[i]);

                    shardDB[i] = null;
                }

                shardDB = null;
            }
        }

        public async Task Close(MySqlConnection? connection)
        {
            if (connection != null)
                await Clear(connection);
        }

        private async Task Clear(MySqlConnection connection)
        {
            if (connection == null)
                return;

            await connection.DisposeAsync().ConfigureAwait(false);
        }

        public async Task<MySqlConnection> DB_Master()
        {
            if (masterDB == null)
                masterDB = new MySqlConnection("임시");

            if (masterDB.State == System.Data.ConnectionState.Closed)
            {
                try
                {
                    await masterDB.OpenAsync().ConfigureAwait(false);
                }
                catch
                {
                    throw;
                }
            }

            return masterDB;
        }

        public async Task<MySqlConnection> DB_Shard(sbyte shardIdx)
        {
            if (shardIdx < 0 || shardIdx >= shardDB?.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(shardIdx));
            }

            if (shardDB[shardIdx] == null)
                shardDB[shardIdx] = new MySqlConnection("임시");

            if (shardDB[shardIdx].State == System.Data.ConnectionState.Closed)
            {
                try
                {
                    await shardDB[shardIdx].OpenAsync().ConfigureAwait(false);
                }
                catch
                {
                    throw;
                }
            }

            return shardDB[shardIdx];
        }

        public async Task<(int, Session?)> TryGetSession(long userIdx, int sessionKey)
        {
            Session? session;

            try
            {
                session = await this.GetRedis.GetSession(CommonDefine.RedisDatabase, userIdx.ToString(), CommonDefine.SESSION_TIME_OUT);
                if (session == null) // 장애로 redis에 없으면 mysql에서 가져온다
                {
                    string masterQuery = $"SELECT shardIdx, lastSessionKey FROM tbl_account WHERE userIdx = {userIdx};";

                    MySqlConnection masterConnection = await DB_Master();

                    using (MySqlCommand cmd = new MySqlCommand(masterQuery, masterConnection))
                    {
                        using (MySqlDataReader reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            if (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                int lastSessionKey = reader.GetSByte("lastSessionKey");
                                if (lastSessionKey != sessionKey)
                                {
                                    return (RC.UNKNOWN, null);
                                }

                                sbyte shardIdx = reader.GetSByte("shardIdx");

                                bool sessionResult = await this.GetRedis.SetSession(null, CommonDefine.RedisDatabase, userIdx.ToString(), new Session { Uid = userIdx, SessionKey = sessionKey, ShardIndex = shardIdx, State = 0, StateValue = 0 });
                                if (sessionResult == false)
                                {
                                    log.Error("ConnectionManager.TrySetSession | " + RC.REDISL_EXCEPTION.ToString());
                                    return (RC.REDISL_EXCEPTION, null);
                                }
                            }
                            else
                            {
                                return (RC.MYSQL_SQL_EXCEPTION, null);
                            }
                        }
                    }

                    session = await GetRedis.GetSession(CommonDefine.RedisDatabase, userIdx.ToString(), CommonDefine.SESSION_TIME_OUT);
                    if (session == null)
                        return (RC.REDISL_EXCEPTION, null);
                }
                else
                {
                    if (session.SessionKey != sessionKey)
                    {
                        return (RC.UNKNOWN, null);
                    }
                }
            }
            catch (MySqlException ex)
            {
                log.Error(ex.ToSimpleString());
                return (RC.MYSQL_SQL_EXCEPTION, null);
            }
            catch (Exception ex)
            {
                log.Error(ex.ToSimpleString());
                return (RC.UNKNOWN, null);
            }

            return (RC.SUCCESS, session);
        }
    }
}
