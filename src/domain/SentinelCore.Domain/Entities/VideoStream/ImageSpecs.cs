namespace SentinelCore.Domain.Entities.VideoStream
{
    public class ImageSpecs(string uri, int width, int height)
    {
        public string Uri { get; private set; } = uri;
        public int Width { get; private set; } = width;
        public int Height { get; private set; } = height;
    }
}
