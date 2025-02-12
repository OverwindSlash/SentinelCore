using MediaLoader.FFMpeg;
using SentinelCore.Domain.Entities.VideoStream;

namespace MediaLoader.Tests
{
    public class FFMpegVideoLoaderTests
    {
        private const string LocalVideoPath = @"Video/video1.avi";
        private const string LanRtspVideoUrl = @"rtsp://admin:CS%40202304@192.168.1.151:554/Streaming/Channels/101?transportmode=unicast&profile=Profile_1";
        private const string WanRtspVideoUrl = @"rtsp://stream.strba.sk:1935/strba/VYHLAD_JAZERO.stream";
        private string _rtspVideoUrl = LanRtspVideoUrl;

        private Dictionary<string, string> preferences = new Dictionary<string, string>();

        public FFMpegVideoLoaderTests()
        {
            preferences.Add("LibrariesPath", "runtimes\\win-x64\\ffmpeg");
            //preferences.Add("LibrariesPath", "/usr/lib/x86_64-linux-gnu");
        }

        [Test]
        public void Test_ProvideAndConsume_InDifferentThread()
        {
            int bufferSize = 100;
            using var loader = new VideoLoader("tempId", bufferSize, preferences);
            loader.Open(LocalVideoPath);

            // Provider
            var providerTask = Task.Run(() => { loader.Play(); });

            // Consumer
            var consumerTask = Task.Run(() =>
            {
                int frameCount = 1;
                int frameInterval = (int)(1000 / loader.Specs.Fps);
                while (loader.BufferedFrameCount != 0 || loader.IsOpened)
                {
                    using var frame = loader.RetrieveFrame();
                    if (frame != null)
                    {
                        Assert.That(frame.FrameId, Is.EqualTo(frameCount));
                        frameCount++;

                        // Cv2.ImShow("test", frame.Scene);
                        // Cv2.WaitKey(frameInterval);
                    }
                }
            });

            // Keep thread running until video ends.
            Task.WaitAll(providerTask, consumerTask);

            Console.WriteLine($"Max Occupied:{loader.BufferedMaxOccupied}");
        }

        [Test]
        public void Test_ProvideAndConsumeAsync_InDifferentThread()
        {
            int bufferSize = 100;
            using var loader = new VideoLoader("tempId", bufferSize, preferences);
            loader.Open(LocalVideoPath);

            // Provider
            var playTask = Task.Run(() => { loader.Play(); });

            // Consumer
            var showTask = Task.Run(async () =>
            {
                int frameCount = 1;
                int frameInterval = (int)(1000 / loader.Specs.Fps);
                while (loader.BufferedFrameCount != 0 || loader.IsOpened)
                {
                    using var frame = loader.RetrieveFrame();
                    if (frame != null)
                    {
                        Assert.That(frame.FrameId, Is.EqualTo(frameCount));
                        frameCount++;

                        // Cv2.ImShow("test", frame.Scene);
                        // Cv2.WaitKey(frameInterval);
                    }
                }
            });

            // Keep thread running until video ends.
            Task.WaitAll(playTask, showTask);

            Console.WriteLine($"Max Occupied:{loader.BufferedMaxOccupied}");
        }

        [Test]
        public void Test_Open_VideoFile()
        {
            int bufferSize = 100;
            using var loader = new VideoLoader("tempId", bufferSize, preferences);
            loader.Open(LocalVideoPath);

            var specs = loader.Specs;
            Assert.That(specs, Is.Not.Null);
            Assert.That(specs.Uri, Is.EqualTo(LocalVideoPath));
            Assert.That(specs.Width, Is.EqualTo(902));
            Assert.That(specs.Height, Is.EqualTo(666));
            Assert.That(specs.Fps, Is.EqualTo(30.0f).Within(0.4));
            Assert.That(specs.FrameCount, Is.EqualTo(151));
        }

        [Test]
        public async Task Test_Play_VideoFile_QueueNotFull()
        {
            int bufferSize = 50;
            using var loader = new VideoLoader("tempId", bufferSize, preferences);
            loader.Open(LocalVideoPath);

            int stride = 1;
            loader.Play(stride, true, bufferSize - 1);

            var consumerTask = Task.Run(() =>
            {
                int frameCount = 0;
                while (loader.BufferedFrameCount != 0 || loader.IsOpened)
                {
                    using var frame = loader.RetrieveFrame();
                    Assert.That(frame, Is.Not.Null);
                    Assert.That(frame.FrameId, Is.EqualTo(frameCount + 1));

                    frameCount++;
                }

                Assert.That(frameCount, Is.EqualTo(bufferSize - 1));
            });

            // Keep thread running until video ends.
            Task.WaitAll(consumerTask);

            Console.WriteLine($"Max Occupied:{loader.BufferedMaxOccupied}");
        }

        [Test]
        public async Task Test_Play_VideoFile_QueueFull()
        {
            int bufferSize = 50;
            using var loader = new VideoLoader("tempId", bufferSize, preferences);
            loader.Open(LocalVideoPath);

            int stride = 1;
            int exceedCount = 0;
            loader.Play(stride, true, bufferSize + exceedCount);

            var consumerTask = Task.Run(async () =>
            {
                int retrievedFrameCount = 0;
                while (loader.BufferedFrameCount != 0 || loader.IsOpened)
                {
                    using var frame = await loader.RetrieveFrameAsync();
                    Assert.That(frame, Is.Not.Null);
                    Assert.That(frame.FrameId, Is.EqualTo(1 + exceedCount + retrievedFrameCount));

                    retrievedFrameCount++;
                }

                Assert.That(retrievedFrameCount, Is.EqualTo(bufferSize));
            });

            // Keep thread running until video ends.
            Task.WaitAll(consumerTask);

            Console.WriteLine($"Max Occupied:{loader.BufferedMaxOccupied}");
        }

        [Test]
        public async Task Test_Play_VideoFile_QueueFull_PlusOne()
        {
            int bufferSize = 50;
            using var loader = new VideoLoader("tempId", bufferSize, preferences);
            loader.Open(LocalVideoPath);

            int stride = 1;
            int exceedCount = 1;
            loader.Play(stride, true, bufferSize + exceedCount);

            var consumerTask = Task.Run(async () =>
            {
                int retrievedFrameCount = 0;
                while (loader.BufferedFrameCount != 0 || loader.IsOpened)
                {
                    using var frame = await loader.RetrieveFrameAsync();
                    Assert.That(frame, Is.Not.Null);
                    Assert.That(frame.FrameId, Is.EqualTo(1 + exceedCount + retrievedFrameCount));

                    retrievedFrameCount++;
                }

                Assert.That(retrievedFrameCount, Is.EqualTo(bufferSize));
            });

            // Keep thread running until video ends.
            Task.WaitAll(consumerTask);

            Console.WriteLine($"Max Occupied:{loader.BufferedMaxOccupied}");
        }

        [Test]
        public async Task Test_Play_VideoFile_QueueFullTwice()
        {
            int bufferSize = 50;
            using var loader = new VideoLoader("tempId", bufferSize, preferences);
            loader.Open(LocalVideoPath);

            int stride = 1;
            int exceedCount = bufferSize;
            loader.Play(stride, true, bufferSize + exceedCount);

            var consumerTask = Task.Run(async () =>
            {
                int retrievedFrameCount = 0;
                while (loader.BufferedFrameCount != 0 || loader.IsOpened)
                {
                    using var frame = await loader.RetrieveFrameAsync();
                    Assert.That(frame, Is.Not.Null);
                    Assert.That(frame.FrameId, Is.EqualTo(1 + exceedCount + retrievedFrameCount));

                    retrievedFrameCount++;
                }

                Assert.That(retrievedFrameCount, Is.EqualTo(bufferSize));
            });

            // Keep thread running until video ends.
            Task.WaitAll(consumerTask);

            Console.WriteLine($"Max Occupied:{loader.BufferedMaxOccupied}");
        }

        [Test]
        public async Task Test_Play_VideoFile_QueueFullTwice_PlusOne()
        {
            int bufferSize = 50;
            using var loader = new VideoLoader("tempId", bufferSize, preferences);
            loader.Open(LocalVideoPath);

            int stride = 1;
            int exceedCount = bufferSize + 1;
            loader.Play(stride, true, bufferSize + exceedCount);

            var consumerTask = Task.Run(async () =>
            {
                int retrievedFrameCount = 0;
                while (loader.BufferedFrameCount != 0 || loader.IsOpened)
                {
                    using var frame = await loader.RetrieveFrameAsync();
                    Assert.That(frame, Is.Not.Null);
                    Assert.That(frame.FrameId, Is.EqualTo(1 + exceedCount + retrievedFrameCount));

                    retrievedFrameCount++;
                }

                Assert.That(retrievedFrameCount, Is.EqualTo(bufferSize));
            });

            // Keep thread running until video ends.
            Task.WaitAll(consumerTask);

            Console.WriteLine($"Max Occupied:{loader.BufferedMaxOccupied}");
        }

        [Test]
        public async Task Test_Play_VideoFile_Stride2()
        {
            int bufferSize = 50;
            using var loader = new VideoLoader("tempId", bufferSize, preferences);
            loader.Open(LocalVideoPath);

            int stride = 2;
            var providerTask = Task.Run(() => { loader.Play(stride, true, bufferSize); });

            var consumerTask = Task.Run(async () =>
            {
                int frameCount = 0;
                while (loader.BufferedFrameCount != 0 || loader.IsOpened)
                {
                    using var frame = await loader.RetrieveFrameAsync();
                    if (frame == null)
                    {
                        continue;
                    }

                    Assert.That(frame.FrameId, Is.EqualTo(frameCount * stride + 1));

                    frameCount++;
                }

                Assert.That(frameCount, Is.EqualTo(bufferSize / 2));
            });

            // Keep thread running until video ends.
            Task.WaitAll(providerTask, consumerTask);

            Console.WriteLine($"Max Occupied:{loader.BufferedMaxOccupied}");
        }

        [Test]
        public async Task Test_Play_RtspStream()
        {
            int bufferSize = 50;
            using var loader = new VideoLoader("tempId", bufferSize, preferences);
            loader.Open(_rtspVideoUrl);

            loader.Play(1, true, bufferSize);

            var bufferedFrameCount = loader.BufferedFrameCount;
            for (int i = 1; i <= bufferedFrameCount; i++)
            {
                var frame = await loader.RetrieveFrameAsync();
                Assert.That(frame, Is.Not.Null);
                Assert.That(frame.FrameId, Is.EqualTo(i));
            }
        }

        [Test]
        public async Task Test_Play_VideoFile_QueueFull_CheckTimeOffset()
        {
            int bufferSize = 50;
            using var loader = new VideoLoader("tempId", bufferSize, preferences);
            loader.Open(LocalVideoPath);

            var videoStartTimeStamp = DateTime.Now;
            loader.Play(1, true, bufferSize);

            Frame lastFrame = null;
            var consumerTask = Task.Run(async () =>
            {
                for (int i = 0; i < loader.BufferedFrameCount; i++)
                {
                    lastFrame = await loader.RetrieveFrameAsync();
                }
            });

            // Keep thread running until video ends.
            Task.WaitAll(consumerTask);

            if (lastFrame != null)
            {
                var elapsedTime = lastFrame.TimeStamp - videoStartTimeStamp;
                long diff = Math.Abs(lastFrame.OffsetMilliSec - (long)elapsedTime.TotalMilliseconds);
                int frameInterval = (int)(1000 / loader.Specs.Fps);
                Assert.That(diff <= frameInterval);
            }
            else
            {
                Assert.Fail("Video can not be played.");
            }
        }

        [Test]
        public async Task Test_Play_RtspStream_CheckTimeOffset()
        {
            int intervalThresh = 3;

            int bufferSize = 50;
            using var loader = new VideoLoader("tempId", bufferSize, preferences);
            loader.Open(_rtspVideoUrl);

            var videoStartTimeStamp = DateTime.Now;
            loader.Play(1, true, bufferSize);

            Frame lastFrame = null;
            var consumerTask = Task.Run(async () =>
            {
                for (int i = 0; i < loader.BufferedFrameCount; i++)
                {
                    lastFrame = await loader.RetrieveFrameAsync();
                }
            });

            // Keep thread running until video ends.
            Task.WaitAll(consumerTask);

            if (lastFrame != null)
            {
                var elapsedTime = lastFrame.TimeStamp - videoStartTimeStamp;
                long diff = Math.Abs(lastFrame.OffsetMilliSec - (long)elapsedTime.TotalMilliseconds);
                int frameInterval = (int)(1000 / loader.Specs.Fps);
                Console.WriteLine(diff);
                Assert.That(diff <= intervalThresh * frameInterval);
            }
            else
            {
                Assert.Fail("Video can not be played.");
            }
        }
    }
}
