namespace SentinelCore.Service.Pipeline.Settings
{
    public class JsonMsgPosterSettings : DynamicModuleSettingsBase
    {
        public string DestinationUrl { get; set; }
        public Dictionary<string, string> Preferences { get; set; }
    }
}
