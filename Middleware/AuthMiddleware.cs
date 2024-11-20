using Newtonsoft.Json.Linq;
using Sample.Common;
using StackExchange.Redis;
using System.Text;
using System.Threading;

namespace Sample.Middleware
{
    public class AuthMiddleware
    {
        private static readonly NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();
        private readonly RequestDelegate next;
        private readonly TimeSpan timeout = TimeSpan.FromSeconds(10);

        public AuthMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                using (var cts = new CancellationTokenSource(timeout))
                {
                    Task timeoutTask = Task.Delay(timeout, cts.Token);
                    Task requestTask = next(context);  // 요청 처리

                    // 요청 처리와 타임아웃 작업을 동시에 실행하여 먼저 완료된 작업 기준으로 응답
                    Task completedTask = await Task.WhenAny(requestTask, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        // 타임아웃 발생 시 상태 코드를 504로 설정하고 응답 본문 작성
                        context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
                        await context.Response.WriteAsync("Error: Gateway Timeout");
                    }
                    else
                    {
                        // 정상적으로 응답이 완료된 경우, 타임아웃 취소
                        cts.Cancel();
                        await requestTask;  // 실제 응답 반환
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                log.Error(ex.ToSimpleString());
                context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
                return;
            }
            catch (Exception ex)
            {
                log.Error(ex.ToSimpleString());
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
        }
    }
}
