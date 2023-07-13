using Domain.Enums;
using Domain.Models;
using System.Collections.Generic;
using System;
using System.Linq;
using ReferenceBot.AI.DataStructures.Pathfinding;
using System.Xml.Linq;

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
        protected static Path? PerformAStarSearch(Point start, Point end, string botMovementStateString)
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
            //TODO infer jump height too
            var startNode = new Node(start.X, start.Y, null, botMovementState, 0, InputCommand.None, true, 0);
            var endNode = new Node(end.X, end.Y, null, MovementState.Idle, 0, InputCommand.None, false, 0);

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

        private static HashSet<Node> Neighbours(Node node, MovementState currentBotMovementState)
        {
            var neighbours = new HashSet<Node>();
            for (int x = node.X - 1; x <= node.X + 1; x++)
            {
                for (int y = node.Y - 1; y <= node.Y + 1; y++)
                {
                    //TODO infer jump height and jumped from previous movements (if reevaluating and already in jumping). Therefore need to store last 3/4 positions and input
                    //also infer delta to me
                    Node neighbour = MovementToNextNode(new Point(x, y), node);

                    if ((y == node.Y && x == node.X) || !neighbour.CommandToMeEvaluable || !IsNodeReachable(node))
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

        private static bool IsNodeReachable(Node node)
        {
            if (WorldMapPerspective.BotOnHazard(node) || WorldMapPerspective.BotInUnachievablePosition(node, node.DugInDirection) || WorldMapPerspective.BotOutOfBounds(node))
            {
                return false;
            }

            return true;
        }

        // Calculate cost using manhattan distance as a heuristic.
        protected static int ManhattanDistance(Point currentPoint, Point goal)
        {
            return Math.Abs(currentPoint.X -  goal.X) + Math.Abs(currentPoint.Y - goal.Y);
        }


        private static Node MovementToNextNode(Point neighbourPosition, Node currentNode)
        {
            Point deltaToNeighbour = new Point(neighbourPosition.X - currentNode.X, neighbourPosition.Y - currentNode.Y);
            MovementState currentBotMovementState = currentNode.ExpectedEndBotMovementState;
            //TODO consider falling trajectory
            if (currentBotMovementState == MovementState.Falling && WorldMapPerspective.BotIsOnStableFooting(currentNode)) currentBotMovementState = MovementState.Idle;
            int currentJumpHeight = currentNode.JumpHeight;
            Point deltaToCurrentNode = currentNode.DeltaToMe;
            bool jumping = false;

            CommandToNeighbour commandToNeighbour;
            bool isAcceptableCommand;
            InputCommand inputCommand;

            switch (deltaToNeighbour)
            {
                case var delta when delta.X == 1 && delta.Y == 1:
                    jumping = !WorldMapPerspective.BotInUnachievablePosition(neighbourPosition, false) && (currentNode.ExpectedEndBotMovementState == MovementState.Jumping || !WorldMapPerspective.BotContainsLadder(currentNode));
                    isAcceptableCommand = !WorldMapPerspective.BotInUnachievablePosition(neighbourPosition, false) && currentBotMovementState != MovementState.Falling && currentJumpHeight < 3;
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
                    isAcceptableCommand = !WorldMapPerspective.BotInUnachievablePosition(neighbourPosition, false) && currentBotMovementState != MovementState.Jumping;
                    inputCommand = currentBotMovementState switch
                    {
                        MovementState.Falling => deltaToCurrentNode.Equals(deltaToNeighbour) ? InputCommand.None : InputCommand.RIGHT,
                        MovementState.Idle => InputCommand.DOWNRIGHT,
                        _ => InputCommand.None
                    };
                    break;
                case var delta when delta.X == 0 && delta.Y == 1:
                    jumping = !WorldMapPerspective.BotInUnachievablePosition(neighbourPosition, false) && (currentNode.ExpectedEndBotMovementState == MovementState.Jumping || !WorldMapPerspective.BotContainsLadder(currentNode));
                    isAcceptableCommand = !WorldMapPerspective.BotInUnachievablePosition(neighbourPosition, false) && (currentBotMovementState == MovementState.Idle || (currentBotMovementState == MovementState.Jumping && deltaToCurrentNode.Equals(deltaToNeighbour) && currentJumpHeight < 3));
                    inputCommand = currentBotMovementState switch
                    {
                        MovementState.Idle => InputCommand.UP,
                        _ => InputCommand.None
                    };
                    break;
                case var delta when delta.X == 0 && delta.Y == -1:
                    isAcceptableCommand = currentBotMovementState == MovementState.Idle || (currentBotMovementState == MovementState.Falling && deltaToCurrentNode.Equals(deltaToNeighbour));
                    inputCommand = currentBotMovementState switch
                    {
                        MovementState.Idle => InputCommand.DIGDOWN,
                        _ => InputCommand.None
                    };
                    break;
                case var delta when delta.X == -1 && delta.Y == 1:
                    jumping = !WorldMapPerspective.BotInUnachievablePosition(neighbourPosition, false) && (currentNode.ExpectedEndBotMovementState == MovementState.Jumping || !WorldMapPerspective.BotContainsLadder(currentNode));
                    isAcceptableCommand = !WorldMapPerspective.BotInUnachievablePosition(neighbourPosition, false) && (currentBotMovementState != MovementState.Falling && currentJumpHeight < 3);
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
                    isAcceptableCommand = !WorldMapPerspective.BotInUnachievablePosition(neighbourPosition, false) && currentBotMovementState != MovementState.Jumping;

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

            commandToNeighbour = (isAcceptableCommand, inputCommand);

            MovementState expectedBotMovementState = jumping && currentNode.JumpHeight < 3 ? MovementState.Jumping :
                WorldMapPerspective.BotIsOnStableFooting(neighbourPosition) ? MovementState.Idle :
                MovementState.Falling;

            Node nextNode = new Node(neighbourPosition.X, neighbourPosition.Y, currentNode, expectedBotMovementState,
                currentNode.ExpectedGameTickOffset + 1, commandToNeighbour.InputCommand, commandToNeighbour.AcceptableCommand, jumping ? currentNode.JumpHeight + 1 : 0);

            return nextNode;
        }
    }
  

    internal record struct CommandToNeighbour(bool AcceptableCommand, InputCommand InputCommand)
    {
        public static implicit operator (bool AcceptableCommand, InputCommand InputCommand)(CommandToNeighbour value)
        {
            return (value.AcceptableCommand, value.InputCommand);
        }

        public static implicit operator CommandToNeighbour((bool AcceptableCommand, InputCommand InputCommand) value)
        {
            return new CommandToNeighbour(value.AcceptableCommand, value.InputCommand);
        }
    }
}