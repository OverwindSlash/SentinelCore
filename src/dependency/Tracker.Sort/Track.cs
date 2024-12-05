using System.Drawing;

namespace Tracker.Sort
{
    public record Track
    {
        public int TrackId { get; set; }

        public int TotalMisses { get; set; }

        public int Misses { get; set; }

        public List<RectangleF> History { get; set; }

        public TrackState State { get; set; }

        public RectangleF Prediction { get; set; }
    }
}