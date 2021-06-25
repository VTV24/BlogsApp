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


        public static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

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