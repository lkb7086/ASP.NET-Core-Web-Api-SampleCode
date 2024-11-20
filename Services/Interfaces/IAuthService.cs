using Sample.Common;
using Sample.Models.DTO;
using Sample.Repository.Interfaces;

namespace Sample.Services.Interfaces
{
    public interface IAuthService
    {
        public Task<LoginRes> AuthLogin(ConnectionManager conMgr, NLog.Logger log, SemaphoreSlim semaphore, LoginReq req);
    }
}
