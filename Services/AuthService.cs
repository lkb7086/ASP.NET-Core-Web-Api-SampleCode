using Microsoft.Extensions.Logging;
using MySqlConnector;
using Sample.Common;
using Sample.Models.DAO;
using Sample.Models.DTO;
using Sample.Repository.Interfaces;
using Sample.Services.Interfaces;
using System.Threading;

namespace Sample.Services
{
    public class AuthService : IAuthService
    {
        private readonly IDb db;

        public AuthService(IDb db)
        {
            this.db = db;
        }

        public async Task<LoginRes> AuthLogin(ConnectionManager conMgr, NLog.Logger log, SemaphoreSlim semaphore, LoginReq req)
        {
            LoginRes res = new LoginRes();

            try
            {
                int sessionKey = req.SessionKey;
                long uid = req.Uid;
                string? hash = req.Hash ?? throw new NullReferenceException();

                // 로그인후 redis에서 session정보 가져오기
                //(res.RetCode, Session? session) = await conMgr.TryGetSession(uid, sessionKey);
                //if (res.RetCode != RC.SUCCESS || session == null)
                //    return res;

                // 임시
                Session session = new Session();
                MySqlConnection shardConnection = await conMgr.DB_Shard(session.ShardIndex);

                string readQuery = "SELECT A.nickName, B.score FROM tbl_user AS A INNER JOIN tbl_pvp AS B ON A.uid = B.uid WHERE A.uid = @uid;";

                using (MySqlCommand command = new MySqlCommand(readQuery, shardConnection))
                {
                    command.Parameters.Add("@uid", MySqlDbType.Int64).Value = uid;

                    using (MySqlDataReader reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            res.NickName = reader.GetString("nickName");
                            res.Score = reader.GetInt64("score");
                        }
                        else
                        {
                            res.RetCode = RC.MYSQL_SQL_EXCEPTION;
                            return res;
                        }
                    }
                }

                string writeQuery = "UPDATE tbl_user SET state = @state WHERE uid = @uid;";

                using (MySqlCommand command = new MySqlCommand(writeQuery, shardConnection))
                {
                    command.Parameters.Add("@state", MySqlDbType.Byte).Value = session.State;
                    command.Parameters.Add("@uid", MySqlDbType.Int64).Value = uid;

                    int affectedRows = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    if (affectedRows == 0)
                    {
                        res.RetCode = RC.MYSQL_SQL_EXCEPTION;
                        return res;
                    }
                }

                // redis에 세션정보 초기화
                bool sessionResult = await conMgr.GetRedis.SetSession(shardConnection, CommonDefine.RedisDatabase, uid.ToString(), new Session { ShardIndex = session.ShardIndex, SessionKey = sessionKey, Uid = uid, State = 0, StateValue = 0 });
                if (sessionResult == false)
                {
                    log.Error("임시");
                    res.RetCode = RC.REDISL_EXCEPTION;
                    return res;
                }

                // 만약 락이 필요할경우 이렇게 사용합니다
                try
                {
                    await semaphore.WaitAsync();
                    // 내용
                }
                catch (Exception ex)
                {
                    log.Error(ex.ToSimpleString());
                    res.RetCode = RC.UNKNOWN;
                    return res;
                }
                finally
                {
                    semaphore.Release();
                }
            }
            catch (MySqlException ex)
            {
                log.Error(ex.ToSimpleString());
                res.RetCode = RC.MYSQL_SQL_EXCEPTION;
                return res;
            }
            catch (Exception ex)
            {
                log.Error(ex.ToSimpleString());
                res.RetCode = RC.UNKNOWN;
                return res;
            }

            return res;
        }
    }
}
