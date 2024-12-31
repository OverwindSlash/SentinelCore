using FFmpeg.AutoGen.Abstractions;
using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;
using OpenCvSharp;
using SentinelCore.Domain.Abstractions.MediaLoader;
using SentinelCore.Domain.DataStructures;
using SentinelCore.Domain.Entities.VideoStream;
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
        private bool _isInPlaying;
        private long _index;

        private AVFormatContext* formatContext;
        private AVCodecContext* codecContext;
        private AVFrame* frame;
        private AVPacket* packet;
        private int videoStreamIndex = -1;
        private SwsContext* swsContext;
        private bool useHardwareAcceleration;

        public string DeviceId => _deviceId;
        public int Width => _videoSpecs.Width;
        public int Height => _videoSpecs.Height;
        public bool IsOpened => _isOpened;
        public bool IsInPlaying => _isInPlaying;
        public VideoSpecs Specs => _videoSpecs;
        public int BufferedFrameCount => _frameBuffer.Count;
        public int BufferedMaxOccupied => _frameBuffer.MaxOccupied;

        public double FrameRate { get; set; }
        public long FrameCount { get; set; }
        public long FrameMilliSec { get; set; }

        public VideoLoader(string deviceId, int bufferSize)
        {
            _deviceId = deviceId;
            _videoSpecs = new VideoSpecs(string.Empty, 0, 0, 0, 0);
            _frameBuffer = new ConcurrentBoundedQueue<Frame>(bufferSize);
            _isInPlaying = false;
            _index = 0;

            DynamicallyLoadedBindings.LibrariesPath = @"runtimes\win-x64\ffmpeg";
            DynamicallyLoadedBindings.Initialize();

            useHardwareAcceleration = true;
        }

        public void Open(string uri)
        {
            Close();

            // Open video file or stream
            formatContext = ffmpeg.avformat_alloc_context();
            fixed (AVFormatContext** pFormatContext = &formatContext)
            {
                if (ffmpeg.avformat_open_input(pFormatContext, uri, null, null) != 0)
                    throw new ApplicationException("Could not open the input stream.");
            }

            // Retrieve stream information
            if (ffmpeg.avformat_find_stream_info(formatContext, null) < 0)
                throw new ApplicationException("Could not find stream information.");

            // Find the video stream
            for (int i = 0; i < formatContext->nb_streams; i++)
            {
                if (formatContext->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    videoStreamIndex = i;
                    break;
                }
            }

            if (videoStreamIndex == -1)
                throw new ApplicationException("Could not find video stream.");

            var stream = formatContext->streams[videoStreamIndex];

            // 获取帧率（avg_frame_rate）
            FrameRate = ffmpeg.av_q2d(stream->avg_frame_rate); // 转换为浮动数字
            if (double.IsNaN(FrameRate))
            {
                FrameRate = 25;
            }

            // 获取帧总数（nb_frames）
            FrameCount = stream->nb_frames;

            // Get codec and create context
            var codecParams = formatContext->streams[videoStreamIndex]->codecpar;
            var codec = ffmpeg.avcodec_find_decoder(codecParams->codec_id);

            if (codec == null)
                throw new ApplicationException("Unsupported codec.");

            codecContext = ffmpeg.avcodec_alloc_context3(codec);
            ffmpeg.avcodec_parameters_to_context(codecContext, codecParams);

            // Enable hardware acceleration if required
            if (useHardwareAcceleration)
            {
                // Replace with the appropriate hardware acceleration configuration (e.g., "cuda" or "dxva2")
                var hwDeviceType = ffmpeg.av_hwdevice_find_type_by_name("dxva2");
                var avhwDeviceType = ffmpeg.avcodec_get_hw_config(codec, 0)->device_type;
                if (avhwDeviceType == hwDeviceType)
                {
                    /*codecContext->hw_device_ctx = ffmpeg.av_hwdevice_ctx_alloc(hwDeviceType);
                    ffmpeg.av_hwdevice_ctx_init(codecContext->hw_device_ctx);*/
                }
                else
                {
                    Console.WriteLine("Hardware acceleration not supported for this codec on this device.");
                }
            }

            // Open codec
            if (ffmpeg.avcodec_open2(codecContext, codec, null) < 0)
                throw new ApplicationException("Could not open codec.");

            frame = ffmpeg.av_frame_alloc();
            packet = ffmpeg.av_packet_alloc();

            // Initialize the scaling context
            swsContext = ffmpeg.sws_getContext(
                codecContext->width, codecContext->height, codecContext->pix_fmt,
                codecContext->width, codecContext->height, AVPixelFormat.AV_PIX_FMT_BGR24,
                ffmpeg.SWS_BILINEAR, null, null, null
            );

            _videoSpecs = new VideoSpecs(uri, codecContext->width, codecContext->height, FrameRate, (int)FrameCount);

            _isOpened = true;
        }

        public void Close()
        {
            _isInPlaying = false;
            _isOpened = false;
        }
        
        public void Play(int stride = 1, bool debugMode = false, int debugFrameCount = 0)
        {
            if (!_isOpened)
            {
                throw new Exception($"Stream source not opened.");
            }

            _isInPlaying = true;

            var stopwatch = Stopwatch.StartNew();

            var frameId = 0;
            int interval = (int)(1000 / FrameRate);
            Mat matImage = GetNextFrame();

            while (_isInPlaying && matImage != null)
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
                //Console.WriteLine($"FM:{FrameMilliSec} EM:{elapsedTimeMs} Diff:{FrameMilliSec - elapsedTimeMs}");

                var sleepMilliSec = (int)Math.Min(100, FrameMilliSec - elapsedTimeMs);
                if (sleepMilliSec > 0)
                {
                    Thread.Sleep(sleepMilliSec);
                }

                var frame = new Frame(_deviceId, frameId, FrameMilliSec, matImage);
                _frameBuffer.Enqueue(frame);

                // Continue get frame.
                matImage = GetNextFrame();

                //Console.WriteLine(_frameBuffer.Count);
            }

            Close();
        }

        private Mat GetNextFrame()
        {
            while (ffmpeg.av_read_frame(formatContext, packet) >= 0)
            {
                if (packet->stream_index == videoStreamIndex)
                {
                    if (ffmpeg.avcodec_send_packet(codecContext, packet) < 0)
                        continue;

                    if (ffmpeg.avcodec_receive_frame(codecContext, frame) == 0)
                    {
                        // 计算当前帧的毫秒偏移量
                        FrameMilliSec = (long)(frame->pts * ffmpeg.av_q2d(formatContext->streams[videoStreamIndex]->time_base) * 1000);
                        //Console.WriteLine($"FM:{FrameMilliSec}");

                        Mat mat = ConvertFrameToMat(frame);
                        ffmpeg.av_packet_unref(packet);
                        return mat;
                    }
                }
                ffmpeg.av_packet_unref(packet);
            }
            return null;
        }

        private Mat ConvertFrameToMat(AVFrame* frame)
        {
            var mat = new Mat(codecContext->height, codecContext->width, MatType.CV_8UC3);
            var dstData = new FFmpeg.AutoGen.byte_ptrArray4 { [0] = mat.DataPointer };
            var dstLinesize = new FFmpeg.AutoGen.int_array4 { [0] = (int)mat.Step(0) };

            ffmpeg.sws_scale(
                swsContext,
                frame->data, frame->linesize, 0, codecContext->height,
                dstData, dstLinesize
            );

            return mat;
        }

        public void Stop()
        {
            if (!_isOpened)
            {
                throw new ApplicationException($"Stream source not opened.");
            }

            _isInPlaying = false;
        }

        public Frame RetrieveFrame()
        {
            return _frameBuffer.Dequeue();
        }

        public Task<Frame> RetrieveFrameAsync()
        {
            return _frameBuffer.DequeueAsync();
        }

        public void Dispose()
        {
            ffmpeg.sws_freeContext(swsContext);

            fixed (AVFrame** pFrame = &frame)
            {
                ffmpeg.av_frame_free(pFrame);
            }

            fixed (AVPacket** pPacket = &packet)
            {
                ffmpeg.av_packet_free(pPacket);
            }

            fixed (AVCodecContext** pCodecContext = &codecContext)
            {
                ffmpeg.avcodec_free_context(pCodecContext);
            }

            fixed (AVFormatContext** pFormatContext = &formatContext)
            {
                ffmpeg.avformat_close_input(pFormatContext);
            }
        }
    }
}
