using Domain.Enums;
using Domain.Models;
using System;
using System.Collections.Generic;

namespace ReferenceBot.AI.DataStructures.Pathfinding
{
    // A node for use in pathfinding.
    class Node : IEquatable<Node>
    {
        public int X;
        public int Y;
        public int ExpectedGameTickOffset;
        public int GCost;
        public int HCost;
        public bool Walkable;
        public InputCommand CommandToReachMe;
        public Node? Parent;
        private MovementState expectedEndBotMovementState;

        public bool DugInDirection => CommandToReachMe == InputCommand.DIGDOWN || CommandToReachMe == InputCommand.DIGLEFT || CommandToReachMe == InputCommand.DIGRIGHT;
        public MovementState ExpectedEndBotMovementState
        {
            get { return expectedEndBotMovementState; }
            set => expectedEndBotMovementState = value;
        }

        public int JumpHeight { get; }
        public Point DeltaToMe { get; set; } = new(0, 0);

        public int FCost => GCost + HCost;

        public bool CommandToMeEvaluable { get; }

        public Node(int _X, int _Y, Node? _parent, MovementState expectedEndBotMovementState, int expectedGameTickOffset, InputCommand commandToReachMe, bool commandToMeEvaluable, int jumpHeight)
        {
            X = _X;
            Y = _Y;
            GCost = Parent != null ? Parent.GCost + 1 : 0;
            Parent = _parent;
            ExpectedEndBotMovementState = expectedEndBotMovementState;
            ExpectedGameTickOffset = expectedGameTickOffset;
            CommandToReachMe = commandToReachMe;
            CommandToMeEvaluable = commandToMeEvaluable;
            JumpHeight = jumpHeight;
        }

        public static implicit operator Point(Node n) => new(n.X, n.Y);

        public bool Equals(Node other)
        {
            return X == other.X && Y == other.Y;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }
    }

    public class NodeCostComparer : IComparer<Node>
    {
        int IComparer<Node>.Compare(Node x, Node y)
        {
            return x.FCost - y.FCost;
        }
    }

    public class NodeEqualityComparer : IEqualityComparer<Node>
    {
        bool IEqualityComparer<Node>.Equals(Node x, Node y)
        {
            return x.Equals(y);
        }

        int IEqualityComparer<Node>.GetHashCode(Node obj)
        {
            return obj.GetHashCode();
        }
    }
}
