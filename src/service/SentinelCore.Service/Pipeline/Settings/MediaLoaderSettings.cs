namespace SentinelCore.Service.Pipeline.Settings
{
    public class MediaLoaderSettings : DynamicModuleSettingsBase
    {
        public int BufferSize { get; set; }
        public int VideoStride { get; set; }
    }
}
