using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic.CompilerServices;
using Polly;
using Polly.Registry;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using Serilog.Sinks.MSSqlServer;

namespace APIClient
{
    class Program
    {
        private static IConfiguration Configuration { get; } = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", false, true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddUserSecrets<Program>()
            .Build();

        static async Task Main(string[] args)
        {
            Serilog.Debugging.SelfLog.Enable(msg =>
            {
                Debug.Print(msg);
                Debugger.Break();
            });

            var serviceCollection = new ServiceCollection();
            Configure(serviceCollection);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var programLogger = serviceProvider.GetService<ILogger>();
            var accountInfo = serviceProvider.GetService<HttpClientFactoryInstanceManagementService>();
            await accountInfo.GetAccountInfo();
            programLogger.ForContext<Program>();
            programLogger.Information("test logging");
            Log.CloseAndFlush();
            ((IDisposable)programLogger).Dispose();
        }

        private static void Configure(IServiceCollection serviceCollection)
        {
            ConfigurePolly(serviceCollection);
            ConfigureSeriLogging(serviceCollection);
            
           serviceCollection.AddHttpClient<AusPostClient>()
                .AddHttpMessageHandler<LogRequestHandler>()
                .AddPolicyHandlerFromRegistry((SelectPolicy));

            serviceCollection.AddSingleton<IConfiguration>(Configuration);
            serviceCollection.AddTransient<HttpClientFactoryInstanceManagementService>();
            serviceCollection.AddTransient<LogRequestHandler>();

        }

        private static void ConfigureSeriLogging(IServiceCollection serviceCollection)
        {
            var connectionString = Configuration.GetConnectionString("NorthWindCon");
            var columnOption = new ColumnOptions();
            columnOption.Store.Remove(StandardColumn.MessageTemplate);
            var minLevel = (LogEventLevel)Enum.Parse(typeof(LogEventLevel), Configuration.GetSection("DBLogLevel").Value);
            //Configuration.GetSection("Serilog:WriteTo:4:Args:restrictedToMinimumLevel").Value;

            columnOption.AdditionalDataColumns = new Collection<DataColumn>
            {
                new DataColumn {DataType = typeof (string), ColumnName = "OtherData"},
                new DataColumn {DataType = typeof (string), ColumnName = "Source"},
                new DataColumn {DataType = typeof (string), ColumnName = "Application"},
            };
            ILogger logger = new Serilog.LoggerConfiguration()
                .ReadFrom.Configuration(Configuration)
                .WriteTo.Logger(lc => lc
                    .MinimumLevel.Information()
                    .Filter.ByIncludingOnly(expression: "StartsWith(Source, 'APIClient.Log')")
                    .WriteTo.MSSqlServer(connectionString, "Logs", restrictedToMinimumLevel: minLevel, columnOptions: columnOption)
                )
                .CreateLogger();
            serviceCollection.AddSingleton<ILogger>(logger);
        }

        private static void ConfigurePolly(IServiceCollection serviceCollection)
        {
            IPolicyRegistry<string> registry = serviceCollection.AddPolicyRegistry();
            IAsyncPolicy<HttpResponseMessage> httpRetryPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode).RetryAsync(3);
            registry.Add("SimpleRetryPolicy", httpRetryPolicy);

            IAsyncPolicy<HttpResponseMessage> httpRetryWaitPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt) / 2));
            registry.Add("WaitRetryPolicy", httpRetryWaitPolicy);

            IAsyncPolicy<HttpResponseMessage> noOpPolicy = Policy.NoOpAsync()
                .AsAsyncPolicy<HttpResponseMessage>();
            registry.Add("NoOpPolicy", noOpPolicy);
        }

        private static IAsyncPolicy<HttpResponseMessage> SelectPolicy(IReadOnlyPolicyRegistry<string> policyRegistry, HttpRequestMessage httpRequestMessage)
        {
            if (httpRequestMessage.Method == HttpMethod.Get)
            {
                return policyRegistry.Get<IAsyncPolicy<HttpResponseMessage>>("SimpleRetryPolicy");
            }

            if (httpRequestMessage.Method != HttpMethod.Post)
            {
                return policyRegistry.Get<IAsyncPolicy<HttpResponseMessage>>("WaitRetryPolicy");
            }

            return policyRegistry.Get<IAsyncPolicy<HttpResponseMessage>>("NoOpPolicy");
        }
    }
}
