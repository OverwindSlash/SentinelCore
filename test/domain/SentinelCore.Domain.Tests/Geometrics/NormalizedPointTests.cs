using SentinelCore.Domain.Entities.AnalysisDefinitions.Geometrics;
using System.Text.Json;

namespace SentinelCore.Domain.Tests.Geometrics;

[TestFixture]
public class NormalizedPointTests
{
    private const int ImageWidth = 1920;
    private const int ImageHeight = 1080;
    private const string PointJson = "{\"NormalizedX\":0.24635416666666668,\"NormalizedY\":0.10092592592592593}";

    [Test]
    public void Test_NormalizedPoint_SaveMode()
    {
        NormalizedPoint p1 = new NormalizedPoint(ImageWidth, ImageHeight, 473, 109);
        
        Assert.That(p1.OriginalX, Is.EqualTo(473));
        Assert.That(p1.OriginalY, Is.EqualTo(109));
        Assert.That(p1.NormalizedX, Is.EqualTo((double)473 / 1920));
        Assert.That(p1.NormalizedY, Is.EqualTo((double)109 / 1080));
    }
        
    [Test]
    public void Test_NormalizedPoint_LoadMode_WithoutSetImageSize()
    {
        NormalizedPoint p1 = JsonSerializer.Deserialize<NormalizedPoint>(PointJson);
        Assert.That(p1, Is.Not.EqualTo(null));
            
        Assert.Catch<ArgumentException>(() =>
        {
            int x = p1.OriginalX;
        });
    }
        
    [Test]
    public void Test_NormalizedPoint_LoadMode_WithSetImageSize()
    {
        NormalizedPoint p1 = JsonSerializer.Deserialize<NormalizedPoint>(PointJson);
        Assert.That(p1, Is.Not.EqualTo(null));
            
        p1.SetImageSize(ImageWidth, ImageHeight);

        Assert.That(p1.OriginalX, Is.EqualTo(473));
        Assert.That(p1.OriginalY, Is.EqualTo(109));
        Assert.That(p1.NormalizedX, Is.EqualTo((double)473 / 1920));
        Assert.That(p1.NormalizedY, Is.EqualTo((double)109 / 1080));
    }
    
    [Test]
    public void TestAbsoluteCoordinatesInitialization()
    {
        // Arrange
        int width = 200;
        int height = 400;
        int x = 50;
        int y = 100;

        // Act
        NormalizedPoint point = new NormalizedPoint(width, height, x, y);

        // Assert
        Assert.That(point.OriginalX, Is.EqualTo(x));
        Assert.That(point.OriginalY, Is.EqualTo(y));
        Assert.That(point.NormalizedX, Is.EqualTo((double)x / width));
        Assert.That(point.NormalizedY, Is.EqualTo((double)y / height));
    }
    
    [Test]
    public void TestNormalizedCoordinatesInitialization()
    {
        // Arrange
        double normalizedX = 0.25;
        double normalizedY = 0.5;

        // Act
        NormalizedPoint point = new NormalizedPoint(normalizedX, normalizedY);
        point.SetImageSize(200, 400);

        // Assert
        Assert.That(point.OriginalX, Is.EqualTo(50));
        Assert.That(point.OriginalY, Is.EqualTo(200));
        Assert.That(point.NormalizedX, Is.EqualTo(normalizedX));
        Assert.That(point.NormalizedY, Is.EqualTo(normalizedY));
    }
}