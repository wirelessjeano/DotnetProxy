using System;
using System.IO;
using System.Net;
using System.Reflection;
using DotnetProxy.SecureProxy.Configs;
using DotnetProxy.SecureProxy.Extensions;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DotnetProxy.SecureProxy
{
    public class Program
    {
           private static ProxyConfigs _proxyConfigs;
           public static void Main(string[] args)
           {
               CreateWebHostBuilder(args).Build().Run();
           }

           public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
               WebHost.CreateDefaultBuilder(args)
                   .UseContentRoot(Directory.GetCurrentDirectory())
                   .ConfigureServiceAppConfiguration("secureproxy")
                   .UseKestrel((hostingContext, options) =>
                   {
                       var env = hostingContext.HostingEnvironment;
                       if (env.IsDevelopment())
                       {
                           options.Listen(IPAddress.Any, 5012);
                       }
                       else
                       {
                           var configurationSection = hostingContext.Configuration.GetSection("ProxyConfigs");
                           _proxyConfigs = new ProxyConfigs
                           {
                               CertificateFile = configurationSection["CertificateFile"],
                               CertificatePassword = configurationSection["CertificatePassword"]
                           };
                           
                           options.Listen(IPAddress.Any, 443,
                               listenOptions =>
                               {
                                   listenOptions.UseHttps(Path.Combine("configs", _proxyConfigs.CertificateFile), _proxyConfigs.CertificatePassword);
                               });
                       }
                   })
                   .UseSetting(WebHostDefaults.ApplicationKey, typeof(Program).GetTypeInfo().Assembly.FullName) 
                   .UseSerilog((ctx, config) =>
                   {
                       config
                           .MinimumLevel.Information()
                           .ReadFrom.Configuration(ctx.Configuration)
                           .Enrich.FromLogContext();
                       
                       //development & docker
                       if (ctx.HostingEnvironment.IsDevelopment())
                       {
                           //See https://github.com/serilog/serilog-aspnetcore
                           Serilog.Debugging.SelfLog.Enable(Console.Error);
                           config.WriteTo.Console();
                       }
   
                       config.WriteTo.File("log.txt", rollingInterval: RollingInterval.Day);
   
                   })
                   .UseStartup<Startup>();

       }
    
}