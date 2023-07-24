using C5;
using Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceBot.AI.DataStructures.Pathfinding
{
    public class Path
    {
        public List<Node> Nodes;

        public Path()
        {
            Nodes = new();
        }

        public Path(List<Node> nodes)
        {
            Nodes = nodes.ToList();
        }

        public void Add(Node node)
        {
            Nodes.Add(node);
        }

        public int Length => Nodes.Count;

    }

    public static class PathExtensions
    {
        public static HashDictionary<Point, Node> ToDictionary(this Path path)
        {
            var nodeDictionary = new HashDictionary<Point, Node>();

            foreach (var node in path.Nodes)
            {
                if (node.Parent != null && !nodeDictionary.Contains(node.Parent)) nodeDictionary[node.Parent] = node;
            }

            return nodeDictionary;
        }
    }


}
