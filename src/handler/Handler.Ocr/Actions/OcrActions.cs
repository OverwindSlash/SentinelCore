using OpenCvSharp;
using SentinelCore.Domain.Utils.Extensions;

namespace Handler.Ocr.Actions
{
    public class OcrActions
    {
        public static string SaveEventImages(string snapshotDir, 
            string carrierSnapshotId, Mat carrierSnapshot, 
            string ocrSnapshotId, Mat ocrSnapshot, string ocrResult)
        {
            if (string.IsNullOrEmpty(carrierSnapshotId) || string.IsNullOrEmpty(ocrSnapshotId) || ocrSnapshot == null)
            {
                return string.Empty;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            string carrierFilename = carrierSnapshotId.Replace(':', '_');
            string ocrFilename = ocrSnapshotId.Replace(':', '_');

            string basePath = Path.Combine(snapshotDir, "Ocr");
            basePath.EnsureDirExistence();

            string carrierPath = Path.Combine(basePath, carrierFilename);
            carrierPath.EnsureDirExistence();

            var carrierSaveFile = Path.Combine(carrierPath, $"{carrierFilename}.jpg");
            var ocrFileSavePath = Path.Combine(carrierPath, $"{ocrFilename}_{ocrResult}.jpg");

            if (carrierSnapshot.Width != 0)
            {
                carrierSnapshot.SaveImage(carrierSaveFile);
            }
            
            ocrSnapshot.SaveImage(ocrFileSavePath);

            return ocrFileSavePath;
        }

        /*public static string SaveEventImages(string snapshotDir, string carrierSnapshotId, string ocrSnapshotId, Mat carrierSnapshot, Mat ocrSnapshot)
        {
            if (string.IsNullOrEmpty(carrierSnapshotId) || string.IsNullOrEmpty(ocrSnapshotId) || carrierSnapshot == null || ocrSnapshot == null)
            {
                return string.Empty;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            string carrierFilename = carrierSnapshotId.Replace(':', '_');
            string ocrFilename = ocrSnapshotId.Replace(':', '_');

            string path = Path.Combine(snapshotDir, "Events");
            path.EnsureDirExistence();

            var carrierFileSavePath = Path.Combine(path, $"{carrierFilename}_{timestamp}.jpg");
            var ocrFileSavePath = Path.Combine(path, $"{carrierFilename}_{ocrFilename}_{timestamp}.jpg");

            carrierSnapshot.SaveImage(carrierFileSavePath);
            ocrSnapshot.SaveImage(ocrFileSavePath);

            return ocrFileSavePath;
        }*/
    }
}
