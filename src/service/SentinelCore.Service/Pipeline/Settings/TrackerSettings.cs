namespace SentinelCore.Service.Pipeline.Settings
{
    public class TrackerSettings : DynamicModuleSettingsBase
    {
        public float IouThreshold { get; set; }
        public int MaxMisses { get; set; }
    }
}
