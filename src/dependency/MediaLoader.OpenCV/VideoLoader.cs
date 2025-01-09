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
    private readonly VideoCaptureAPIs _videoCaptureApIs;
    private readonly VideoCapturePara _videoCapturePara;
    private VideoSpecs _videoSpecs;
    
    private readonly IConcurrentBoundedQueue<Frame> _frameBuffer;

    private string _uri;
    private volatile bool _isInPlaying;
    private long _index = 1;

    private CancellationTokenSource _cancellationTokenSource;

    public string DeviceId => _deviceId;
    public VideoSpecs Specs => _videoSpecs;
    public int Width => _videoSpecs.Width;
    public int Height => _videoSpecs.Height;
    public bool IsOpened => _capture.IsOpened();
    public bool IsInPlaying => _isInPlaying;

    public int BufferedFrameCount => _frameBuffer.Count;
    public int BufferedMaxOccupied => _frameBuffer.MaxOccupied;

    private bool _disposed = false;

    private int _retryCount = 0;
    private const int MaxRetries = 5;
    private const int RetryDelayMs = 1000;

    public VideoLoader(string deviceId, int bufferSize, Dictionary<string, string> preferences = null)
    {
        Log.Information($"Initialize OpenCV video capture...");

        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device Id cannot be null or empty.", nameof(deviceId));

        if (bufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be positive.");

        _deviceId = deviceId;

        _capture = new VideoCapture();
        _videoCaptureApIs = VideoCaptureAPIs.FFMPEG;
        _videoCapturePara = new VideoCapturePara(VideoAccelerationType.D3D11, 0);
        _videoSpecs = new VideoSpecs(string.Empty, 0, 0, 0, 0);

        Log.Information($"OpenCV video capture using capture api: {nameof(VideoCaptureAPIs.FFMPEG)} " +
                        $"acceleration type: {nameof(VideoAccelerationType.D3D11)}.");

        _frameBuffer = new ConcurrentBoundedQueue<Frame>(bufferSize);
    }

    public void Open(string uri)
    {
        Close();

        _capture = new VideoCapture(uri, _videoCaptureApIs, _videoCapturePara);
        if (!_capture.IsOpened())
        {
            string message = $"Stream source '{uri}' can not be opened.";
            Log.Error(message);
            throw new ApplicationException(message);
        }

        _uri = uri;
        _videoSpecs = new VideoSpecs(_uri, _capture.FrameWidth, _capture.FrameHeight,
            _capture.Fps, _capture.FrameCount);

        ResetPlayStatus();
    }

    private void ResetPlayStatus()
    {
        _frameBuffer.Clear();
        _isInPlaying = false;
        _index = 1;
    }

    public void Close()
    {
        _cancellationTokenSource?.Cancel();

        if (_capture.IsOpened())
        {
            CleanUpCapture();
        }
    }

    private void CleanUpCapture()
    {
        _capture.Release();
    }

    public void Play(int stride = 1, bool debugMode = false, int debugFrameCount = 0)
    {
        if (!_capture.IsOpened())
        {
            throw new ApplicationException($"Stream source not opened yet.");
        }

        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;
        _isInPlaying = true;

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
                // End of video file.
                if (_capture.FrameCount > 0 && _index > _capture.FrameCount)
                {
                    Stop();
                    break;
                }

                // Reconnect video streaming.
                if (_retryCount++ < MaxRetries)
                {
                    Log.Warning($"Video source grab failed. Attempting to reconnect {_retryCount}/{MaxRetries}.");

                    CleanUpCapture();

                    // fast retry when error first occur.
                    if (_retryCount != 0)
                    {
                        Thread.Sleep(RetryDelayMs);
                    }
                    _capture = new VideoCapture(_uri, _videoCaptureApIs, _videoCapturePara);
                    continue;   // re-grab
                }
                else
                {
                    Log.Error("Maximum reconnection attempts reached. Stopping video streaming.");
                    Stop();
                    break;
                }
            }

            _retryCount = 0;

            if (_index++ % stride != 0)
            {
                continue;
            }

            var image = new Mat();
            if (!_capture.Retrieve(image))
            {
                Log.Warning("Retrieve image failed. Skip this frame.");
                continue;
            }

            if (image.Width == 0 || image.Height == 0)
            {
                Log.Warning("Image invalid. Skip this frame.");
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
            return;
        }

        _cancellationTokenSource?.Cancel();
        ResetPlayStatus();
    }

    public Frame RetrieveFrame()
    {
        return _frameBuffer.Dequeue();
    }

    public async Task<Frame> RetrieveFrameAsync()
    {
        return await _frameBuffer.DequeueAsync();
    }

    // public void Dispose()
    // {
    //     Close();
    //     _capture.Dispose();
    // }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            Close();
            _capture?.Dispose();
        }

        _disposed = true;
    }

    ~VideoLoader()
    {
        Dispose(false);
    }
}