using Newtonsoft.Json.Linq;
using Sample.Common;
using StackExchange.Redis;
using System.Text;

namespace Sample.Middleware
{
    // 요청을 처리하기 시작할때 락을 걸고 완료되면 락을 푸는 미들웨어
    public class AuthMiddleware
    {
        private static readonly NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();
        private readonly RequestDelegate next;
        private static readonly string[] ignorePages = { "Login" };
        private const double timeOutSeconds = 10.0d;

        public AuthMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            IDatabase? redis = null;
            string lockKey = string.Empty;
            long uid = -1;

            try
            {
                CancellationTokenSource cancellationToken = new CancellationTokenSource();
                cancellationToken.CancelAfter(TimeSpan.FromSeconds(timeOutSeconds));  // 웹요청에 타임아웃 설정
                context.RequestAborted = cancellationToken.Token;  // 요청이 취소되면 토큰에 의해 타임아웃 발생

                // 특정 요청은 처리하지 않음
                //if (string.Compare(context.Request.Path.Value, ignorePage1, StringComparison.OrdinalIgnoreCase) == 0 || string.Compare(context.Request.Path.Value, ignorePage2, StringComparison.OrdinalIgnoreCase) == 0)
                if (ignorePages.Any(page => string.Equals(context.Request.Path.Value, page, StringComparison.OrdinalIgnoreCase)))
                {
                    await next(context).ConfigureAwait(false);

                    // CancellationToken이 요청 취소 상태인지 확인
                    if (cancellationToken.IsCancellationRequested)
                    {
                        // 취소 요청이 발생하면 예외를 던져서 작업을 중단함
                        throw new TaskCanceledException();
                    }

                    return;
                }

                // 요청 본문을 스트림으로 읽기 위해 활성화
                context.Request.EnableBuffering();
                // 요청 본문을 스트림에서 읽기
                using (StreamReader reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
                {
                    string reqBody = await reader.ReadToEndAsync().ConfigureAwait(false);
                    JObject resJObject = JObject.Parse(reqBody?.ToString() ?? throw new NullReferenceException(nameof(reqBody)));
                    uid = long.Parse(resJObject["uid"]?.ToString() ?? throw new NullReferenceException(nameof(uid)));

                    context.Items["resJObject"] = resJObject;

                    // 스트림 위치를 0으로 다시 설정 (다음 미들웨어나 컨트롤러에서 읽을 수 있도록)
                    context.Request.Body.Position = 0;
                }

                if (uid == -1)
                {
                    log.Debug("AuthMiddleware | userIdx is -1");
                    return;
                }

                redis = RedisManager.redisClient?.GetDatabase(CommonDefine.RedisDatabase) ?? throw new NullReferenceException("AuthMiddleware | RedisManager.redisClient is null");
                if (redis == null)
                {
                    log.Error("AuthMiddleware | redis is null 1");
                    return;
                }

                lockKey = "Lock:" + uid;
                TimeSpan lockTimeout = TimeSpan.FromSeconds(timeOutSeconds);

                // 락 획득
                if (await redis.StringSetAsync(lockKey, "locked", lockTimeout, When.NotExists).ConfigureAwait(false) == false)
                {
                    log.Debug("AuthMiddleware | aleady locked redis");
                    return;
                }

                await next(context).ConfigureAwait(false);

                // CancellationToken이 요청 취소 상태인지 확인
                if (cancellationToken.IsCancellationRequested)
                {
                    // 취소 요청이 발생하면 예외를 던져서 작업을 중단함
                    throw new TaskCanceledException();
                }
            }
            catch (TaskCanceledException ex)
            {
                log.Error(ex.ToSimpleString());
                context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
                return;
            }
            catch (Exception ex)
            {
                log.Error(ex.ToSimpleString());
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            finally
            {
                // 락 해제
                if (redis != null)
                {
                    if (lockKey.Equals(string.Empty) == false)
                    {
                        bool isDeleted = await redis.KeyDeleteAsync(lockKey).ConfigureAwait(false);
                        if (isDeleted == false)
                            log.Debug("AuthMiddleware | not found redis key");
                    }
                    else
                        log.Debug("AuthMiddleware | invalid redis key");
                }
            }
        }
    }
}
