using Domain.Enums;

namespace Domain.Models
{
    public static class WorldMapPerspective
    {
        // Define the map size constants
        public const int MapXLength = 100;
        public const int MapYLength = 100;

        public static bool[][] KnownCoordinates { get; }
        public static List<Point> Collectibles { get; }
        private static int level;

        public static ObjectType[][] ObjectCoordinates { get; }
        public static bool DiggingMode { get; set; }

        static WorldMapPerspective()
        {
            Collectibles = new List<Point>();
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
            if (botState.CurrentLevel != level)
            {
                Collectibles.Clear();
                level = botState.CurrentLevel;
                for (int i = 0; i < MapXLength; i++)
                {
                    for (int j = 0; j < MapYLength; j++)
                    {
                        SetCoordinates(i, j, (int)ObjectType.Air);
                        SetKnownCoordinates(i, j, false);
                    }
                }
            }

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
            return !BotOutOfBounds(position) && position.Y > 1 && (ObjectCoordinates[position.X][position.Y -1] == ObjectType.Ladder || ObjectCoordinates[position.X + 1][position.Y - 1] == ObjectType.Ladder);
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
            return !BotOutOfBounds(position) && position.Y > 1 && (ObjectCoordinates[position.X][position.Y - 1] == ObjectType.Solid || ObjectCoordinates[position.X + 1][position.Y - 1] == ObjectType.Solid);
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

                if (BoundingBoxHasSolid(nextPoint, dug)) consideredPoints.Add(nextPoint);
            }

            return consideredPoints;
        }
    }
}