using Domain.Enums;
using Domain.Models;
using ReferenceBot.AI.DataStructures.Pathfinding;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ReferenceBot.AI.States
{
    class SearchingState : State
    {
        private bool IsSearching;

        public bool PathFound { get; private set; }
        public bool Exploring { get; private set; }

        public SearchingState(BotStateMachine StateMachine) : base(StateMachine)
        { }

        public override void EnterState(State PreviousState)
        {
            Console.WriteLine("Entering Searching");
        }

        public override void ExitState(State NextState)
        {
        }

        private class CollectibleSorter : IComparer<Point>
        {
            Point Goal;

            public CollectibleSorter(Point goal)
            {
                Goal = goal;
            }

            public int Compare(Point x, Point y)
            {
                return WorldMapPerspective.ManhattanDistance(x, Goal) - WorldMapPerspective.ManhattanDistance(y, Goal);
            }
        }

        private Path FindPathToNearestCollectableAndEnterCollectMode(BotStateDTO BotState, Dictionary<int, (Point Position, string MovementState, InputCommand CommandSent, Point DeltaToPosition, int Level)> gameStateDict)
        {
            // Prevent accidentally searching multiple times.
            if (IsSearching)
            {
                return null;
            }
            Console.WriteLine("Searching for collectibles");
            IsSearching = true;
            PathFound = false;
            Exploring = false;
            var playerBounds = new BoundingBox(BotState.X, BotState.Y, 2, 2);

            // Find all collectibles
            List<Point> collectibles = WorldMapPerspective.Collectibles;

            Console.WriteLine($"Search found {collectibles.Count()} collectibles");
            if (collectibles.Count() == 0)
            {
                IsSearching = false;
                return null;
            }
            List<Point> closestCollectibles;

            SortedSet<Point> sortedCollectibles = new SortedSet<Point>(new CollectibleSorter(playerBounds.Position));
            foreach (var collectible in collectibles)
            {
                sortedCollectibles.Add(collectible);
            }
            //var count = sortedCollectibles.Count;
            //count = count > 100 ? 30 : 3;
            closestCollectibles = sortedCollectibles.Take(3).ToList();

            //Stopwatch sw = Stopwatch.StartNew();
            // Calculate which collectible has the shortest path
            Point closestCollectibleByPath = closestCollectibles.First();
            Path? closestPath = PerformBFS(playerBounds.Position, closestCollectibleByPath, BotState.CurrentState, Exploring, gameStateDict, BotState.GameTick);
            foreach (var collectible in closestCollectibles.Skip(1))
            {
                int closestPathDistance = closestPath is Path path ? path.Length : Int32.MaxValue;
                Console.WriteLine("Finding path");
                var newPath = PerformBFS(playerBounds.Position, collectible, BotState.CurrentState, Exploring, gameStateDict, BotState.GameTick);
                if (newPath != null)
                {
                    Console.WriteLine($"Found path of length {newPath.Length}");
                }
                else
                {
                    Console.WriteLine($"Failed to find path");
                }

                if (newPath is Path newP && newP.Length < closestPathDistance)
                {
                    closestCollectibleByPath = collectible;
                    closestPath = newPath;
                }
            }
            //sw.Stop();
            //Console.WriteLine($"Finding path took {0}ms", sw.ElapsedMilliseconds);

            // If closestPath is null, we haven't managed to pathfind to any collectibles, so keep searching.
            if (closestPath is Path)
            {
                Console.WriteLine($"Closest path found of length {closestPath.Length}");
                var newState = new Collecting(StateMachine, closestCollectibleByPath, closestPath, Exploring);
                ChangeState(newState);
                PathFound = true;
                return closestPath;
            }
            else
            {
                Console.WriteLine("Failed to find a suitable path");
                IsSearching = false;
                return null;
                //var newState = new Digging(StateMachine);
                //ChangeState(newState);

            }
        }

        public override InputCommand Update(BotStateDTO BotState, Dictionary<int, (Point Position, string MovementState, InputCommand CommandSent, Point DeltaToPosition, int Level)> gameStateDict)
        {
            var pathToCollectible = FindPathToNearestCollectableAndEnterCollectMode(BotState, gameStateDict);
            if (PathFound)
            {
                var nextNode = pathToCollectible.Nodes.FirstOrDefault(x => x.Parent != null && ((Point)x.Parent).Equals(BotState.CurrentPosition));
                return nextNode != null ? nextNode.CommandToReachMe : InputCommand.None;
            }

            var explorePath = FindPathToExploreAndEnterCollectMode(BotState, gameStateDict);
            if (explorePath != null)
            {
                var nextNode = explorePath.Nodes.FirstOrDefault(x => x.Parent != null && ((Point)x.Parent).Equals(BotState.CurrentPosition));
                return nextNode != null ? nextNode.CommandToReachMe : InputCommand.None;
            }
            else
            {
                return InputCommand.None;
            }
        }

        private Path FindPathToExploreAndEnterCollectMode(BotStateDTO botState, Dictionary<int, (Point Position, string MovementState, InputCommand CommandSent, Point DeltaToPosition, int Level)> gameStateDict)
        {
            // Prevent accidentally searching multiple times.
            if (IsSearching)
            {
                return null;
            }
            Console.WriteLine("Searching for collectibles");
            IsSearching = true;
            PathFound = false;
            Exploring = false;
            var playerBounds = new BoundingBox(botState.X, botState.Y, 2, 2);

            // Find all collectibles
            List<Point> collectibles = new List<Point>();

            for (int i = 0; i < WorldMapPerspective.MapXLength; i++)
            {
                for (int j = 0; j < WorldMapPerspective.MapYLength; j++)
                {
                    if (WorldMapPerspective.KnownCoordinates[i][j])
                    {
                        if (WorldMapPerspective.ObjectCoordinates[i][j] == ObjectType.Ladder && WorldMapPerspective.BoundingBoxHasUnknown(new Point(i, j), true))
                        {
                            collectibles.Add(new Point(i, j));
                            Exploring = true;
                        }
                        if (WorldMapPerspective.ObjectCoordinates[i][j] == ObjectType.Platform && WorldMapPerspective.BoundingBoxHasUnknown(new Point(i, j + 1)))
                        {
                            collectibles.Add(new Point(i, j + 1));
                            Exploring = true;
                        }
                    }
                }
            }


            if (collectibles.Count() == 0)
            {
                var random = new Random();
                //TODO find dig algo
                for (int i = 0; i < 4; i++)
                {
                    GaussianRandom gaussianRandom = new GaussianRandom();
                    int yCoord = (int)gaussianRandom.NextGaussian(70, 20);
                    var xCoord = random.Next(0, WorldMapPerspective.MapXLength);
                    while (yCoord >= WorldMapPerspective.MapYLength || yCoord < 0 || WorldMapPerspective.ObjectCoordinates[xCoord][yCoord] != ObjectType.Solid)
                    {
                        xCoord = random.Next(0, WorldMapPerspective.MapXLength);
                        yCoord = (int)gaussianRandom.NextGaussian(70, 20);
                    }
                    
                    collectibles.Add(new Point(xCoord, yCoord));
                    Exploring = true;
                }
                //var newState = new Digging(StateMachine);
                //ChangeState(newState);
            }

            Console.WriteLine($"Search found {collectibles.Count()} collectibles");
            if (collectibles.Count() == 0)
            {
                IsSearching = false;
                return null;
            }
            List<Point> closestCollectibles;

            SortedSet<Point> sortedCollectibles = new SortedSet<Point>(new CollectibleSorter(playerBounds.Position));
            foreach (var collectible in collectibles)
            {
                sortedCollectibles.Add(collectible);
            }
            //var count = sortedCollectibles.Count;
            //count = count > 100 ? 30 : 3;
            closestCollectibles = sortedCollectibles.Take(3).ToList();

            //Stopwatch sw = Stopwatch.StartNew();
            // Calculate which collectible has the shortest path
            Point closestCollectibleByPath = closestCollectibles.First();
            Path? closestPath = PerformAStarSearch(playerBounds.Position, closestCollectibleByPath, botState.CurrentState, Exploring, gameStateDict, botState.GameTick);
            foreach (var collectible in closestCollectibles.Skip(1))
            {
                int closestPathDistance = closestPath is Path path ? path.Length : Int32.MaxValue;
                Console.WriteLine("Finding path");
                var newPath = PerformAStarSearch(playerBounds.Position, collectible, botState.CurrentState, Exploring, gameStateDict, botState.GameTick);
                if (newPath != null)
                {
                    Console.WriteLine($"Found path of length {newPath.Length}");
                }
                else
                {
                    Console.WriteLine($"Failed to find path");
                }

                if (newPath is Path newP && newP.Length < closestPathDistance)
                {
                    closestCollectibleByPath = collectible;
                    closestPath = newPath;
                }
            }
            //sw.Stop();
            //Console.WriteLine($"Finding path took {0}ms", sw.ElapsedMilliseconds);

            // If closestPath is null, we haven't managed to pathfind to any collectibles, so keep searching.
            if (closestPath is Path)
            {
                Console.WriteLine($"Closest path found of length {closestPath.Length}");
                var newState = new Collecting(StateMachine, closestCollectibleByPath, closestPath, Exploring);
                ChangeState(newState);
                PathFound = true;
                return closestPath;
            }
            else
            {
                Console.WriteLine("Failed to find a suitable path");
                IsSearching = false;
                return null;
                //var newState = new Digging(StateMachine);
                //ChangeState(newState);

            }
        }
    }

    public class GaussianRandom
    {
        private Random random = new Random();

        public double NextGaussian(double mean, double standardDeviation)
        {
            double u1 = 1.0 - random.NextDouble(); // Uniform random number from 0 to 1
            double u2 = 1.0 - random.NextDouble(); // Uniform random number from 0 to 1

            // Use Box-Muller transform to generate numbers with a Gaussian distribution
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

            // Scale and shift the distribution to match the desired mean and standard deviation
            return mean + standardDeviation * randStdNormal;
        }
    }
}