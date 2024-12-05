namespace SentinelCore.Domain.Entities.VideoStream
{
    public class VideoSpecs(string uri, int width, int height, double fps, int frameCount)
        : ImageSpecs(uri, width, height)
    {
        public double Fps { get; private set; } = fps;
        public int FrameCount { get; private set; } = frameCount;
    }
}
