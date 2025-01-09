namespace SentinelCore.Service.Pipeline.Settings
{
    public class DynamicModuleSettingsBase
    {
        public string AssemblyFile { get; set; }
        public string FullQualifiedClassName { get; set; }
        public Dictionary<string, string> Preferences { get; set; }

        public DynamicModuleSettingsBase()
        {
            Preferences = new Dictionary<string, string>();
        }
    }
}
