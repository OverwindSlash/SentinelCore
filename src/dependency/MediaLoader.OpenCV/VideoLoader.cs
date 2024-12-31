using OpenCvSharp;
using SentinelCore.Domain.Abstractions.MediaLoader;
using SentinelCore.Domain.DataStructures;
using SentinelCore.Domain.Entities.VideoStream;
using Serilog;
using System.Diagnostics;

namespace MediaLoader.OpenCV;

public class VideoLoader : IVideoLoader
{
    private readonly string _deviceId;
    private VideoCapture _capture;
    private VideoSpecs _videoSpecs;
    private VideoCapturePara _videoCapturePara;
    private readonly IConcurrentBoundedQueue<Frame> _frameBuffer;
    private string _uri;

    private volatile bool _isInPlaying;
    private long _index;

    private readonly VideoCaptureAPIs _videoCaptureApIs;

    private CancellationTokenSource _cancellationTokenSource;

    public VideoLoader()
        : this("tempId", 100)
    { }

    public VideoLoader(string deviceId, int bufferSize)
    {
        _deviceId = deviceId;
        _capture = new VideoCapture();
        _videoCapturePara = new VideoCapturePara(VideoAccelerationType.Any, 0);
        _videoSpecs = new VideoSpecs(string.Empty, 0, 0, 0, 0);
        _frameBuffer = new ConcurrentBoundedQueue<Frame>(bufferSize);
        _isInPlaying = false;
        _index = 0;

        _videoCaptureApIs = VideoCaptureAPIs.FFMPEG;
    }

    public string DeviceId => _deviceId;
    public int Width => _videoSpecs.Width;
    public int Height => _videoSpecs.Height;
    public bool IsOpened => _capture.IsOpened();
    public bool IsInPlaying => _isInPlaying;
    public VideoSpecs Specs => _videoSpecs;
    public int BufferedFrameCount => _frameBuffer.Count;       
    public int BufferedMaxOccupied => _frameBuffer.MaxOccupied;

    public void Open(string uri)
    {
        Close();

        _capture = new VideoCapture(uri, _videoCaptureApIs);
        if (!_capture.IsOpened())
        {
            throw new ApplicationException($"Stream source '{uri}' not available.");
        }

        _videoSpecs = new VideoSpecs(uri, _capture.FrameWidth, _capture.FrameHeight,
            _capture.Fps, _capture.FrameCount);

        _frameBuffer.Clear();
        _isInPlaying = false;
        _index = 0;
        _uri = uri;
    }

    public void Close()
    {
        if (_capture != null && _capture.IsOpened())
        {
            _capture.Release();
        }
    }

    public void Play(int stride = 1, bool debugMode = false, int debugFrameCount = 0)
    {
        if (!_capture.IsOpened())
        {
            throw new ApplicationException($"Stream source not opened.");
        }

        _isInPlaying = true;
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        var stopwatch = Stopwatch.StartNew();

        while (_isInPlaying && !token.IsCancellationRequested)
        {
            #region retrive specified amount of frame for debug
            if (debugMode && debugFrameCount-- <= 0)
            {
                break;
            }
            #endregion

            if (!_capture.Grab())
            {
                if (_index > _capture.FrameCount)
                {
                    break;
                }
                
                // Reconnect
                _capture.Release();
                _capture = new VideoCapture(_uri, _videoCaptureApIs);
                continue;
            }

            if (_index++ % stride != 0)
            {
                continue;
            }

            var image = new Mat();
            if (!_capture.Retrieve(image))
            {
                continue;
            }

            if (image.Width == 0 || image.Height == 0)
            {
                continue;
            }

            var frameId = (long)_capture.Get(VideoCaptureProperties.PosFrames);
            var offsetMilliSec = (long)_capture.Get(VideoCaptureProperties.PosMsec);

            long elapsedTimeMs = stopwatch.ElapsedMilliseconds;
            var sleepMilliSec = (int)Math.Min(100, offsetMilliSec - elapsedTimeMs);

            Log.Debug($"FM:{offsetMilliSec} EM:{elapsedTimeMs} Diff:{offsetMilliSec - elapsedTimeMs}");

            if (sleepMilliSec > 0)
            {
                Thread.Sleep(sleepMilliSec);
            }

            var frame = new Frame(_deviceId, frameId, offsetMilliSec, image);
            _frameBuffer.Enqueue(frame);

            Log.Debug($"Buffer frame count:{_frameBuffer.Count}");
        }

        Close();
    }

    public void Stop()
    {
        if (!_capture.IsOpened())
        {
            throw new ApplicationException($"Stream source not opened.");
        }

        _isInPlaying = false;
        _cancellationTokenSource?.Cancel();
    }

    public Frame RetrieveFrame()
    {
        return _frameBuffer.Dequeue();
    }

    public async Task<Frame> RetrieveFrameAsync()
    {
        return await _frameBuffer.DequeueAsync();
    }

    public void Dispose()
    {
        Close();
        _capture.Dispose();
    }
}