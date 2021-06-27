using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.IO;

namespace Hermes
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json")
                    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", false)
                    .Build();

                Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(configuration)
                    .CreateLogger();

                using var host = CreateHostBuilder(args).Build();

                var dataImportStatusQueue = host.Services.GetRequiredService<DataImportStatusQueue>();
                dataImportStatusQueue.StartMonitors();

                var unprocessableMessageQueue = host.Services.GetRequiredService<UnprocessableMessageQueue>();
                unprocessableMessageQueue.StartMonitor();

                host.Run();
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<HostOptions>(option => { option.ShutdownTimeout = TimeSpan.FromSeconds(90); });
                    services.AddHostedService<DataImporter>();
                    services.AddSingleton<DataImportStatusQueue>();
                    services.AddSingleton<UnprocessableMessageQueue>();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
