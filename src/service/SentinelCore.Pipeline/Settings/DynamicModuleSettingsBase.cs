namespace SentinelCore.Pipeline.Settings
{
    public class DynamicModuleSettingsBase
    {
        public string AssemblyFile { get; set; }
        public string FullQualifiedClassName { get; set; }
        public string[] Parameters { get; set; }

        public DynamicModuleSettingsBase()
        {
            Parameters = new string[0];
        }
    }
}
