using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace PlanetGame.Util.Orometry
{
    public class HeightmapAnalyzer1
    {
        public List<Simplex> Simplices => [.. GradientData.Keys];

        public readonly Dictionary<Simplex, Gradient> GradientData = [];

        public Dictionary<Point, HashSet<Edge>> PointToEdge { get; private set; } = [];
        public Dictionary<Edge, HashSet<Triangle>> EdgeToTriangle { get; private set; } = [];

        private bool _debug;

        public struct Gradient(Simplex simplex, Direction direction)
        {
            public Simplex Simplex { get; private set; } = simplex;
            public Direction Direction { get; private set; } = direction;

            public readonly bool IsUnassigned()
            {
                return Direction == Direction.UNASSIGNED;
            }

            public readonly bool IsPointingNowhere()
            {
                return Direction == Direction.NONE;
            }

            public override readonly string ToString()
            {
                string gradient = Simplex == null ? "null" : Simplex.ToString();
                return $"({gradient} {Direction})";
            }

            public override readonly bool Equals(object obj)
            {
                if (obj is Gradient other)
                    return Direction == other.Direction && Simplex == other.Simplex;
                return false;
            }

            public override readonly int GetHashCode()
            {
                return HashCode.Combine(Simplex, Direction);
            }
            public static bool operator ==(Gradient left, Gradient right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(Gradient left, Gradient right)
            {
                return !(left == right);
            }
        }

        public enum Direction
        {
            UNASSIGNED,
            NONE,
            UP,
            DOWN,
            LEFT,
            RIGHT,
            TOP_LEFT,
            TOP_RIGHT,
            BOTTOM_LEFT,
            BOTTOM_RIGHT,
            CRITICAL,
        }

        public readonly static HashSet<Direction> ValidDirections =
        [
            Direction.UP,
            Direction.TOP_RIGHT,
            Direction.RIGHT,
            Direction.DOWN, Direction.BOTTOM_LEFT, Direction.LEFT,
        ];

        public static Direction GetReverseDirection(Direction direction)
        {
            return direction switch
            {
                Direction.UP => Direction.DOWN,
                Direction.DOWN => Direction.UP,
                Direction.LEFT => Direction.RIGHT,
                Direction.RIGHT => Direction.LEFT,
                Direction.TOP_LEFT => Direction.BOTTOM_RIGHT,
                Direction.BOTTOM_RIGHT => Direction.TOP_LEFT,
                Direction.TOP_RIGHT => Direction.BOTTOM_LEFT,
                Direction.BOTTOM_LEFT => Direction.TOP_RIGHT,
                _ => Direction.NONE
            };
        }

        public static Direction VectorToDirection(Vector2I vector)
        {
            return vector switch
            {
                { X: 0, Y: -1 } => Direction.UP,
                { X: 0, Y: 1 } => Direction.DOWN,
                { X: -1, Y: 0 } => Direction.LEFT,
                { X: 1, Y: 0 } => Direction.RIGHT,
                { X: -1, Y: -1 } => Direction.TOP_LEFT,
                { X: 1, Y: 1 } => Direction.BOTTOM_RIGHT,
                { X: 1, Y: -1 } => Direction.TOP_RIGHT,
                { X: -1, Y: 1 } => Direction.BOTTOM_LEFT,
                _ => Direction.NONE
            };
        }

        public static Vector2I DirectionToVector(Direction direction)
        {
            return direction switch
            {
                Direction.UP => Vector2I.Up,
                Direction.DOWN => Vector2I.Down,
                Direction.LEFT => Vector2I.Left,
                Direction.RIGHT => Vector2I.Right,
                Direction.TOP_LEFT => Vector2I.Up + Vector2I.Left,
                Direction.BOTTOM_RIGHT => Vector2I.Down + Vector2I.Right,
                Direction.TOP_RIGHT => Vector2I.Up + Vector2I.Right,
                Direction.BOTTOM_LEFT => Vector2I.Down + Vector2I.Left,
                _ => Vector2I.Zero
            };
        }

        public static Direction[] ValidTriangleDirections(Direction direction)
        {
            return direction switch
            {
                Direction.UP => [Direction.TOP_RIGHT, Direction.LEFT],
                Direction.DOWN => [Direction.RIGHT, Direction.BOTTOM_LEFT],
                Direction.LEFT => [Direction.UP, Direction.BOTTOM_LEFT],
                Direction.RIGHT => [Direction.DOWN, Direction.TOP_RIGHT],
                Direction.TOP_RIGHT => [Direction.UP, Direction.RIGHT],
                Direction.BOTTOM_LEFT => [Direction.DOWN, Direction.LEFT],
                _ => []
            };
        }

        public static float ToRotation(Direction direction)
        {
            return direction switch
            {
                Direction.UP => 0,
                Direction.DOWN => Mathf.Pi,
                Direction.LEFT => Mathf.Pi / 2,
                Direction.RIGHT => Mathf.Pi * 3 / 2,
                Direction.TOP_LEFT => Mathf.Pi / 4,
                Direction.TOP_RIGHT => Mathf.Pi * 7 / 4,
                Direction.BOTTOM_LEFT => Mathf.Pi * 3 / 4,
                Direction.BOTTOM_RIGHT => Mathf.Pi * 5 / 4,
                _ => 0
            };
        }

        public HeightmapAnalyzer1(Image image, bool debug)
        {
            _debug = debug;
            float[,] refactoredImageData = RefactorHeightMap(image.GetWidth(), image.GetHeight(), (x, y) => image.GetPixel(x, y).R);
            GetHeightMapData(refactoredImageData.GetLength(0), refactoredImageData.GetLength(1), refactoredImageData);
            ProcessData();
        }

        public HeightmapAnalyzer1(float[,] imageData, bool debug)
        {
            _debug = debug;
            float[,] refactoredImageData = RefactorHeightMap(imageData.GetLength(0), imageData.GetLength(1), (x, y) => imageData[x, y]);
            GetHeightMapData(refactoredImageData.GetLength(0), refactoredImageData.GetLength(1), refactoredImageData);
            ProcessData();
        }

        private void ProcessData()
        {
            CalculateGradient();
            if (GradientData.Values.Any(x => x.Direction == Direction.UNASSIGNED))
            {
                GD.PrintErr("Some entries are still unassigned.");
            }
            LogEvent($"Points: {GradientData.Keys.Count(x => x is Point)}");
            LogEvent($"Edges: {GradientData.Keys.Count(x => x is Edge)}");
            LogEvent($"Squares: {GradientData.Keys.Count(x => x is Square)}");
        }

        private void GetHeightMapData(int width, int height, float[,] data)
        {
            Point[,] pointGrid = new Point[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Point point = new(x, y, data[x, y]);
                    pointGrid[x, y] = point;

                    FindNeighborsFromGrid(point, pointGrid);
                }
            }
        }

        private float[,] RefactorHeightMap(int width, int height, Func<int, int, float> getHeight)
        {
            float[,] heightmap = new float[width + 1, height + 1];
            int[,] count = new int[width + 1, height + 1];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    foreach (Vector2I corner in new Vector2I[] { new(x, y), new(x + 1, y), new(x, y + 1), new(x + 1, y + 1) })
                    {
                        heightmap[corner.X, corner.Y] += getHeight(x, y);
                        count[corner.X, corner.Y]++;
                    }
                }
            }
            for (int x = 0; x < width + 1; x++)
            {
                for (int y = 0; y < height + 1; y++)
                {
                    heightmap[x, y] /= count[x, y];
                }
            }
            return heightmap;
        }

        public ArrayMesh GetHeightMapMesh(Image image, float scale)
        {

            float[,] heightmap = RefactorHeightMap(image.GetWidth(), image.GetHeight(), (x, y) => image.GetPixel(x, y).R);
            int width = heightmap.GetLength(0);
            int height = heightmap.GetLength(1);

            Vector3[] vertices = new Vector3[width * height];
            Vector3[] normals = new Vector3[width * height];
            Vector2[] uvs = new Vector2[width * height];
            int[] triangles = new int[(width - 1) * (height - 1) * 6];

            int triIndex = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int currentIndex = y * width + x;

                    vertices[currentIndex] = new(2 * x, heightmap[x, y] * scale, 2 * y);
                    vertices[currentIndex] -= new Vector3(width - 1, 0, height - 1);
                    uvs[currentIndex] = new Vector2(x, y) / (width - 1);

                    float hL = (x > 0) ? heightmap[x - 1, y] * scale : heightmap[x, y] * scale;
                    float hR = (x < width - 1) ? heightmap[x + 1, y] * scale : heightmap[x, y] * scale;
                    float hD = (y > 0) ? heightmap[x, y - 1] * scale : heightmap[x, y] * scale;
                    float hU = (y < height - 1) ? heightmap[x, y + 1] * scale : heightmap[x, y] * scale;

                    Vector3 normal = new Vector3(hL - hR, 2.0f, hD - hU).Normalized();
                    normals[currentIndex] = normal;

                    if (x < width - 1 && y < height - 1)
                    {
                        int topLeft = currentIndex + width;
                        int bottomLeft = currentIndex;
                        int topRight = currentIndex + width + 1;
                        int bottomRight = currentIndex + 1;

                        triangles[triIndex++] = topLeft;
                        triangles[triIndex++] = bottomLeft;
                        triangles[triIndex++] = bottomRight;

                        triangles[triIndex++] = topLeft;
                        triangles[triIndex++] = bottomRight;
                        triangles[triIndex++] = topRight;
                    }

                }
            }

            ArrayMesh mesh = new();
            Godot.Collections.Array arrays = [];
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = vertices;
            arrays[(int)Mesh.ArrayType.Index] = triangles;
            arrays[(int)Mesh.ArrayType.Normal] = normals;
            arrays[(int)Mesh.ArrayType.TexUV] = uvs;

            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            return mesh;
        }

        private static Point GetNeighborFromGrid(Point point, Direction direction, Point[,] pointGrid)
        {
            int width = pointGrid.GetLength(0);
            int height = pointGrid.GetLength(1);

            Vector2I vector = DirectionToVector(direction) + new Vector2I(point.X, point.Y);
            if (vector.X >= 0 && vector.X < width && vector.Y >= 0 && vector.Y < height)
                return pointGrid[vector.X, vector.Y];
            return null;
        }

        private void FindNeighborsFromGrid(Point point, Point[,] pointGrid)
        {
            AssignGradient(point, null, Direction.UNASSIGNED);
            foreach (Direction direction in ValidDirections)
            {
                Point neighbor = GetNeighborFromGrid(point, direction, pointGrid);
                if (neighbor == null)
                    continue;

                Edge edge = new(point, neighbor);
                edge.GetPoints().ToList().ForEach(p => AddEdgeToPoint(p, edge));
                AssignGradient(edge, null, Direction.UNASSIGNED);

                foreach (Direction triangleDirection in ValidTriangleDirections(direction))
                {
                    Point otherNeighbor = GetNeighborFromGrid(point, triangleDirection, pointGrid);

                    if (otherNeighbor == null)
                        continue;

                    Triangle triangle = new(point, neighbor, otherNeighbor);
                    triangle.GetEdges().ToList().ForEach(e => AddTriangleToEdge(e, triangle));
                    AssignGradient(triangle, null, Direction.UNASSIGNED);
                }
            }
        }

        private void AddEdgeToPoint(Point point, Edge edge)
        {
            if (!PointToEdge.TryGetValue(point, out HashSet<Edge> edges))
            {
                edges = [];
                PointToEdge[point] = edges;
            }
            edges.Add(edge);
        }

        private void AddTriangleToEdge(Edge edge, Triangle triangle)
        {
            if (!EdgeToTriangle.TryGetValue(edge, out var triangles))
            {
                triangles = [];
                EdgeToTriangle[edge] = triangles;
            }
            triangles.Add(triangle);
        }

        private void AssignGradient(Simplex from, Simplex to, Direction direction)
        {
            GradientData[from] = new(to, direction);
        }

        private bool IsGradientUnassigned(Simplex simplex)
        {
            bool isContained = GradientData.ContainsKey(simplex);
            return !isContained || (isContained && GradientData[simplex].IsUnassigned());
        }

        private void CalculateGradient()
        {
            PriorityQueue<Simplex, (float, float)> simplexQueue = new();
            foreach (Simplex simplex in Simplices)
            {
                simplexQueue.Enqueue(simplex, (GetSimplexTypePriority(simplex), simplex.GetUpperValue()));
            }

            while (simplexQueue.Count > 0)
            {
                LogEvent("[color=yellow]===========================================================================================");

                Simplex simplex = simplexQueue.Dequeue();

                if (simplex is Point point)
                {
                    LogEvent($"Currently Processing Point: {point}");
                    ProcessNeighbors(point);
                }
                else if (simplex is Edge edge)
                {
                    LogEvent($"Currently Processing Edge: {edge}");
                    ValidiateSimplex(edge, edge.GetPoints());

                }
                else if (simplex is Triangle triangle)
                {
                    LogEvent($"Currently Processing Square: {triangle}");
                    ValidiateSimplex(triangle, triangle.GetPoints());
                }
            }
        }

        private List<Edge> GetLowerStarNeighbors(Point point)
        {
            return [.. PointToEdge[point]
                .Where(edge => point > edge.PointA || point > edge.PointB)
                .OrderBy(edge => edge.GetLowerValue())];
        }

        private List<Edge> GetEqualStarNeighbors(Point point)
        {
            return [.. PointToEdge[point]
                .Where(edge => point.EqualElevation(edge.PointA) && point.EqualElevation(edge.PointB))
                .OrderBy(edge => edge.GetLowerValue())];
        }

        private List<Edge> EliminateUsedEdges(List<Edge> edges)
        {
            return [.. edges.Where(IsGradientUnassigned).OrderBy(e => e.GetLowerValue())];
        }

        private void ProcessNeighbors(Point point)
        {
            LogEvent($"[color=teal]Processing Neighbors of Point: {point}");
            if (IsGradientUnassigned(point))
            {
                List<Edge> equalStarNeighbors = EliminateUsedEdges(GetEqualStarNeighbors(point));
                List<Edge> lowerStarNeighbors = EliminateUsedEdges(GetLowerStarNeighbors(point));
                if (equalStarNeighbors.Count == PointToEdge[point].Count)
                {
                    AssignGradient(point, null, Direction.NONE);
                    LogEvent("Point's neighbors are the same as itself");
                }
                else if (lowerStarNeighbors.Count > 0)
                {
                    Edge steepestDescent = lowerStarNeighbors[0];
                    lowerStarNeighbors.RemoveAt(0);

                    LogEvent($"[color=green]Edge of Steepest Descent: {steepestDescent}");

                    AssignGradient(point, steepestDescent, GetDirectionToEdge(point, steepestDescent));
                    AssignGradient(steepestDescent, null, Direction.NONE);

                    if (lowerStarNeighbors.Count == 0)
                        LogEvent($"No Lower Star Neighbors");
                    else
                        LogEvent($"Processing Lower Star Neighbors");

                    lowerStarNeighbors.Reverse();
                    AssignNeighbors(lowerStarNeighbors);

                    if (equalStarNeighbors.Count == 0)
                        LogEvent($"No Equal Star Neighbors");
                    else
                        LogEvent($"Processing Equal Star Neighbors");
                    equalStarNeighbors.Reverse();
                    AssignNeighbors(equalStarNeighbors);
                }
                // else if (equalStarNeighbors.Count == 1)
                // {
                //     Edge soleNeighbor = equalStarNeighbors[0];
                //     LogEvent($"[color=orange]Edge of Sole Neighbor: {soleNeighbor}");

                //     AssignGradient(point, soleNeighbor, GetDirectionToEdge(point, soleNeighbor));
                //     AssignGradient(soleNeighbor, null, Direction.NONE);
                // }
                else
                {
                    AssignGradient(point, null, Direction.CRITICAL);
                    LogEvent("[color=orange]Point is Critical");
                }
            }
        }

        private void AssignNeighbors(List<Edge> neighbors)
        {
            foreach (Edge edge in neighbors)
            {
                List<Triangle> associatedTriangles = [.. EdgeToTriangle[edge]
                    .Where(IsGradientUnassigned).Where(s =>
                        s.GetLowerValue() < edge.GetLowerValue() || s.GetUpperValue() < edge.GetUpperValue() || s.GetAverageValue() < edge.GetAverageValue()
                    )
                    .OrderBy(s => (s.GetLowerValue(), s.GetUpperValue(), s.GetAverageValue()))
                ];

                LogEvent($"\t{edge} {edge.GetLowerValue():F3} {edge.GetUpperValue():F3} {edge.GetAverageValue():F3}");
                if (EdgeToTriangle[edge].Count > 0)
                    LogEvent($"\t\t{EdgeToTriangle[edge].ToList()[0]} {EdgeToTriangle[edge].ToList()[0].GetLowerValue():F3} {EdgeToTriangle[edge].ToList()[0].GetUpperValue():F3} {EdgeToTriangle[edge].ToList()[0].GetAverageValue():F3}");
                if (EdgeToTriangle[edge].Count > 1)
                    LogEvent($"\t\t{EdgeToTriangle[edge].ToList()[1]} {EdgeToTriangle[edge].ToList()[1].GetLowerValue():F3} {EdgeToTriangle[edge].ToList()[1].GetUpperValue():F3} {EdgeToTriangle[edge].ToList()[1].GetAverageValue():F3}");

                switch (associatedTriangles.Count)
                {
                    case 0:
                        LogEvent($"[color=pink]\t\t\tChoose: None");
                        break;
                    case 1:
                    case 2:
                        AssignGradient(edge, associatedTriangles[0], GetDirectionToTriangle(edge, associatedTriangles[0]));
                        AssignGradient(associatedTriangles[0], null, Direction.NONE);
                        LogEvent($"[color=pink]\t\t\tChoose: None");
                        break;
                    default:
                        throw new ArgumentException("Edge has more than 2 triangles attached.");
                }
            }
        }

        public void ValidiateSimplex(Simplex simplex, Point[] points)
        {
            if (points.Any(p => GradientData[p].IsPointingNowhere()))
            {
                AssignGradient(simplex, null, Direction.NONE);
                LogEvent($"[color=red]{simplex} is not Considered");
            }
            else if (IsGradientUnassigned(simplex))
            {
                AssignGradient(simplex, null, Direction.CRITICAL);
                LogEvent($"[color=red]{simplex} is Critical");
            }
            else
            {
                LogEvent($"[color=red]{simplex} remains the same: {GradientData[simplex].Direction}");
            }
        }

        public static Direction GetDirectionToTriangle(Edge edge, Triangle triangle)
        {
            Point otherPoint = triangle.GetPoints().First(p => p != edge.PointA && p != edge.PointB);
            Vector2I vectorA = edge.PointA.Position;
            Vector2I vectorB = edge.PointB.Position;
            Vector2I vectorC = otherPoint.Position;

            Vector2I directionVector;

            if (vectorA.X != vectorB.X && vectorA.Y != vectorB.Y)
            {
                directionVector = 2 * vectorC - vectorA - vectorB;
            }
            else if (vectorA.X == vectorB.X)
            {
                directionVector = new(vectorC.X - vectorA.X, 0);
            }
            else
            {
                directionVector = new(0, vectorC.Y - vectorA.Y);
            }

            Direction direction = VectorToDirection(directionVector);
            if (direction != Direction.NONE)
                return direction;
            else
                throw new ArgumentException($"Invalid direction calculated from edge to triangle: {directionVector}");
        }

        public static Direction GetDirectionToEdge(Point point, Edge edge)
        {
            Point otherPoint = edge.GetPoints().First(p => p != point);
            return VectorToDirection(otherPoint.Position - point.Position);
        }

        public static Direction GetDirectionToPoint(Point pointA, Point pointB)
        {
            return VectorToDirection(pointB.Position - pointA.Position);
        }

        private static int GetSimplexTypePriority(Simplex simplex) => simplex switch
        {
            Point => 0,
            Edge => 1,
            Triangle => 2,
            _ => 3
        };

        private void LogEvent(string message)
        {
            if (_debug)
                GD.PrintRich(message);
        }
    }
}