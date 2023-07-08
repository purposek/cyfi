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
        public string PreviousBotMovementState;
        public int ExpectedGameTickOffset;
        public int GCost;
        public int HCost;
        public bool Walkable;
        public InputCommand CommandToReachMe;
        public Node? Parent;

        public bool DugInDirection => CommandToReachMe == InputCommand.DIGDOWN || CommandToReachMe == InputCommand.DIGLEFT || CommandToReachMe == InputCommand.DIGRIGHT;
        public string ExpectedBotMovementState { get; set; }
        public bool JumpedInDirection { get; set; }
        public int JumpHeight => JumpedInDirection ? (Parent == null ? 0 : Parent.JumpHeight + 1) : 0;

        public int FCost => GCost + HCost;

        public Node(int _X, int _Y, bool _Walkable,  Node? _parent, string previousBotMovementState, int expectedGameTickOffset, InputCommand commandToReachMe)
        {
            X = _X;
            Y = _Y;
            Walkable = _Walkable;
            GCost = Parent != null ? Parent.GCost + 1 : 0;
            Parent = _parent;
            PreviousBotMovementState = previousBotMovementState;
            ExpectedBotMovementState = previousBotMovementState;
            ExpectedGameTickOffset = expectedGameTickOffset;
            CommandToReachMe = commandToReachMe;
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
