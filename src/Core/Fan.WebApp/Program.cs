using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.IO;

namespace Fan.WebApp
{
    public class Program
    {
        /// <summary>
        /// The entry point for the entire program.
        /// </summary>
        /// <param name="args"></param>
        public static void Main(string[] args)
        {
            // Setting values are being overriden in the order of how builder adds them.
            // For example AddEnvironmentVariables() will enable Azure App settings to override what is
            // in appsettings.Production.json which overrides appsettings.json.  
            // For envrionments that don't have ASPNETCORE_ENVIRONMENT set, it gets Production.
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            // We always log to Application Insights, the key is from either appsettings.json or Azure App Service > App settings
            Log.Logger = new LoggerConfiguration()
               .ReadFrom.Configuration(configuration)
               .Enrich.FromLogContext()
               .WriteTo.ApplicationInsights(configuration.GetValue<string>("ApplicationInsights:InstrumentationKey"),
                        TelemetryConverter.Traces, Serilog.Events.LogEventLevel.Information)
               .CreateLogger();

            try
            {
                Log.Information("Starting web host");

                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStaticWebAssets(); 
                    webBuilder.UseStartup<Startup>();
                })
                .UseSerilog();
    }
}