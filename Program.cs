using SyncRoutineWS.OCPCModel;
using SyncRoutineWS.PCNWModel;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
                    services.AddDbContext<OCPCProjectDBContext>(options =>
                    options.UseSqlServer(hostContext.Configuration.GetConnectionString("OCPCDefaultConnection")),ServiceLifetime.Singleton);

                    services.AddDbContext<PCNWProjectDBContext>(options =>
                    options.UseSqlServer(hostContext.Configuration.GetConnectionString("PCNWDefaultConnection")), ServiceLifetime.Singleton);

                    services.AddHostedService<Worker>();
                })
                .UseWindowsService(); 
    }
}