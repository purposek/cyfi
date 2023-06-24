using System;
using System.Collections.Generic;

namespace NETCoreBot.Enums
{
    public static class WorldMapPerspective
    {

        private static bool[,] knownCoordinates { get; set; } = new bool[500, 200];
        public static List<Tuple<int, int>> Collectibles = new List<Tuple<int, int>>();
        public static List<Tuple<int, int>> Intersections = new List<Tuple<int, int>>();
        public static ObjectType[,] ObjectCoordinates { get; set; } = new ObjectType[500, 200];

        public static void SetCoordinates(int x, int y, ObjectType objectType)
        {
            ObjectCoordinates[x, y] = objectType;
        }
        public static void UpdateSegments()
        {
        }
    }
}