// See https://aka.ms/new-console-template for more information

using SentinelCore.AppService;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("log.txt",
        rollingInterval: RollingInterval.Day,
        rollOnFileSizeLimit: true)
    .CreateLogger();

const string configFile = "console-settings.json";

Log.Information($"Analysis begin...");
Log.Information($"using configuration file: {configFile}");

var pipelineAppService = new AnalysisPipelineAppService();

try
{
    pipelineAppService.RunWithConfigFile(configFile);
}
catch (Exception e)
{
    Log.Fatal(e.Message);
}
