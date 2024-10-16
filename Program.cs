using SyncRoutineWS.OCPCModel;
using SyncRoutineWS.PCNWModel;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Identity;

namespace SyncRoutineWS
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
             .ConfigureAppConfiguration((hostingContext, config) =>
             {
                 config.SetBasePath(AppContext.BaseDirectory)
                 .AddJsonFile($"appsettings.json", optional: true, reloadOnChange: true);
             })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();
                    services.AddDbContext<OCPCProjectDBContext>(options =>
                    options.UseSqlServer(hostContext.Configuration.GetConnectionString("OCPCDefaultConnection"), options => options.CommandTimeout(180)),ServiceLifetime.Singleton);


                    services.AddDbContext<PCNWProjectDBContext>(options =>
                    options.UseSqlServer(hostContext.Configuration.GetConnectionString("PCNWDefaultConnection"), options => options.CommandTimeout(180)), ServiceLifetime.Singleton);

                    services.AddIdentity<IdentityUser, IdentityRole>()
                    .AddDefaultTokenProviders()
                    .AddEntityFrameworkStores<PCNWProjectDBContext>();
                    services.Configure<IdentityOptions>(options =>
                    {
                        options.Password.RequireDigit = false; 
                        options.Password.RequireLowercase = false; 
                        options.Password.RequireUppercase = false; 
                        options.Password.RequireNonAlphanumeric = false; 
                        options.Password.RequiredLength = 1; 
                    });
                })
                .UseWindowsService(); 
    }
}