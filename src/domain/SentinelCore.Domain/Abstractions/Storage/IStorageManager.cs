using OpenCvSharp;

namespace SentinelCore.Domain.Abstractions.Storage
{
    public interface IStorageManager
    {
        Task SaveImage(string objId, Mat image);
    }
}
