using Domain.Enums;

namespace Domain.Models
{
    public class SolidLump
    {
        public int Size => Points.Count;
        public Point TopLeft => Points.Where(x => x.Y == Points.Max(p => p.Y)).OrderBy(x => x.X).First();
        public Point TopRight => Points.Where(x => x.Y == Points.Max(p => p.Y)).OrderByDescending(x => x.X).First();
        public Point BottomLeft => Points.Where(x => x.Y == Points.Min(p => p.Y)).OrderBy(x => x.X).First();
        public Point BottomRight => Points.Where(x => x.Y == Points.Min(p => p.Y)).OrderByDescending(x => x.X).First();
        public List<Point> Points { get; set; } = new();

        public Point PointOnMyLevel(int myY)
        {
            return Points.Where(x => x.Y == Math.Max(myY, Points.Min(p => p.Y))).First();
        }
    }
}