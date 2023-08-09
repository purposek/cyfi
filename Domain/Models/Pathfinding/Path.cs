using C5;
using Domain.Enums;
using System.Reflection;

namespace Domain.Models.Pathfinding
{
    public class Path
    {
        public List<Node> Nodes;

        public Point Target;

        public PathType PathType;

        public Path()
        {
            Nodes = new();
        }

        public Path(Path other)
        {
            Nodes = other.Nodes;
            Target = other.Target; 
            PathType = other.PathType; 
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

        public Dictionary<Point, Path> RemainingPath { get; set; } = new();
    }

    public static class ModelExtensions
    {
        public static void CopyPropertiesTo<T, TU>(this T source, TU dest)
        {
            List<PropertyInfo> sourceProps = typeof(T).GetProperties().Where(x => x.CanRead).ToList();
            List<PropertyInfo> destProps = typeof(TU).GetProperties().Where(x => x.CanWrite).ToList();

            foreach (var sourceProp in sourceProps)
            {
                if (destProps.Any(x => x.Name == sourceProp.Name))
                {
                    var p = destProps.First(x => x.Name == sourceProp.Name);
                    if (p.CanWrite)
                    {
                        // check if the property can be set or no.
                        p.SetValue(dest, sourceProp.GetValue(source, null), null);
                    }
                }
            }
        }
    }


}
