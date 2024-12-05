namespace SentinelCore.Domain.Abstractions.MediaLoader
{
    public interface IMediaLoader : IDisposable
    {
        public string DeviceId { get; }

        public int Width { get; }
        public int Height { get; }

        public bool IsOpened { get; }

        void Open(string uri);
        void Close();
    }
}
