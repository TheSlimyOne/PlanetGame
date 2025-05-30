using System.Collections.Generic;
using Godot;


public class DivideTree
{
    public class DivideTreeNode(Vector3 position)
    {
        public Vector3 Position = position;
        public DivideTreeNode Parent;
        public List<DivideTreeNode> Children = [];
        public float Prominence = 0;
        public float Elevation => Position.Y;
        public float SaddleHeight => Elevation - Prominence;
    }


    public List<DivideTreeNode> Nodes = [];
    public DivideTreeNode Root;

    public void AddNode(DivideTreeNode node)
    {
        Nodes.Add(node);
    }

    public DivideTreeNode FindNearestTaller(Vector3 position)
    {
        float minSquareDistance = float.MaxValue;
        DivideTreeNode closest = null;

        foreach (DivideTreeNode node in Nodes)
        {
            // GD.PrintS(node.Position, node.Elevation, position.Y);
            if (node.Elevation <= position.Y) continue;

            float squareDistance = (node.Position - position).LengthSquared();
            if (squareDistance < minSquareDistance)
            {
                closest = node;
                minSquareDistance = squareDistance;
            }
        }

        return closest;
    }

    public IEnumerable<(DivideTreeNode child, DivideTreeNode parent)> GetEdges()
    {
        foreach (DivideTreeNode node in Nodes)
        {
            if (node.Parent != null)
                yield return (node, node.Parent);
        }
    }
}
