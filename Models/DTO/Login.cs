using Sample.Common;

namespace Sample.Models.DTO
{
    // 클라이언트와 데이터를 주고 받기 위한 객체
    public class LoginReq
    {
        public int SessionKey { get; set; }
        public long Uid { get; set; }
        public string? Hash { get; set; }
    }

    public class LoginRes
    {
        public int RetCode { get; set; } = RC.SUCCESS;
        public string? NickName { get; set; }
        public long Score { get; set; }
    }
}
