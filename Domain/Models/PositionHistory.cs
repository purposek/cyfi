namespace Domain.Models
{
    public static class PositionHistory
    {
        private const int _capacity = 30;
        private static Queue<Point> _latestPositions = new Queue<Point>(_capacity);

        public static void AddPosition(Point position)
        {
            if (_latestPositions.Count == _capacity)
            {
                _latestPositions.Dequeue();
            }

            _latestPositions.Enqueue(position);
        }

        public static bool Contains(Point position)
        {
            return _latestPositions.Contains(position);
        }

        public static List<Point> GetLatestPositions()
        {
            return _latestPositions.ToList();
        }
    }
}
