using SentinelCore.Domain.Entities.VideoStream;

namespace SentinelCore.Domain.Abstractions.MediaLoader
{
    public interface IVideoLoader : IMediaLoader
    {
        public VideoSpecs Specs { get; }

        public int BufferedFrameCount { get; }
        public int BufferedMaxOccupied { get; }

        public bool IsInPlaying { get; }

        void Play(int stride = 1, bool debugMode = false, int debugFrameCount = 0);
        void Stop();

        Frame RetrieveFrame();
        Task<Frame> RetrieveFrameAsync();
    }
}
