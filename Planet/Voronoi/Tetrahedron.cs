using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class Tetrahedron
{
    public HashSet<Triangle> Triangles { get; private set; }
    private Random random = new Random(1207);

    public Tetrahedron(Vector3[] initialVertices)
    {
        Vector3 centroid = VectorUtils.GetCentroid(initialVertices);

        Triangle triangleA = new Triangle(new Vector3[] { initialVertices[0], initialVertices[1], initialVertices[2] }, centroid);
        Triangle triangleB = new Triangle(new Vector3[] { initialVertices[0], initialVertices[2], initialVertices[3] }, centroid);
        Triangle triangleC = new Triangle(new Vector3[] { initialVertices[0], initialVertices[3], initialVertices[1] }, centroid);
        Triangle triangleD = new Triangle(new Vector3[] { initialVertices[2], initialVertices[1], initialVertices[3] }, centroid);

        Triangles = new HashSet<Triangle> { triangleA, triangleB, triangleC, triangleD };

        Edge.GlueEdges(triangleA.GetEdge(0), triangleC.GetEdge(2)); Edge.GlueEdges(triangleB.GetEdge(1), triangleD.GetEdge(2));
        Edge.GlueEdges(triangleA.GetEdge(1), triangleD.GetEdge(0)); Edge.GlueEdges(triangleB.GetEdge(2), triangleC.GetEdge(0));
        Edge.GlueEdges(triangleA.GetEdge(2), triangleB.GetEdge(0)); Edge.GlueEdges(triangleC.GetEdge(1), triangleD.GetEdge(1));
    }

    public Tetrahedron()
    {
        Triangles = new HashSet<Triangle>();
    }

    public void AddTriangle(Triangle triangle)
    {
        Triangles.Add(triangle);
    }


    public void GetMesh(int showIndex, Node3D node, float radius, ShaderMaterial material, bool isVoronoi, bool isCentroid)
    {
        foreach (var child in node.GetChildren()) { child.QueueFree(); }
        int itter = 0;
        foreach (Triangle triangle in Triangles)
        {
            if (isVoronoi)
            {
                itter++;
                // if (itter == showIndex)
                GenerateVoronoi(node, radius, triangle, isCentroid);
            }
            else
                GenerateDelaunay(node, radius, triangle, material);
        }
    }

    public void GenerateDelaunay(Node3D node, float radius, Triangle triangle, Material material)
    {
        Vector3[] vertices;
        Vector3[] normals;
        int[] indices;
        Vector3[] triangleVertices = triangle.GetVertices();
        vertices = new Vector3[]
        {
            triangleVertices[2].Normalized() * radius,
            triangleVertices[1].Normalized() * radius,
            triangleVertices[0].Normalized() * radius,
        };
        indices = new int[] { 0, 1, 2 };
        normals = new Vector3[] { triangle.GetNormal(), triangle.GetNormal(), triangle.GetNormal() };

        Instance(node, vertices, indices, normals, material);
    }

    public void GenerateVoronoi(Node3D node, float radius, Triangle triangle, bool isCentroid)
    {
        Edge startingEdge = triangle.Edges[0];
        Vector3 flipVertex = startingEdge.VertexA;

        List<Vector3> vertices = new List<Vector3> { Vector3.Zero };
        List<Vector3> normal = new List<Vector3>() { vertices[0].Normalized() };
        List<int> indices = new List<int>();

        Edge otherEdge = startingEdge.ReverseEdge.ParentTriangle.GetEdgesFromVertex(flipVertex)[0];
        int amount = 0;
        do
        {
            indices.Add(0);
            indices.Add(amount + 2);
            indices.Add(amount + 1);

            otherEdge = otherEdge.ReverseEdge.ParentTriangle.GetEdgesFromVertex(flipVertex)[0];
            vertices.Add(otherEdge.ParentTriangle.GetCentroid() * radius);
            vertices[0] += vertices[amount];
            normal.Add(vertices[amount].Normalized());
            amount++;
        }
        while (!startingEdge.Equals(otherEdge));
        vertices[0] = (vertices[0] / amount).Normalized() * radius;
        indices[indices.Count - 2] = 1;
        Instance(node, vertices.ToArray(), indices.ToArray(), normal.ToArray(), new StandardMaterial3D() { AlbedoColor = new Color(random.NextSingle(), random.NextSingle(), random.NextSingle()) });
    }

    public void Instance(Node3D node, Vector3[] vertices, int[] indices, Vector3[] normals, Material material)
    {
        MeshInstance3D meshInstance3D = new MeshInstance3D();
        meshInstance3D.Mesh = new ArrayMesh();
        Godot.Collections.Array arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        ((ArrayMesh)meshInstance3D.Mesh).AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        meshInstance3D.Mesh.SurfaceSetMaterial(0, material);
        node.AddChild(meshInstance3D);
    }

    public void InstanceAllTriangles(Node3D spawnPoint)
    {
        foreach (Triangle triangle in Triangles)
        {
            Vector3[] vertices = triangle.GetVertices();
         
    
            Instance(spawnPoint, 
                vertices,
                triangle.GetIndices(),
                new Vector3[]{triangle.GetNormal(), triangle.GetNormal(), triangle.GetNormal()},
                new StandardMaterial3D() { AlbedoColor = new Color(random.NextSingle(), random.NextSingle(), random.NextSingle()) }
            );
        }
    }

    public static MeshInstance3D CreatePoint(Vector3 position, float radius, Color color)
    {
        MeshInstance3D sphere = new MeshInstance3D();
        sphere.Mesh = new SphereMesh() { Radius = radius, Height = radius * 2 };
        sphere.Position = position;
        sphere.Mesh.SurfaceSetMaterial(0, new StandardMaterial3D() { AlbedoColor = color });
        return sphere;
    }

    public Vector3 GenerateEdgeVertex(Vector3 pointA, Vector3 pointB, Edge targetEdge)
    {
        float angleA = (pointA - targetEdge.GetMidPoint()).AngleTo(pointA - pointB);
        float angleB = (pointB - targetEdge.GetMidPoint()).AngleTo(pointB - pointA);

        Vector3 dirA = pointA - targetEdge.GetMidPoint() + Mathf.Tan(angleA) * targetEdge.ParentTriangle.GetNormal();
        Vector3 dirB = pointB - targetEdge.GetMidPoint() + Mathf.Tan(angleB) * targetEdge.ReverseEdge.ParentTriangle.GetNormal();

        float ub;
        if ((dirA.X * dirB.Y) - (dirA.Y * dirB.X) != 0)
        {
            ub = (dirA.Y * (pointB.X - pointA.X) - dirA.X * (pointB.Y - pointA.Y)) / ((dirA.X * dirB.Y) - (dirA.Y * dirB.X));
        }
        else if ((dirA.X * dirB.Z) - (dirA.Z * dirB.X) != 0)
        {
            ub = (dirA.Z * (pointB.X - pointA.X) - dirA.X * (pointB.Z - pointA.Z)) / ((dirA.X * dirB.Z) - (dirA.Z * dirB.X));
        }
        else if ((dirA.Y * dirB.Z) - (dirA.Z * dirB.Y) != 0)
        {
            ub = (dirA.Z * (pointB.Y - pointA.Y) - dirA.Y * (pointB.Z - pointA.Z)) / ((dirA.Y * dirB.Z) - (dirA.Z * dirB.Y));
        }
        else
        {
            throw new DivideByZeroException("Idk what to put here lol!");
        }

        return pointB + ub * dirB;
    }

    public void RemoveTriangles(Triangle[] triangles)
    {
        foreach (Triangle triangle in triangles)
            Triangles.Remove(triangle);
    }

    public Vector3 GetCentroid()
    {
        Vector3 centroid = Vector3.Zero;

        foreach (Triangle triangle in Triangles)
            centroid += triangle.GetCentroid();

        return centroid / Triangles.Count;
    }

    public Vector3[] GetVoronoiSeeds(bool isCentroid)
    {
        Vector3[] seeds = new Vector3[Triangles.Count];
        int index = 0;
        foreach (Triangle triangle in Triangles)
        {
            if (isCentroid)
                seeds[index++] = triangle.GetCentroid();
            else
                seeds[index++] = triangle.GetCircumcenter();
        }
        return seeds;
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("state = {");
        sb.AppendLine("    \"version\": 11,");
        sb.AppendLine("    \"randomSeed\": \"f183f270b2e84a75667545cf9c6581e2\",");
        sb.AppendLine("    \"graph\": {");
        sb.AppendLine("        \"viewport\": {");
        sb.AppendLine("            \"xmin\": -5,");
        sb.AppendLine("            \"ymin\": -5,");
        sb.AppendLine("            \"zmin\": -5,");
        sb.AppendLine("            \"xmax\": 5,");
        sb.AppendLine("            \"ymax\": 5,");
        sb.AppendLine("            \"zmax\": 5");
        sb.AppendLine("        },");
        sb.AppendLine("        \"threeDMode\": true,");
        sb.AppendLine("        \"showAxis3D\": false,");
        sb.AppendLine("        \"product\": \"graphing-3d\"");
        sb.AppendLine("    },");
        sb.AppendLine("    \"expressions\": {");
        sb.AppendLine("        \"list\": [");


        int j = 0;
        foreach (Triangle triangle in Triangles)
        {
            // s.AppendLine(triangle.ToString());
            for (int i = 0; i < 3; i++)
            {
                sb.AppendLine("            {");
                sb.AppendLine("                \"type\": \"expression\",");
                sb.AppendLine($"                \"id\": \"{++j}\",");
                sb.AppendLine($"                \"color\": \"#fff000\",");
                sb.AppendLine($"                \"latex\": \"{triangle.GetVoronoiEdge(i, false).ToString()}\",");
                sb.AppendLine("                \"lines\": true");
                sb.AppendLine("            },");
            }
        }


        sb.AppendLine("        ]");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine("Calc.setState(state)");
        return sb.ToString();
    }
}