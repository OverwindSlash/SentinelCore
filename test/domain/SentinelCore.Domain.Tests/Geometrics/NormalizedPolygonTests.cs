using SentinelCore.Domain.Entities.AnalysisDefinitions.Geometrics;
using System.Text.Json;

namespace Sentinel.Geometrics;

[TestFixture]
public class NormalizedPolygonTests
{
    private const int ImageWidth = 1920;
    private const int ImageHeight = 1080;

    private string polygonJson =
        "{\"Points\":[" +
        "{\"NormalizedX\":0.24635416666666668,\"NormalizedY\":0}," +
        "{\"NormalizedX\":0.09739583333333333,\"NormalizedY\":0.26296296296296295}," +
        "{\"NormalizedX\":0.24479166666666666,\"NormalizedY\":0.2601851851851852}," +
        "{\"NormalizedX\":0.3328125,\"NormalizedY\":0}]}";

    [Test]
    public void Test_NormalizedPolygon_SaveMode_UseList_WithSameScale()
    {
        NormalizedPoint p1 = new NormalizedPoint(ImageWidth, ImageHeight, 473, 0);
        NormalizedPoint p2 = new NormalizedPoint(ImageWidth, ImageHeight, 187, 284);
        NormalizedPoint p3 = new NormalizedPoint(ImageWidth, ImageHeight, 470, 281);
        NormalizedPoint p4 = new NormalizedPoint(ImageWidth, ImageHeight, 639, 0);

        List<NormalizedPoint> points = new List<NormalizedPoint>();
        points.Add(p1);
        points.Add(p2);
        points.Add(p3);
        points.Add(p4);

        NormalizedPolygon polygon1 = new NormalizedPolygon(points);
        
        Assert.That(polygon1.Points[0].OriginalX, Is.EqualTo(473));
        Assert.That(polygon1.Points[1].OriginalY, Is.EqualTo(284));
        Assert.That(polygon1.Points[2].OriginalX, Is.EqualTo(470));
        Assert.That(polygon1.Points[3].OriginalY, Is.EqualTo(0));
    }

    [Test]
    public void Test_NormalizedPolygon_SaveMode_UseList_WithoutSameScale()
    {
        NormalizedPoint p1 = new NormalizedPoint(ImageWidth, ImageHeight, 473, 0);
        NormalizedPoint p2 = new NormalizedPoint(ImageWidth, ImageHeight, 187, 284);
        NormalizedPoint p3 = new NormalizedPoint(ImageWidth, ImageHeight, 470, 281);
        NormalizedPoint p4 = new NormalizedPoint(ImageWidth / 2, ImageHeight / 2, 639, 0);

        List<NormalizedPoint> points = new List<NormalizedPoint>();
        points.Add(p1);
        points.Add(p2);
        points.Add(p3);
        points.Add(p4);

        Assert.Catch<ArgumentException>(() =>
        {
            NormalizedPolygon polygon1 = new NormalizedPolygon(points);
        });
    }

    [Test]
    public void Test_NormalizedPolygon_SaveMode_UseAdd_WithSameScale()
    {
        // Lane1 points
        NormalizedPoint p1 = new NormalizedPoint(ImageWidth, ImageHeight, 473, 0);
        NormalizedPoint p2 = new NormalizedPoint(ImageWidth, ImageHeight, 187, 284);
        NormalizedPoint p3 = new NormalizedPoint(ImageWidth, ImageHeight, 470, 281);
        NormalizedPoint p4 = new NormalizedPoint(ImageWidth, ImageHeight, 639, 0);

        NormalizedPolygon polygon1 = new NormalizedPolygon();
        polygon1.AddPoint(p1);
        polygon1.AddPoint(p2);
        polygon1.AddPoint(p3);
        polygon1.AddPoint(p4);

        Assert.That(polygon1.Points[0].OriginalX, Is.EqualTo(473));
        Assert.That(polygon1.Points[1].OriginalY, Is.EqualTo(284));
        Assert.That(polygon1.Points[2].OriginalX, Is.EqualTo(470));
        Assert.That(polygon1.Points[3].OriginalY, Is.EqualTo(0));
    }

    [Test]
    public void Test_NormalizedPolygon_SaveMode_UseAdd_WithoutSameScale()
    {
        // Lane1 points
        NormalizedPoint p1 = new NormalizedPoint(ImageWidth, ImageHeight, 473, 0);
        NormalizedPoint p2 = new NormalizedPoint(ImageWidth, ImageHeight, 187, 284);
        NormalizedPoint p3 = new NormalizedPoint(ImageWidth, ImageHeight, 470, 281);
        NormalizedPoint p4 = new NormalizedPoint(ImageWidth / 2, ImageHeight / 2, 639, 0);

        NormalizedPolygon polygon1 = new NormalizedPolygon();
        polygon1.AddPoint(p1);
        polygon1.AddPoint(p2);
        polygon1.AddPoint(p3);

        Assert.Catch<ArgumentException>(() => { polygon1.AddPoint(p4); });
    }

    [Test]
    public void Test_NormalizedPolygon_LoadMode_WithSetImageSize()
    {
        NormalizedPolygon polygon1 = JsonSerializer.Deserialize<NormalizedPolygon>(polygonJson);
        Assert.That(polygon1, Is.Not.Null);

        polygon1.SetImageSize(ImageWidth, ImageHeight);

        Assert.That(polygon1.Points[0].OriginalX, Is.EqualTo(473));
        Assert.That(polygon1.Points[1].OriginalY, Is.EqualTo(284));
        Assert.That(polygon1.Points[2].OriginalX, Is.EqualTo(470));
        Assert.That(polygon1.Points[3].OriginalY, Is.EqualTo(0));
    }

    [Test]
    public void Test_NormalizedPolygon_LoadMode_WithoutSetImageSize()
    {
        NormalizedPolygon polygon1 = JsonSerializer.Deserialize<NormalizedPolygon>(polygonJson);
        Assert.That(polygon1, Is.Not.Null);

        Assert.Catch<ArgumentException>(() =>
        {
            int originalX = polygon1.Points[0].OriginalX;
        });
    }

    [Test]
    public void Test_NormalizedPolygon_SaveMode_UseAdd_IsPointInPolygon()
    {
        // Lane1 points
        NormalizedPoint p1 = new NormalizedPoint(ImageWidth, ImageHeight, 473, 0);
        NormalizedPoint p2 = new NormalizedPoint(ImageWidth, ImageHeight, 187, 284);
        NormalizedPoint p3 = new NormalizedPoint(ImageWidth, ImageHeight, 470, 281);
        NormalizedPoint p4 = new NormalizedPoint(ImageWidth, ImageHeight, 639, 0);

        NormalizedPolygon polygon1 = new NormalizedPolygon();
        polygon1.AddPoint(p1);
        polygon1.AddPoint(p2);
        polygon1.AddPoint(p3);
        polygon1.AddPoint(p4);

        // points to check
        NormalizedPoint inPoint = new NormalizedPoint(ImageWidth, ImageHeight, 433, 142);
        bool inResult = polygon1.IsPointInPolygon(inPoint);
        Assert.That(inResult, Is.True);

        NormalizedPoint outPoint = new NormalizedPoint(ImageWidth, ImageHeight, 618, 194);
        bool outResult = polygon1.IsPointInPolygon(outPoint);
        Assert.That(outResult, Is.False);
    }

    [Test]
    public void Test_NormalizedPolygon_SaveMode_ForJsonGeneration()
    {
        int width = 1920;
        int height = 1080;

        NormalizedPoint p1 = new NormalizedPoint(width, height, 0, 26);
        NormalizedPoint p2 = new NormalizedPoint(width, height, 830, 92);

        NormalizedPoint p3 = new NormalizedPoint(width, height, 201, 965);
        NormalizedPoint p4 = new NormalizedPoint(width, height, 711, 1008);

        NormalizedPoint p5 = new NormalizedPoint(width, height, 1393, 920);
        NormalizedPoint p6 = new NormalizedPoint(width, height, 1877, 989);

        var p1NormalizedX = p1.NormalizedX;
        var p1NormalizedY = p1.NormalizedY;

        var p2NormalizedX = p2.NormalizedX;
        var p2NormalizedY = p2.NormalizedY;

        var p3NormalizedX = p3.NormalizedX;
        var p3NormalizedY = p3.NormalizedY;

        var p4NormalizedX = p4.NormalizedX;
        var p4NormalizedY = p4.NormalizedY;

        var p5NormalizedX = p5.NormalizedX;
        var p5NormalizedY = p5.NormalizedY;

        var p6NormalizedX = p6.NormalizedX;
        var p6NormalizedY = p6.NormalizedY;

        Console.WriteLine("OK");
    }
}