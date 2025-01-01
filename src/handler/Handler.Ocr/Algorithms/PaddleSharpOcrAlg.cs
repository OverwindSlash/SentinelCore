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
using System.Collections.Concurrent;
using Serilog;

namespace Handler.Ocr.Algorithms
{
    public class PaddleSharpOcrAlg : ObjectExpiredSubscriber, IAnalysisHandler
    {
        private readonly AnalysisPipeline _pipeline;

        private IPublisher<OcrResultEvent> _eventPublisher;

        private readonly string _ocrType;
        private readonly string _carrierType;
        private readonly PaddleOcrAll _paddleOcrAll;
        private readonly float _scoreThresh;

        public string HandlerName => nameof(PaddleSharpOcrAlg);

        private IServiceProvider _serviceProvider;

        private ConcurrentDictionary<string, HashSet<string>> _carrierAndOcrIds;

        public PaddleSharpOcrAlg(AnalysisPipeline pipeline, Dictionary<string, string> preferences)
        {
            _pipeline = pipeline;

            _ocrType = preferences["TypeToOCR"].ToLower();
            _carrierType = preferences["CarrierType"].ToLower(); 
            _scoreThresh = float.Parse(preferences["ScoreThresh"]);

            _paddleOcrAll = new PaddleOcrAll(LocalFullModels.ChineseV4)
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

                    OcrActions.SaveEventImages(_snapshotManager.SnapshotDir, carrierId, @event.Id, ocrSnapshot);

                    Log.Information($"CarrierObjId:{carrierId} OcrObjId:{@event.Id} Text: {region.Text}, Score: {region.Score}");
                }
            }
        }

        private Mat SharpenImageText(Mat ocrSnapshot)
        {
            // 1. 转换为灰度图
            Mat gray = new Mat();
            Cv2.CvtColor(ocrSnapshot, gray, ColorConversionCodes.BGR2GRAY);

            // 2. 去除噪声
            Mat denoised = new Mat();
            Cv2.GaussianBlur(gray, denoised, new Size(3, 3), 0);

            // 3. 对比度增强（直方图均衡化）
            /*Mat equalized = new Mat();
            Cv2.EqualizeHist(denoised, equalized);*/

            return denoised;
        }

        public override void Dispose()
        {
            base.Dispose();
            _paddleOcrAll.Dispose();
        }
    }
}
