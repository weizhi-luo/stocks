using HealthChecks.UI.Client;
using Hermes.HealthChecks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace Hermes
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            services.AddHealthChecks()
                .AddSqlServer(
                    connectionString: Configuration.GetConnectionString("Metis"),
                    healthQuery: "SELECT 1;",
                    name: "database",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                    tags: new string[] { "Hermes", "Metis", "database", "SQLServer" },
                    timeout: new TimeSpan(hours: 0, minutes: 0, seconds: 1))
                .AddRabbitMQ(
                    rabbitConnectionString: $"amqp://{Configuration["MessageQueue:UserName"]}:{Configuration["MessageQueue:Password"]}@{Configuration["MessageQueue:HostName"]}:5672/",
                    name: "message queue",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                    tags: new string[] { "Hermes", "rabbitmq", "mq", "RabbitMQ" },
                    timeout: new TimeSpan(hours: 0, minutes: 0, seconds: 1))
                .AddCheck<DataImportStatusHealthCheck>(
                    name: "Data import",
                    failureStatus: null,
                    tags: new string[] { "Hermes", "DataImport" });

            services.AddSwaggerDocument(config =>
            {
                config.PostProcess = document =>
                {
                    document.Info.Version = "v1";
                    document.Info.Title = "Hermes service API";
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

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/healthcheck", new HealthCheckOptions
                {
                    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                });
            });
        }
    }
}
