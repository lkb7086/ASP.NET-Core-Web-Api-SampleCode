using Sample.Common;
using System.Text;

namespace Sample.Common
{
    internal class RankServerTool
    {
        private static readonly NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

        public async Task<(int, string?)> WebRequestRankServer(string jsonString, string url)
        {
            //ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            //string jsonString = JsonConvert.SerializeObject(req);
            string responseText;
            string retUrl = "임시";

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(jsonString);
                double httpTimeout = CommonDefine.IsDevelopment ? 10000.0d : 10.0d;

                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(httpTimeout);
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, retUrl)
                    {
                        Content = new ByteArrayContent(bytes)
                    };

                    request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                    // 요청 보내기
                    HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);

                    // 응답이 성공적이면 (상태 코드가 200-299) 메서드는 아무런 동작도 하지 않고 계속 진행됩니다.
                    // 응답이 실패했으면 (상태 코드가 400 또는 500 범위 등) HttpRequestException 예외를 던집니다.
                    response.EnsureSuccessStatusCode();

                    // 응답 읽기
                    responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
            catch (HttpRequestException httpEx)
            {
                // HTTP 요청 실패 예외 처리
                log.Error(httpEx.ToSimpleString());
                return (RC.UNKNOWN, null);
            }
            catch (Exception ex)
            {
                log.Error(ex.ToSimpleString());
                return (RC.UNKNOWN, null);
            }

            return (RC.SUCCESS, responseText);
        }
    }
}
