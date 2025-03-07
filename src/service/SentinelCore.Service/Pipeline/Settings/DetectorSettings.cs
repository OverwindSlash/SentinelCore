﻿namespace SentinelCore.Service.Pipeline.Settings
{
    public class DetectorSettings : DynamicModuleSettingsBase
    {
        public string ModelPath { get; set; }
        public string ModelConfig { get; set; }
        public bool UseCuda { get; set; }
        public int GpuId { get; set; }
        public float Thresh { get; set; }
        public string TargetTypes { get; set; }
        public int DetectionStride { get; set; }
    }
}
