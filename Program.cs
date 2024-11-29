using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using SyncRoutineWS.Controllers;
using SyncRoutineWS.OCPCModel;
using SyncRoutineWS.PCNWModel;

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
            .UseSerilog((context, services, configuration) => configuration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .WriteTo.Console()
                .WriteTo.File("G:\\MyLogs\\SyncRoutineLogs\\log.txt", rollingInterval: RollingInterval.Day)
                .Filter.ByExcluding(Matching.FromSource("Microsoft.EntityFrameworkCore"))
            )

             .ConfigureAppConfiguration((context, config) =>
             {
                 var env = context.HostingEnvironment;

                 config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                       .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                       .AddEnvironmentVariables();
             })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddTransient<SyncController>();
                    services.AddHostedService<WorkerService>();
                    services.AddDbContext<OCPCProjectDBContext>(options =>
                    options.UseSqlServer(hostContext.Configuration.GetConnectionString("OCPCDefaultConnection"), options => options.CommandTimeout(180)), ServiceLifetime.Singleton);

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