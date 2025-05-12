using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GradientDirection = PlanetGame.Util.Orometry.Gradient.GradientDirection;

namespace PlanetGame.Util.Orometry
{
    public class HeightmapAnalyzer
    {
        public List<Simplex> Simplices => [.. Gradients.Keys];

        public Dictionary<Simplex, Gradient> Gradients { get; private set; } = [];
        public Dictionary<Simplex, HashSet<Simplex>> ConnectivityTable { get; private set; } = [];
        public List<Simplex> ProcessOrder = [];

        public Manifold Manifold { get; private set; } = new();

        private readonly bool _debug;

        public readonly static HashSet<GradientDirection> ValidDirections =
        [
            GradientDirection.UP,
            GradientDirection.RIGHT,
            GradientDirection.DOWN,
            GradientDirection.LEFT,
        ];

        public static GradientDirection[] ValidComplementDirections(GradientDirection direction)
        {
            return direction switch
            {
                GradientDirection.UP => [GradientDirection.LEFT, GradientDirection.RIGHT],
                GradientDirection.DOWN => [GradientDirection.LEFT, GradientDirection.RIGHT],
                GradientDirection.LEFT => [GradientDirection.UP, GradientDirection.DOWN],
                GradientDirection.RIGHT => [GradientDirection.UP, GradientDirection.DOWN],
                _ => []
            };
        }
        public static (GradientDirection, GradientDirection)[] ValidSquareDirections(GradientDirection direction)
        {
            return direction switch
            {
                GradientDirection.UP => [(GradientDirection.TOP_RIGHT, GradientDirection.RIGHT), (GradientDirection.TOP_LEFT, GradientDirection.LEFT)],
                GradientDirection.DOWN => [(GradientDirection.BOTTOM_RIGHT, GradientDirection.RIGHT), (GradientDirection.BOTTOM_LEFT, GradientDirection.LEFT)],
                GradientDirection.LEFT => [(GradientDirection.UP, GradientDirection.TOP_LEFT), (GradientDirection.DOWN, GradientDirection.BOTTOM_LEFT)],
                GradientDirection.RIGHT => [(GradientDirection.UP, GradientDirection.TOP_RIGHT), (GradientDirection.DOWN, GradientDirection.BOTTOM_RIGHT)],
                _ => []
            };
        }

        public HeightmapAnalyzer(Image image, float persistenceThreshold, bool debug, int timestep, MeshInstance3D tracker) : this(RefactorHeightMap(image.GetWidth(), image.GetHeight(), (x, y) => image.GetPixel(x, y).R), persistenceThreshold, debug, timestep, tracker) { }

        public HeightmapAnalyzer(float[,] imageData, float persistenceThreshold, bool debug, int timestep, MeshInstance3D tracker)
        {
            _debug = debug;
            GetHeightMapData(imageData.GetLength(0), imageData.GetLength(1), imageData);
            CalculateGradient(timestep, new Vector2(imageData.GetLength(0), imageData.GetLength(1)), tracker);
            CalculateManifolds();
            Manifold.Simplify(persistenceThreshold);
            if (timestep < -1 && Gradients.Values.Any(x => x.Direction == GradientDirection.UNASSIGNED))
            {
                GD.PrintErr("Some entries are still unassigned.");

                Gradients.Where(x => x.Value.Direction == GradientDirection.UNASSIGNED).ToList().ForEach(x => GD.PrintErr(x));
            }
            LogEvent($"Points: {Gradients.Keys.Count(x => x is Point)}");
            LogEvent($"Edges: {Gradients.Keys.Count(x => x is Edge)}");
            LogEvent($"Squares: {Gradients.Keys.Count(x => x is Square)}");
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

        public static float[,] ImageTo2dArray(Image image)
        {
            int width = image.GetWidth();
            int height = image.GetHeight();

            float[,] heightmap = new float[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    heightmap[x, y] = image.GetPixel(x, y).R;
                }
            }

            return heightmap;
        }

        public static float[,] RefactorHeightMap(Image image) => RefactorHeightMap(image.GetWidth(), image.GetHeight(), (x, y) => image.GetPixel(x, y).R);

        private static float[,] RefactorHeightMap(int width, int height, Func<int, int, float> getHeight)
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

        public ArrayMesh GetHeightMapMesh(float[,] heightmap, Material material, float scale)
        {
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

                    float hL = (x > 0) ? heightmap[x - 1, y] : heightmap[x, y];
                    float hR = (x < width - 1) ? heightmap[x + 1, y] : heightmap[x, y];
                    float hD = (y > 0) ? heightmap[x, y - 1] : heightmap[x, y];
                    float hU = (y < height - 1) ? heightmap[x, y + 1] : heightmap[x, y];

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
            mesh.SurfaceSetMaterial(0, material);

            return mesh;
        }

        private static Point GetNeighborFromGrid(Point point, GradientDirection direction, Point[,] pointGrid)
        {
            int width = pointGrid.GetLength(0);
            int height = pointGrid.GetLength(1);

            Vector2I vector = Gradient.DirectionToVector(direction) + new Vector2I(point.X, point.Y);
            if (vector.X >= 0 && vector.X < width && vector.Y >= 0 && vector.Y < height)
                return pointGrid[vector.X, vector.Y];
            return null;
        }

        private void FindNeighborsFromGrid(Point point, Point[,] pointGrid)
        {
            AssignGradient(point, null, GradientDirection.UNASSIGNED);
            foreach (GradientDirection direction in ValidDirections)
            {
                Point neighbor = GetNeighborFromGrid(point, direction, pointGrid);
                if (neighbor == null)
                    continue;

                Edge edge = new(point, neighbor);
                edge.GetPoints().ToList().ForEach(p => AddConnectivity(p, edge));
                AssignGradient(edge, null, GradientDirection.UNASSIGNED);

                foreach ((GradientDirection, GradientDirection) squareDirections in ValidSquareDirections(direction))
                {
                    Point otherNeighborA = GetNeighborFromGrid(point, squareDirections.Item1, pointGrid);
                    Point otherNeighborB = GetNeighborFromGrid(point, squareDirections.Item2, pointGrid);
                    if (otherNeighborA == null || otherNeighborB == null)
                        continue;

                    Square square = new(point, neighbor, otherNeighborA, otherNeighborB);
                    square.GetEdges().ToList().ForEach(e => AddConnectivity(e, square));
                    AssignGradient(square, null, GradientDirection.UNASSIGNED);
                }
            }
        }

        private void AddConnectivity(Simplex source, Simplex destination)
        {
            if (!ConnectivityTable.TryGetValue(source, out HashSet<Simplex> destinations))
            {
                destinations = [];
                ConnectivityTable[source] = destinations;
            }
            destinations.Add(destination);
        }

        private void AssignGradient(Simplex assignee, Simplex from, Simplex to, GradientDirection direction)
        {
            Gradients[assignee] = new Gradient(from, to, direction);
        }

        private void AssignGradient(Simplex from, Simplex to, GradientDirection direction)
        {
            Gradients[from] = new Gradient(from, to, direction);
        }

        private bool IsGradientUnassigned(Simplex simplex)
        {
            bool isContained = Gradients.ContainsKey(simplex);
            return !isContained || (isContained && Gradients[simplex].IsUnassigned());
        }
        private Vector3 TransformCentroid(Vector3 position, Vector2 size)
        {
            position.X *= 2;
            position.Y *= 1;
            position.Z *= 2;
            return position - new Vector3(size.X - 1, 0, size.Y - 1);
        }

        private void CalculateGradient(int timestep, Vector2 size, MeshInstance3D tracker)
        {
            PriorityQueue<Simplex, (int, float)> simplexQueue = new();
            foreach (Simplex simplex in Simplices)
            {
                simplexQueue.Enqueue(simplex, (GetSimplexTypePriority(simplex), simplex.GetLowerValue()));
            }

            int iteration = 0;

            while (simplexQueue.Count > 0)
            {

                LogEvent("[color=yellow]===========================================================================================");

                Simplex simplex = simplexQueue.Dequeue();
                ProcessOrder.Add(simplex);

                if (iteration == timestep)
                {
                    // List<Edge> lowerStarNeighbors = GetLowerStarNeighbors(p);
                    // List<Edge> equalStarNeighbors = GetEqualStarNeighbors(p);
                    // GD.Print(simplex);
                    // // GD.Print();
                    // lowerStarNeighbors.ForEach(x => GD.Print("L: \t"+ x + " " + x.GetLowerValue() * 100));
                    // GD.Print("");
                    // equalStarNeighbors.ForEach(x => GD.Print("E: \t"+ x + " " + x.GetLowerValue() * 100));
                    // GD.Print("");
                    ConnectivityTable[simplex].ToList().ForEach(x => GD.Print("N: \t" + x + " " + x.GetLowerValue()));
                    tracker.GlobalPosition = TransformCentroid(simplex.GetCentroid(), size);



                }


                if (simplex is Point point && (timestep <= -1 || timestep >= iteration))
                {
                    LogEvent($"Currently Processing Point: {point}");
                    ProcessNeighbors(point);

                }
                else if (simplex is Edge edge && (timestep <= -1 || timestep >= iteration))
                {
                    LogEvent($"Currently Processing Edge: {edge}");
                    ValidiateSimplex(edge, edge.GetPoints());

                }
                else if (simplex is Square square && (timestep <= -1 || timestep >= iteration))
                {
                    LogEvent($"Currently Processing Square: {square}");
                    ValidiateSimplex(square, square.GetEdges());

                }
                iteration++;
            }
            // PrintTable(Gradients.OrderBy(x => GetSimplexTypePriority(x.Key)), "Gradients");
        }

        private void PrintTable<T>(IEnumerable<T> table, string name)
        {
            GD.Print($"{name}:");
            foreach (T element in table)
                GD.PrintRich($"\t[color=orange]>{element}");
        }


        private List<Edge> GetLowerStarNeighbors(Point point)
        {
            return [.. ConnectivityTable[point].OfType<Edge>()
                .Where(edge => point > edge.GetOtherPoint(point))
                .OrderBy(edge => edge.GetLowerValue())];
        }

        private List<Edge> GetEqualStarNeighbors(Point point)
        {
            return [.. ConnectivityTable[point].OfType<Edge>()
                .Where(edge => point.Elevation == edge.PointA.Elevation &&
                               point.Elevation == edge.PointB.Elevation)
                .OrderBy(edge => (edge.GetOtherPoint(point).X, edge.GetOtherPoint(point).Y))];
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
                List<Edge> lowerStarNeighbors = EliminateUsedEdges(GetLowerStarNeighbors(point));
                List<Edge> equalStarNeighbors = EliminateUsedEdges(GetEqualStarNeighbors(point));
                if (equalStarNeighbors.Count == ConnectivityTable[point].Count)
                {
                    AssignGradient(point, null, null, GradientDirection.IGNORED);
                }
                else if (lowerStarNeighbors.Count > 0)
                {
                    Edge steepestDescent = lowerStarNeighbors[0];
                    lowerStarNeighbors.RemoveAt(0);

                    LogEvent($"[color=green]Edge of Steepest Descent: {steepestDescent}");

                    AssignGradient(point, steepestDescent, GetDirectionToEdge(point, steepestDescent));
                    AssignGradient(steepestDescent, point, null, GradientDirection.NON_CRITICAL);

                    LogEvent(lowerStarNeighbors.Count == 0 ? $"No Lower Star Neighbors" : $"Processing Lower Star Neighbors");

                    lowerStarNeighbors.Reverse();

                    AssignNeighbors(lowerStarNeighbors);

                    LogEvent(equalStarNeighbors.Count == 0 ? $"No Equal Star Neighbors" : $"Processing Equal Star Neighbors");

                    AssignNeighbors(equalStarNeighbors);
                }
                // else if (equalStarNeighbors.Count == 1)
                // {
                //     Edge soleNeighbor = equalStarNeighbors[0];
                //     LogEvent($"[color=orange]Edge of Sole Neighbor: {soleNeighbor}");
                //     AssignGradient(point, soleNeighbor, GetDirectionToEdge(point, soleNeighbor));
                //     AssignGradient(soleNeighbor, point, null, GradientDirection.NON_CRITICAL);
                // }
                else
                {
                    AssignGradient(point, null, GradientDirection.CRITICAL);
                    LogEvent("[color=orange]Point is Critical");
                }
            }
        }

        private void AssignNeighbors(List<Edge> neighbors)
        {
            foreach (Edge edge in neighbors)
            {
                List<Square> associatedSquares = [.. ConnectivityTable[edge].OfType<Square>()
                    .Where(IsGradientUnassigned).Where(s =>
                        s.GetLowerValue() < edge.GetLowerValue() || s.GetUpperValue() < edge.GetUpperValue() || s.GetAverageValue() < edge.GetAverageValue()
                    )
                    .OrderBy(s => (s.GetLowerValue(), s.GetUpperValue(), s.GetAverageValue()))
                ];

                LogEvent($"\t{edge} {edge.GetLowerValue():F3} {edge.GetUpperValue():F3} {edge.GetAverageValue():F3}");
                if (ConnectivityTable[edge].Count > 0)
                    LogEvent($"\t\t{ConnectivityTable[edge].ToList()[0]} {ConnectivityTable[edge].ToList()[0].GetLowerValue():F3} {ConnectivityTable[edge].ToList()[0].GetUpperValue():F3} {ConnectivityTable[edge].ToList()[0].GetAverageValue():F3}");
                if (ConnectivityTable[edge].Count > 1)
                    LogEvent($"\t\t{ConnectivityTable[edge].ToList()[1]} {ConnectivityTable[edge].ToList()[1].GetLowerValue():F3} {ConnectivityTable[edge].ToList()[1].GetUpperValue():F3} {ConnectivityTable[edge].ToList()[1].GetAverageValue():F3}");

                switch (associatedSquares.Count)
                {
                    case 0:
                        LogEvent($"[color=pink]\t\t\tChoose: IGNORE");
                        break;
                    case 1:
                    case 2:
                        AssignGradient(edge, associatedSquares[0], GetDirectionToSquare(edge, associatedSquares[0]));
                        AssignGradient(associatedSquares[0], edge, null, GradientDirection.NON_CRITICAL);
                        LogEvent($"[color=pink]\t\t\tChoose: {associatedSquares[0]}");
                        break;
                    default:
                        throw new ArgumentException("Edge has more than 2 squares attached.");
                }
            }
        }

        private void ValidiateSimplex(Simplex simplex, Simplex[] parts)
        {
            // if ((simplex is Edge e && e.GetPoints().All(p => Gradients[p].IsIgnored())) ||
            //     (simplex is Square s && s.GetPoints().All(p => Gradients[p].IsIgnored())))
            // {
            //     AssignGradient(simplex, null, null, GradientDirection.IGNORED);
            //     LogEvent($"[color=red]{simplex} is not Considered");
            // }
            // else if (IsGradientUnassigned(simplex))
            // {
            //     AssignGradient(simplex, null, null, GradientDirection.CRITICAL);
            //     LogEvent($"[color=red]{simplex} is Critical");
            // }
            // else
            // {
            //     LogEvent($"[color=red]{simplex} remains the same: {Gradients[simplex].Direction}");
            // }

            if (simplex is Edge && IsGradientUnassigned(simplex) && parts.All(p => (Gradients[p].IsDirectional() || Gradients[p].IsCritical()) && Gradients[p].Higher != simplex))
            {
                AssignGradient(simplex, null, null, GradientDirection.CRITICAL);
            }
            else if (simplex is Square && IsGradientUnassigned(simplex) && parts.All(p => (Gradients[p].IsDirectional() || Gradients[p].IsNonCritical() || Gradients[p].IsCritical()) && Gradients[p].Higher != simplex))
            {
                AssignGradient(simplex, null, null, GradientDirection.CRITICAL);
            }
            else
            {
                LogEvent($"[color=red]{simplex} remains the same: {Gradients[simplex].Direction}");
            }
        }

        private void CalculateManifolds()
        {
            foreach ((Simplex source, Gradient gradient) in Gradients)
            {
                if (source is not Edge edge || !gradient.IsCritical())
                    continue;

                foreach (Square square in ConnectivityTable[edge].Cast<Square>())
                {
                    // Remove squares that do not contribute to path to a critical point
                    if (square.GetEdges().All(e => Gradients[e].Lower != square) && Gradients[square].IsIgnored())
                        continue;
                    Manifold.CreateJunction(edge, square, s => Gradients[s].IsCritical(), GetNext);
                }

                foreach (Point point in edge.GetPoints())
                {
                    Manifold.CreateJunction(edge, point, s => Gradients[s].IsCritical(), GetNext);
                }
            }
        }

        private Simplex GetNext(Simplex simplex)
        {
            if (simplex is Point point)
            {
                if (Gradients.TryGetValue(point, out Gradient gradient) && gradient.Lower is Edge edge)
                {
                    return edge.GetOtherPoint(point);
                }
            }
            else if (simplex is Square square)
            {
                Edge directionEdge = GetEdgePointingToSquare(square);
                return directionEdge != null ? GetOtherSquare(square, directionEdge) : null;
            }

            return null;
        }

        public Edge GetEdgePointingToSquare(Square square)
        {
            foreach (Edge edge in square.GetEdges())
            {
                if (Gradients[edge].Lower == square)
                    return edge;
            }
            return null;
        }

        #region Getting Direction
        public static GradientDirection GetDirectionToEdge(Point point, Edge edge)
        {
            Point otherPoint = edge.GetPoints().First(p => p != point);
            return Gradient.VectorToDirection(otherPoint.Position - point.Position);
        }

        public static GradientDirection GetDirectionToPoint(Point pointA, Point pointB)
        {
            return Gradient.VectorToDirection(pointB.Position - pointA.Position);
        }

        public static GradientDirection GetDirectionToSquare(Edge edge, Square square)
        {
            Point[] points = square.GetPoints();
            Point otherPointA = points.First(p => p != edge.PointA && p != edge.PointB);
            Point otherPointB = points.Last(p => p != edge.PointA && p != edge.PointB);

            Vector2 vectorA = edge.PointA.Position;
            Vector2 vectorB = edge.PointB.Position;
            Vector2 vectorC = otherPointA.Position;
            Vector2 vectorD = otherPointB.Position;

            Vector2 center = (vectorA + vectorB + vectorC + vectorD) / 4;
            Vector2 midpoint = (vectorA + vectorB) / 2;
            Vector2I directionVector = (Vector2I)(center - midpoint).Normalized();

            GradientDirection direction = Gradient.VectorToDirection(directionVector);
            if (direction != GradientDirection.IGNORED)
                return direction;
            else
                throw new ArgumentException($"Invalid direction calculated from edge to square: {directionVector}");
        }
        #endregion

        public Square GetOtherSquare(Square square, Edge edge)
        {
            foreach (Square otherSquare in ConnectivityTable[edge].Cast<Square>())
            {
                if (otherSquare != square)
                    return otherSquare;
            }
            return null;
        }

        private Edge GetEdgeFromDirection(Point point, GradientDirection direction)
        {
            Vector2I directionVector = Gradient.DirectionToVector(direction);
            foreach (Edge edge in ConnectivityTable[point].Cast<Edge>())
            {
                Point otherPoint = edge.GetOtherPoint(point);
                if (point.Position + directionVector == otherPoint.Position)
                    return edge;
            }
            return null;
        }

        private Square GetSquareFromDirection(Edge edge, GradientDirection direction)
        {
            foreach (Square square in ConnectivityTable[edge].Cast<Square>())
            {
                if (GetDirectionToSquare(edge, square) == direction)
                    return square;
            }
            return null;
        }

        private static int GetSimplexTypePriority(Simplex simplex) => simplex switch
        {
            Point => 0,
            Edge => 1,
            Square => 2,
            _ => 3
        };

        private void LogEvent(string message) { if (_debug) GD.PrintRich(message); }
    }
}