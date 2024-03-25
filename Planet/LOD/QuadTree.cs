using Godot;
using System;
using System.Collections.Generic;
using System.Linq;


[Tool]
/// <summary>
/// 
/// QuadTree
/// 
/// </summary>
public partial class QuadTree : Node
{

    public enum NormalDirectionType
    {
        None = 0b000,
        Up = 0b010,
        Down = 0b101,
        Left = 0b011,
        Right = 0b100,
        Forward = 0b110,
        Backward = 0b001
    }


    [Export]
    public NormalDirectionType NormalDirection
    {
        get
        {

            if (_normal == Vector3.Zero)
            {
                return NormalDirectionType.None;
            }

            bool isNegative = _normal.X == -1 || _normal.Y == -1 || _normal.Z == -1;
            int index = (int)_normal.X * 4 + (int)_normal.Y * 2 + (int)_normal.Z;
            return (NormalDirectionType)(isNegative ? ((-1 * index) ^ 0b111) & 0b111 : index);
        }
        set
        {
            uint intValue = (uint)value;

            if (intValue == 0)
            {
                _normal = Vector3.Zero;
                return;
            }

            int negative = intValue == 1 || intValue == 2 || intValue == 4 ? 1 : -1;
            uint x = (intValue & 0b100) >> 2;
            uint y = (intValue & 0b010) >> 1;
            uint z =  intValue & 0b001;

            x = negative == 1 ? x : x^1;
            y = negative == 1 ? y : y^1;
            z = negative == 1 ? z : z^1;
            _normal = new Vector3(x, y, z) * negative;

        }
    }

    [Export] public bool IsDisabled { get => _isDisabled; set { _isDisabled = value; } }
    public Vector3 AxisA { get => _axisA; }
    public Vector3 AxisB { get => _axisB; }
    public Vector3 Mask { get => _mask; }

    private Vector3 _normal;
    private bool _isDisabled = false;
    private QuadTreeNode _root;
    private Vector3 _mask;

    private Planet _planet;
    private Vector3 _axisA; // axisA is the same as localRight
    private Vector3 _axisB; // axisB is the same as localForward

    int _indexOfX = -1;
    int _indexOfY = -1;

    private readonly List<QuadTreeNode> _nodeBuffer = new List<QuadTreeNode>();
    private QuadTree() { }

    private Dictionary<int, Vector3> _localCardinalDirections;

    public Vector3 GetNormal()
    {
        return _normal;
    }

    public void Initialize(Planet planet)
    {
        _planet = planet;

        _axisA = new Vector3(_normal.Y, _normal.Z, _normal.X);
        _axisB = _normal.Cross(_axisA);
        _localCardinalDirections = new Dictionary<int, Vector3>
        {
            {0, _axisA},
            {1, -_axisA},
            {2, _axisB},
            {3, -_axisB}
        };
        _indexOfX = Vector3Utils.GetIndexOfNormalComponent(_axisA);
        _indexOfY = Vector3Utils.GetIndexOfNormalComponent(_axisB);
        _mask = Vector3Utils.GenerateVectorExclusionMaskFrom(_normal);
    }


    public void UpdateQuadTree(Vector3 target)
    {
        _root = new QuadTreeNode(this, null, _normal, _axisA, _axisB, QuadTreeNode.Coordinate.Root, 0);
        if (IsDisabled) return;
        _nodeBuffer.Clear();
        UpdateQuadTree(target, _root);
    }

    public QuadTreeNode Traverse(uint hashPath)
    {

        string stringIterator = Convert.ToString(hashPath, 2)[1..];
        return Traverse(hashPath, stringIterator, _root);
    }

    private QuadTreeNode Traverse(uint originalHash, string stringIterator, QuadTreeNode node)
    {

        if (originalHash == node.hashValue || node.hasChildren == false || stringIterator.Length == 0)
        {
            return node;
        }

        string encoding = stringIterator[..2];
        int index = Convert.ToInt32(encoding, 2);
        QuadTreeNode childNode = node.GetChild((QuadTreeNode.Coordinate)index);

        return Traverse(originalHash, stringIterator[2..], childNode);
    }

    public QuadTreeNode Traverse(uint hashPath, int subdivisionLevel)
    {
        if (IsDisabled) return _root;
        return Traverse(hashPath, subdivisionLevel, _root);
    }

    private QuadTreeNode Traverse(uint originalHash, int subdivisionLevel, QuadTreeNode node)
    {

        if (originalHash == node.hashValue || node.hasChildren == false || subdivisionLevel == 0)
        {
            return node;
        }

        // Commonly used value
        uint operationInt = (uint)Mathf.Pow(4, subdivisionLevel - 1);

        // Make a mask that excludes all but the 2nd and 3rd to last digit in the hashPath
        // 0b11100 -> 0b01100
        uint leadingMask = 3 * operationInt;

        uint leading = (leadingMask & originalHash) >> (2 * (subdivisionLevel - 1));

        QuadTreeNode childNode = node.GetChild((QuadTreeNode.Coordinate)leading);

        return Traverse(originalHash, subdivisionLevel - 1, childNode);
    }

    public void SetVisibleNodes(Dictionary<int, List<QuadTreeNode>> nodeMeshType)
    {
        if (IsDisabled) return;

        if (_nodeBuffer.Count == 0 && Engine.IsEditorHint())
        {
            _nodeBuffer.Add(_root);
        }

        for (int i = 0; i < _nodeBuffer.Count; i++)
        {
            QuadTreeNode node = _nodeBuffer[i];

            byte[] neighbors = node.GetNeighborLODs();

            if (neighbors[0] == 1 || neighbors[1] == 1 || neighbors[2] == 1 || neighbors[3] == 1)
            {
                node.isFan = true;
            }
            else
            {
                node.isFan = false;
            }

            int index = 1 * neighbors[0] + 2 * neighbors[1] + 4 * neighbors[2] + 8 * neighbors[3];

            nodeMeshType[index].Add(node);

        }
    }


    public float Normalize(float value, float min, float max)
    {
        return (value - min) / (max - min);
    }

    readonly Dictionary<int, float> renderDistanceLOD = new Dictionary<int, float>()
    {
        {0, 350},
        {1, 250},
        {2, 150},
        {3, 100},
        {4, 90},
        {5, 80},
        {6, 70},
        {7, 60},
        {8, 50},
        {9, 50},
    };

    readonly Dictionary<int, float> renderAngleLOD = new Dictionary<int, float>()
    {
        {0, 180},
        {1, 128},
        {2, 64},
        {3, 32},
        {4, 16},
        {5, 8},
        {6, 4},
        {7, 2},
        // {8, 1},
        // {9, 0.5f},
    };


    private void UpdateQuadTree(Vector3 target, QuadTreeNode node)
    {
        float renderAngle = node.spherePosition.AngleTo(target);
        float renderDistance = target.DistanceTo(_planet.Transform.Origin);

        if (Mathf.DegToRad(_planet.Subdivision[node.subdivisionLevel]) >= renderAngle)
        {
            if (node.subdivisionLevel < _planet.Subdivision.Length - 1)
            {
                if (!node.hasChildren)
                {
                    node.GenerateChildren();
                }

                foreach (QuadTreeNode child in node.children)
                {
                    UpdateQuadTree(target, child);
                }
            }
            else
            {
                _nodeBuffer.Add(node);
            }
        }
        else
        {
            _nodeBuffer.Add(node);   
        }
    }

    public override int GetHashCode()
    {
        return _normal.GetHashCode() + _planet.GetHashCode();
    }


    public override string ToString()
    {
        return $"{{\n{_root}\n}}";
    }

    /// <summary>
    /// 
    /// QuadTreeNode
    /// 
    /// </summary>
    public partial class QuadTreeNode
    {
        internal QuadTree quadTree;
        internal QuadTreeNode parent;
        internal QuadTreeNode[] children = new QuadTreeNode[4];
        internal Vector3 cubePosition;
        internal Vector3 spherePosition;
        internal Coordinate coordinate;
        internal bool hasChildren;
        internal Vector3 axisA;
        internal Vector3 axisB;
        internal int subdivisionLevel;
        internal uint hashValue;
        internal bool isFan;
        internal bool[] isEdge = new bool[4];

        public bool[] cornerType = new bool[4];

        internal enum Coordinate
        {
            Root = 4,
            NorthWest = 0,
            NorthEast = 1,
            SouthEast = 2,
            SouthWest = 3
        }

        private QuadTreeNode() { }

        internal QuadTreeNode(QuadTree quadTree, QuadTreeNode parent, Vector3 cubePosition, Vector3 axisA, Vector3 axisB, Coordinate coordinate, int subdivisionLevel)
        {
            this.quadTree = quadTree;
            this.parent = parent;

            hashValue = coordinate == Coordinate.Root ? 1 : parent.hashValue * 4 + (uint)coordinate;

            if (coordinate == Coordinate.NorthWest && subdivisionLevel == 5)
                cornerType[0] = true;
            else if (coordinate == Coordinate.NorthEast && subdivisionLevel == 5)
                cornerType[1] = true;
            else if (coordinate == Coordinate.SouthEast && subdivisionLevel == 5)
                cornerType[2] = true;
            else if (coordinate == Coordinate.SouthWest && subdivisionLevel == 5)
                cornerType[3] = true;

            // GD.PrintS(coordinate, Convert.ToString(hashValue, 2));

            this.cubePosition = cubePosition;
            this.coordinate = coordinate;
            this.axisA = axisA;
            this.axisB = axisB;


            this.subdivisionLevel = subdivisionLevel;
            spherePosition = PointOnCubeToPointOnSphere(cubePosition);
            hasChildren = false;
        }

        internal void GenerateChildren()
        {
            Vector3[] coordinates = GenerateCornerCoordinates(0.5f);
            children[(int)Coordinate.NorthWest] = new QuadTreeNode(quadTree, this, coordinates[0], axisA / 2, axisB / 2, Coordinate.NorthWest, subdivisionLevel + 1);
            children[(int)Coordinate.NorthEast] = new QuadTreeNode(quadTree, this, coordinates[1], axisA / 2, axisB / 2, Coordinate.NorthEast, subdivisionLevel + 1);
            children[(int)Coordinate.SouthEast] = new QuadTreeNode(quadTree, this, coordinates[2], axisA / 2, axisB / 2, Coordinate.SouthEast, subdivisionLevel + 1);
            children[(int)Coordinate.SouthWest] = new QuadTreeNode(quadTree, this, coordinates[3], axisA / 2, axisB / 2, Coordinate.SouthWest, subdivisionLevel + 1);

            hasChildren = true;
        }

        internal Vector3[] GenerateCornerCoordinates(float scale)
        {


            Vector3[] coordinates = new Vector3[4];
            // Direction is relative to axisA and axisB
            coordinates[0] = cubePosition + scale * (-axisA + axisB);
            coordinates[1] = cubePosition + scale * (axisA + axisB);
            coordinates[2] = cubePosition + scale * (axisA - axisB);
            coordinates[3] = cubePosition + scale * (-axisA - axisB);

            return coordinates;
        }

        internal QuadTreeNode GetChild(Coordinate coordinate)
        {
            return children[(int)coordinate];
        }


        public static Vector3 PointOnCubeToPointOnSphere(Vector3 point)
        {
            float x2 = point.X * point.X;
            float y2 = point.Y * point.Y;
            float z2 = point.Z * point.Z;

            float x = point.X * Mathf.Sqrt(1 - (y2 + z2) / 2 + y2 * z2 / 3);
            float y = point.Y * Mathf.Sqrt(1 - (z2 + x2) / 2 + z2 * x2 / 3);
            float z = point.Z * Mathf.Sqrt(1 - (x2 + y2) / 2 + x2 * y2 / 3);

            return new Vector3(x, y, z);
        }

        public override string ToString()
        {
            string s = "";
            s += $"\n\"{coordinate}\": {{";
            s += $"\n\"position\": [{cubePosition.X}, {cubePosition.Y}, {cubePosition.Z}],";
            s += $"\n{(children[0] != null ? children[0] : $"\"{Coordinate.NorthWest}\": null")},";
            s += $"\n{(children[1] != null ? children[1] : $"\"{Coordinate.NorthEast}\": null")},";
            s += $"\n{(children[2] != null ? children[2] : $"\"{Coordinate.SouthWest}\": null")},";
            s += $"\n{(children[3] != null ? children[3] : $"\"{Coordinate.SouthEast}\": null")}";
            s += $"\n}}";
            return s;
        }

        internal byte[] GetNeighborLODs()
        {
            byte[] neighbors = new byte[4];
            
            

            Vector3[] corners = GenerateCornerCoordinates(1);

            // To see if a node is at an edge we must check if any of its corner coordinates is equal to axisA or axisB 
            // since every tree is relative only to itself
            isEdge[0] = corners[1][quadTree._indexOfX] == quadTree._axisA[quadTree._indexOfX]; 
            isEdge[1] = corners[0][quadTree._indexOfX] == -quadTree._axisA[quadTree._indexOfX];
            isEdge[2] = corners[0][quadTree._indexOfY] == quadTree._axisB[quadTree._indexOfY];
            isEdge[3] = corners[2][quadTree._indexOfY] == -quadTree._axisB[quadTree._indexOfY];
        
            // 0 = east, 1 = west, 2 = north, 3 = south

            if (coordinate == Coordinate.NorthWest)
            {
                neighbors[1] = CheckNeighborLOD(1);
                neighbors[2] = CheckNeighborLOD(2);
            }
            else if (coordinate == Coordinate.NorthEast)
            {
                neighbors[0] = CheckNeighborLOD(0);
                neighbors[2] = CheckNeighborLOD(2);
            }
            else if (coordinate == Coordinate.SouthEast)
            {
                neighbors[0] = CheckNeighborLOD(0);
                neighbors[3] = CheckNeighborLOD(3);
            }
            else if (coordinate == Coordinate.SouthWest)
            {
                neighbors[1] = CheckNeighborLOD(1);
                neighbors[3] = CheckNeighborLOD(3);
            }

            return neighbors;
        }

        internal byte CheckNeighborLOD(byte side)
        {
            uint bitmask = 0;

            uint twoLast;
            uint hashValue = this.hashValue;

            for (int i = 0; i < subdivisionLevel; i++)
            {

                twoLast = hashValue & 3;
                bitmask *= 4;

                bitmask += (uint)((side == 2 || side == 3) ? 3 : 1);

                if ((side == 0 && (twoLast == 0 || twoLast == 3)) ||
                    (side == 1 && (twoLast == 1 || twoLast == 2)) ||
                    (side == 2 && (twoLast == 3 || twoLast == 2)) ||
                    (side == 3 && (twoLast == 0 || twoLast == 1)))
                {
                    break;
                }

                hashValue >>= 2;
            }

    
            QuadTree selectedQuadTree = quadTree;
            string traversePath = Convert.ToString(this.hashValue ^ bitmask, 2);

            if (isEdge[side])
            {
                selectedQuadTree = quadTree._planet.Surface.GetQuadTree(quadTree._localCardinalDirections[side]);
                
                string path = "1";
                string stringTraversePath = traversePath[1..];

                uint majority = 1;
                uint minority = 3;

                if (quadTree._normal.X == -1 || quadTree._normal.Y == -1 || quadTree._normal.Z == -1)
                {
                    majority = 3;
                    minority = 1;
                }

                bool isAxisA = selectedQuadTree._normal == quadTree.AxisA;

                for (int i = 0; i < stringTraversePath.Length; i += 2)
                {
                    uint pathSegment = Convert.ToUInt32(stringTraversePath[i..(i + 2)]);
                    pathSegment = (isAxisA ? (pathSegment + minority) : (pathSegment + majority)) % 4;
                    path += Convert.ToString(pathSegment, 2).PadLeft(2,'0');
                }

                traversePath = path;
            }

            QuadTreeNode neighborNode = selectedQuadTree.Traverse(Convert.ToUInt32(traversePath, 2), subdivisionLevel);

            return (byte)(neighborNode.subdivisionLevel < subdivisionLevel ? 1 : 0);


        }
    }
}
