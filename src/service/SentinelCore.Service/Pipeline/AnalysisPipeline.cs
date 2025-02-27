using MessagePipe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenCvSharp;
using SentinelCore.Domain.Abstractions.AnalysisHandler;
using SentinelCore.Domain.Abstractions.MediaLoader;
using SentinelCore.Domain.Abstractions.MessagePoster;
using SentinelCore.Domain.Abstractions.ObjectDetector;
using SentinelCore.Domain.Abstractions.ObjectTracker;
using SentinelCore.Domain.Abstractions.RegionManager;
using SentinelCore.Domain.Abstractions.SnapshotManager;
using SentinelCore.Domain.Entities.AnalysisDefinitions.Geometrics;
using SentinelCore.Domain.Entities.AnalysisEngine;
using SentinelCore.Domain.Entities.ObjectDetection;
using SentinelCore.Domain.Entities.VideoStream;
using SentinelCore.Domain.Events.AnalysisEngine;
using SentinelCore.Service.Pipeline.Settings;
using Serilog;
using System.Collections.Concurrent;
using System.Reflection;

namespace SentinelCore.Service.Pipeline
{
    public class AnalysisPipeline : FrameAndObjectExpiredSubscriber, IDisposable
    {
        private PipelineSettings _pipeLineSettings;
        private MediaLoaderSettings _mediaLoaderSettings;
        private DetectorSettings _detectorSettings;
        private TrackerSettings _trackerSettings;
        private RegionManagerSettings _regionManagerSettings;
        private SnapshotSettings _snapshotSettings;
        private List<AnalysisHandlerSettings> _analysisHandlerSettings;
        private List<JsonMsgPosterSettings> _jsonMsgPosterSettings;

        private readonly VideoFrameSlideWindow _slideWindow;
        private readonly VideoFrameBuffer _analyzedFrameBuffer;

        private ServiceCollection _services;
        private ServiceProvider _provider;

        private IVideoLoader _videoLoader;
        private IObjectDetector _objectDetector;
        private IObjectTracker _objectTracker;
        private IRegionManager _regionManager;
        private ISnapshotManager _snapshotManager;
        private List<IAnalysisHandler> _analysisHandlers;
        private List<IJsonMessagePoster> _jsonMsgPosters;

        public string DeviceName => _pipeLineSettings.DeviceName;
        public IRegionManager RegionManager => _regionManager;
        public ISnapshotManager SnapshotManager => _snapshotManager;
        public List<IJsonMessagePoster> JsonMessagePosters => _jsonMsgPosters;

        public ConcurrentDictionary<string, Scalar> _objectColors = new();

        public AnalysisPipeline(IConfiguration config)
        {
            Log.Information("Create analysis pipeline...");

            LoadAllSettings(config);

            RegisterComponents();

            _slideWindow = new VideoFrameSlideWindow(_pipeLineSettings.FrameLifetime);
            _slideWindow.SetPublisher(_provider.GetRequiredService<IPublisher<FrameExpiredEvent>>());
            _slideWindow.SetPublisher(_provider.GetRequiredService<IPublisher<ObjectExpiredEvent>>());

            _analyzedFrameBuffer = new VideoFrameBuffer(_pipeLineSettings.FrameLifetime);

            Log.Information("Analysis pipeline created.");
        }

        private void LoadAllSettings(IConfiguration config)
        {
            _pipeLineSettings = config.GetSection("Pipeline").Get<PipelineSettings>();

            _mediaLoaderSettings = config.GetSection("MediaLoader").Get<MediaLoaderSettings>();
            _detectorSettings = config.GetSection("Detector").Get<DetectorSettings>();
            _trackerSettings = config.GetSection("Tracker").Get<TrackerSettings>();
            _regionManagerSettings = config.GetSection("RegionManager").Get<RegionManagerSettings>();
            _snapshotSettings = config.GetSection("Snapshot").Get<SnapshotSettings>();

            _analysisHandlerSettings = config.GetSection("AnalysisHandlers").Get<List<AnalysisHandlerSettings>>();
            _jsonMsgPosterSettings = config.GetSection("MessagePoster").Get<List<JsonMsgPosterSettings>>();
        }

        private void RegisterComponents()
        {
            _services = new ServiceCollection();

            _services.AddMessagePipe();

            var mediaLoader = CreateInstance<IVideoLoader>(
                _mediaLoaderSettings.AssemblyFile, _mediaLoaderSettings.FullQualifiedClassName,
                new object?[] { _pipeLineSettings.DeviceName, _mediaLoaderSettings.BufferSize, _mediaLoaderSettings.Preferences });
            _services.AddTransient<IVideoLoader>(sp => mediaLoader);

            var detector = CreateInstance<IObjectDetector>(
                _detectorSettings.AssemblyFile, _detectorSettings.FullQualifiedClassName);
            _services.AddTransient<IObjectDetector>(sp => detector);

            var regionManager = CreateInstance<IRegionManager>(
                _regionManagerSettings.AssemblyFile, _regionManagerSettings.FullQualifiedClassName);
            _services.AddTransient<IRegionManager>(sp => regionManager);

            var tracker = CreateInstance<IObjectTracker>(
                _trackerSettings.AssemblyFile, _trackerSettings.FullQualifiedClassName,
                new object?[] { _trackerSettings.IouThreshold, _trackerSettings.MaxMisses, _trackerSettings.Preferences });
            _services.AddTransient<IObjectTracker>(sp => tracker);

            var snapshot = CreateInstance<ISnapshotManager>(
                _snapshotSettings.AssemblyFile, _snapshotSettings.FullQualifiedClassName, new object?[] { _snapshotSettings.Preferences });
            _services.AddTransient<ISnapshotManager>(sp => snapshot);

            foreach (var setting in _analysisHandlerSettings)
            {
                var handler = CreateInstance<IAnalysisHandler>(setting.AssemblyFile, setting.FullQualifiedClassName,
                    new object?[] { this, setting.Preferences });
                _services.AddTransient<IAnalysisHandler>(sp => handler);
            }

            foreach (var setting in _jsonMsgPosterSettings)
            {
                var jsonMsgPoster = CreateInstance<IJsonMessagePoster>(setting.AssemblyFile, setting.FullQualifiedClassName,
                    new object?[] { setting.DestinationUrl, setting.Preferences });
                _services.AddTransient<IJsonMessagePoster>(sp => jsonMsgPoster);
            }

            _provider = _services.BuildServiceProvider();

            Log.Information("Register base components successfully.");
        }

        private static T CreateInstance<T>(string assemblyFile, string fullQualifiedClassName, object?[] parameters = null)
        {
            Assembly assembly = Assembly.LoadFrom(assemblyFile);
            Type type = assembly.GetType(fullQualifiedClassName);

            T instance = default;
            if (parameters == null)
            {
                instance = (T)Activator.CreateInstance(type);
            }
            else
            {
                instance = (T)Activator.CreateInstance(type, parameters);
            }

            return instance;
        }

        public void Run()
        {
            InitializeComponents();

            var videoTask = Task.Run(() =>
            {
                Log.Information($"Open video source: {_videoLoader.Specs.Uri}");
                _videoLoader.Play(_mediaLoaderSettings.VideoStride);
            });

            var analysisTask = Task.Run(() =>
            {
                Log.Information($"Begin analysis process...");
                while (!videoTask.IsCompleted || _videoLoader.BufferedFrameCount != 0)
                {
                    var frame = _videoLoader.RetrieveFrame();
                    if (frame == null)
                    {
                        continue;
                    }

                    frame.AddDetectedObjects(_objectDetector.Detect(frame.Scene, _detectorSettings.Thresh));
                    _regionManager.CalcRegionProperties(frame.DetectedObjects);
                    _objectTracker.Track(frame.Scene, frame.DetectedObjects);
                    _snapshotManager.ProcessSnapshots(frame);
                    var analyzedFrame = Analyze(frame);
                    PushAanlysisResults(analyzedFrame);
                }
            });

            var displayTask = Task.Run(() =>
            {
                if (_pipeLineSettings.EnableDebugDisplay)
                {
                    while (!videoTask.IsCompleted || _analyzedFrameBuffer.Count != 0)
                    {
                        DebugDisplay(_analyzedFrameBuffer.Dequeue());
                    }
                }
            });

            Task.WaitAll(analysisTask, videoTask, displayTask);

            Log.Information($"Analysis complete.");
        }

        private void InitializeComponents()
        {
            // 耗时组件优先于视频加载器初始化，以防止视频解码被延迟导致错误.
            _objectDetector = _provider.GetService<IObjectDetector>();
            _objectDetector.Init(new Dictionary<string, string>() {
                {"model_path", _detectorSettings.ModelPath},
                {"model_config", _detectorSettings.ModelConfig},
                {"use_cuda", _detectorSettings.UseCuda.ToString()},
                {"gpu_id", _detectorSettings.GpuId.ToString()},
                {"target_types", _detectorSettings.TargetTypes}},
                _detectorSettings.Preferences);

            _videoLoader = _provider.GetService<IVideoLoader>();
            _videoLoader.Open(_pipeLineSettings.Uri);

            _regionManager = _provider.GetService<IRegionManager>();
            _regionManager.LoadAnalysisDefinition(_regionManagerSettings.DefinitionFilePath,
                _videoLoader.Width, _videoLoader.Height);

            _objectTracker = _provider.GetService<IObjectTracker>();

            _snapshotManager = _provider.GetService<ISnapshotManager>();

            _jsonMsgPosters = _provider.GetServices<IJsonMessagePoster>().ToList();

            _analysisHandlers = _provider.GetServices<IAnalysisHandler>().ToList();

            _analysisHandlers.ForEach(handler => handler.SetServiceProvider(_provider));

            // 以下开始绑定事件
            var objectExpiredSubscriber = _provider.GetRequiredService<ISubscriber<ObjectExpiredEvent>>();
            var frameExpiredSubscriber = _provider.GetRequiredService<ISubscriber<FrameExpiredEvent>>();

            _regionManager.SetSubscriber(objectExpiredSubscriber);

            // 最后再由_snapshot 组件处理对象和帧过期事件, 以防止分析过程中截图被清理
            _snapshotManager.SetSubscriber(frameExpiredSubscriber);
            _snapshotManager.SetSubscriber(objectExpiredSubscriber);

            this.SetSubscriber(objectExpiredSubscriber);
            this.SetSubscriber(frameExpiredSubscriber);

            Log.Information("Initialize base components successfully.");
        }

        private Frame Analyze(Frame frame)
        {
            foreach (IAnalysisHandler handler in _analysisHandlers)
            {
                var analysisResult = handler.Analyze(frame);

                // TODO with result.
            }

            _slideWindow.AddNewFrame(frame);

            return frame;
        }

        private void PushAanlysisResults(Frame analyzedFrame)
        {
            if (analyzedFrame == null)
            {
                return;
            }

            _analyzedFrameBuffer.Enqueue(analyzedFrame);
        }

        private void DebugDisplay(Frame analyzedFrame)
        {
            if (analyzedFrame == null)
            {
                return;
            }

            DisplayDefinitions(analyzedFrame);

            // Select one display pattern.
            // DisplayBasicResults(analyzedFrame);
            // DisplayObjectDensityResults(analyzedFrame);
            DisplayRegionAccessResults(analyzedFrame);
            // DisplaySmugglingResult(analyzedFrame);
            // DisplayTrajectory(analyzedFrame);

            var definition = _regionManager.AnalysisDefinition;
            foreach (var analysisArea in definition.AnalysisAreas)
            {
                var roi = new Rect(analysisArea.Points[0].OriginalX, analysisArea.Points[0].OriginalY,
                    analysisArea.Points[2].OriginalX - analysisArea.Points[0].OriginalX,
                    analysisArea.Points[2].OriginalY - analysisArea.Points[0].OriginalY);
            
                Mat roiImage = new Mat(analyzedFrame.Scene, roi);
            
                Cv2.ImShow("test", roiImage);
            }

            //Cv2.ImShow("test", analyzedFrame.Scene.Resize(new Size(1920, 1080)));
            Cv2.WaitKey(1);
        }
        
        private void DisplayDefinitions(Frame analyzedFrame)
        {
            var definition = _regionManager.AnalysisDefinition;

            foreach (var analysisArea in definition.AnalysisAreas)
            {
                DrawRegion(analysisArea, analyzedFrame.Scene, Scalar.Lime);
            }
            //
            // foreach (var excludedArea in definition.ExcludedAreas)
            // {
            //     DrawRegion(excludedArea, analyzedFrame.Scene, Scalar.Red);
            // }
            //
            // foreach (var lane in definition.Lanes)
            // {
            //     DrawRegion(lane, analyzedFrame.Scene, Scalar.Yellow);
            // }
            //
            foreach (var interestArea in definition.InterestAreas)
            {
                DrawRegion(interestArea, analyzedFrame.Scene, Scalar.Yellow);
                var centerPoint = interestArea.GetCenterNormalizedPoint();
                analyzedFrame.Scene.PutText(interestArea.Name, new Point(centerPoint.OriginalX - 30, centerPoint.OriginalY - 30), HersheyFonts.HersheyPlain, 2.0, Scalar.Yellow);
            }
            //
            // foreach (var countLine in definition.CountLines)
            // {
            //     DrawLine(countLine.Item1, analyzedFrame.Scene, Scalar.Black);
            //     DrawLine(countLine.Item2, analyzedFrame.Scene, Scalar.Black);
            // }
        }

        private void DisplayTrajectory(Frame analyzedFrame)
        {
            var image = analyzedFrame.Scene;

            foreach (var detectedObject in analyzedFrame.DetectedObjects)
            {
                if (!detectedObject.IsUnderAnalysis)
                {
                    continue;
                }

                var bbox = detectedObject.Bbox;
                image.Rectangle(new Point(bbox.X, bbox.Y), new Point(bbox.X + bbox.Width, bbox.Y + bbox.Height), Scalar.Aqua);
                //image.Circle(new Point(bbox.CenterX, bbox.CenterY), 5, Scalar.Aqua);
            }

            var trajectories = analyzedFrame.GetProperty<ConcurrentDictionary<string, Queue<Point>>>("trajectory");
            if (trajectories != null)
            {
                foreach (var trajectory in trajectories)
                {
                    var color = Scalar.RandomColor();

                    if (_objectColors.ContainsKey(trajectory.Key))
                    {
                        color = _objectColors[trajectory.Key];
                    }
                    else
                    {
                        _objectColors.TryAdd(trajectory.Key, color);
                    }

                    image.Polylines(new List<IEnumerable<Point>>() { trajectory.Value }, false, color);
                }
            }
        }

        private void DisplaySmugglingResult(Frame analyzedFrame)
        {
            var image = analyzedFrame.Scene;

            foreach (var detectedObject in analyzedFrame.DetectedObjects)
            {
                if (!detectedObject.IsUnderAnalysis)
                {
                    continue;
                }
                
                var bbox = detectedObject.Bbox;

                Scalar boxColor = new Scalar(0, 255, 0);    // 默认绿色边框

                if (detectedObject.Label == "boat")
                {
                    image.Rectangle(new Point(bbox.X, bbox.Y), new Point(bbox.X + bbox.Width, bbox.Y + bbox.Height), boxColor);
                }

                if (detectedObject.Label == "person")
                {
                    image.Circle(new Point(bbox.CenterX, bbox.CenterY), 5, Scalar.Aqua);
                }
            }

            // Display gatherings.
            var gatherings = analyzedFrame.GetProperty<List<ObjectGroup>>("gatherings");
            if (gatherings != null)
            {
                foreach (var gathering in gatherings)
                {
                    var bbox = gathering.Bbox;
                    var persons = gathering.GroupObjects;
                    var isFlee = gathering.GetProperty<bool>("isFlee");

                    if (isFlee)
                    {
                        image.Rectangle(new Point(bbox.X, bbox.Y), new Point(bbox.X + bbox.Width, bbox.Y + bbox.Height), Scalar.Crimson);
                        image.PutText(persons.Count.ToString(), new Point(bbox.CenterX, bbox.CenterY), HersheyFonts.HersheyPlain, 3.0, Scalar.Crimson);
                    }
                    else
                    {
                        //image.Rectangle(new Point(bbox.X, bbox.Y), new Point(bbox.X + bbox.Width, bbox.Y + bbox.Height), Scalar.Lime);
                        //image.PutText(persons.Count.ToString(), new Point(bbox.CenterX, bbox.CenterY), HersheyFonts.HersheyPlain, 3.0, Scalar.Lime);
                    }
                }
            }

            // Display people gathering status.
            bool isPeopleGathering = analyzedFrame.GetProperty<bool>("people_gathering");
            if (isPeopleGathering)
            {
                image.PutText("Crowd", new Point(image.Width - 150, image.Height / 5), HersheyFonts.HersheyPlain, 2.0, Scalar.Crimson);
            }

            // Display boat existence status.
            bool isBoatExistence = analyzedFrame.GetProperty<bool>("boat_existence");
            if (isBoatExistence)
            {
                image.PutText("Boat", new Point(image.Width - 150, image.Height / 5 + 50), HersheyFonts.HersheyPlain, 2.0, Scalar.Crimson);
            }

            // Display people away from boat status.
            var isPeopleAwayFromBoat = analyzedFrame.GetProperty<bool>("people_away_from_boat");
            if (isPeopleAwayFromBoat)
            {
                image.PutText("Flee", new Point(image.Width - 150, image.Height / 5 + 100), HersheyFonts.HersheyPlain, 2.0, Scalar.Crimson);
            }

            if (isBoatExistence && isPeopleGathering && isPeopleAwayFromBoat)
            {
                image.PutText("Smuggling", new Point((image.Width / 2) - 100, 50), HersheyFonts.HersheyPlain, 3.0, Scalar.Crimson);
            }
        }

        private void DisplayBasicResults(Frame analyzedFrame)
        {
            foreach (var detectedObject in analyzedFrame.DetectedObjects)
            {
                if (!detectedObject.IsUnderAnalysis)
                {
                    continue;
                }

                var image = analyzedFrame.Scene;
                var bbox = detectedObject.Bbox;

                Scalar boxColor = new Scalar(0, 255, 0);    // 默认绿色边框

                // Display box for all objects.
                image.Rectangle(new Point(bbox.X, bbox.Y), new Point(bbox.X + bbox.Width, bbox.Y + bbox.Height), boxColor);

                // Display id.
                image.PutText(detectedObject.Id, new Point(bbox.X, bbox.Y - 20), HersheyFonts.HersheyPlain, 2.0, boxColor);

                // Display lane
                //image.PutText("L:" + detectedObject.LaneIndex.ToString(), new Point(bbox.X + 20, bbox.Y - 20), HersheyFonts.HersheyPlain, 1.0, Scalar.Red);
            }
        }

        private void DisplayObjectDensityResults(Frame analyzedFrame)
        {
            var definition = _regionManager.AnalysisDefinition;

            // 定义透明度
            double alpha = 0.4; // 边框透明度
            double textAlpha = 0.8; // 文本透明度

            // 创建一个与原始图像相同大小的覆盖图层，初始化为全透明
            using Mat overlay = new Mat(analyzedFrame.Scene.Size(), analyzedFrame.Scene.Type(), Scalar.All(0));

            // 遍历检测到的对象
            foreach (var detectedObject in analyzedFrame.DetectedObjects)
            {
                if (!detectedObject.IsUnderAnalysis)
                {
                    continue;
                }

                var bbox = detectedObject.Bbox;

                // 定义边框颜色和厚度
                Scalar boxColor = new Scalar(255, 255, 0); // 绿色边框
                int boxThickness = 1;

                // 在覆盖图层上绘制矩形
                Cv2.Rectangle(overlay, new Point(bbox.X, bbox.Y), new Point(bbox.X + bbox.Width, bbox.Y + bbox.Height), boxColor, boxThickness, LineTypes.AntiAlias);

                // 定义文本内容和属性
                string label = $"{detectedObject.Id}";
                // string laneText = $"L: {detectedObject.LaneIndex}";

                // 定义字体参数
                var fontFace = HersheyFonts.HersheyComplexSmall;
                double fontScale = 1;
                int fontThickness = 1;
                int baseline = 0;

                // 计算文本大小以绘制背景矩形
                Size textSize = Cv2.GetTextSize(label, fontFace, fontScale, fontThickness, out baseline);
                // Size laneTextSize = Cv2.GetTextSize(laneText, fontFace, fontScale, fontThickness, out baseline);

                // 定义文本背景矩形
                // Rect textBgRect = new Rect(bbox.X, bbox.Y - textSize.Height - 10, textSize.Width + 10, textSize.Height + 5);
                // Rect laneTextBgRect = new Rect(bbox.X + 80, bbox.Y - laneTextSize.Height - 10, laneTextSize.Width + 10, laneTextSize.Height + 5);

                // 绘制半透明背景矩形
                // Cv2.Rectangle(overlay, textBgRect, boxColor, -1);
                // Cv2.Rectangle(overlay, laneTextBgRect, boxColor, -1);

                // 在覆盖图层上绘制文本
                Cv2.PutText(overlay, label, new Point(bbox.X, bbox.Y - 5), fontFace, fontScale, Scalar.White, fontThickness, LineTypes.AntiAlias);
                // Cv2.PutText(overlay, laneText, new Point(bbox.X + 85, bbox.Y - 5), fontFace, fontScale, Scalar.White, fontThickness, LineTypes.AntiAlias);
            }

            // Draw specified area for debug
            foreach (var analysisArea in definition.AnalysisAreas)
            {
                var counting = analyzedFrame.GetProperty<int>("counting");
                if (counting != null)
                {
                    var areaPoint = analysisArea.Points[1];

                    if (counting < 10)
                    {
                        DrawRegion(analysisArea, analyzedFrame.Scene, Scalar.Lime);
                        Cv2.PutText(analyzedFrame.Scene, counting.ToString(), new Point(areaPoint.OriginalX - 20, areaPoint.OriginalY - 10), HersheyFonts.HersheyPlain, 1.5, Scalar.Lime);
                    }
                    else
                    {
                        DrawRegion(analysisArea, analyzedFrame.Scene, Scalar.Crimson);
                        Cv2.PutText(analyzedFrame.Scene, counting.ToString(), new Point(areaPoint.OriginalX - 20, areaPoint.OriginalY - 10), HersheyFonts.HersheyPlain, 1.5, Scalar.Crimson);
                    }
                }
            }

            // 将覆盖图层与原始图像混合
            Cv2.AddWeighted(overlay, alpha, analyzedFrame.Scene, 1.0, 0, analyzedFrame.Scene);
        }

        private void DisplayRegionAccessResults(Frame analyzedFrame)
        {
            foreach (var detectedObject in analyzedFrame.DetectedObjects)
            {
                if (!detectedObject.IsUnderAnalysis)
                {
                    continue;
                }
            
                var image = analyzedFrame.Scene;
                var bbox = detectedObject.Bbox;

                Scalar boxColor = new Scalar(0, 255, 0);    // 默认绿色边框

                // Display id.
                //image.PutText(detectedObject.TrackingId.ToString(), new Point(bbox.X, bbox.Y - 20), HersheyFonts.HersheyPlain, 2.0, boxColor);

                if (detectedObject.GetProperty<bool>("EnterRegion"))
                {
                    boxColor = new Scalar(0, 0, 255); // 红色边框
                    // Display id.
                    image.PutText($"Entering", new Point(bbox.X, bbox.Y - 20), HersheyFonts.HersheyPlain, 2.0, boxColor);
                }

                if (detectedObject.GetProperty<bool>("InRegion"))
                {
                    boxColor = new Scalar(255, 0, 255); // 紫色边框
                    // Display id.
                    image.PutText($"In Region", new Point(bbox.X, bbox.Y - 20), HersheyFonts.HersheyPlain, 2.0, boxColor);
                }

                if (detectedObject.GetProperty<bool>("LeaveRegion"))
                {
                    boxColor = new Scalar(255, 0, 0); // 蓝色边框
                    // Display id.
                    image.PutText($"Leaving", new Point(bbox.X, bbox.Y - 20), HersheyFonts.HersheyPlain, 2.0, boxColor);
                }

                // Display box for all objects.
                image.Rectangle(new Point(bbox.X, bbox.Y), new Point(bbox.X + bbox.Width, bbox.Y + bbox.Height), boxColor);
            }
        }
        
        private static void DrawRegion(NormalizedPolygon region, Mat frame, Scalar color)
        {
            List<Point> points = new List<Point>();
            foreach (NormalizedPoint normalizedPoint in region.Points)
            {
                var point = new Point(normalizedPoint.OriginalX, normalizedPoint.OriginalY);
                points.Add(point);
            }

            List<IEnumerable<Point>> allPoints = new List<IEnumerable<Point>>();
            allPoints.Add(points);

            frame.Polylines(allPoints, true, color);
        }

        private static void DrawLine(NormalizedLine line, Mat frame, Scalar color)
        {
            Point start = new Point(line.Start.OriginalX, line.Start.OriginalY);
            Point stop = new Point(line.Stop.OriginalX, line.Stop.OriginalY);

            frame.Line(start, stop, color);
        }

        public override void ProcessEvent(ObjectExpiredEvent @event)
        {
            if (_objectColors.ContainsKey(@event.Id))
            {
                _objectColors.TryRemove(@event.Id, out var _);
            }
        }

        public void Dispose()
        {
            _slideWindow.Dispose();
            _provider.Dispose();
        }
    }
}
