using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Sample.Common;
using Sample.Models.DAO;
using Sample.Models.DTO;
using Sample.Repository.Interfaces;
using Sample.Services.Interfaces;

namespace Sample.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AccountController
    {
        private readonly IDb db;
        private readonly IAuthService authService;
        private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private static readonly NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

        public AccountController(IDb db, IAuthService authService)            
        {
            this.db = db;
            this.authService = authService;
        }

        [HttpPost("Login")]
        public async Task<ActionResult<LoginRes>> Login([FromBody] LoginReq req)
        {
            LoginRes? res = null;

            try
            {
                await using ConnectionManager conMgr = new ConnectionManager();

                res = await authService.AuthLogin(conMgr, log, semaphore, req);
                if (res.RetCode != RC.SUCCESS)
                {
                    res.RetCode = res.RetCode;
                    return res;
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.ToSimpleString());
                if (res == null)
                    res = new LoginRes();
                
                res.RetCode = RC.UNKNOWN;
                return res;
            }
            
            return res;           
        }
    }
}
