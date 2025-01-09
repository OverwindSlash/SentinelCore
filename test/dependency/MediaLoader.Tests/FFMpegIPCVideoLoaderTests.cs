using MediaLoader.FFMpeg.IPC;

namespace MediaLoader.Tests
{
    public class FFMpegIPCVideoLoaderTests
    {
        private const string LocalVideoPath = @"Video/video1.avi";
        private const string LanRtspVideoUrl = @"rtsp://admin:CS%40202304@192.168.1.151:554/Streaming/Channels/101?transportmode=unicast&profile=Profile_1";
        private const string WanRtspVideoUrl = @"rtsp://stream.strba.sk:1935/strba/VYHLAD_JAZERO.stream";
        private readonly string _rtspVideoUrl = LanRtspVideoUrl;

        [Test]
        public void Test_Open_VideoFile()
        {
            int bufferSize = 100;
            using var loader = new VideoLoader("tempId", bufferSize);
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
            using var loader = new VideoLoader("tempId", bufferSize);
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
    }
}
