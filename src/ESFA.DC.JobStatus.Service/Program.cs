using System.Collections.Generic;
using System.IO;
using System.Threading;
using ESFA.DC.JobStatus.Interface;
using ESFA.DC.JobStatus.WebServiceCall.Service;
using ESFA.DC.JobStatus.WebServiceCall.Service.Interface;
using ESFA.DC.Logging;
using ESFA.DC.Logging.Config;
using ESFA.DC.Logging.Config.Interfaces;
using ESFA.DC.Logging.Enums;
using ESFA.DC.Logging.Interfaces;
using ESFA.DC.Queueing;
using ESFA.DC.Queueing.Interface;
using ESFA.DC.Queueing.Interface.Configuration;
using ESFA.DC.Serialization.Interfaces;
using ESFA.DC.Serialization.Json;
using Microsoft.Extensions.Configuration;
using ExecutionContext = ESFA.DC.Logging.ExecutionContext;

namespace ESFA.DC.JobStatus.Service
{
    public static class Program
    {
#if DEBUG
        private const string ConfigFile = "privatesettings.json";
#else
        private const string ConfigFile = "appsettings.json";
#endif

        static void Main(string[] args)
        {
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(ConfigFile);

            IConfiguration configuration = configBuilder.Build();

            IJobStatusWebServiceCallServiceConfig jobStatusWebServiceCallConfig = new JobStatusWebServiceCallServiceConfig(configuration["jobSchedulerApiEndPoint"]);
            IQueueConfiguration queueConfiguration = new QueueConfiguration(configuration["queueConnectionString"], configuration["queueName"], 1);
            ISerializationService serializationService = new JsonSerializationService();
            IApplicationLoggerSettings applicationLoggerOutputSettings = new ApplicationLoggerSettings
            {
                ApplicationLoggerOutputSettingsCollection = new List<IApplicationLoggerOutputSettings>
                {
                    new MsSqlServerApplicationLoggerOutputSettings
                    {
                        ConnectionString = configuration["logConnectionString"],
                        MinimumLogLevel = LogLevel.Information
                    },
                    new ConsoleApplicationLoggerOutputSettings
                    {
                        MinimumLogLevel = LogLevel.Information
                    }
                },
                TaskKey = "Job Status",
                EnableInternalLogs = true,
                JobId = "Job Status Service",
                MinimumLogLevel = LogLevel.Information
            };
            IExecutionContext executionContext = new ExecutionContext
            {
                JobId = "Job Status Service",
                TaskKey = "Job Status"
            };
            ILogger logger = new SeriLogger(applicationLoggerOutputSettings, executionContext);
            IQueueSubscriptionService<JobStatusDto> queueSubscriptionService = new QueueSubscriptionService<JobStatusDto>(queueConfiguration, serializationService, logger);
            IJobStatusWebServiceCallService<JobStatusDto> jobStatusWebServiceCallService = new JobStatusWebServiceCallService<JobStatusDto>(jobStatusWebServiceCallConfig, queueSubscriptionService, serializationService, logger);

            jobStatusWebServiceCallService.Subscribe();

            logger.LogInfo("Started!");

            ManualResetEvent oSignalEvent = new ManualResetEvent(false);
            oSignalEvent.WaitOne();
        }
    }
}
