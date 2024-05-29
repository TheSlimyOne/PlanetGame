using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class Triangle
{
    public Edge[] Edges { get; private set; }
    readonly public bool HasReversed;

    public Triangle(Vector3[] vertices, Vector3 centroid)
    {
        Vector3 CentroidToVertex = (vertices[0] - centroid).Normalized();
        Vector3 normal = Vector3Utils.GetTriangularNormal(vertices);
        if (normal.Dot(CentroidToVertex) < 0)
        {
            vertices = vertices.Reverse().ToArray();
            HasReversed = true;
        }
        else
            HasReversed = false;

        Edges = new Edge[]
        {
            new Edge(vertices[0], vertices[1], this),
            new Edge(vertices[1], vertices[2], this),
            new Edge(vertices[2], vertices[0], this)
        };
    }
    
    public Edge GetVoronoiEdge(int index, bool isCentroid)
    {
        if (isCentroid)
            return new Edge(GetCentroid(), GetEdge(index).ReverseEdge.ParentTriangle.GetCentroid(), this);
        
        else
            return new Edge(GetCircumcenter(), GetEdge(index).ReverseEdge.ParentTriangle.GetCircumcenter(), this);
    }

    public Edge GetEdge(int index)
    {
        if (HasReversed)
            switch (index)
            {
                case 0: index = 1; break;
                case 1: index = 0; break;
                case 2: index = 2; break;
            }

        return Edges[index];
    }

    public Edge[] GetIllegalEdges()
    {
        List<Edge> edges = new List<Edge>();
        foreach (Edge edge in Edges)
            if (edge.ReverseEdge == null)
                edges.Add(edge);
        return edges.ToArray();
    }

    public Edge[] GetEdgesFromVertex(Vector3 vertex, bool normalized = false)
    {
        int[] edgeOrder;

        if (Edges[0].VertexA == vertex)
        {
            edgeOrder = new int[] { 2, 0 };
        }
        else if (Edges[1].VertexA == vertex)
        {
            edgeOrder = new int[] { 0, 1 };
        }
        else if (Edges[2].VertexA == vertex)
        {
            edgeOrder = new int[] { 1, 2 };
        }
        else
        {
            throw new ArgumentException("Triangle does not contain " + vertex);
        }
        
        Edge[] resultEdges = new Edge[] { Edges[edgeOrder[0]], Edges[edgeOrder[1]] };

        if (normalized)
        {
            resultEdges[0].Normalized();
            resultEdges[1].Normalized();
        }

        return resultEdges;

    }

    public Edge GetOtherEdge(Edge edge)
    {
        foreach (Edge thisTriangleEdge in Edges)
        {
            if (thisTriangleEdge.Equals(edge))
                return thisTriangleEdge;
        }

        throw new ArgumentException("Triangle does not contain " + edge);
    }

    public Vector3[] GetVertices()
    {
        return new Vector3[] { Edges[0].VertexA, Edges[1].VertexA, Edges[2].VertexA };
    }

    public bool IsPointVisible(Vector3 seed)
    {
        return GetNormal().Dot((seed - GetVertices()[0]).Normalized()) >= 0;
    }

    public Vector3 GetNormal()
    {
        Vector3[] vertices = GetVertices();
        return (vertices[1] - vertices[0]).Cross(vertices[2] - vertices[0]).Normalized();
    }

    public Vector3[] GetNormals()
    {
        Vector3 normal = GetNormal();
        return new Vector3[]{normal, normal, normal};
    }

    public int[] GetIndices()
    {
        
        return new int[]{0, 2, 1};
    }

    public Vector3 GetCentroid()
    {
        Vector3[] vertices = GetVertices();
        return (vertices[0] + vertices[1] + vertices[2]) / 3;
    }

    public Vector3 GetCircumcenter()
    {
        Vector3[] vertices = GetVertices();
        Vector3 midPoint1 = (vertices[0] + vertices[1]) / 2;
        Vector3 midPoint2 = (vertices[1] + vertices[2]) / 2;

        Vector3 normal1 = (vertices[1] - vertices[0]).Cross(GetNormal()).Normalized();
        Vector3 normal2 = (vertices[2] - vertices[1]).Cross(GetNormal()).Normalized();

        Vector3 circumcenter = Intersect(midPoint1, normal1, midPoint2, normal2);
        return circumcenter;

    }

    public static Vector3 Intersect(Vector3 point1, Vector3 direction1, Vector3 point2, Vector3 direction2)
    {
        // Calculate the parameter t at which the lines intersect
        float t = ((point2.X - point1.X) * direction2.Z - (point2.Z - point1.Z) * direction2.X) /
                  (direction1.X * direction2.Z - direction1.Z * direction2.X);

        // Calculate the intersection point
        Vector3 intersection = new Vector3(point1.X + t * direction1.X, point1.Y + t * direction1.Y, point1.Z + t * direction1.Z);

        return intersection;
    }

    public static Edge[] GetAllUniqueEdges(Triangle[] triangles)
    {
        List<Edge> edges = new List<Edge>();

        if (triangles.Distinct().Count() != triangles.Count())
        {
            GD.Print("FALSE!");
        }

        foreach (Triangle triangle in triangles.Distinct().ToList())
            edges.AddRange(triangle.Edges);

        return edges.GroupBy(x => x).Where(x => !x.Skip(1).Any()).Select(x => x.Key).ToArray();
    }

    public static bool IsCoplanar(Triangle triangleA, Triangle triangleB)
    {
        Vector3 normalA = triangleA.GetNormal();
        Vector3 normalB = triangleB.GetNormal();

        if (normalA.Cross(normalB).IsZeroApprox())
            return true;

        return false;
    }

    public void Instance(Node3D node, Material material = null)
    {
        MeshInstance3D meshInstance3D = new MeshInstance3D { Mesh = new ArrayMesh() };
        Godot.Collections.Array arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);

        arrays[(int)Mesh.ArrayType.Vertex] = GetVertices();
        arrays[(int)Mesh.ArrayType.Index] = GetIndices();
        arrays[(int)Mesh.ArrayType.Normal] = GetNormals();
        ((ArrayMesh)meshInstance3D.Mesh).AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        if (material != null)
        {
            meshInstance3D.Mesh.SurfaceSetMaterial(0, material);
        }
        node.AddChild(meshInstance3D);
    }

    public override bool Equals(object obj)
    {
        if (!(obj is Triangle otherTriangle))
            return false;

        return Array.TrueForAll(Edges, edge => otherTriangle.Edges.Contains(edge));
    }

    public override int GetHashCode()
    {
        Vector3[] vertices = GetVertices();
        return vertices[0].GetHashCode() & vertices[1].GetHashCode() & vertices[2].GetHashCode();
    }

    public override string ToString()
    {
        return $"triangle({Edges[0].VertexA}, {Edges[1].VertexA}, {Edges[2].VertexA})";
    }

}
