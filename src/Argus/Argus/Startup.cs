using Argus.HealthChecks;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace Argus
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddGrpc();
            services.AddGrpcReflection();
            services.AddSingleton<UnitedStatesStockTickersScrapeService>();
            services.AddSingleton<NasdaqTickersScrapeService>();
            services.AddSingleton<UnitedStatesStockPricesScrapeService>();

            services.AddSingleton<DataPublishQueue>();
            services.AddSingleton<GrpcServiceProcedureStatusQueue>();
            services.AddSingleton<UnpublishableMessageQueue>();
            
            services.AddControllers();

            services.AddHttpClient("iShares", c =>
            {
                c.BaseAddress = new Uri("https://www.ishares.com/");
            });

            services.AddHttpClient("YahooFinance", c =>
            {
                c.BaseAddress = new Uri("https://query1.finance.yahoo.com/v7/finance/download/");
            });

            services.AddHealthChecks()
                .AddSqlServer(
                    connectionString: Configuration.GetConnectionString("Argus"),
                    healthQuery: "SELECT 1;",
                    name: "database",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                    tags: new string[] { "Argus", "Argus", "database", "SQLServer" },
                    timeout: new TimeSpan(hours: 0, minutes: 0, seconds: 1))
                .AddRabbitMQ(
                    rabbitConnectionString: $"amqp://{Configuration["MessageQueue:UserName"]}:{Configuration["MessageQueue:Password"]}@{Configuration["MessageQueue:HostName"]}:5672/",
                    name: "message queue",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                    tags: new string[] { "Argus", "rabbitmq", "mq", "RabbitMQ" },
                    timeout: new TimeSpan(hours: 0, minutes: 0, seconds: 1))
                .AddCheck<GrpcServiceProcedureStatusHealthCheck>(
                    name: "GRPC service/procedure",
                    failureStatus: null,
                    tags: new string[] { "Argus", "procedure", "service", "GRPC" })
                .AddCheck<DataPublishHealthCheck>(
                    name: "Data publish",
                    failureStatus: null,
                    tags: new string[] { "Argus", "publish", "service", "RabbitMQ" });

            services.AddSwaggerDocument(config =>
            {
                config.PostProcess = document =>
                {
                    document.Info.Version = "v1";
                    document.Info.Title = "Argus service API";
                    document.Info.Description = "Web API for checking status and manage service";
                    document.Info.Contact = new NSwag.OpenApiContact
                    {
                        Name = "Weizhi Luo",
                        Email = "weizhi.luo@googlemail.com"
                    };
                };
            });

            services.Configure<HostOptions>(options => { options.ShutdownTimeout = TimeSpan.FromSeconds(90); });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseOpenApi();
            app.UseSwaggerUi3();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<UnitedStatesStockTickersScrapeService>();
                endpoints.MapGrpcService<NasdaqTickersScrapeService>();
                endpoints.MapGrpcService<UnitedStatesStockPricesScrapeService>();

                if (env.IsDevelopment())
                {
                    endpoints.MapGrpcReflectionService();
                }

                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
                });

                endpoints.MapControllers();
                endpoints.MapHealthChecks("/healthcheck", new HealthCheckOptions
                {
                    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                });
            });

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }
    }
}
