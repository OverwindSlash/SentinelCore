using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenCvSharp;
using SentinelCore.Domain.Abstractions.EventHandler;
using SentinelCore.Domain.Entities.AnalysisEngine;
using SentinelCore.Domain.Entities.ObjectDetection;
using SentinelCore.Domain.Entities.VideoStream;
using SentinelCore.Domain.Events;
using SentinelCore.Domain.Events.AnalysisEngine;

namespace SentinelCore.Domain.Tests.AnalysisEngine;

public class VideoFrameSlideWindowTests
{
    private readonly IPublisher<FrameExpiredEvent> _frmExpiredPublisher;
    private readonly IPublisher<ObjectExpiredEvent> _objExpiredPublisher;
    
    private readonly ISubscriber<FrameExpiredEvent> _frmExpiredSubscriber1;
    private readonly ISubscriber<ObjectExpiredEvent> _objExpiredSubscriber1;

    private readonly ISubscriber<FrameExpiredEvent> _frmExpiredSubscriber2;
    private readonly ISubscriber<ObjectExpiredEvent> _objExpiredSubscriber2;

    private readonly ISubscriber<ObjectExpiredEvent> _objExpiredSubscriber3;
    private readonly ISubscriber<EventBase> _baseEventSubscriber;

    public VideoFrameSlideWindowTests()
    {
        var services = new ServiceCollection();
        services.AddMessagePipe();
        var provider = services.BuildServiceProvider();

        _frmExpiredPublisher = provider.GetRequiredService<IPublisher<FrameExpiredEvent>>();
        _objExpiredPublisher = provider.GetRequiredService<IPublisher<ObjectExpiredEvent>>();

        _frmExpiredSubscriber1 = provider.GetRequiredService<ISubscriber<FrameExpiredEvent>>();
        _objExpiredSubscriber1 = provider.GetRequiredService<ISubscriber<ObjectExpiredEvent>>();

        _frmExpiredSubscriber2 = provider.GetRequiredService<ISubscriber<FrameExpiredEvent>>();
        _objExpiredSubscriber2 = provider.GetRequiredService<ISubscriber<ObjectExpiredEvent>>();

        _objExpiredSubscriber3 = provider.GetRequiredService<ISubscriber<ObjectExpiredEvent>>();
        _baseEventSubscriber = provider.GetRequiredService<ISubscriber<EventBase>>();
    }

    [Test]
    public void TestVideoFrameBuffer_EnqueueOne()
    {
        int windowSize = 3;
        var slideWindow = new VideoFrameSlideWindow(windowSize);
        slideWindow.SetPublisher(_frmExpiredPublisher);
        slideWindow.SetPublisher(_objExpiredPublisher);

        // frame 1: car1
        var frame1 = CreateFrame1();

        slideWindow.AddNewFrame(frame1);

        var car1Frames = slideWindow.GetFramesContainObjectId("car:1");
        Assert.That(car1Frames.Count, Is.EqualTo(1));

        var car2Frames = slideWindow.GetFramesContainObjectId("car:2");
        Assert.That(car2Frames.Count, Is.EqualTo(0));

        var car1IsUnderTracking = slideWindow.IsObjIdAlive("car:1");
        Assert.That(car1IsUnderTracking, Is.EqualTo(true));

        var car2IsUnderTracking = slideWindow.IsObjIdAlive("car:2");
        Assert.That(car2IsUnderTracking, Is.EqualTo(false));
    }

    private static Frame CreateFrame1()
    {
        var frame1 = new Frame("tempId", 1, 0, new Mat());
        frame1.AddDetectedObjects(new List<BoundingBox>()
        {
            new BoundingBox(labelId: 2, label: "car", confidence: 0.8f,
                x: 100, y: 100, width: 100, height: 100)
            {
                TrackingId = 1
            }
        });
        return frame1;
    }

    [Test]
    public void TestVideoFrameBuffer_EnqueueFull()
    {
        int windowSize = 3;
        var slideWindow = new VideoFrameSlideWindow(windowSize);
        slideWindow.SetPublisher(_frmExpiredPublisher);
        slideWindow.SetPublisher(_objExpiredPublisher);

        // frame 1: car1
        var frame1 = CreateFrame1();

        // frame 2: car1, person1
        var frame2 = CreateFrame2();

        // frame 3: person1
        var frame3 = CreateFrame3();

        slideWindow.AddNewFrame(frame1);
        slideWindow.AddNewFrame(frame2);
        slideWindow.AddNewFrame(frame3);

        var car1Frames = slideWindow.GetFramesContainObjectId("car:1");
        Assert.That(car1Frames.Count, Is.EqualTo(2));

        var person1Frames = slideWindow.GetFramesContainObjectId("person:1");
        Assert.That(person1Frames.Count, Is.EqualTo(2));

        var car1IsUnderTracking = slideWindow.IsObjIdAlive("car:1");
        Assert.That(car1IsUnderTracking, Is.EqualTo(true));

        var person1IsUnderTracking = slideWindow.IsObjIdAlive("person:1");
        Assert.That(person1IsUnderTracking, Is.EqualTo(true));
    }

    private static Frame CreateFrame2()
    {
        var frame2 = new Frame("tempId", 2, 40, new Mat());
        frame2.AddDetectedObjects(new List<BoundingBox>()
        {
            // object 1
            new BoundingBox(labelId: 2, label: "car", confidence: 0.8f,
                x: 100, y: 150, width: 100, height: 100)
            {
                TrackingId = 1
            },

            // object 2
            new BoundingBox(labelId: 0, label: "person", confidence: 0.7f,
                x: 200, y: 200, width: 30, height: 100)
            {
                TrackingId = 1
            },
        });
        return frame2;
    }

    private static Frame CreateFrame3()
    {
        var frame3 = new Frame("tempId", 3, 80, new Mat());
        frame3.AddDetectedObjects(new List<BoundingBox>()
        {
            // object 2
            new BoundingBox(labelId: 0, label: "person", confidence: 0.7f,
                x: 250, y: 200, width: 30, height: 100)
            {
                TrackingId = 1
            },
        });
        return frame3;
    }

    [Test]
    public void TestVideoFrameBuffer_EnqueueMoreThan1()
    {
        int windowSize = 3;
        var slideWindow = new VideoFrameSlideWindow(windowSize);
        slideWindow.SetPublisher(_frmExpiredPublisher);
        slideWindow.SetPublisher(_objExpiredPublisher);

        // frame 1: car1
        var frame1 = CreateFrame1();

        // frame 2: car1, person1
        var frame2 = CreateFrame2();

        // frame 3: person1
        var frame3 = CreateFrame3();

        // frame 4: person1
        var frame4 = CreateFrame4();

        slideWindow.AddNewFrame(frame1);
        slideWindow.AddNewFrame(frame2);
        slideWindow.AddNewFrame(frame3);
        slideWindow.AddNewFrame(frame4);

        var car1Frames = slideWindow.GetFramesContainObjectId("car:1");
        Assert.That(car1Frames.Count, Is.EqualTo(1));

        var person1Frames = slideWindow.GetFramesContainObjectId("person:1");
        Assert.That(person1Frames.Count, Is.EqualTo(3));

        var car1IsUnderTracking = slideWindow.IsObjIdAlive("car:1");
        Assert.That(car1IsUnderTracking, Is.EqualTo(true));

        var person1IsUnderTracking = slideWindow.IsObjIdAlive("person:1");
        Assert.That(person1IsUnderTracking, Is.EqualTo(true));
    }

    private static Frame CreateFrame4()
    {
        var frame4 = new Frame("tempId", 4, 120, new Mat());
        frame4.AddDetectedObjects(new List<BoundingBox>()
        {
            // object 2
            new BoundingBox(labelId: 0, label: "person", confidence: 0.7f,
                x: 250, y: 250, width: 30, height: 100)
            {
                TrackingId = 1
            },
        });
        return frame4;
    }

    [Test]
    public void TestVideoFrameBuffer_EnqueueMoreThan2()
    {
        int windowSize = 3;
        var slideWindow = new VideoFrameSlideWindow(windowSize);
        slideWindow.SetPublisher(_frmExpiredPublisher);
        slideWindow.SetPublisher(_objExpiredPublisher);

        // frame 1: car1
        var frame1 = CreateFrame1();

        // frame 2: car1, person1
        var frame2 = CreateFrame2();

        // frame 3: person1
        var frame3 = CreateFrame3();

        // frame 4: person1
        var frame4 = CreateFrame4();

        // frame 5: person1, car2
        var frame5 = CreateFrame5();

        slideWindow.AddNewFrame(frame1);
        slideWindow.AddNewFrame(frame2);
        slideWindow.AddNewFrame(frame3);
        slideWindow.AddNewFrame(frame4);
        slideWindow.AddNewFrame(frame5);

        var car1Frames = slideWindow.GetFramesContainObjectId("car:1");
        Assert.That(car1Frames.Count, Is.EqualTo(0));

        var person1Frames = slideWindow.GetFramesContainObjectId("person:1");
        Assert.That(person1Frames.Count, Is.EqualTo(3));

        var car2Frames = slideWindow.GetFramesContainObjectId("car:2");
        Assert.That(car2Frames.Count, Is.EqualTo(1));

        var car1IsUnderTracking = slideWindow.IsObjIdAlive("car:1");
        Assert.That(car1IsUnderTracking, Is.EqualTo(false));

        var person1IsUnderTracking = slideWindow.IsObjIdAlive("person:1");
        Assert.That(person1IsUnderTracking, Is.EqualTo(true));

        var car2IsUnderTracking = slideWindow.IsObjIdAlive("car:2");
        Assert.That(car2IsUnderTracking, Is.EqualTo(true));
    }

    private static Frame CreateFrame5()
    {
        var frame5 = new Frame("tempId", 5, 160, new Mat());
        frame5.AddDetectedObjects(new List<BoundingBox>()
        {
            // object 2
            new BoundingBox(labelId: 0, label: "person", confidence: 0.7f,
                x: 250, y: 270, width: 30, height: 100)
            {
                TrackingId = 1
            },
            // object 3
            new BoundingBox(labelId: 2, label: "car", confidence: 0.8f,
                x: 50, y: 50, width: 130, height: 100)
            {
                TrackingId = 2
            },
        });
        return frame5;
    }

    [Test]
    public void TestVideoFrameBuffer_EnqueueMoreThan3()
    {
        int windowSize = 3;
        var slideWindow = new VideoFrameSlideWindow(windowSize);
        slideWindow.SetPublisher(_frmExpiredPublisher);
        slideWindow.SetPublisher(_objExpiredPublisher);

        // frame 1: car1
        var frame1 = CreateFrame1();

        // frame 2: car1, person1
        var frame2 = CreateFrame2();

        // frame 3: person1
        var frame3 = CreateFrame3();

        // frame 4: person1
        var frame4 = CreateFrame4();

        // frame 5: person1, car2
        var frame5 = CreateFrame5();

        // frame 6: car2
        var frame6 = CreateFrame6();

        slideWindow.AddNewFrame(frame1);
        slideWindow.AddNewFrame(frame2);
        slideWindow.AddNewFrame(frame3);
        slideWindow.AddNewFrame(frame4);
        slideWindow.AddNewFrame(frame5);
        slideWindow.AddNewFrame(frame6);

        var car1Frames = slideWindow.GetFramesContainObjectId("car:1");
        Assert.That(car1Frames.Count, Is.EqualTo(0));

        var person1Frames = slideWindow.GetFramesContainObjectId("person:1");
        Assert.That(person1Frames.Count, Is.EqualTo(2));

        var car2Frames = slideWindow.GetFramesContainObjectId("car:2");
        Assert.That(car2Frames.Count, Is.EqualTo(2));

        var car1IsUnderTracking = slideWindow.IsObjIdAlive("car:1");
        Assert.That(car1IsUnderTracking, Is.EqualTo(false));

        var person1IsUnderTracking = slideWindow.IsObjIdAlive("person:1");
        Assert.That(person1IsUnderTracking, Is.EqualTo(true));

        var car2IsUnderTracking = slideWindow.IsObjIdAlive("car:2");
        Assert.That(car2IsUnderTracking, Is.EqualTo(true));
    }

    private static Frame CreateFrame6()
    {
        var frame6 = new Frame("tempId", 5, 160, new Mat());
        frame6.AddDetectedObjects(new List<BoundingBox>()
        {
            // object 3
            new BoundingBox(labelId: 2, label: "car", confidence: 0.8f,
                x: 100, y: 100, width: 130, height: 100)
            {
                TrackingId = 2
            },
        });
        return frame6;
    }

    [Test]
    public void TestVideoFrameBuffer_EnqueueWithNoObject()
    {
        int windowSize = 3;
        var slideWindow = new VideoFrameSlideWindow(windowSize);
        slideWindow.SetPublisher(_frmExpiredPublisher);
        slideWindow.SetPublisher(_objExpiredPublisher);

        // frame 1: car1
        var frame1 = CreateFrame1();

        // frame 2: car1, person1
        var frame2 = CreateFrame2();

        // frame 3: person1
        var frame3 = CreateFrame3();

        // frame 4: person1
        var frame4 = CreateFrame4();

        // frame 5: person1, car2
        var frame5 = CreateFrame5();

        // frame 6: car2
        var frame6 = CreateFrame6();

        // frame 7: 
        var frame7 = new Frame("tempId", 7, 240, new Mat());
        frame7.AddDetectedObjects(new List<BoundingBox>());

        // frame 8: 
        var frame8 = new Frame("tempId", 7, 280, new Mat());
        frame8.AddDetectedObjects(new List<BoundingBox>());

        slideWindow.AddNewFrame(frame1);
        slideWindow.AddNewFrame(frame2);
        slideWindow.AddNewFrame(frame3);
        slideWindow.AddNewFrame(frame4);
        slideWindow.AddNewFrame(frame5);
        slideWindow.AddNewFrame(frame6);
        slideWindow.AddNewFrame(frame7);
        slideWindow.AddNewFrame(frame8);

        var car1Frames = slideWindow.GetFramesContainObjectId("car:1");
        Assert.That(car1Frames.Count, Is.EqualTo(0));

        var person1Frames = slideWindow.GetFramesContainObjectId("person:1");
        Assert.That(person1Frames.Count, Is.EqualTo(0));

        var car2Frames = slideWindow.GetFramesContainObjectId("car:2");
        Assert.That(car2Frames.Count, Is.EqualTo(1));

        var car1IsUnderTracking = slideWindow.IsObjIdAlive("car:1");
        Assert.That(car1IsUnderTracking, Is.EqualTo(false));

        var person1IsUnderTracking = slideWindow.IsObjIdAlive("person:1");
        Assert.That(person1IsUnderTracking, Is.EqualTo(false));

        var car2IsUnderTracking = slideWindow.IsObjIdAlive("car:2");
        Assert.That(car2IsUnderTracking, Is.EqualTo(true));
    }

    [Test]
    public void TestVideoFrameBuffer_ObserverInvoke()
    {
        int windowSize = 3;
        var slideWindow = new VideoFrameSlideWindow(windowSize);
        slideWindow.SetPublisher(_frmExpiredPublisher);
        slideWindow.SetPublisher(_objExpiredPublisher);

        var frmEventSubscriber1 = Substitute.For<IEventSubscriber<FrameExpiredEvent>>();
        var objEventSubscriber1 = Substitute.For<IEventSubscriber<ObjectExpiredEvent>>();

        _frmExpiredSubscriber1.Subscribe(x => frmEventSubscriber1.ProcessEvent(x));
        _objExpiredSubscriber1.Subscribe(x => objEventSubscriber1.ProcessEvent(x));

        var frmEventSubscriber2 = Substitute.For<IEventSubscriber<FrameExpiredEvent>>();
        var objEventSubscriber2 = Substitute.For<IEventSubscriber<ObjectExpiredEvent>>();

        _frmExpiredSubscriber2.Subscribe(x => frmEventSubscriber2.ProcessEvent(x));
        _objExpiredSubscriber2.Subscribe(x => objEventSubscriber2.ProcessEvent(x));

        var objEventSubscriber3 = Substitute.For<IEventSubscriber<ObjectExpiredEvent>>();

        _objExpiredSubscriber3.Subscribe(x => objEventSubscriber3.ProcessEvent(x));
        
        // var baseEventSubscriber = Substitute.For<IEventSubscriber<EventBase>>();
        // baseEventSubscriber.SetSubscriber(_baseEventSubscriber);
        // _baseEventSubscriber.Subscribe(e => Console.WriteLine("baseSubsriber:" + e.EventId));

        // frame 1: car1
        var frame1 = CreateFrame1();

        // frame 2: car1, person1
        var frame2 = CreateFrame2();

        // frame 3: person1
        var frame3 = CreateFrame3();

        // frame 4: person1
        var frame4 = CreateFrame4();

        // frame 5: person1, car2
        var frame5 = CreateFrame5();

        // frame 6: car2
        var frame6 = CreateFrame6();

        // frame 7: 
        var frame7 = new Frame("tempId", 7, 240, new Mat());
        frame7.AddDetectedObjects(new List<BoundingBox>());

        // frame 8: 
        var frame8 = new Frame("tempId", 7, 280, new Mat());
        frame8.AddDetectedObjects(new List<BoundingBox>());

        slideWindow.AddNewFrame(frame1);
        slideWindow.AddNewFrame(frame2);
        slideWindow.AddNewFrame(frame3);

        slideWindow.AddNewFrame(frame4);
        frmEventSubscriber1.Received().ProcessEvent(Arg.Is<FrameExpiredEvent>(e => e.FrameId == 1));
        frmEventSubscriber2.Received().ProcessEvent(Arg.Is<FrameExpiredEvent>(e => e.FrameId == 1));

        slideWindow.AddNewFrame(frame5);
        frmEventSubscriber1.Received().ProcessEvent(Arg.Is<FrameExpiredEvent>(e => e.FrameId == 2));
        objEventSubscriber1.Received().ProcessEvent(Arg.Is<ObjectExpiredEvent>(e => e.Id == "car:1"));
        frmEventSubscriber2.Received().ProcessEvent(Arg.Is<FrameExpiredEvent>(e => e.FrameId == 2));
        objEventSubscriber2.Received().ProcessEvent(Arg.Is<ObjectExpiredEvent>(e => e.Id == "car:1"));
        objEventSubscriber3.Received().ProcessEvent(Arg.Is<ObjectExpiredEvent>(e => e.Id == "car:1"));

        slideWindow.AddNewFrame(frame6);
        frmEventSubscriber1.Received().ProcessEvent(Arg.Is<FrameExpiredEvent>(e => e.FrameId == 3));
        frmEventSubscriber2.Received().ProcessEvent(Arg.Is<FrameExpiredEvent>(e => e.FrameId == 3));

        slideWindow.AddNewFrame(frame7);
        frmEventSubscriber1.Received().ProcessEvent(Arg.Is<FrameExpiredEvent>(e => e.FrameId == 4));
        frmEventSubscriber2.Received().ProcessEvent(Arg.Is<FrameExpiredEvent>(e => e.FrameId == 4));

        slideWindow.AddNewFrame(frame8);
        frmEventSubscriber1.Received().ProcessEvent(Arg.Is<FrameExpiredEvent>(e => e.FrameId == 5));
        objEventSubscriber1.Received().ProcessEvent(Arg.Is<ObjectExpiredEvent>(e => e.Id == "person:1"));
        frmEventSubscriber2.Received().ProcessEvent(Arg.Is<FrameExpiredEvent>(e => e.FrameId == 5));
        objEventSubscriber2.Received().ProcessEvent(Arg.Is<ObjectExpiredEvent>(e => e.Id == "person:1"));
        objEventSubscriber3.Received().ProcessEvent(Arg.Is<ObjectExpiredEvent>(e => e.Id == "person:1"));

        var car1Frames = slideWindow.GetFramesContainObjectId("car:1");
        Assert.That(car1Frames.Count, Is.EqualTo(0));

        var person1Frames = slideWindow.GetFramesContainObjectId("person:1");
        Assert.That(person1Frames.Count, Is.EqualTo(0));

        var car2Frames = slideWindow.GetFramesContainObjectId("car:2");
        Assert.That(car2Frames.Count, Is.EqualTo(1));

        var car1IsUnderTracking = slideWindow.IsObjIdAlive("car:1");
        Assert.That(car1IsUnderTracking, Is.EqualTo(false));

        var person1IsUnderTracking = slideWindow.IsObjIdAlive("person:1");
        Assert.That(person1IsUnderTracking, Is.EqualTo(false));

        var car2IsUnderTracking = slideWindow.IsObjIdAlive("car:2");
        Assert.That(car2IsUnderTracking, Is.EqualTo(true));
    }

    /*[Test]
    public void TestPublishSubscrber()
    {
        var services = new ServiceCollection();
        services.AddMessagePipe();
        var provider = services.BuildServiceProvider();

        var publisher = provider.GetRequiredService<IPublisher<EventBase>>();

        var baseSubscriber = provider.GetRequiredService<ISubscriber<EventBase>>();
        var frameExpiredSubscriber = provider.GetRequiredService<ISubscriber<FrameExpiredEvent>>();
        var objectExpiredSuEvent = provider.GetRequiredService<ISubscriber<ObjectExpiredEvent>>();

        baseSubscriber.Subscribe(e => Console.WriteLine("base:" + e.EventId));
        frameExpiredSubscriber.Subscribe(e => Console.WriteLine("frame:" + e.EventId));
        objectExpiredSuEvent.Subscribe(e => Console.WriteLine("obj:" + e.EventId));

        publisher.Publish(new FrameExpiredEvent(1));
        publisher.Publish(new ObjectExpiredEvent("person:1",0, "person", 1));
    }*/
}