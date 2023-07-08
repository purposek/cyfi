using Domain.Enums;
using Domain.Models;
using ReferenceBot.AI.DataStructures.Pathfinding;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ReferenceBot.AI.States
{
    class SearchingState : State
    {
        // -1 for left, 1 for right
        private int direction;
        private int SearchCooldown;
        private bool IsSearching;

        public bool PathFound { get; private set; }

        public SearchingState(BotStateMachine StateMachine) : base(StateMachine)
        { }

        public override void EnterState(State PreviousState)
        {
            SearchCooldown = 0;
            direction = 1;
            Console.WriteLine("Entering Searching");
        }

        public override void ExitState(State NextState)
        {
            direction = 0;
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
                return ManhattanDistance(x, Goal) - ManhattanDistance(y, Goal);
            }
        }

        private void FindPathToNearestCollectableAndEnterCollectMode(BotStateDTO BotState)
        {
            // Prevent accidentally searching multiple times.
            if (IsSearching)
            {
                return;
            }
            Console.WriteLine("Searching for collectibles");
            IsSearching = true;
            PathFound = false;
            var playerBounds = new BoundingBox(BotState.X, BotState.Y, 2, 2);

            // Find all collectibles
            List<Point> collectibles = WorldMapPerspective.Collectibles;
            if (collectibles.Count() == 0) {
                for (int i = 0; i < 100; i++)
                {
                    for (int j = 0; j < 100; j++)
                    {
                        if (WorldMapPerspective.KnownCoordinates[i][j] && WorldMapPerspective.BoundingBoxHasUnknown(new Point(i, j)))
                        {
                            collectibles.Add(new Point(i, j));
                        }
                    }
                }
            }

            if (collectibles.Count() == 0)
            {
                for (int i = 0; i < 100; i++)
                {
                    for (int j = 0; j < 100; j++)
                    {
                        if (WorldMapPerspective.ObjectCoordinates[i][j] == ObjectType.Solid)
                        {
                            collectibles.Add(new Point(i, j));
                        }
                    }
                }
            }

            Console.WriteLine($"Search found {collectibles.Count()} collectibles");
            if (collectibles.Count() == 0)
            {
                SearchCooldown = 5;
                IsSearching = false;
                return;
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
            Path? closestPath = PerformAStarSearch(playerBounds.Position, closestCollectibleByPath, BotState.CurrentState);
            foreach (var collectible in closestCollectibles.Skip(1))
            {
                int closestPathDistance = closestPath is Path path ? path.Length : Int32.MaxValue;
                Console.WriteLine("Finding path");
                var newPath = PerformAStarSearch(playerBounds.Position, collectible, BotState.CurrentState);
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
                var newState = new Collecting(StateMachine, closestCollectibleByPath, closestPath);
                ChangeState(newState);
                PathFound = true;
            }
            else
            {
                Console.WriteLine("Failed to find a suitable path");
                SearchCooldown = 5;
                IsSearching = false;
            }
        }

        public override InputCommand Update(BotStateDTO BotState)
        {
            FindPathToNearestCollectableAndEnterCollectMode(BotState);
            if(PathFound)
            {
                return InputCommand.None;
            }

            //moving upright for now
            if (BotState.X > 90) direction = -1;
            if (BotState.X < 10) direction = 1;

            return direction == -1 ? InputCommand.DIGLEFT : InputCommand.DIGRIGHT;
            
        }
    }
}