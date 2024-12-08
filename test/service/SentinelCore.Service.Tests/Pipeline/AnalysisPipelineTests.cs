using Microsoft.Extensions.Configuration;
using SentinelCore.Service.Pipeline;

namespace SentinelCore.Service.Tests.Pipeline
{
    public class AnalysisPipelineTests
    {
        [Test]
        public void TestCreatePipeline()
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("test-settings.json", true, true)
                .Build();

            using var pipeline = new AnalysisPipeline(config);

            //pipeline.Run();

            Assert.That(pipeline, Is.Not.Null);
        }
    }
}
