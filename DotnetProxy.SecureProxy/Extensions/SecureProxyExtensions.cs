using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DotnetProxy.SecureProxy.Extensions
{
    public static class SecureProxyExtensions
    {
        public static IApplicationBuilder UseSecureProxy(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecureProxy.Middleware.SecureProxy>();
        }
        
         public static IWebHostBuilder ConfigureServiceAppConfiguration(this IWebHostBuilder builder, string appName)
        {
            builder.ConfigureAppConfiguration((hostingContext, config) =>
            {
                var env = hostingContext.HostingEnvironment;

                string baseFolder = string.Empty;
                string configFolder = string.Empty;
                string versionFile = string.Empty;

                if (env.IsDevelopment())
                {
                    baseFolder = System.IO.Directory.GetParent(Directory.GetCurrentDirectory()).FullName;
                    configFolder = baseFolder + "/configs";
                    versionFile = baseFolder + "/version";
                }
                else
                {
                    configFolder = "configs";
                    versionFile = "version";
                }

                Console.WriteLine("Version file is: " + versionFile);
                Console.WriteLine($"Machine name is: {Environment.MachineName}");
                
                //set version singleton
                string version = File.ReadLines(versionFile).First();
                Console.WriteLine("Version is: " + version);
                
                //load the SharedSettings first, so that appsettings.json overrwrites it
                config
                    .AddJsonFile(Path.Combine(configFolder, $"{appName.ToLower()}appsettings.json"), optional: false)
                    .AddJsonFile(Path.Combine(configFolder, $"{appName.ToLower()}appsettings.{env.EnvironmentName}.json"), optional: true);

                config.AddEnvironmentVariables();

            });

            return builder;
        }
        
    }
}