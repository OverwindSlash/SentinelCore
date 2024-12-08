namespace SentinelCore.Service.Pipeline.Settings
{
    public class EventSubscriberSettings : DynamicModuleSettingsBase
    {
        public Dictionary<string, string> Preferences { get; set; }
    }
}
