using FFmpeg.AutoGen.Abstractions;
using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;
using OpenCvSharp;
using SentinelCore.Domain.Abstractions.MediaLoader;
using SentinelCore.Domain.DataStructures;
using SentinelCore.Domain.Entities.VideoStream;
using Serilog;
using System.Diagnostics;

namespace MediaLoader.FFMpeg
{
    public unsafe class VideoLoader : IVideoLoader
    {
        private string _deviceId;

        private VideoSpecs _videoSpecs;

        private readonly IConcurrentBoundedQueue<Frame> _frameBuffer;

        private string _uri;
        private bool _isOpened;
        private volatile bool _isInPlaying;
        private long _index;

        private AVFormatContext* _formatContext;
        private AVCodecContext* _codecContext;
        private AVStream* _stream;
        private AVFrame* _frame;
        private AVPacket* _packet;
        private int _videoStreamIndex = -1;
        private SwsContext* _swsContext;
        private readonly bool _useHardwareAcceleration;

        private CancellationTokenSource _cancellationTokenSource;

        public string DeviceId => _deviceId;
        public VideoSpecs Specs => _videoSpecs;
        public int Width => _videoSpecs.Width;
        public int Height => _videoSpecs.Height;
        public bool IsOpened => _isOpened;
        public bool IsInPlaying => _isInPlaying;

        public int BufferedFrameCount => _frameBuffer.Count;
        public int BufferedMaxOccupied => _frameBuffer.MaxOccupied;

        public double FrameRate { get; set; }
        public long FrameCount { get; set; }
        public long FrameMilliSec { get; set; }

        private bool _disposed = false;

        public VideoLoader(string deviceId, int bufferSize)
        {
            Log.Information($"Initialize FFMpeg video capture...");

            if (string.IsNullOrWhiteSpace(deviceId))
                throw new ArgumentException("Device Id cannot be null or empty.", nameof(deviceId));

            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be positive.");

            _deviceId = deviceId;

            _videoSpecs = new VideoSpecs(string.Empty, 0, 0, 0, 0);
            _frameBuffer = new ConcurrentBoundedQueue<Frame>(bufferSize);

            DynamicallyLoadedBindings.LibrariesPath = @"runtimes\win-x64\ffmpeg";
            DynamicallyLoadedBindings.Initialize();

            _useHardwareAcceleration = true;
        }

        public void Open(string uri)
        {
            Close();

            // AVDictionary* options = null;
            //
            // // 设置传输协议为 TCP
            // ffmpeg.av_dict_set(&options, "rtsp_transport", "tcp", 0);
            //
            // // 设置最大延迟为 0.5 秒（500000 微秒）
            // ffmpeg.av_dict_set(&options, "max_delay", "500000", 0);
            //
            // // 设置缓冲区大小为 4MB
            // ffmpeg.av_dict_set(&options, "buffer_size", "4194304", 0);
            //
            // // 设置分析时长为 1 秒，减少打开文件的延迟
            // ffmpeg.av_dict_set(&options, "analyzeduration", "1000000", 0);
            // ffmpeg.av_dict_set(&options, "probesize", "1000000", 0);
            //
            // // 启用自动重连
            // ffmpeg.av_dict_set(&options, "reconnect", "1", 0);
            //
            // // 设置重连尝试次数
            // ffmpeg.av_dict_set(&options, "reconnect_streamed", "1", 0);
            // ffmpeg.av_dict_set(&options, "reconnect_delay_max", "10", 0); // 最长重连延迟 10 秒

            // Open video file or stream
            _formatContext = ffmpeg.avformat_alloc_context();
            fixed (AVFormatContext** pFormatContext = &_formatContext)
            {
                if (ffmpeg.avformat_open_input(pFormatContext, uri, null, null) != 0)
                {
                    string message = $"Stream source '{uri}' can not be opened.";
                    Log.Error(message);
                    throw new ApplicationException(message);
                }
            }

            // ffmpeg.av_dict_free(&options);

            // Retrieve stream information
            if (ffmpeg.avformat_find_stream_info(_formatContext, null) < 0)
            {
                string message = $"Could not find stream information.";
                Log.Error(message);
                throw new ApplicationException(message);
            }

            // Find the video stream
            for (int i = 0; i < _formatContext->nb_streams; i++)
            {
                if (_formatContext->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    _videoStreamIndex = i;
                    break;
                }
            }

            if (_videoStreamIndex == -1)
            {
                string message = $"Could not find video stream.";
                Log.Error(message);
                throw new ApplicationException(message);
            }

            // Get video stream
            _stream = _formatContext->streams[_videoStreamIndex];

            // Get frame rate（avg_frame_rate）
            FrameRate = ffmpeg.av_q2d(_stream->avg_frame_rate); // 转换为浮动数字
            if (double.IsNaN(FrameRate))
            {
                FrameRate = 25;
            }

            // Get frame count（nb_frames）
            FrameCount = _stream->nb_frames;

            // Get codec and create context
            var codecParams = _stream->codecpar;
            var codec = ffmpeg.avcodec_find_decoder(codecParams->codec_id);

            if (codec == null)
            {
                string message = $"Unsupported codec.";
                Log.Error(message);
                throw new ApplicationException(message);
            }

            _codecContext = ffmpeg.avcodec_alloc_context3(codec);
            if (ffmpeg.avcodec_parameters_to_context(_codecContext, codecParams) < 0)
            {
                string message = $"Could not copy codec parameters.";
                Log.Error(message);
                throw new ApplicationException(message);
            }

            // Enable hardware acceleration if required
            if (_useHardwareAcceleration)
            {
                _codecContext->hw_device_ctx = ffmpeg.av_hwdevice_ctx_alloc(AVHWDeviceType.AV_HWDEVICE_TYPE_OPENCL);
                Log.Information($"FFMpeg video capture using hardware acceleration type: {nameof(AVHWDeviceType.AV_HWDEVICE_TYPE_OPENCL)}");
            }

            // Open codec
            if (ffmpeg.avcodec_open2(_codecContext, codec, null) < 0)
            {
                string message = $"Could not open codec.";
                Log.Error(message);
                throw new ApplicationException(message);
            }

            _uri = uri;
            _videoSpecs = new VideoSpecs(_uri, _codecContext->width, _codecContext->height, FrameRate, (int)FrameCount);
            _isOpened = true;

            ResetPlayStatus();
        }

        private void ResetPlayStatus()
        {
            _frameBuffer.Clear();
            _isInPlaying = false;
            _index = 0;
        }

        public void Close()
        {
            _cancellationTokenSource?.Cancel();
            _isOpened = false;
        }
        
        public void Play(int stride = 1, bool debugMode = false, int debugFrameCount = 0)
        {
            if (!_isOpened)
            {
                throw new Exception($"Stream source not opened yet.");
            }

            // Prepare video frame and packet
            _frame = ffmpeg.av_frame_alloc();
            _packet = ffmpeg.av_packet_alloc();
            
            // Initialize the scaling context
            _swsContext = ffmpeg.sws_getContext(
                _codecContext->width, _codecContext->height, _codecContext->pix_fmt,
                _codecContext->width, _codecContext->height, AVPixelFormat.AV_PIX_FMT_BGR24,
                ffmpeg.SWS_LANCZOS, null, null, null
            );
            
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;
            _isInPlaying = true;

            var stopwatch = Stopwatch.StartNew();
            var frameId = 0;
            int interval = (int)(1000 / FrameRate);

            Mat matImage = GetNextFrame();
            while (_isInPlaying && !token.IsCancellationRequested)
            {
                #region retrive specified amount of frame for debug
                if (debugMode && debugFrameCount-- <= 0)
                {
                    break;
                }
                #endregion

                if (frameId++ % stride != 0)
                {
                    continue;
                }

                long elapsedTimeMs = stopwatch.ElapsedMilliseconds;

                Log.Debug($"FM:{FrameMilliSec} EM:{elapsedTimeMs} Diff:{FrameMilliSec - elapsedTimeMs}");

                var sleepMilliSec = (int)Math.Min(100, FrameMilliSec - elapsedTimeMs);
                if (sleepMilliSec > 0)
                {
                    Thread.Sleep(sleepMilliSec);
                }

                if (matImage.Width != 0)
                {
                    var frame = new Frame(_deviceId, frameId, FrameMilliSec, matImage);
                    _frameBuffer.Enqueue(frame);
                }

                // Continue get frame.
                matImage = GetNextFrame();

                Log.Debug($"Buffer frame count:{_frameBuffer.Count}");
            }

            Close();
        }

        private Mat GetNextFrame()
        {
            while (ffmpeg.av_read_frame(_formatContext, _packet) >= 0)
            {
                if (_packet->stream_index == _videoStreamIndex)
                {
                    if (ffmpeg.avcodec_send_packet(_codecContext, _packet) < 0)
                        continue;

                    if (ffmpeg.avcodec_receive_frame(_codecContext, _frame) == 0)
                    {
                        // 计算当前帧的毫秒偏移量
                        FrameMilliSec = (long)(_frame->pts * ffmpeg.av_q2d(_stream->time_base) * 1000);
                        //Console.WriteLine($"FM:{FrameMilliSec}");

                        Mat matImage = ConvertFrameToMat(_frame);
                        ffmpeg.av_packet_unref(_packet);

                        return matImage;
                    }
                }
                ffmpeg.av_packet_unref(_packet);
            }

            _cancellationTokenSource?.Cancel();
            ResetPlayStatus();

            return new Mat();
        }

        private Mat ConvertFrameToMat(AVFrame* frame)
        {
            var mat = new Mat(_codecContext->height, _codecContext->width, MatType.CV_8UC3);
            var data = new FFmpeg.AutoGen.byte_ptrArray4 { [0] = mat.DataPointer };
            var stride = new FFmpeg.AutoGen.int_array4 { [0] = (int)mat.Step(0) };

            ffmpeg.sws_scale(
                _swsContext,
                frame->data, frame->linesize, 0, _codecContext->height,
                data, stride
            );

            return mat;
        }

        public void Stop()
        {
            if (!_isOpened)
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

        public Task<Frame> RetrieveFrameAsync()
        {
            return _frameBuffer.DequeueAsync();
        }

        // public void Dispose()
        // {
        //     ffmpeg.sws_freeContext(_swsContext);
        //
        //     fixed (AVFrame** pFrame = &_frame)
        //     {
        //         ffmpeg.av_frame_free(pFrame);
        //     }
        //
        //     fixed (AVPacket** pPacket = &_packet)
        //     {
        //         ffmpeg.av_packet_free(pPacket);
        //     }
        //
        //     fixed (AVCodecContext** pCodecContext = &_codecContext)
        //     {
        //         ffmpeg.avcodec_free_context(pCodecContext);
        //     }
        //
        //     fixed (AVFormatContext** pFormatContext = &_formatContext)
        //     {
        //         ffmpeg.avformat_close_input(pFormatContext);
        //     }
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
                // 释放托管资源
                 Close();
            }

            // 释放非托管资源
            ffmpeg.sws_freeContext(_swsContext);

            fixed (AVCodecContext** pCodecContext = &_codecContext)
            {
                ffmpeg.avcodec_free_context(pCodecContext);
            }

            fixed (AVFrame** pFrame = &_frame)
            {
                ffmpeg.av_frame_free(pFrame);
            }

            fixed (AVPacket** pPacket = &_packet)
            {
                ffmpeg.av_packet_free(pPacket);
            }

            fixed (AVCodecContext** pCodecContext = &_codecContext)
            {
                ffmpeg.avcodec_free_context(pCodecContext);
            }

            fixed (AVFormatContext** pFormatContext = &_formatContext)
            {
                ffmpeg.avformat_close_input(pFormatContext);
            }

            _disposed = true;
        }

        ~VideoLoader()
        {
            Dispose(false);
        }
    }
}
