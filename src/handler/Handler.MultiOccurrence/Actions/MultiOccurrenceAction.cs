using OpenCvSharp;
using SentinelCore.Domain.Utils.Extensions;

namespace Handler.MultiOccurrence.Actions
{
    public class MultiOccurrenceAction
    {
        public static string SaveEventImages(string snapshotDir, string snapshotId, Mat snapshot)
        {
            if (string.IsNullOrEmpty(snapshotId) || snapshot == null)
            {
                return string.Empty;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            string filename = snapshotId.Replace(':', '_');

            string path = Path.Combine(snapshotDir, "Events");
            path.EnsureDirExistence();
            var fileSavePath = Path.Combine(path, $"{filename}_{timestamp}.jpg");

            snapshot.SaveImage(fileSavePath);

            return fileSavePath;
        }
    }
}
