using Handler.Ocr.Actions;
using Handler.Ocr.Events;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using OpenCvSharp;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.Local;
using SentinelCore.Domain.Abstractions.AnalysisHandler;
using SentinelCore.Domain.Entities.AnalysisEngine;
using SentinelCore.Domain.Entities.VideoStream;
using SentinelCore.Domain.Events.AnalysisEngine;
using SentinelCore.Service.Pipeline;
using Serilog;
using System.Collections.Concurrent;

namespace Handler.Ocr.Algorithms
{
    public class PaddleSharpOcrAlg : ObjectExpiredSubscriber, IAnalysisHandler
    {
        private readonly AnalysisPipeline _pipeline;
        private readonly string _eventName;
        private IPublisher<OcrResultEvent> _eventPublisher;

        private readonly string _ocrType;
        private readonly string _carrierType;
        private readonly PaddleOcrAll _paddleOcrAll;
        private readonly float _scoreThresh;

        public string HandlerName => nameof(PaddleSharpOcrAlg);

        private IServiceProvider _serviceProvider;

        private ConcurrentDictionary<string, HashSet<string>> _carrierAndOcrIds;

        private readonly Mat _kernelSharp = Mat.FromPixelData(3, 3, MatType.CV_32F, new float[,]
        {
            { 0, -1, 0 },
            { -1, 5, -1 },
            { 0, -1, 0 }
        });

        public PaddleSharpOcrAlg(AnalysisPipeline pipeline, Dictionary<string, string> preferences)
        {
            _pipeline = pipeline;

            _eventName = preferences["EventName"];
            _ocrType = preferences["TypeToOCR"].ToLower();
            _carrierType = preferences["CarrierType"].ToLower(); 
            _scoreThresh = float.Parse(preferences["ScoreThresh"]);

            _paddleOcrAll = new PaddleOcrAll(LocalFullModels.ChineseV3)
            {
                AllowRotateDetection = true,
                Enable180Classification = false,
            };

            _carrierAndOcrIds = new ConcurrentDictionary<string, HashSet<string>>();
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _eventPublisher = _serviceProvider.GetRequiredService<IPublisher<OcrResultEvent>>();

            var subscriber = serviceProvider.GetService<ISubscriber<ObjectExpiredEvent>>();
            this.SetSubscriber(subscriber);
        }

        public AnalysisResult Analyze(Frame frame)
        {
            foreach (var carrierObj in frame.DetectedObjects)
            {
                if (!carrierObj.IsUnderAnalysis)
                    continue;

                if (carrierObj.Label != _carrierType)
                    continue;

                foreach (var ocrObj in frame.DetectedObjects)
                {
                    if (carrierObj == ocrObj)
                        continue;

                    if (!ocrObj.IsUnderAnalysis)
                        continue;

                    if (ocrObj.Label != _ocrType)
                        continue;

                    if (!carrierObj.Bbox.Contains(ocrObj.Bbox))
                        continue;

                    if (!_carrierAndOcrIds.ContainsKey(carrierObj.Id))
                    {
                        _carrierAndOcrIds.TryAdd(carrierObj.Id, new HashSet<string>());
                    }

                    _carrierAndOcrIds.TryGetValue(carrierObj.Id, out var ocrObjSet);
                    if (ocrObjSet != null)
                    {
                        ocrObjSet.Add(ocrObj.Id);
                    }
                }
            }
            
            return new AnalysisResult(true);
        }

        public override void ProcessEvent(ObjectExpiredEvent @event)
        {
            var _snapshotManager = _pipeline.SnapshotManager;

            if (@event.Label != _ocrType)
            {
                return;
            }

            Mat ocrSnapshot = _snapshotManager.GetBestSnapshotByObjectId(@event.Id);
            if (ocrSnapshot.Width == 0)
            {
                return;
            }

            Mat sharpenedImage = SharpenImageText(ocrSnapshot);

            PaddleOcrResult result = _paddleOcrAll.Run(sharpenedImage);
            foreach (PaddleOcrResultRegion region in result.Regions)
            {
                string carrierId = string.Empty;

                if (region.Score > _scoreThresh)
                {
                    foreach (var carrierObjId in _carrierAndOcrIds.Keys)
                    {
                        var ocrObjIds = _carrierAndOcrIds[carrierObjId];

                        if (ocrObjIds.Contains(@event.Id))
                        {
                            carrierId = carrierObjId;
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(carrierId))
                    {
                        continue;
                    }

                    Mat carrierSnapshot = _snapshotManager.GetBestSnapshotByObjectId(carrierId);

                    OcrActions.SaveEventImages(_snapshotManager.SnapshotDir, carrierId, carrierSnapshot, @event.Id, ocrSnapshot, region.Text);

                    Log.Information($"CarrierObjId:{carrierId} OcrObjId:{@event.Id} Text: {region.Text}, Score: {region.Score}");
                }
            }
        }

        private Mat SharpenImageText(Mat ocrSnapshot)
        {
            // 1. 转换为灰度图
            using Mat gray = new Mat();
            Cv2.CvtColor(ocrSnapshot, gray, ColorConversionCodes.BGR2GRAY);

            // 2. 去除噪声
            using Mat denoised = new Mat();
            Cv2.GaussianBlur(gray, denoised, new Size(3, 3), 0);

            // 3. 图像锐化
            Mat denoisedSharpened = new Mat();
            Cv2.Filter2D(denoised, denoisedSharpened, -1, _kernelSharp);

            return denoisedSharpened;
        }

        public override void Dispose()
        {
            base.Dispose();
            _paddleOcrAll.Dispose();
        }
    }
}
