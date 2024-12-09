// See https://aka.ms/new-console-template for more information

using SentinelCore.AppService;

Console.WriteLine("Analysis begin...");

var pipelineAppService = new AnalysisPipelineAppService();
pipelineAppService.RunWithConfigFile("console-settings.json");