using Domain.Enums;
using Domain.Models;
using System.Collections.Generic;
using System;
using System.Linq;
using ReferenceBot.AI.DataStructures.Pathfinding;

namespace ReferenceBot.AI
{
    abstract class State
    {

        protected BotStateMachine StateMachine;

        protected const int HeroWidth = 2;
        protected const int HeroHeight = 2;

        protected State(BotStateMachine _stateMachine)
        {
            StateMachine = _stateMachine;
        }

        public abstract void EnterState(State PreviousState);
        public abstract void ExitState(State NextState);

        protected void ChangeState(State NewState)
        {
            StateMachine.ChangeState(NewState);
        }

        public abstract InputCommand Update(BotStateDTO BotState);

       // A* algorithm
        protected static Path? PerformAStarSearch(Point start, Point end, string botMovementState)
        {
            //Initialise jump height too
            var startNode = new Node(start.X, start.Y, true, null, botMovementState, 0, InputCommand.None);
            var endNode = new Node(end.X, end.Y, true, null, "", 0, InputCommand.None);

            startNode.HCost = ManhattanDistance(start, end);

            HashSet<Node> openSet = new();
            HashSet<Node> closedSet = new();

            openSet.Add(startNode);

            while (openSet.Count > 0)
            {
                var currentNode = openSet.First();
                //Console.WriteLine($"Processing point: (X: {currentNode.X}, Y: {currentNode.Y}, FCost: {currentNode.FCost})");
                if (WorldMapPerspective.BotBoundsContainPoint(currentNode, endNode))
                {
                    //endNode.Parent = currentNode.Parent;
                    return ConstructPath(currentNode);
                }
                openSet.Remove(currentNode);
                closedSet.Add(currentNode);

                var neighbours = Neighbours(currentNode, botMovementState);

                foreach (var neighbour in neighbours)
                {
                    if (closedSet.Contains(neighbour))
                    {
                        continue;
                    }
                    neighbour.HCost = ManhattanDistance(neighbour, end);
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
                openSet = openSet.OrderBy((node) => node.FCost).ToHashSet();
            }

            return null;
        }

        private static HashSet<Node> Neighbours(Node node, string botMovementState)
        {
            var neighbours = new HashSet<Node>();
            for (int x = node.X - 1; x <= node.X + 1; x++)
            {
                for (int y = node.Y - 1; y <= node.Y + 1; y++)
                {
                    var inputCommand = InputCommandToNextNode(new Point(x - node.X, y - node.Y),botMovementState, node.JumpHeight);

                    Node neighbour = new(x, y, true, node, node.ExpectedBotMovementState, node.ExpectedGameTickOffset + 1, inputCommand);
                    var jumpedInDirection = JumpedInDirection(neighbour, node);
                    neighbour.JumpedInDirection = jumpedInDirection;
                    if ((y == node.Y && x == node.X) || !IsNodeReachable(neighbour))
                    {
                        continue;
                    }
                    neighbours.Add(neighbour);
                }
            }
            return neighbours;
        }

        private static bool JumpedInDirection(Node neighbour, Node node)
        {
            return neighbour.Y > node.Y && !WorldMapPerspective.BotContainsLadder(node);
        }

        private static Path ConstructPath(Node node)
        {
            Path path = new();
            path.Add(node);

            while (node.Parent != null)
            {
                node = node.Parent;
                path.Add(node);
            }
            path.Nodes.Reverse();
            return path;
        }

        private static bool IsNodeReachable(Node node)
        {
            if (WorldMapPerspective.BotOnHazard(node) || WorldMapPerspective.BotInUnachievablePosition(node, node.DugInDirection) || WorldMapPerspective.BotOutOfBounds(node) || node.JumpHeight > 3)
            {
                return false;
            }

            if (WorldMapPerspective.BotIsOnStableFooting(node))
            {
                return true;
            }


            return false;
        }

        // Calculate cost using manhattan distance as a heuristic.
        protected static int ManhattanDistance(Point currentPoint, Point goal)
        {
            return Math.Abs(currentPoint.X -  goal.X) + Math.Abs(currentPoint.Y - goal.Y);
        }


        private static InputCommand InputCommandToNextNode(Point deltaToNextNode, string previousBotMovementState, int previousJumpHeight)
        {
            switch ((deltaToNextNode.X, deltaToNextNode.Y))
            {
                case (1, 1):
                    return InputCommand.UPRIGHT;
                case (1, 0):
                    return InputCommand.DIGRIGHT;
                case (1, -1):
                    return InputCommand.DOWNRIGHT;
                case (0, 1):
                    return InputCommand.UP;
                case (0, -1):
                    return InputCommand.DIGDOWN;
                case (-1, 1):
                    return InputCommand.UPLEFT;
                case (-1, 0):
                    return InputCommand.DIGLEFT;
                case (-1, -1):
                    return InputCommand.DOWNLEFT;
                default:
                    return InputCommand.None;
            }

        }
    }
}