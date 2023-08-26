using C5;
using Domain.Enums;
using Domain.Models;
using Domain.Models.Pathfinding;
using ReferenceBot.Services.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ReferenceBot.Services
{
    public class PathfindingService : IPathfindingService
    {
        private bool[][] dug;

        public event EventHandler<Path> BestPathFound;
        public List<Point> pointsToExclude = new();
        private int pathfindFailures;
        private bool amDesperate;

        public bool Busy { get; set; }

        public Path FindBestPath(Point startPosition, Point deltaToStartPosition, MovementState botMovementStateAtStart, int jumpHeightAtStart, PathType pathType, bool clearExcludedPoints)
        {
            Busy = true;
            WorldMapPerspective.DiggingMode = false;
            amDesperate = false;
            if (clearExcludedPoints)
            {
                pointsToExclude.Clear();
                pathfindFailures = 0;
            }

            List<Point> collectibles = pathType == PathType.Collecting ? WorldMapPerspective.Collectibles.Except(pointsToExclude).Where(col => col.X > startPosition.X - 17 && col.X < startPosition.X + 18 && col.Y > startPosition.Y - 11 && col.Y < startPosition.Y + 12).ToList() : new();

            //Console.WriteLine($"Search found {collectibles.Count()} collectibles");
            if (collectibles.Count == 0 && pathType != PathType.Digging)
            {
                collectibles = new();
                pathType = PathType.Exploring;
                for (int i = 0; i < WorldMapPerspective.MapXLength; i++)
                {
                    for (int j = 0; j < WorldMapPerspective.MapYLength; j++)
                    {
                        if (WorldMapPerspective.KnownCoordinates[i][j])
                        {
                            if (WorldMapPerspective.ObjectCoordinates[i][j] == ObjectType.Ladder && WorldMapPerspective.BoundingBoxHasUnknown(new Point(i, j), true))
                            {
                                collectibles.Add(new Point(i, j));
                            }

                            if (WorldMapPerspective.ObjectCoordinates[i][j] == ObjectType.Platform && WorldMapPerspective.BoundingBoxHasUnknown(new Point(i, j + 1), true))
                            {
                                collectibles.Add(new Point(i, j + 1));
                            }
                        }
                    }
                }

                collectibles = collectibles.Except(pointsToExclude).ToList();
                if (collectibles.Count == 0)
                {
                    pathType = PathType.Digging;
                    dug = new bool[WorldMapPerspective.MapXLength][];
                    for (int i = 0; i < WorldMapPerspective.MapXLength; i++)
                    {
                        dug[i] = new bool[WorldMapPerspective.ObjectCoordinates[i].Length];
                    }

                    if (startPosition.Y > WorldMapPerspective.PlatformsLeastY) collectibles = WorldMapPerspective.NextSolidContainingPoints(startPosition, dug);
                    collectibles = collectibles.Except(pointsToExclude).ToList();

                    if (collectibles.Count == 0)
                    {
                        collectibles = new();

                        if (WorldMapPerspective.BotOnSolid(startPosition) && startPosition.Y > 0)
                        {
                            if (WorldMapPerspective.ObjectCoordinates[startPosition.X][startPosition.Y - 1] == ObjectType.Solid)
                            {
                                var lump = WorldMapPerspective.GetLumpFromPoint(new(startPosition.X, startPosition.Y - 1));
                                if (lump.Size > 10 && lump.TopLeft.Y > WorldMapPerspective.PlatformsLeastY)
                                {
                                    if (WorldMapPerspective.BotIsOnStableFooting(lump.TopLeft) || WorldMapPerspective.BotIsOnStableFooting(new(lump.TopLeft.X, lump.TopLeft.X + 1))) collectibles.Add(lump.TopLeft);
                                    if (WorldMapPerspective.BotIsOnStableFooting(lump.TopRight) || WorldMapPerspective.BotIsOnStableFooting(new(lump.TopRight.X, lump.TopRight.X - 1))) collectibles.Add(lump.TopRight);
                                }
                            }
                            else if (WorldMapPerspective.ObjectCoordinates[startPosition.X + 1][startPosition.Y - 1] == ObjectType.Solid)
                            {
                                var lump = WorldMapPerspective.GetLumpFromPoint(new(startPosition.X + 1, startPosition.Y - 1));
                                if (lump.Size > 10 && lump.TopLeft.Y > WorldMapPerspective.PlatformsLeastY)
                                {
                                    if (WorldMapPerspective.BotIsOnStableFooting(lump.TopLeft) || WorldMapPerspective.BotIsOnStableFooting(new(lump.TopLeft.X, lump.TopLeft.X + 1))) collectibles.Add(lump.TopLeft);
                                    if (WorldMapPerspective.BotIsOnStableFooting(lump.TopRight) || WorldMapPerspective.BotIsOnStableFooting(new(lump.TopRight.X, lump.TopRight.X - 1))) collectibles.Add(lump.TopRight);
                                }
                            }

                        }

                        var random = new Random();
                        if (collectibles.Count < 4)
                        {
                            for (int i = 0; i < 10; i++)
                            {
                                GaussianRandom gaussianRandom = new GaussianRandom();
                                int delta = 2;
                                int counter = 1;
                                if (Math.Abs(startPosition.Y - WorldMapPerspective.PlatformsGreatestY) < 10) delta = 10;
                                int yCoord = (int)gaussianRandom.NextGaussian(startPosition.Y, delta);
                                var xCoord = (int)gaussianRandom.NextGaussian(startPosition.X, 5);

                                while (yCoord >= WorldMapPerspective.MapYLength || yCoord < WorldMapPerspective.PlatformsLeastY || xCoord >= WorldMapPerspective.MapXLength || xCoord < 0 || WorldMapPerspective.ObjectCoordinates[xCoord][yCoord] != ObjectType.Solid)
                                {
                                    if (counter < 5) counter++;
                                    yCoord = (int)gaussianRandom.NextGaussian(startPosition.Y, delta * counter);
                                    xCoord = (int)gaussianRandom.NextGaussian(startPosition.X, 5 * counter);
                                }

                                var lump = WorldMapPerspective.GetLumpFromPoint(new(xCoord, yCoord));
                                var yVariable = lump.TopLeft.Y;
                                if (lump.Size > 10 && yVariable > WorldMapPerspective.PlatformsLeastY)
                                {
                                    if (startPosition.Y < WorldMapPerspective.PlatformsLeastY + 5)
                                    {
                                        var xVariable = lump.TopLeft.X;
                                        var topRightX = lump.TopRight.X;
                                        while (xVariable <= topRightX)
                                        {
                                            var considered = new Point(xVariable, yVariable);
                                            if (WorldMapPerspective.BotIsOnStableFooting(considered)) collectibles.Add(considered);
                                            xVariable++;
                                        }
                                    }
                                    else
                                    {
                                        if (WorldMapPerspective.BotIsOnStableFooting(lump.TopLeft) || WorldMapPerspective.BotIsOnStableFooting(new(lump.TopLeft.X, lump.TopLeft.X + 1))) collectibles.Add(lump.TopLeft);
                                        if (WorldMapPerspective.BotIsOnStableFooting(lump.TopRight) || WorldMapPerspective.BotIsOnStableFooting(new(lump.TopRight.X, lump.TopRight.X - 1))) collectibles.Add(lump.TopRight);
                                    }
                                }

                                if (lump.Size > 10 && yVariable > WorldMapPerspective.PlatformsGreatestY)
                                {
                                    collectibles.Add(lump.PointOnMyLevel(startPosition.Y));
                                }

                                if (collectibles.Count > 4) break;
                            }
                        }

                        if (collectibles.Count == 0)
                        {
                            amDesperate = true;
                            foreach (var point in WorldMapPerspective.PointsOnStableFootingAtGivenManhattanDistance(startPosition, 5).OrderByDescending(s => s.Y).Take(3))
                            {
                                collectibles.Add(point);
                            }
                        }
                    }
                    else
                    {
                        WorldMapPerspective.DiggingMode = true;
                    }
                }
            }

            List<Point> closestCollectibles;

            SortedSet<Point> sortedCollectibles = pathType == PathType.Exploring && WorldMapPerspective.TicksInLevel < 130 ? new SortedSet<Point>(new CollectibleYSorter()) : new SortedSet<Point>(new CollectibleSorter(startPosition));
            foreach (var collectible in collectibles)
            {
                sortedCollectibles.Add(collectible);
            }
            //var count = sortedCollectibles.Count;
            var count = amDesperate ? 10 : 3;
            closestCollectibles = sortedCollectibles.Take(3).ToList();

            //Stopwatch sw = Stopwatch.StartNew();
            // Calculate which collectible has the shortest path
            Point closestCollectibleByPath = closestCollectibles.Any() ? closestCollectibles.First() : RandomPoint(startPosition);
            Path closestPath = pathType == PathType.Collecting ? PerformBFS(startPosition, closestCollectibleByPath, botMovementStateAtStart, pathType, jumpHeightAtStart, deltaToStartPosition)
                : (WorldMapPerspective.DiggingMode ? PerformDiggingDFS(startPosition, botMovementStateAtStart, pathType, collectibles) : PerformAStarSearch(startPosition, closestCollectibleByPath, botMovementStateAtStart, pathType, jumpHeightAtStart, deltaToStartPosition));
            if (!WorldMapPerspective.DiggingMode)
            {
                foreach (var collectible in closestCollectibles.Skip(1))
                {
                    int closestPathDistance = closestPath is Path path ? (pathType == PathType.Exploring ? path.Target.Y : path.Length) : Int32.MaxValue;
                    //Console.WriteLine("Finding path");
                    var newPath = pathType == PathType.Collecting ? PerformBFS(startPosition, closestCollectibleByPath, botMovementStateAtStart, pathType, jumpHeightAtStart, deltaToStartPosition)
                    : PerformAStarSearch(startPosition, closestCollectibleByPath, botMovementStateAtStart, pathType, jumpHeightAtStart, deltaToStartPosition);
                    //if (newPath != null)
                    //{
                    //    Console.WriteLine($"Found path of length {newPath.Length}");
                    //}
                    //else
                    //{
                    //    Console.WriteLine($"Failed to find path");
                    //}

                    if (newPath is Path newP && (pathType == PathType.Exploring ? newP.Target.Y < closestPathDistance : newP.Length < closestPathDistance))
                    {
                        closestCollectibleByPath = collectible;
                        closestPath = newPath;
                    }
                }
            }

            if (closestPath is Path)
            {
                //Console.WriteLine($"Closest path found of length {closestPath.Length}");
                OnBestPathFound(closestPath);
                pointsToExclude.Add(closestPath.Target);
                Busy = false;
                return closestPath;
            }
            else
            {
                //Console.WriteLine("Failed to find a suitable path");
                pointsToExclude.AddRange(closestCollectibles);
                pathfindFailures++;
                if (pathfindFailures > 10)
                {
                    Busy = false;
                    return null;
                }

                return FindBestPath(startPosition, deltaToStartPosition, botMovementStateAtStart, jumpHeightAtStart, pathType, false);
            }
        }

        private Point RandomPoint(Point startPosition)
        {
            GaussianRandom gaussianRandom = new GaussianRandom();
            int yCoord = (int)gaussianRandom.NextGaussian(startPosition.Y + 5, 5);
            var xCoord = (int)gaussianRandom.NextGaussian(startPosition.X, 5);

            return new(xCoord, yCoord);
        }

        private Path PerformDiggingDFS(Point startPosition, MovementState botMovementStateAtStart, PathType pathType, List<Point> closestSolidContaingPoints)
        {
            var nextPoints = closestSolidContaingPoints.Where(p => WorldMapPerspective.BotIsOnStableFooting(p) && !WorldMapPerspective.BotOnHazard(p)).OrderByDescending(s => s.Y).ToList();
            var currentNode = new Node(startPosition.X, startPosition.Y, null, botMovementStateAtStart, 0, InputCommand.None, true, 0);
            foreach (var point in WorldMapPerspective.BoundingBox(startPosition))
            {
                dug[point.X][point.Y] = true;
            }

            while (nextPoints.Count > 0)
            {
                var nextPoint = nextPoints.First();
                foreach (var point in WorldMapPerspective.BoundingBox(nextPoint))
                {
                    dug[point.X][point.Y] = true;
                }

                InputCommand inputCommand = nextPoint.Y < currentNode.Y ? InputCommand.DIGDOWN : (nextPoint.X < currentNode.X ? InputCommand.DIGLEFT : InputCommand.DIGRIGHT);
                currentNode = new Node(nextPoint.X, nextPoint.Y, currentNode, botMovementStateAtStart, 0, inputCommand, true, 0);
                nextPoints = WorldMapPerspective.NextSolidContainingPoints(nextPoint, dug).Where(p => WorldMapPerspective.BotIsOnStableFooting(p) && !WorldMapPerspective.BotOnHazard(p)).OrderByDescending(s => s.Y).ToList();
            }

            if (currentNode.Parent == null)
            {
                if (currentNode.Y < WorldMapPerspective.PlatformsLeastY + 15)
                {
                    return null;
                }
                else
                {
                    nextPoints = closestSolidContaingPoints.OrderByDescending(s => s.Y).ToList();
                    var nextPoint = nextPoints.First();
                    InputCommand inputCommand = nextPoint.Y < currentNode.Y ? InputCommand.DIGDOWN : (nextPoint.X < currentNode.X ? InputCommand.DIGLEFT : InputCommand.DIGRIGHT);
                    currentNode = new Node(nextPoint.X, nextPoint.Y, currentNode, botMovementStateAtStart, 0, inputCommand, true, 0);
                }
            }

            return ConstructPath(currentNode, currentNode, pathType);
        }

        protected virtual void OnBestPathFound(Path bestPath)
        {
            BestPathFound?.Invoke(this, bestPath);
        }

        // A* algorithm
        protected static Path PerformAStarSearch(Point start, Point end, MovementState botMovementState, PathType pathType, int jumpHeight, Point deltaToStartPosition)
        {
            var exploring = pathType == PathType.Exploring;

            var startNode = new Node(start.X, start.Y, null, botMovementState, 0, InputCommand.None, true, jumpHeight);
            startNode.DeltaToMe = deltaToStartPosition;

            startNode.HCost = WorldMapPerspective.ManhattanDistance(start, end);

            var openSet = new IntervalHeap<Node>(new NodeComparer());
            var closedSet = new C5.HashSet<Point>();

            openSet.Add(startNode);

            while (openSet.Count > 0)
            {
                var currentNode = openSet.DeleteMin();//openSet.First();
                //Console.WriteLine($"Processing point: (X: {currentNode.X}, Y: {currentNode.Y}, FCost: {currentNode.FCost})");
                if (WorldMapPerspective.BotBoundsContainPoint(currentNode, end) || (exploring && startNode.HCost > 10 && (currentNode.HCost < 5)))
                {
                    //endNode.Parent = currentNode.Parent;
                    return ConstructPath(currentNode, end, pathType);
                }
                //openSet.Remove(currentNode);
                closedSet.Add(currentNode);

                var neighbours = Neighbours(currentNode, exploring);

                foreach (var neighbour in neighbours)
                {
                    if (closedSet.Contains(neighbour))
                    {
                        continue;
                    }
                    neighbour.HCost = WorldMapPerspective.ManhattanDistance(neighbour, end);
                    if (!openSet.Contains(neighbour))
                    {
                        openSet.Add(neighbour);
                        continue;
                    }
                    var openNeighbour = openSet.Where(neighbour.Equals).First();
                    if (neighbour.GCost < openNeighbour.GCost)
                    {
                        openNeighbour.GCost = neighbour.GCost;
                        openNeighbour.Parent = neighbour.Parent;
                    }
                }
                //openSet = openSet.OrderBy((node) => node.FCost).ToHashSet();
            }

            return null;
        }

        // Breadth First Search algorithm
        protected static Path PerformBFS(Point start, Point end, MovementState botMovementState, PathType pathType, int jumpHeight, Point deltaToStartPosition, bool attemptToLandSafely = false)
        {
            var exploring = pathType == PathType.Exploring;

            var startNode = new Node(start.X, start.Y, null, botMovementState, 0, InputCommand.None, true, jumpHeight);
            startNode.DeltaToMe = deltaToStartPosition;

            startNode.HCost = WorldMapPerspective.ManhattanDistance(start, end);

            var openSet = new Queue<Node>();
            var closedSet = new C5.HashSet<Point>();

            openSet.Enqueue(startNode);
            closedSet.Add(startNode);

            while (openSet.Count > 0)
            {
                var currentNode = openSet.Dequeue();
                //if (WorldMapPerspective.BotBoundsContainPoint(currentNode, end)) attemptToLandSafely = true;
                if (WorldMapPerspective.BotBoundsContainPoint(currentNode, end) || (exploring && startNode.HCost > 10 && (currentNode.HCost < 5)) || (attemptToLandSafely && WorldMapPerspective.BotIsOnStableFooting(currentNode) && !WorldMapPerspective.BotOnHazard(currentNode)))
                {
                    return ConstructPath(currentNode, end, pathType);
                }

                var neighbours = Neighbours(currentNode, exploring);

                foreach (var neighbour in neighbours)
                {
                    if (closedSet.Contains(neighbour))
                    {
                        continue;
                    }
                    neighbour.HCost = WorldMapPerspective.ManhattanDistance(neighbour, end);

                    if (neighbour.FCost > startNode.FCost + 19)
                    {
                        continue;
                    }
                    closedSet.Add(neighbour);
                    openSet.Enqueue(neighbour);
                }
            }

            //if(attemptToLandSafely) return

            return null;
        }
        protected static Path PerformEvadingBFS(Point currentPosition, Point opponentPosition, MovementState botMovementState, PathType pathType, int jumpHeight, Point deltaToStartPosition, int limitation)
        {
            var startNode = new Node(currentPosition.X, currentPosition.Y, null, botMovementState, 0, InputCommand.None, true, jumpHeight);
            startNode.DeltaToMe = deltaToStartPosition;

            var openSet = new Queue<Node>();
            var closedSet = new C5.HashSet<Point>();

            openSet.Enqueue(startNode);
            var farthestPoint = startNode;
            double maxDistanceAway = WorldMapPerspective.EuclideanDistance(currentPosition, opponentPosition);

            while (openSet.Count > 0)
            {
                var currentNode = openSet.Dequeue();
                if (currentNode.GCost >= limitation) continue;  // Consider adjusting/removing this limitation
                if (currentNode.Y <= WorldMapPerspective.PlatformsLeastY + 2) continue;

                var neighbours = Neighbours(currentNode, false);

                foreach (var neighbour in neighbours)
                {
                    if (closedSet.Contains(neighbour))
                    {
                        continue;
                    }
                    closedSet.Add(neighbour);
                    openSet.Enqueue(neighbour);

                    double currentDistance = WorldMapPerspective.EuclideanDistance(opponentPosition, neighbour);
                    if (currentDistance > maxDistanceAway)
                    {
                        maxDistanceAway = currentDistance;  // Update the max distance
                        farthestPoint = neighbour;
                    }
                }
            }

            if (farthestPoint.Equals(startNode))
            {
                return null;
            }
            else
            {
                return ConstructPath(farthestPoint, farthestPoint, pathType);
            }
        }
        private static C5.HashSet<Node> Neighbours(Node node, bool exploring)
        {
            var neighbours = new C5.HashSet<Node>();
            for (int x = node.X - 1; x <= node.X + 1; x++)
            {
                for (int y = node.Y - 1; y <= node.Y + 1; y++)
                {
                    Node neighbour = MovementToNextNode(new Point(x, y), node, exploring);

                    if ((y == node.Y && x == node.X) || !neighbour.CommandToMeEvaluable || !IsNodeReachable(neighbour, exploring))
                    {
                        continue;
                    }
                    neighbours.Add(neighbour);
                }
            }
            return neighbours;
        }

        private static Path ConstructPath(Node node, Point target, PathType pathType)
        {
            Path path = new();
            path.Target = target;
            path.PathType = pathType;
            path.Add(node);
            path.RemainingPath[node] = null;

            while (node.Parent != null)
            {
                path.RemainingPath[node.Parent] = new Path
                {
                    Target = path.Target,
                    PathType = path.PathType,
                    Nodes = new List<Node>(path.Nodes),
                };
                path.RemainingPath[node.Parent].Nodes.Reverse();

                node = node.Parent;
                path.Add(node);
            }
            path.Nodes.Reverse();
            return path;
        }

        private static bool IsNodeReachable(Node node, bool exploring)
        {
            if (WorldMapPerspective.BotOnHazard(node) || WorldMapPerspective.BotInUnachievablePosition(node, node.DugInDirection, exploring) || WorldMapPerspective.BotOutOfBounds(node))
            {
                return false;
            }

            return true;
        }

        private static Node MovementToNextNode(Point neighbourPosition, Node currentNode, bool exploring)
        {
            Point deltaToNeighbour = new Point(neighbourPosition.X - currentNode.X, neighbourPosition.Y - currentNode.Y);
            MovementState currentBotMovementState = currentNode.ExpectedEndBotMovementState;

            if (currentBotMovementState == MovementState.Falling && WorldMapPerspective.BotIsOnStableFooting(currentNode)) currentBotMovementState = MovementState.Idle;
            int currentJumpHeight = currentNode.JumpHeight;
            Point deltaToCurrentNode = currentNode.DeltaToMe;
            bool jumping = false;

            bool isAcceptableCommand;
            bool jumpDeltaYReverting = currentNode.JumpHeight == 3 || WorldMapPerspective.BotInUnachievablePosition(new Point(currentNode.X + currentNode.DeltaToMe.X, currentNode.Y + currentNode.DeltaToMe.Y), false, exploring);
            bool jumpDeltaXReverting = currentNode.JumpHeight < 3 && WorldMapPerspective.BotInUnachievablePosition(new Point(currentNode.X + currentNode.DeltaToMe.X, currentNode.Y + currentNode.DeltaToMe.Y), false, exploring);
            InputCommand inputCommand;

            switch (deltaToNeighbour)
            {
                case var delta when delta.X == 1 && delta.Y == 1:
                    jumping = JumpingPossible(neighbourPosition, currentNode, exploring);
                    isAcceptableCommand = !WorldMapPerspective.BotInUnachievablePosition(neighbourPosition, false, exploring) && currentBotMovementState != MovementState.Falling && currentJumpHeight < 3;
                    inputCommand = currentBotMovementState switch
                    {
                        MovementState.Jumping => deltaToCurrentNode.Equals(deltaToNeighbour) ? InputCommand.None : InputCommand.RIGHT,
                        MovementState.Idle => InputCommand.UPRIGHT,
                        _ => InputCommand.None
                    };
                    break;
                case var delta when delta.X == 1 && delta.Y == 0:
                    isAcceptableCommand = currentBotMovementState == MovementState.Idle;
                    inputCommand = InputCommand.DIGRIGHT;
                    break;

                case var delta when delta.X == 1 && delta.Y == -1:
                    isAcceptableCommand = !WorldMapPerspective.BotInUnachievablePosition(neighbourPosition, false, exploring) && (currentBotMovementState != MovementState.Jumping || (jumpDeltaYReverting && !jumpDeltaXReverting));
                    inputCommand = currentBotMovementState switch
                    {
                        MovementState.Falling => deltaToCurrentNode.Equals(deltaToNeighbour) ? InputCommand.None : InputCommand.RIGHT,
                        MovementState.Idle => InputCommand.DOWNRIGHT,
                        _ => InputCommand.None
                    };
                    break;
                case var delta when delta.X == 0 && delta.Y == 1:
                    jumping = JumpingPossible(neighbourPosition, currentNode, exploring);
                    isAcceptableCommand = !WorldMapPerspective.BotInUnachievablePosition(neighbourPosition, false, exploring) && (currentBotMovementState == MovementState.Idle || (currentBotMovementState == MovementState.Jumping && deltaToCurrentNode.Equals(deltaToNeighbour) && currentJumpHeight < 3));
                    inputCommand = currentBotMovementState switch
                    {
                        MovementState.Idle => InputCommand.UP,
                        _ => InputCommand.None
                    };
                    break;
                case var delta when delta.X == 0 && delta.Y == -1:
                    isAcceptableCommand = currentBotMovementState == MovementState.Idle || (currentBotMovementState == MovementState.Falling && deltaToCurrentNode.Equals(deltaToNeighbour)) || jumpDeltaYReverting && jumpDeltaXReverting;
                    inputCommand = currentBotMovementState switch
                    {
                        MovementState.Idle => InputCommand.DIGDOWN,
                        _ => InputCommand.None
                    };
                    break;
                case var delta when delta.X == -1 && delta.Y == 1:
                    jumping = JumpingPossible(neighbourPosition, currentNode, exploring);
                    isAcceptableCommand = !WorldMapPerspective.BotInUnachievablePosition(neighbourPosition, false, exploring) && (currentBotMovementState != MovementState.Falling && currentJumpHeight < 3);
                    inputCommand = currentBotMovementState switch
                    {
                        MovementState.Jumping => deltaToCurrentNode.Equals(deltaToNeighbour) ? InputCommand.None : InputCommand.LEFT,
                        MovementState.Idle => InputCommand.UPLEFT,
                        _ => InputCommand.None
                    };
                    break;
                case var delta when delta.X == -1 && delta.Y == 0:
                    isAcceptableCommand = currentBotMovementState == MovementState.Idle;
                    inputCommand = InputCommand.DIGLEFT;
                    break;
                case var delta when delta.X == -1 && delta.Y == -1:
                    isAcceptableCommand = !WorldMapPerspective.BotInUnachievablePosition(neighbourPosition, false, exploring) && (currentBotMovementState != MovementState.Jumping || (jumpDeltaYReverting && !jumpDeltaXReverting));

                    inputCommand = currentBotMovementState switch
                    {
                        MovementState.Falling => deltaToCurrentNode.Equals(deltaToNeighbour) ? InputCommand.None : InputCommand.LEFT,
                        MovementState.Idle => InputCommand.DOWNLEFT,
                        _ => InputCommand.None
                    };
                    break;
                default:
                    isAcceptableCommand = false;
                    inputCommand = InputCommand.None;
                    break;
            }

            MovementState expectedBotMovementState = jumping && CanContinueJumping(neighbourPosition, currentNode, exploring) ? MovementState.Jumping :
                (WorldMapPerspective.BotIsOnStableFooting(neighbourPosition) || (jumping && currentNode.JumpHeight < 2)) ? MovementState.Idle : MovementState.Falling;

            Node nextNode = new Node(neighbourPosition.X, neighbourPosition.Y, currentNode, expectedBotMovementState,
                currentNode.ExpectedGameTickOffset + 1, inputCommand, isAcceptableCommand, jumping ? currentNode.JumpHeight + 1 : 0);

            return nextNode;
        }

        private static bool JumpingPossible(Point neighbourPosition, Node currentNode, bool exploring)
        {
            return !WorldMapPerspective.BotInUnachievablePosition(neighbourPosition, false, exploring) && (currentNode.ExpectedEndBotMovementState == MovementState.Jumping || !WorldMapPerspective.BotContainsLadder(currentNode));
        }

        private static bool CanContinueJumping(Point neighbourPosition, Node currentNode, bool exploring)
        {
            if (currentNode.JumpHeight < 2)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!WorldMapPerspective.BotInUnachievablePosition(new Point(neighbourPosition.X + dx, neighbourPosition.Y + 1), false, exploring))
                    {
                        return true;
                    }
                }

            }

            return false;
        }

        public Path GetNextSafeNavigationPathAway(BotCommand command, Point currentPosition, Point firstPos, BotStateDTO botState, Dictionary<int, (Point Position, string MovementState, InputCommand CommandSent, Point DeltaToPosition, int Level)> gameStateDict)
        {
            MovementState botMovementState;
            try
            {
                botMovementState = (MovementState)Enum.Parse(typeof(MovementState), botState.CurrentState);
            }
            catch (ArgumentException)
            {
                botMovementState = MovementState.Idle; // Default value
            }

            var jumpHeight = 0;
            var gameTickToCheck = botState.GameTick;
            while (gameStateDict.ContainsKey(gameTickToCheck) && gameStateDict[gameTickToCheck].MovementState == "Jumping")
            {
                jumpHeight++;
                gameTickToCheck--;
            }
            var limitation = botState.Collected > WorldMapPerspective.LevelMinCollectablesForPursuit[botState.CurrentLevel] ? 8 : 5;
            var path = PerformEvadingBFS(currentPosition, firstPos, botMovementState, PathType.Evading, jumpHeight, gameStateDict[botState.GameTick].DeltaToPosition, limitation);

            if (path != null)
            {
                OnBestPathFound(path);
                WorldMapPerspective.PerformingAdversarialManouvreToTick = botState.GameTick + path.Nodes.Count  - 1;
            }

            return null;
        }


        public Path GetNextSafeNavigationPathTowards(BotCommand command, Point currentPosition, Point target, BotStateDTO botState, Dictionary<int, (Point Position, string MovementState, InputCommand CommandSent, Point DeltaToPosition, int Level)> gameStateDict)
        {
            MovementState botMovementState;
            try
            {
                botMovementState = (MovementState)Enum.Parse(typeof(MovementState), botState.CurrentState);
            }
            catch (ArgumentException)
            {
                botMovementState = MovementState.Idle; // Default value
            }

            var jumpHeight = 0;
            var gameTickToCheck = botState.GameTick;
            while (gameStateDict.ContainsKey(gameTickToCheck) && gameStateDict[gameTickToCheck].MovementState == "Jumping")
            {
                jumpHeight++;
                gameTickToCheck--;
            }
            var path = PerformBFS(currentPosition, target, botMovementState, PathType.Pursuing, jumpHeight, gameStateDict[botState.GameTick].DeltaToPosition);

            if (path != null)
            {
                OnBestPathFound(path);
                WorldMapPerspective.PerformingAdversarialManouvreToTick = botState.GameTick + path.Nodes.Count - 1;
            }

            return null;
        }
    }
    public class NodeComparer : IComparer<Node>
    {
        public int Compare(Node x, Node y)
        {
            return x.FCost.CompareTo(y.FCost);
        }
    }

    public class CollectibleSorter : IComparer<Point>
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

    public class CollectibleYSorter : IComparer<Point>
    {
        public int Compare(Point x, Point y)
        {
            return x.Y - y.Y;
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