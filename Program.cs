using Sample.Common;
using Sample.Repository;
using Sample.Repository.Interfaces;
using Sample.Services;
using Sample.Services.Interfaces;
using System;

namespace Sample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddSingleton<IDb, Db>();
            builder.Services.AddTransient<IAuthService, AuthService>();

            builder.Services.AddControllers();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            app.UseMiddleware<Middleware.AuthMiddleware>();

            app.UseAuthorization();
            app.MapControllers();

            CommonDefine.Configuration = app.Configuration;
            CommonDefine.IsDevelopment = app.Environment.IsDevelopment();

            app.Run();
        }
    }
}
