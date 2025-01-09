using OpenCvSharp;
using SentinelCore.Domain.Abstractions.MediaLoader;
using SentinelCore.Domain.DataStructures;
using SentinelCore.Domain.Entities.VideoStream;
using Serilog;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace MediaLoader.FFMpeg.IPC
{
    public class VideoLoader : IVideoLoader
    {
        private readonly string _deviceId;

        private string _ffprobePath;
        private string _ffprobeParams;

        private string _ffmpegPath;
        private string _ffmpegParams;

        private VideoSpecs _videoSpecs;

        private readonly IConcurrentBoundedQueue<Frame> _frameBuffer;

        private string _uri;
        private bool _isOpened;
        private volatile bool _isInPlaying;
        private long _index = 1;

        private CancellationTokenSource _cancellationTokenSource;
        
        public string DeviceId => _deviceId;
        public VideoSpecs Specs => _videoSpecs;
        public int Width => _videoSpecs.Width;
        public int Height => _videoSpecs.Height;
        public bool IsOpened => _isOpened;
        public bool IsInPlaying => _isInPlaying;

        public int BufferedFrameCount => _frameBuffer.Count;
        public int BufferedMaxOccupied => _frameBuffer.MaxOccupied;

        private bool _disposed = false;

        public VideoLoader(string deviceId, int bufferSize, Dictionary<string, string> preferences = null)
        {
            Log.Information($"Initialize FFMpeg IPC video capture...");

            if (string.IsNullOrWhiteSpace(deviceId))
                throw new ArgumentException("Device Id cannot be null or empty.", nameof(deviceId));

            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be positive.");

            _deviceId = deviceId;

            _videoSpecs = new VideoSpecs(string.Empty, 0, 0, 0, 0);
            _frameBuffer = new ConcurrentBoundedQueue<Frame>(bufferSize);

            _ffprobePath = "ffprobe";
            _ffmpegPath = "ffmpeg";

            _cancellationTokenSource = new CancellationTokenSource();
        }
        
        public void Open(string uri)
        {
            _uri = uri;
            _ffprobeParams = $"-v quiet -print_format json -show_streams \"{_uri}\"";
            
            var startInfo = new ProcessStartInfo()
            {
                FileName = _ffprobePath,
                Arguments = _ffprobeParams,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                string message = $"Probe video souce failed. Error: {error}";
                Log.Error(message);
                throw new ApplicationException(message);
            }

            using JsonDocument json = JsonDocument.Parse(output);
            // 获取格式信息（包括 duration）
            double duration = 0;
            if (json.RootElement.TryGetProperty("format", out JsonElement formatElement))
            {
                if (formatElement.TryGetProperty("duration", out JsonElement durationElement))
                {
                    double.TryParse(durationElement.GetString(), out duration);
                }
            }

            foreach (var stream in json.RootElement.GetProperty("streams").EnumerateArray())
            {
                // 只处理视频流
                if (stream.GetProperty("codec_type").GetString() == "video")
                {
                    int width = stream.GetProperty("width").GetInt32();
                    int height = stream.GetProperty("height").GetInt32();

                    // 有些视频文件的帧率信息在 "r_frame_rate" 或 "avg_frame_rate" 字段中
                    string frameRateStr = stream.GetProperty("r_frame_rate").GetString();

                    // 计算帧率
                    double frameRate = 0;
                    if (frameRateStr.Contains("/"))
                    {
                        var parts = frameRateStr.Split('/');
                        if (parts.Length == 2 && double.TryParse(parts[0], out double numerator) && double.TryParse(parts[1], out double denominator) && denominator != 0)
                        {
                            frameRate = numerator / denominator;
                        }
                    }
                    else
                    {
                        double.TryParse(frameRateStr, out frameRate);
                    }

                    // 尝试获取nb_frames
                    long frameCount = 0;
                    if (stream.TryGetProperty("nb_frames", out JsonElement nbFramesElement))
                    {
                        long.TryParse(nbFramesElement.GetString(), out frameCount);
                    }
                    else
                    {
                        // 如果nb_frames不可用，使用duration和帧率计算
                        frameCount = (long)(duration * frameRate);
                    }

                    _videoSpecs = new VideoSpecs(_uri, width, height, frameRate, (int)frameCount);
                    break;
                }
            }

            _isOpened = true;
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
            _isOpened = false;
        }

        public void Play(int stride = 1, bool debugMode = false, int debugFrameCount = 0)
        {
            if (!IsOpened)
            {
                throw new ApplicationException($"Stream source not opened yet.");
            }

            // FFmpeg 命令配置
            _ffmpegParams =
                $"-fflags +discardcorrupt -i \"{_uri}\" -rtsp_transport tcp -buffer_size 1024000 -f image2pipe -pix_fmt bgr24 -vcodec rawvideo -preset veryfast -tune zerolatency -an -";

            var startInfo = new ProcessStartInfo()
            {
                FileName = _ffmpegPath,
                Arguments = _ffmpegParams,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var token = _cancellationTokenSource.Token;
            using var process = new Process { StartInfo = startInfo };
            _isInPlaying = true;

            process.Start();

            using var stream = process.StandardOutput.BaseStream;
            int frameSize = _videoSpecs.Width * _videoSpecs.Height * 3; // 每帧的字节大小 (BGR24)

            byte[] buffer = new byte[frameSize];
            while (_isInPlaying && !token.IsCancellationRequested)
            {
                #region retrive specified amount of frame for debug
                if (debugMode && debugFrameCount-- <= 0)
                {
                    break;
                }
                #endregion

                int bytesRead = 0;

                // 读取完整的帧数据
                while (bytesRead < frameSize)
                {
                    int read = stream.Read(buffer, bytesRead, frameSize - bytesRead);
                    if (read == 0)
                    {
                        _cancellationTokenSource?.Cancel();
                        break;
                    }
                    bytesRead += read;
                }

                // 将读取的字节数组转换为 Mat
                using var image = Mat.FromPixelData(_videoSpecs.Height, _videoSpecs.Width, MatType.CV_8UC3, buffer);

                var frame = new Frame(_deviceId, _index++, 0, image);
                _frameBuffer.Enqueue(frame);
            }

            Close();
        }

        public void Stop()
        {
            if (!IsOpened)
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
            }

            _disposed = true;
        }

        ~VideoLoader()
        {
            Dispose(false);
        }
    }
}
