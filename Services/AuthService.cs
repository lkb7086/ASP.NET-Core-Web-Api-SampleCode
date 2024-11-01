using Microsoft.Extensions.Logging;
using Sample.Repository.Interfaces;
using Sample.Services.Interfaces;

namespace Sample.Services
{
    public class AuthService : IAuthService
    {
        private readonly IDb db;

        public AuthService(IDb db)
        {
            this.db = db;
        }
    }
}
