using Domain.Enums;
using Domain.Models.Pathfinding;
using System;
using System.ComponentModel;

namespace Domain.Models
{
    public static class WorldMapPerspective
    {
        // Define the map size constants
        public const int MapXLength = 100;
        public const int MapYLength = 100;
        public static Dictionary<int, int> LevelTargetCollectables = new() { { 0, 10 }, { 1, 30 }, { 2, 50 }, { 3, 60 } };
        public static Dictionary<int, int> LevelMinCollectablesForPursuit = new() { { 0, 10 }, { 1, 16 }, { 2, 27 }, { 3, 32 } };


        public static bool[][] KnownCoordinates { get; }
        public static List<Point> Collectibles { get; }
        public static List<Point> OpponentPositions { get; }
        public static int Level;
        private static int GameTick;
        private static int StartedLevelAtTick;

        public static int TicksInLevel => GameTick - StartedLevelAtTick;

        public static ObjectType[][] ObjectCoordinates { get; }
        public static bool DiggingMode { get; set; }
        public static bool OpponentsInStealRange { get; set; }
        public static bool OpponentsInCloseRange { get; set; }
        public static int PlatformsLeastY { get; set; } = MapYLength;
        public static int PlatformsGreatestY { get; set; }

        public static bool CheckIfAheadWhenLanded;

        public static int PerformingAdversarialManouvreToTick { get; set; }

        static WorldMapPerspective()
        {
            Collectibles = new List<Point>();
            OpponentPositions = new List<Point>();
            ObjectCoordinates = new ObjectType[MapXLength][];
            KnownCoordinates = new bool[MapXLength][];
            for (int i = 0; i < MapXLength; i++)
            {
                ObjectCoordinates[i] = new ObjectType[MapYLength];
                KnownCoordinates[i] = new bool[MapYLength];
            }
        }

        public static void SetCoordinates(int x, int y, ObjectType objectType)
        {
            ObjectCoordinates[x][y] = objectType;
        }

        public static void SetKnownCoordinates(int x, int y, bool known)
        {
            KnownCoordinates[x][y] = known;
        }

        public static void UpdateState(BotStateDTO botState)
        {
            if (botState.CurrentLevel != Level)
            {
                Collectibles.Clear();
                Level = botState.CurrentLevel;
                StartedLevelAtTick = botState.GameTick;
                PlatformsLeastY = MapYLength;
                PlatformsGreatestY = 0;
                CheckIfAheadWhenLanded = true;
                for (int i = 0; i < MapXLength; i++)
                {
                    for (int j = 0; j < MapYLength; j++)
                    {
                        SetCoordinates(i, j, (int)ObjectType.Air);
                        SetKnownCoordinates(i, j, false);
                    }
                }
            }
            else
            {
                CheckIfAheadWhenLanded = false;
            }
            GameTick = botState.GameTick;   
            OpponentsInStealRange = false;
            OpponentsInCloseRange = false;
            OpponentPositions.Clear();
            for (int i = 0; i < botState.HeroWindow.Length; i++)
            {
                for (int j = 0; j < botState.HeroWindow[i].Length; j++)
                {
                    var worldX = botState.X + i - 16;
                    var worldY = botState.Y + j - 10;
                    if (worldX >= 0 && worldX < MapXLength && worldY >= 0 && worldY < MapYLength)
                    {
                        SetCoordinates(worldX, worldY, (ObjectType)botState.HeroWindow[i][j]);
                        SetKnownCoordinates(worldX, worldY, true);
                        var currentPoint = new Point(worldX, worldY);
                        if (Collectibles.Contains(currentPoint) && (ObjectType)botState.HeroWindow[i][j] != ObjectType.Collectible)
                        {
                            Collectibles.Remove(currentPoint);
                        }

                        if ((ObjectType)botState.HeroWindow[i][j] == ObjectType.Collectible && !Collectibles.Contains(currentPoint))
                        {
                            Collectibles.Add(currentPoint);
                        }

                        if (((ObjectType)botState.HeroWindow[i][j] == ObjectType.Ladder || (ObjectType)botState.HeroWindow[i][j] == ObjectType.Platform) && worldY < PlatformsLeastY) PlatformsLeastY = worldY;
                        if (((ObjectType)botState.HeroWindow[i][j] == ObjectType.Ladder || (ObjectType)botState.HeroWindow[i][j] == ObjectType.Platform) && worldY > PlatformsGreatestY) PlatformsGreatestY = worldY;
                        var stealWindow = BotStealWindow(botState.CurrentPosition, 2, 2);
                        if ((ObjectType)botState.HeroWindow[i][j] == ObjectType.Opponent && worldX > stealWindow[0].X &&
                            worldX < stealWindow[1].X &&
                            worldY > stealWindow[0].Y &&
                            worldY < stealWindow[1].Y) OpponentsInStealRange = true;

                        var closeRange = BotStealWindow(botState.CurrentPosition, 10, 10);
                        if ((ObjectType)botState.HeroWindow[i][j] == ObjectType.Opponent && worldX > closeRange[0].X &&
                            worldX < closeRange[1].X &&
                            worldY > closeRange[0].Y &&
                            worldY < closeRange[1].Y)
                        {
                            OpponentPositions.Add(new(worldX, worldY));
                            OpponentsInCloseRange = true;
                        }
                    }
                }
            }
        }


        public static bool BotIsOnStableFooting(Point position)
        {
            return BotOnPlatform(position) || BotOnLadder(position) || BotOnSolid(position) || BotContainsLadder(position);
        }

        public static bool BotOnLadder(Point position)
        {
            return !BotOutOfBounds(position) && position.Y > 1 && (ObjectCoordinates[position.X][position.Y - 1] == ObjectType.Ladder || ObjectCoordinates[position.X + 1][position.Y - 1] == ObjectType.Ladder);
        }

        public static bool BotOnPlatform(Point position)
        {
            return !BotOutOfBounds(position) && position.Y > 1 && (ObjectCoordinates[position.X][position.Y - 1] == ObjectType.Platform || ObjectCoordinates[position.X + 1][position.Y - 1] == ObjectType.Platform);
        }

        public static bool BotOnAirOrCollectable(Point position)
        {
            return !BotOutOfBounds(position) && position.Y > 1 &&
                ((ObjectCoordinates[position.X][position.Y - 1] == ObjectType.Air || ObjectCoordinates[position.X][position.Y - 1] == ObjectType.Collectible)
                &&
                (ObjectCoordinates[position.X + 1][position.Y - 1] == ObjectType.Air || ObjectCoordinates[position.X + 1][position.Y - 1] == ObjectType.Collectible));
        }

        public static bool BotContainsLadder(Point position)
        {
            return !BotOutOfBounds(position) && BoundingBox(position).Any(point => ObjectCoordinates[point.X][point.Y] == ObjectType.Ladder);
        }

        public static bool BotOnSolid(Point position)
        {
            return !BotOutOfBounds(position) && ((position.Y > 1 && (ObjectCoordinates[position.X][position.Y - 1] == ObjectType.Solid || ObjectCoordinates[position.X + 1][position.Y - 1] == ObjectType.Solid)) || position.Y == 0);
        }

        public static bool BotOnHazard(Point position)
        {
            if (BotOutOfBounds(position)) return false;

            var hazardBelow = position.Y > 1 && (ObjectCoordinates[position.X][position.Y - 1] == ObjectType.Hazard || ObjectCoordinates[position.X + 1][position.Y - 1] == ObjectType.Hazard);
            var containsHazard = BoundingBox(position).Any(point => ObjectCoordinates[point.X][point.Y] == ObjectType.Hazard);
            return hazardBelow || containsHazard;
        }

        public static bool BotInUnachievablePosition(Point position, bool dugInDirection, bool exploring)
        {
            if (BotOutOfBounds(position)) return true;

            var containsSolidOrUnknown = BoundingBox(position).Any(point => ObjectCoordinates[point.X][point.Y] == ObjectType.Solid || (!exploring && !KnownCoordinates[point.X][point.Y]));

            return !dugInDirection && containsSolidOrUnknown;
        }

        public static bool BotOutOfBounds(Point position)
        {
            return BoundingBox(position).Any(point => point.X < 0 || point.X >= MapXLength || point.Y < 0 || point.Y >= MapYLength);
        }

        public static Point[] BoundingBox(Point position) => new Point[]
        {
            new Point(position.X, position.Y),
            new Point(position.X,  position.Y + 1),
            new Point(position.X + 1,  position.Y),
            new Point(position.X + 1,  position.Y + 1)
        };
        public static Point[] BoundingBox(Point position, bool wider) => new Point[]
        {
            new Point(position.X + 1,  position.Y + 1),
            new Point(position.X - 1,  position.Y - 1),
            new Point(position.X - 1,  position.Y + 1),
            new Point(position.X + 1,  position.Y - 1)
        };

        public static bool BotBoundsContainPoint(Point currentNode, Point endNode)
        {
            return !BotOutOfBounds(currentNode) && BoundingBox(currentNode).Any(point => point.Equals(endNode));
        }

        public static bool BoundingBoxHasUnknown(Point point)
        {
            return !BotOutOfBounds(point) && BoundingBox(point).Any(p => !KnownCoordinates[p.X][p.Y]);
        }

        public static bool BoundingBoxHasSolid(Point point, bool[][] dug)
        {
            return !BotOutOfBounds(point) && BoundingBox(point).Any(p => ObjectCoordinates[p.X][p.Y] == ObjectType.Solid && !dug[p.X][p.Y]);
        }

        public static bool BoundingBoxHasUnknown(Point point, bool wider)
        {
            return BoundingBox(point, wider).Any(p => p.X >= 0 && p.Y >= 0 && p.X < MapXLength && p.Y < MapYLength && !KnownCoordinates[p.X][p.Y]);
        }
        public static Point[] BotStealWindow(Point position, int stealRangeX, int StealRangeY)
        {
            int minWindowX = position.X - stealRangeX;
            int maxWindowX = position.X + 2 + stealRangeX;
            int minWindowY = position.Y - StealRangeY;
            int maxWindowY = position.Y + 2 + StealRangeY;

            return new Point[]
            {
                new Point(minWindowX, minWindowY),
                new Point(maxWindowX, maxWindowY),
            };
        }

        public static int ManhattanDistance(Point currentPoint, Point goal)
        {
            return Math.Abs(currentPoint.X - goal.X) + Math.Abs(currentPoint.Y - goal.Y);
        }

        public static List<Point> NextSolidContainingPoints(Point currentPoint, bool[][] dug)
        {
            List<Point> consideredPoints = new();

            int[][] directions = new int[3][];
            directions[0] = new int[] { -1, 0 };
            directions[1] = new int[] { 0, -1 };
            directions[2] = new int[] { 1, 0 };

            foreach (int[] direction in directions)
            {
                int x = currentPoint.X + direction[0];
                int y = currentPoint.Y + direction[1];
                Point nextPoint = new(x, y);

                if (BoundingBoxHasSolid(nextPoint, dug) && nextPoint.Y > PlatformsLeastY) consideredPoints.Add(nextPoint);
            }

            return consideredPoints;
        }

        public static SolidLump GetLumpFromPoint(Point point)
        {
            bool[][] visited = new bool[MapXLength][];
            for (int i = 0; i < MapXLength; i++)
            {
                visited[i] = new bool[MapYLength];
            }

            var lump = new SolidLump();

            DFS(point.X, point.Y, visited, lump);
            return lump;
        }

        private static void DFS(int x, int y, bool[][] visited, SolidLump lump)
        {
            if (x < 0 || x >= MapXLength || y < 0 || y >= MapYLength || visited[x][y] || ObjectCoordinates[x][y] != ObjectType.Solid)
            {
                return;
            }

            lump.Points.Add(new(x, y));
            visited[x][y] = true;
            if (lump.Size > 250) return;
            DFS(x + 1, y, visited, lump);
            DFS(x - 1, y, visited, lump);
            DFS(x, y + 1, visited, lump);
            DFS(x, y - 1, visited, lump);
        }

        public static List<Point> PointsOnStableFootingAtGivenManhattanDistance(Point startPosition, int distance)
        {
            List<Point> points = new List<Point>();

            for (int dx = 0; dx <= distance; dx++)
            {
                int dy = distance - dx;
                if (dy > 3) continue;
                points.Add(new Point(startPosition.X + dx, startPosition.Y + dy));
                points.Add(new Point(startPosition.X - dx, startPosition.Y + dy));
                points.Add(new Point(startPosition.X + dx, startPosition.Y - dy));
                points.Add(new Point(startPosition.X - dx, startPosition.Y - dy));

                // If dx is zero, it will add the same point 4 times, so we add a condition to avoid it
                if (dx != 0)
                {
                    points.Add(new Point(startPosition.X + dy, startPosition.Y + dx));
                    points.Add(new Point(startPosition.X - dy, startPosition.Y + dx));
                    points.Add(new Point(startPosition.X + dy, startPosition.Y - dx));
                    points.Add(new Point(startPosition.X - dy, startPosition.Y - dx));
                }
            }

            // Removing duplicates
            points = points.Distinct().Where(x => !BotOutOfBounds(x) && x.Y >= Math.Min(startPosition.Y, PlatformsLeastY)).ToList();

            return points;
        }

        public static double EuclideanDistance(Point currentPosition, Point opponentPosition)
        {
            int xDiff = opponentPosition.X - currentPosition.X;
            int yDiff = opponentPosition.Y - currentPosition.Y;

            return Math.Sqrt(xDiff * xDiff + yDiff * yDiff);
        }

        public static bool BotStealWindowContainsPoint(Node currentNode, Point end)
        {
            var stealWindow = BotStealWindow(currentNode, 2, 2);
            return end.X > stealWindow[0].X && end.X < stealWindow[1].X && end.Y > stealWindow[0].Y && end.Y < stealWindow[1].Y;
        }

        public static InputCommand GetDirection(Point currentPosition, Point target)
        {
            var x1 = currentPosition.X;
            var y1 = currentPosition.Y;
            var x2 = target.X;
            var y2 = target.Y;
            if (x1 > x2)
                return y1 > y2 ? InputCommand.DOWNLEFT : y1 < y2 ? InputCommand.UPLEFT : InputCommand.LEFT;
            else if (x1 < x2)
                return y1 > y2 ? InputCommand.DOWNRIGHT : y1 < y2 ? InputCommand.UPRIGHT : InputCommand.RIGHT;
            else
                return y1 > y2 ? InputCommand.DOWN : y1 < y2 ? InputCommand.UP : 0;
        }

        public static int CalculatePercentage(double distance, int contactValue)
        {
            double percentage = (distance / contactValue) * 100;
            if (percentage > 100)
                return 0;
            else if (percentage > 75)
                return 4;
            else if (percentage > 50)
                return 3;
            else if (percentage > 25)
                return 2;
            else
                return 1;
        }

        public static int GetDistanceFactor(Point currentPosition, Point posn)
        {
            var distance = EuclideanDistance(currentPosition, posn);
            return CalculatePercentage(distance, 50);
        }

        public static Point GetNearestSolid(Point startPoint)
        {
            var visited = new bool[MapXLength][];
            for (int i = 0; i < MapXLength; i++)
            {
                visited[i] = new bool[MapYLength];
            }
            Queue<Point> queue = new Queue<Point>();
            queue.Enqueue(startPoint);
            visited[startPoint.X][startPoint.Y] = true;

            while (queue.Count > 0)
            {
                Point current = queue.Dequeue();

                if (ObjectCoordinates[current.X][current.Y] == ObjectType.Solid)
                {
                    return current;
                }

                foreach (Point neighbor in GetNeighbors(current))
                {
                    if (!visited[neighbor.X][neighbor.Y])
                    {
                        queue.Enqueue(neighbor);
                        visited[neighbor.X][neighbor.Y] = true;
                    }
                }
            }

            return new(-1, -1);  // Return an invalid point if no DesiredValue is found
        }

        private static List<Point> GetNeighbors(Point current)
        {
            List<Point> neighbors = new List<Point>();
            int[] dx = { -1, 1, 0, 0, -1, -1, 1, 1 };
            int[] dy = { 0, 0, -1, 1, -1, 1, -1, 1 };

            for (int i = 0; i < 8; i++)
            {
                int newX = current.X + dx[i];
                int newY = current.Y + dy[i];

                if (IsInBounds(newX, newY))
                {
                    neighbors.Add(new Point(newX, newY));
                }
            }

            return neighbors;
        }

        private static bool IsInBounds(int x, int y)
        {
            return x >= 0 && x < MapXLength && y >= 0 && y < MapYLength;
        }
    }
}