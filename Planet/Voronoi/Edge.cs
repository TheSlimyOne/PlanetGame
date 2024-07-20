using Godot;
using System;


public class Edge : IComparable<Edge>
{
    public readonly Triangle ParentTriangle;
    public readonly Vector3 VertexA;
    public readonly Vector3 VertexB;
    public Edge ReverseEdge { get; private set; }


    public Edge(Vector3 vertexA, Vector3 vertexB, Triangle parentTriangle)
    {
        VertexA = vertexA;
        VertexB = vertexB;
        ParentTriangle = parentTriangle;
    }

    public Vector3 GetEdgeByIndex(int index)
    {
        switch (index)
        {
            case 0:
            return VertexA;

            case 1:
            return VertexB;

            default:
            throw new ArgumentOutOfRangeException($"Index: {index} is not vaild for edges");
        }

    }

    public static void GlueEdges(Edge edgeA, Edge edgeB)
    {
        if (!edgeA.Equals(edgeB))
            throw new ArgumentException($"{edgeA} is not the same as {edgeB}");

        edgeA.ReverseEdge = edgeB;
        edgeB.ReverseEdge = edgeA;
    }

    public Edge Normalized()
    {
        return new Edge(VertexA.Normalized(), VertexB.Normalized(), ParentTriangle);
    }

    public Vector3 GetMidPoint()
    {
        return (VertexA + VertexB) / 2;
    }

    public override int GetHashCode()
    {
        int hashA = VertexA.GetHashCode();
        int hashB = VertexB.GetHashCode();

        return hashA ^ hashB;
    }

    public override string ToString()
    {
        return $"{VertexA}, {VertexB}";
    }

    public bool ExactlyEquals(object obj)
    {
        if (!(obj is Edge otherEdge))
            return false;

        return
            VectorUtils.IsEqualVector3(VertexA, otherEdge.VertexA) &&
            VectorUtils.IsEqualVector3(VertexB, otherEdge.VertexB);
    }

    public override bool Equals(object obj)
    {
        if (!(obj is Edge otherEdge))
            return false;

        return
            (VectorUtils.IsEqualVector3(VertexA, otherEdge.VertexA) &&
            VectorUtils.IsEqualVector3(VertexB, otherEdge.VertexB)) ||
            (VectorUtils.IsEqualVector3(VertexA, otherEdge.VertexB) &&
            VectorUtils.IsEqualVector3(VertexB, otherEdge.VertexA));

    }
    
    public int CompareTo(Edge other)
    {
        if (Equals(other))
            return 0;
        else if (other.GetHashCode() > GetHashCode())
            return -1;
        return 1;
    }
}