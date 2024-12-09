using Microsoft.Extensions.Configuration;
using SentinelCore.Service.Pipeline;

namespace SentinelCore.AppService
{
    public class AnalysisPipelineAppService
    {
        public void RunWithConfigFile(string configFile)
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile(configFile, true, true)
                .Build();

            using var pipeline = new AnalysisPipeline(config);

            pipeline.Run();
        }
    }
}
