using Domain.Enums;
using Domain.Models;
using System.Collections.Generic;
using System;
using System.Linq;
using ReferenceBot.AI.DataStructures.Pathfinding;
using C5;

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

        public abstract InputCommand Update(BotStateDTO BotState, Dictionary<int, (Point Position, string MovementState, InputCommand CommandSent, Point DeltaToPosition, int Level)> gameStateDict);

        // A* algorithm
        protected static Path? PerformAStarSearch(Point start, Point end, string botMovementStateString, bool exploring, Dictionary<int, (Point Position, string MovementState, InputCommand CommandSent, Point DeltaToPosition, int Level)> gameStateDict, int gameTick)
        {
            MovementState botMovementState;
            try
            {
                botMovementState = (MovementState)Enum.Parse(typeof(MovementState), botMovementStateString);
            }
            catch (ArgumentException)
            {
                botMovementState = MovementState.Idle; // Default value
            }

            var jumpHeight = 0;
            var gameTickToCheck = gameTick;
            while (gameStateDict.ContainsKey(gameTickToCheck) && gameStateDict[gameTickToCheck].MovementState == "Jumping")
            {
                jumpHeight++;
                gameTickToCheck--;
            }

            var startNode = new Node(start.X, start.Y, null, botMovementState, 0, InputCommand.None, true, jumpHeight);
            startNode.DeltaToMe = gameStateDict[gameTick].DeltaToPosition;

            startNode.HCost = WorldMapPerspective.ManhattanDistance(start, end);

            var openSet = new IntervalHeap<Node>(new NodeComparer());
            var closedSet = new Dictionary<Node, bool>();

            openSet.Add(startNode);

            while (openSet.Count > 0)
            {
                var currentNode = openSet.DeleteMin();//openSet.First();
                //Console.WriteLine($"Processing point: (X: {currentNode.X}, Y: {currentNode.Y}, FCost: {currentNode.FCost})");
                if (WorldMapPerspective.BotBoundsContainPoint(currentNode, end) || (exploring && (currentNode.HCost < 10)) || currentNode.StepsToMe > 50)
                {
                    //endNode.Parent = currentNode.Parent;
                    return ConstructPath(currentNode);
                }
                //openSet.Remove(currentNode);
                closedSet[currentNode] = true;

                var neighbours = Neighbours(currentNode, botMovementState, exploring);

                foreach (var neighbour in neighbours)
                {
                    if (closedSet.ContainsKey(neighbour))
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

        private static C5.HashSet<Node> Neighbours(Node node, MovementState currentBotMovementState, bool exploring)
        {
            var neighbours = new C5.HashSet<Node>();
            for (int x = node.X - 1; x <= node.X + 1; x++)
            {
                for (int y = node.Y - 1; y <= node.Y + 1; y++)
                {
                    //TODO infer jump height and jumped from previous movements (if reevaluating and already in jumping). Therefore need to store last 3/4 positions and input
                    //also infer delta to me
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
            //TODO consider falling trajectory
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
                    jumping = !WorldMapPerspective.BotInUnachievablePosition(neighbourPosition, false, exploring) && (currentNode.ExpectedEndBotMovementState == MovementState.Jumping || !WorldMapPerspective.BotContainsLadder(currentNode));
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
                    jumping = !WorldMapPerspective.BotInUnachievablePosition(neighbourPosition, false, exploring) && (currentNode.ExpectedEndBotMovementState == MovementState.Jumping || !WorldMapPerspective.BotContainsLadder(currentNode));
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
                    jumping = !WorldMapPerspective.BotInUnachievablePosition(neighbourPosition, false, exploring) && (currentNode.ExpectedEndBotMovementState == MovementState.Jumping || !WorldMapPerspective.BotContainsLadder(currentNode));
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

            MovementState expectedBotMovementState = jumping && currentNode.JumpHeight < 2 ? MovementState.Jumping :
                WorldMapPerspective.BotIsOnStableFooting(neighbourPosition) ? MovementState.Idle :
                MovementState.Falling;

            Node nextNode = new Node(neighbourPosition.X, neighbourPosition.Y, currentNode, expectedBotMovementState,
                currentNode.ExpectedGameTickOffset + 1, inputCommand, isAcceptableCommand, jumping ? currentNode.JumpHeight + 1 : 0);

            return nextNode;
        }
    }

    public class NodeComparer : IComparer<Node>
    {
        public int Compare(Node x, Node y)
        {
            return x.FCost.CompareTo(y.FCost);
        }
    }
}