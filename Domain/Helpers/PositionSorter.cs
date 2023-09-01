using Domain.Models;

namespace Domain.Helpers
{

    public class PositionSorter : IComparer<Point>
    {
        Point Goal;

        public PositionSorter(Point goal)
        {
            Goal = goal;
        }

        public int Compare(Point x, Point y)
        {
            return WorldMapPerspective.ManhattanDistance(x, Goal) - WorldMapPerspective.ManhattanDistance(y, Goal);
        }
    }
}
