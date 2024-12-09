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
using SentinelCore.Domain.Entities.VideoStream;
using SentinelCore.Domain.Events.AnalysisEngine;
using SentinelCore.Service.Pipeline.Settings;
using System.Reflection;

namespace SentinelCore.Service.Pipeline
{
    public class AnalysisPipeline : IDisposable
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
        public ISnapshotManager SnapshotManager => _snapshotManager;
        public List<IJsonMessagePoster> JsonMessagePosters => _jsonMsgPosters;

        public AnalysisPipeline(IConfiguration config)
        {
            LoadAllSettings(config);

            RegisterComponents();

            _slideWindow = new VideoFrameSlideWindow(_pipeLineSettings.FrameLifetime);
            _slideWindow.SetPublisher(_provider.GetRequiredService<IPublisher<FrameExpiredEvent>>());
            _slideWindow.SetPublisher(_provider.GetRequiredService<IPublisher<ObjectExpiredEvent>>());

            _analyzedFrameBuffer = new VideoFrameBuffer(_pipeLineSettings.FrameLifetime);
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
                new object?[] { _pipeLineSettings.DeviceName, int.Parse(_mediaLoaderSettings.Parameters[0]) });
            _services.AddTransient<IVideoLoader>(sp => mediaLoader);

            var detector = CreateInstance<IObjectDetector>(
                _detectorSettings.AssemblyFile, _detectorSettings.FullQualifiedClassName);
            _services.AddTransient<IObjectDetector>(sp => detector);

            var regionManager = CreateInstance<IRegionManager>(
                _regionManagerSettings.AssemblyFile, _regionManagerSettings.FullQualifiedClassName);
            _services.AddTransient<IRegionManager>(sp => regionManager);

            var tracker = CreateInstance<IObjectTracker>(
                _trackerSettings.AssemblyFile, _trackerSettings.FullQualifiedClassName,
                new object?[] { float.Parse(_trackerSettings.Parameters[0]), int.Parse(_trackerSettings.Parameters[1]) });
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
                _videoLoader.Play(_mediaLoaderSettings.VideoStride);
            });

            var analysisTask = Task.Run(() =>
            {
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
        }

        private void InitializeComponents()
        {
            _videoLoader = _provider.GetService<IVideoLoader>();
            _videoLoader.Open(_pipeLineSettings.Uri);

            _objectDetector = _provider.GetService<IObjectDetector>();
            _objectDetector.Init(new Dictionary<string, string>() {
                {"model_path", _detectorSettings.ModelPath},
                {"model_config", _detectorSettings.ModelConfig},
                {"use_cuda", _detectorSettings.UseCuda.ToString()},
                {"gpu_id", _detectorSettings.GpuId.ToString()}
            });

            _regionManager = _provider.GetService<IRegionManager>();
            
            _regionManager.LoadAnalysisDefinition(_regionManagerSettings.Parameters[0],
                _videoLoader.Width, _videoLoader.Height);

            _objectTracker = _provider.GetService<IObjectTracker>();

            _snapshotManager = _provider.GetService<ISnapshotManager>();

            _jsonMsgPosters = _provider.GetServices<IJsonMessagePoster>().ToList();

            _analysisHandlers = _provider.GetServices<IAnalysisHandler>().ToList();

            _analysisHandlers.ForEach(handler => handler.SetServiceProvider(_provider));

            // 以下开始绑定事件
            _regionManager.SetSubscriber(_provider.GetRequiredService<ISubscriber<ObjectExpiredEvent>>());

            // 最后再由_snapshot 组件处理对象和帧过期事件, 以防止分析过程中截图被清理
            _snapshotManager.SetSubscriber(_provider.GetRequiredService<ISubscriber<FrameExpiredEvent>>());
            _snapshotManager.SetSubscriber(_provider.GetRequiredService<ISubscriber<ObjectExpiredEvent>>());
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

            // Draw specified area for debug
            // DrawRegion(_regionManager.AnalysisDefinition.AnalysisAreas[0], analyzedFrame.Scene, Scalar.Green);
            // DrawRegion(_regionManager.AnalysisDefinition.ExcludedAreas[0], analyzedFrame.Scene, Scalar.Red);
            // DrawRegion(_regionManager.AnalysisDefinition.ExcludedAreas[1], analyzedFrame.Scene, Scalar.Red);
            // DrawRegion(_regionManager.AnalysisDefinition.ExcludedAreas[2], analyzedFrame.Scene, Scalar.Red);
            // DrawRegion(_regionManager.AnalysisDefinition.Lanes[0], analyzedFrame.Scene, Scalar.Yellow);
            // DrawRegion(_regionManager.AnalysisDefinition.Lanes[1], analyzedFrame.Scene, Scalar.Yellow);
            // DrawLine(_regionManager.AnalysisDefinition.CountLines[0].Item1, analyzedFrame.Scene, Scalar.Black);

            // Debug Display
            foreach (var detectedObject in analyzedFrame.DetectedObjects)
            {
                if (!detectedObject.IsUnderAnalysis)
                {
                    continue;
                }

                var image = analyzedFrame.Scene;
                var bbox = detectedObject.Bbox;

                // Display box for all objects.
                image.Rectangle(new Point(bbox.X, bbox.Y), new Point(bbox.X + bbox.Width, bbox.Y + bbox.Height), Scalar.Red);

                // Display id.
                image.PutText(detectedObject.Id, new Point(bbox.X, bbox.Y - 20), HersheyFonts.HersheyPlain, 1.0, Scalar.Red);

                // Display lane
                //image.PutText("L:" + detectedObject.LaneIndex.ToString(), new Point(bbox.X + 20, bbox.Y - 20), HersheyFonts.HersheyPlain, 1.0, Scalar.Red);
            }

            Cv2.ImShow("test", analyzedFrame.Scene.Resize(new Size(1920, 1080)));
            Cv2.WaitKey(1);
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

        public void Dispose()
        {
            _slideWindow.Dispose();
            _provider.Dispose();
        }
    }
}
