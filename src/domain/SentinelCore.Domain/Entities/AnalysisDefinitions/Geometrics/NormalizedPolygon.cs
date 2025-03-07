using SentinelCore.Domain.Entities.ObjectDetection;

namespace SentinelCore.Domain.Entities.AnalysisDefinitions.Geometrics;

public class NormalizedPolygon : ImageBasedGeometric
{
    private List<NormalizedPoint> _points;

    public List<NormalizedPoint> Points
    {
        // get
        // {
        //     CheckImageSizeInitialized();
        //     return _points;
        // }
        get => _points;
        set => _points = value;
    }

    // For polygon generation by json deserialization
    // 1. Use constructor to create a new NormalizedPolygon object.
    // 2. Use Properties set method to set _points.
    // 3. Manual call SetImageSize to specify real image width and height,
    // then call the SetImageSize functions of each NormalizedPoint object.
    public NormalizedPolygon()
    {
        Points = new List<NormalizedPoint>();
    }

    public override void SetImageSize(int width, int height)
    {
        base.SetImageSize(width, height);

        foreach (NormalizedPoint point in Points)
        {
            point.SetImageSize(width, height);
        }
    }

    // For polygon generation by hand
    public NormalizedPolygon(List<NormalizedPoint> points)
    {
        if ((points == null) || (points.Count == 0))
        {
            throw new ArgumentException("null or empty points list.");
        }

        int imageWidth = points[0].ImageWidth;
        int imageHeight = points[0].ImageHeight;
        foreach (NormalizedPoint point in points)
        {
            if ((point.ImageWidth != imageWidth) || (point.ImageHeight != imageHeight))
            {
                throw new ArgumentException("point does not match scale of others.");
            }
        }

        base.SetImageSize(imageWidth, imageHeight);

        _points = points;
    }

    public void AddPoint(NormalizedPoint point)
    {
        // When first point to be add.
        if (!IsInitialized())
        {
            base.SetImageSize(point.ImageWidth, point.ImageHeight);
        }

        if ((point.ImageWidth != ImageWidth) || (point.ImageHeight != ImageHeight))
        {
            throw new ArgumentException("point does not match scale of others.");
        }

        _points.Add(point);
    }

    public void RemovePoint(NormalizedPoint point)
    {
        if (point == null)
        {
            return;
        }

        _points.Remove(point);
    }

    public NormalizedPoint GetCenterNormalizedPoint()
    {
        if (_points == null || _points.Count == 0)
        {
            throw new InvalidOperationException("Invalid normalized polygon.");
        }

        double area = 0.0;
        double Cx = 0.0;
        double Cy = 0.0;
        int count = _points.Count;

        for (int i = 0; i < count; i++)
        {
            int j = (i + 1) % count; // 下一个顶点的索引，环绕回第一个顶点
            double xi = _points[i].OriginalX;
            double yi = _points[i].OriginalY;
            double xj = _points[j].OriginalX;
            double yj = _points[j].OriginalY;

            double cross = xi * yj - xj * yi;
            area += cross;
            Cx += (xi + xj) * cross;
            Cy += (yi + yj) * cross;
        }

        area *= 0.5;

        if (Math.Abs(area) < 1e-10)
        {
            // 如果面积为零，可能是退化的多边形（例如所有点共线）
            // 此时，可以返回顶点的平均值作为中心点
            double avgX = _points.Average(p => p.OriginalX);
            double avgY = _points.Average(p => p.OriginalY);
            return new NormalizedPoint(ImageWidth, ImageHeight, (int)avgX, (int)avgY);
        }

        Cx /= (6.0 * area);
        Cy /= (6.0 * area);

        return new NormalizedPoint(ImageWidth, ImageHeight, (int)Cx, (int)Cy);
    }

    public bool IsPointInPolygon(NormalizedPoint p)
    {
        if (_points.Count == 0)
        {
            return false;
        }

        double minX = _points[0].OriginalX;
        double maxX = _points[0].OriginalX;
        double minY = _points[0].OriginalY;
        double maxY = _points[0].OriginalY;
        for (int i = 1; i < _points.Count; i++)
        {
            NormalizedPoint q = _points[i];
            minX = Math.Min(q.OriginalX, minX);
            maxX = Math.Max(q.OriginalX, maxX);
            minY = Math.Min(q.OriginalY, minY);
            maxY = Math.Max(q.OriginalY, maxY);
        }

        if (p.OriginalX < minX || p.OriginalX > maxX || p.OriginalY < minY || p.OriginalY > maxY)
        {
            return false;
        }

        bool inside = false;
        for (int i = 0, j = _points.Count - 1; i < _points.Count; j = i++)
        {
            if ((_points[i].OriginalY > p.OriginalY) != (_points[j].OriginalY > p.OriginalY) &&
                p.OriginalX < (_points[j].OriginalX - _points[i].OriginalX) * (p.OriginalY - _points[i].OriginalY)
                / (_points[j].OriginalY - _points[i].OriginalY) + _points[i].OriginalX)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}