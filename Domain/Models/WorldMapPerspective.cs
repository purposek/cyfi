using Domain.Enums;

namespace Domain.Models
{
    public static class WorldMapPerspective
    {

        public static bool[][] KnownCoordinates { get; }
        public static List<Point> Collectibles { get; }
        private static int level;

        public static ObjectType[][] ObjectCoordinates { get; }

        static WorldMapPerspective()
        {
            Collectibles = new List<Point>();
            ObjectCoordinates = new ObjectType[100][];
            KnownCoordinates = new bool[100][];
            for (int i = 0; i < 100; i++)
            {
                ObjectCoordinates[i] = new ObjectType[100];
                KnownCoordinates[i] = new bool[100];
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
                for (int i = 0; i < 100; i++)
                {
                    for (int j = 0; j < 100; j++)
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
                    if (worldX >= 0 && worldX < 100 && worldY >= 0 && worldY < 100)
                    {
                        SetCoordinates(worldX, worldY, (ObjectType)botState.HeroWindow[i][j]);

                        if (Collectibles.Contains(new Point(worldX, worldY)) && (ObjectType)botState.HeroWindow[i][j] != ObjectType.Collectible)
                        {
                            Collectibles.Remove(new Point(worldX, worldY));
                        }

                        if ((ObjectType)botState.HeroWindow[i][j] == ObjectType.Collectible && !Collectibles.Contains(new Point(worldX, worldY)))
                        {
                            Collectibles.Add(new Point(worldX, worldY));
                        }
                    }
                }
            }
        }


        public static bool BotIsOnStableFooting(Point position)
        {
            return BotOnPlatform(position) || BotOnLadder(position) || BotOnSolid(position);
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

        public static bool BotInUnachievablePosition(Point position, bool dugInDirection)
        {
            if (BotOutOfBounds(position)) return true;

            var containsSolid = BoundingBox(position).Any(point => ObjectCoordinates[point.X][point.Y] == ObjectType.Solid);

            return !dugInDirection && containsSolid;
        }

        public static bool BotOutOfBounds(Point position)
        {
            return BoundingBox(position).Any(point => point.X < 0 || point.X > 99 || point.Y < 0 || point.Y > 99);
        }
        public static Point[] BoundingBox(Point position) => new Point[]
        {
            new Point(position.X, position.Y),
            new Point(position.X,  position.Y + 1),
            new Point(position.X + 1,  position.Y),
            new Point(position.X + 1,  position.Y + 1)
        };

        public static bool BotBoundsContainPoint(Point currentNode, Point endNode)
        {
            return !BotOutOfBounds(currentNode) && BoundingBox(currentNode).Any(point => point.Equals(endNode));
        }
    }
}