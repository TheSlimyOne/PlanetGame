using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[Tool]
public partial class ConvexHull
{
    public Tetrahedron Hull { get; }

    public ConvexHull(Vector3[] seeds)
    {
        Hull = DivideAndConquer(seeds);
    }
 
    public Tetrahedron DivideAndConquer(Vector3[] seeds)
    {
        if (seeds.Length <= float.MaxValue)
        {
            return Incremental(seeds);
        }

        (Vector3[] firstHalf, Vector3[] secondHalf) = Split(seeds);
        Tetrahedron firstHull = DivideAndConquer(firstHalf);
        Tetrahedron secondHull = DivideAndConquer(secondHalf);
        return Merge(firstHull, secondHull);
    }

    internal static Tetrahedron Merge(Tetrahedron firstHull, Tetrahedron secondHull)
    {
        throw new NotImplementedException();
    }

    internal static Tetrahedron Incremental(Vector3[] seeds)
    {
        Tetrahedron tetrahedron = new Tetrahedron(seeds.Take(4).ToArray());
        seeds = seeds.Skip(4).ToArray();
        BipartiteGraph<Vector3, Triangle> conflictGraph = InitializeConflictGraph(seeds.ToList(), tetrahedron);

        foreach (Vector3 seed in seeds)
        {
            List<Triangle> _conflicts;
            if (conflictGraph.TryGetByFirst(seed, out _conflicts))
            {
                Triangle[] conflicts = _conflicts.ToArray();

                Edge[] conflictEdges = Triangle.GetAllUniqueEdges(conflicts);
                tetrahedron.RemoveTriangles(conflicts);

                List<Edge> illegalEdges = new List<Edge>();
                List<Triangle> newTriangles = new List<Triangle>();

                foreach (Edge edge in conflictEdges)
                {

                    Triangle newTriangle = new Triangle(new Vector3[] { edge.VertexA, seed, edge.VertexB }, tetrahedron.GetCentroid());
                    Triangle oldTriangle = edge.ParentTriangle;
                    Triangle otherTriangle = edge.ReverseEdge.ParentTriangle;


                    newTriangles.Add(newTriangle);

                    Edge.GlueEdges(edge.ReverseEdge, newTriangle.GetOtherEdge(edge.ReverseEdge));


                    illegalEdges.AddRange(newTriangle.GetIllegalEdges());


                    tetrahedron.Triangles.Add(newTriangle);

                    if (Triangle.IsCoplanar(newTriangle, otherTriangle))
                    {
                        GD.PrintRich("[color=red] merging");
                        conflictGraph.MergeBySecond(newTriangle, otherTriangle);
                    }
                    else
                    {
                        List<Vector3> possibleConflicts = new List<Vector3>();

                        List<Vector3> holder;
                        if (conflictGraph.TryGetBySecond(oldTriangle, out holder))
                            possibleConflicts.AddRange(holder);

                        if (conflictGraph.TryGetBySecond(otherTriangle, out holder))
                            possibleConflicts.AddRange(holder);


                        possibleConflicts = possibleConflicts.Distinct().ToList();

                        foreach (Vector3 possibleSeed in possibleConflicts)
                            if (newTriangle.IsPointVisible(possibleSeed))
                                conflictGraph.Add(possibleSeed, newTriangle);


                    }
                }


                illegalEdges.Sort((a, b) =>
                {
                    int result = a.CompareTo(b);
                    if (result == 0)
                        if (a.VertexA == seed)
                            return -1;
                        else
                            return 1;

                    else
                        return result;
                });

                for (int i = 0; i < illegalEdges.Count; i += 2)
                    Edge.GlueEdges(illegalEdges[i], illegalEdges[i + 1]);



                foreach (Triangle triangle in conflicts)
                    conflictGraph.RemoveKeyFromSecond(triangle);
                conflictGraph.RemoveKeyFromFirst(seed);


            }
        }
        // GD.Print(tetrahedron);
        return tetrahedron;
    }

    internal static BipartiteGraph<Vector3, Triangle> InitializeConflictGraph(List<Vector3> seeds, Tetrahedron tetrahedron)
    {
        BipartiteGraph<Vector3, Triangle> conflictGraph = new BipartiteGraph<Vector3, Triangle>();
        foreach (Triangle triangle in tetrahedron.Triangles)
        {
            conflictGraph.InitializeSecond(triangle);
            foreach (Vector3 seed in seeds.Where(x => triangle.IsPointVisible(x)))
                conflictGraph.Add(seed, triangle);

        }
        return conflictGraph;
    }

    internal static (Vector3[], Vector3[]) Split(Vector3[] seeds)
    {
        int halfIndex = seeds.Length / 2;
        Vector3[] firstHalf = seeds.Take(halfIndex).ToArray();
        Vector3[] secondHalf = seeds.Skip(halfIndex).ToArray();
        return (firstHalf, secondHalf);
    }
}
