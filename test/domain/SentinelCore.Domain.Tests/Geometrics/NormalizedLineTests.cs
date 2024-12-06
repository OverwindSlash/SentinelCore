using SentinelCore.Domain.Entities.AnalysisDefinitions.Geometrics;
using System.Text.Json;

namespace SentinelCore.Domain.Tests.Geometrics;

[TestFixture]
public class NormalizedLineTests
{
    private const int ImageWidth = 1920;
    private const int ImageHeight = 1080;

    private const string LineJson = "{\"Start\":{\"NormalizedX\":0.24635416666666668,\"NormalizedY\":0.10092592592592593}," + "\"Stop\":{\"NormalizedX\":0.09739583333333333,\"NormalizedY\":0.26296296296296295}}";

    [Test]
    public void Test_NormalizedLine_SaveMode_WithSameScale()
    {
        NormalizedPoint p1 = new NormalizedPoint(ImageWidth, ImageHeight, 473, 109);
        NormalizedPoint p2 = new NormalizedPoint(ImageWidth, ImageHeight, 187, 284);

        NormalizedLine l1 = new NormalizedLine(p1, p2);
        
        Assert.That(l1.Start, Is.EqualTo(p1));
        Assert.That(l1.Stop, Is.EqualTo(p2));
    }

    [Test]
    public void Test_NormalizedLine_SaveMode_WithDifferentScale()
    {
        NormalizedPoint p1 = new NormalizedPoint(ImageWidth, ImageHeight, 473, 109);
        NormalizedPoint p2 = new NormalizedPoint(ImageWidth / 2, ImageHeight / 2, 187, 284);

        Assert.Catch<ArgumentException>(() =>
        {
            NormalizedLine l1 = new NormalizedLine(p1, p2);
        });
    }
    
    [Test]
    public void Test_NormalizedLine_LoadMode_WithoutSetImageSize()
    {
        NormalizedLine l1 = JsonSerializer.Deserialize<NormalizedLine>(LineJson);
        Assert.That(l1, Is.Not.EqualTo(null));

        Assert.Catch<ArgumentException>(() =>
        {
            int originalX = l1.Start.OriginalX;
        });
        
        Assert.Catch<ArgumentException>(() =>
        {
            int originalX = l1.Stop.OriginalX;
        });
    }

    [Test]
    public void Test_NormalizedLine_LoadMode_WithSetImageSize()
    {
        NormalizedLine l1 = JsonSerializer.Deserialize<NormalizedLine>(LineJson);
        Assert.That(l1, Is.Not.EqualTo(null));

        l1.SetImageSize(ImageWidth, ImageHeight);
        
        Assert.That(l1.Start.OriginalX, Is.EqualTo(473));
        Assert.That(l1.Start.OriginalY, Is.EqualTo(109));
        Assert.That(l1.Stop.OriginalX, Is.EqualTo(187));
        Assert.That(l1.Stop.OriginalY, Is.EqualTo(284));
    }

    [Test]
    public void Test_NormalizedLine_IsCrossLine()
    {
        NormalizedPoint p1 = new NormalizedPoint(ImageWidth, ImageHeight, 473, 109);
        NormalizedPoint p2 = new NormalizedPoint(ImageWidth, ImageHeight, 187, 284);

        NormalizedLine l1 = new NormalizedLine(p1, p2);

        NormalizedPoint tp1 = new NormalizedPoint(ImageWidth, ImageHeight, 300, 100);
        NormalizedPoint tp2 = new NormalizedPoint(ImageWidth, ImageHeight, 400, 300);
        NormalizedPoint tp3 = new NormalizedPoint(ImageWidth, ImageHeight, 100, 200);
        
        Assert.That(l1.IsCrossedLine(tp1, tp2), Is.True);
        Assert.That(l1.IsCrossedLine(tp1, tp3), Is.False);
    }
    
    [Test]
    public void TestInitialization()
    {
        // Arrange
        int width = 200;
        int height = 400;
        NormalizedPoint startPoint = new NormalizedPoint(width, height, 50, 100);
        NormalizedPoint endPoint = new NormalizedPoint(width, height, 150, 200);

        // Act
        NormalizedLine line = new NormalizedLine(startPoint, endPoint);

        // Assert
        Assert.That(line.Start.OriginalX, Is.EqualTo(50));
        Assert.That(line.Start.OriginalY, Is.EqualTo(100));
        Assert.That(line.Stop.OriginalX, Is.EqualTo(150));
        Assert.That(line.Stop.OriginalY, Is.EqualTo(200));
        Assert.That(line.Start.NormalizedX, Is.EqualTo(0.25));
        Assert.That(line.Start.NormalizedY, Is.EqualTo(0.25));
        Assert.That(line.Stop.NormalizedX, Is.EqualTo(0.75));
        Assert.That(line.Stop.NormalizedY, Is.EqualTo(0.5));
    }
    
    [Test]
    public void TestSerialization()
    {
        // Arrange
        NormalizedPoint startPoint = new NormalizedPoint(200, 400, 50, 100);
        NormalizedPoint endPoint = new NormalizedPoint(200, 400, 150, 200);
        NormalizedLine line = new NormalizedLine(startPoint, endPoint);

        // Act
        string serializedLine = JsonSerializer.Serialize(line);

        // Assert
        string expectedJson = "{\"Start\":{\"NormalizedX\":0.25,\"NormalizedY\":0.25},\"Stop\":{\"NormalizedX\":0.75,\"NormalizedY\":0.5}}";
        Assert.That(serializedLine, Is.EqualTo(expectedJson));
    }
    
    [Test]
    public void TestDeserialization()
    {
        // Arrange
        string json = "{\"Start\":{\"NormalizedX\":0.25,\"NormalizedY\":0.25},\"Stop\":{\"NormalizedX\":0.75,\"NormalizedY\":0.5}}";

        // Act
        NormalizedLine line = JsonSerializer.Deserialize<NormalizedLine>(json);
        line.SetImageSize(200, 400);

        // Assert
        Assert.That(line.Start.OriginalX, Is.EqualTo(50));
        Assert.That(line.Start.OriginalY, Is.EqualTo(100));
        Assert.That(line.Stop.OriginalX, Is.EqualTo(150));
        Assert.That(line.Stop.OriginalY, Is.EqualTo(200));
        Assert.That(line.Start.NormalizedX, Is.EqualTo(0.25));
        Assert.That(line.Start.NormalizedY, Is.EqualTo(0.25));
        Assert.That(line.Stop.NormalizedX, Is.EqualTo(0.75));
        Assert.That(line.Stop.NormalizedY, Is.EqualTo(0.5));
    }
    
    [Test]
    public void TestImageSizeNotSetException()
    {
        // Arrange
        string json = "{\"Start\":{\"NormalizedX\":0.25,\"NormalizedY\":0.25},\"Stop\":{\"NormalizedX\":0.75,\"NormalizedY\":0.5}}";
        NormalizedLine line = JsonSerializer.Deserialize<NormalizedLine>(json);

        // Act & Assert
        Assert.That(() => line.Start.OriginalX, Throws.ArgumentException);
        Assert.That(() => line.Stop.OriginalY, Throws.ArgumentException);
    }
    
    [Test]
    public void TestSetImageSizeAfterDeserialization()
    {
        // Arrange
        string json = "{\"Start\":{\"NormalizedX\":0.25,\"NormalizedY\":0.25},\"Stop\":{\"NormalizedX\":0.75,\"NormalizedY\":0.5}}";
        NormalizedLine line = JsonSerializer.Deserialize<NormalizedLine>(json);

        // Act
        line.SetImageSize(200, 400);

        // Assert
        Assert.That(line.Start.OriginalX, Is.EqualTo(50));
        Assert.That(line.Start.OriginalY, Is.EqualTo(100));
        Assert.That(line.Stop.OriginalX, Is.EqualTo(150));
        Assert.That(line.Stop.OriginalY, Is.EqualTo(200));
    }
}