using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordIan.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

namespace DiscordIan
{
    internal class Ian
    {
        private const string ConfigFilename = "settings.json";

        public static readonly DateTime Startup = DateTime.Now;

        public async Task GoAsync(string[] args)
        {
            try
            {
                using var services = ConfigureServices(args);

                var loggerFactory = services.GetRequiredService<ILoggerFactory>();
                loggerFactory.AddSerilog();

                Log.Logger.Information("Starting up");

                using var client = services.GetRequiredService<DiscordSocketClient>();

                client.Log += LogAsync;

                services.GetRequiredService<CommandService>().Log += LogAsync;

                var optionsAccessor = services.GetRequiredService<IOptionsMonitor<Model.Options>>();

                var token = optionsAccessor.CurrentValue.IanLoginToken;

                if (string.IsNullOrWhiteSpace(token))
                {
                    Log.Fatal("Required setting {SettingName} not found in configuration",
                        nameof(optionsAccessor.CurrentValue.IanLoginToken));
                }
                else
                {
                    await client.LoginAsync(TokenType.Bot, token);

                    Log.Logger.Information("Logged in, starting bot");
                    await client.StartAsync();

                    Log.Logger.Information("Loading command handling service");
                    await services.GetRequiredService<CommandHandlingService>().InitializeAsync();

                    Log.Logger.Information("Everything is kicked off, going to sleep");
                    await Task.Delay(-1);
                }

                Log.Logger.Information("Exiting.");
            }
            catch (Exception ex)
            {
                if (Log.Logger != null)
                {
                    Log.Fatal(ex, "Startup failed: {ErrorMessage}", ex.Message);
                }
                else
                {
                    Console.WriteLine($"Startup failed: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                }
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static ServiceProvider ConfigureServices(string[] args)
        {
            var services = new ServiceCollection();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile(ConfigFilename)
                .AddUserSecrets<Ian>()
                .AddCommandLine(args)
                .AddEnvironmentVariables()
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            services.AddLogging();

            services.Configure<Model.Options>(configuration);
            services.AddOptions();

            services.AddDistributedMemoryCache();

            // discord services
            services.AddSingleton<DiscordSocketClient>();
            services.AddSingleton<CommandService>();

            // project services
            services.AddSingleton<CommandHandlingService>();
            services.AddTransient<FetchService>();

            // system services
            services.AddTransient(x =>
                new HttpClient(BuildHTTPHandler())
                {
                    DefaultRequestVersion = HttpVersion.Version10
                }
            );

            return services.BuildServiceProvider();
        }

        private static HttpClientHandler BuildHTTPHandler()
        {
            var handler = new HttpClientHandler();

            handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) =>
            {
                if (policyErrors == SslPolicyErrors.None)
                {
                    return true;
                }

                if (cert.GetCertHashString() == "12CBB66DF711F3E96A181BA6CEDF3320341E5073")
                {
                    return true;
                }

                return false;
            };

            return handler;
        }

        private static Task LogAsync(LogMessage logMessage)
        {
            switch (logMessage.Severity)
            {
                case LogSeverity.Critical:
                    Log.Logger.Fatal(logMessage.Exception, logMessage.Message);
                    break;
                case LogSeverity.Debug:
                    Log.Logger.Debug(logMessage.Exception, logMessage.Message);
                    break;
                case LogSeverity.Error:
                    Log.Logger.Error(logMessage.Exception, logMessage.Message);
                    break;
                case LogSeverity.Info:
                    Log.Logger.Information(logMessage.Exception, logMessage.Message);
                    break;
                case LogSeverity.Verbose:
                    Log.Logger.Verbose(logMessage.Exception, logMessage.Message);
                    break;
                default:
                    Log.Logger.Warning(logMessage.Exception, logMessage.Message);
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
