using Godot;
using Godot.Collections;
using System.Threading.Tasks;
[Tool]

public partial class Dispatcher : Node
{

    [ExportGroup("Required")]
    [Export] private MultiMeshInstance3D _multimesh;
    [Export] private ShaderMaterial _material;

    [ExportGroup("Settings")]
    [Export(PropertyHint.Range, "1, 1000")] private int _updateFrequency = 60;
    [Export] private uint MaximumNodes = 30000;

    [ExportGroup("Shaders")]
    [Export(PropertyHint.File)] private string _computeCullShader;
    [Export(PropertyHint.File)] private string _copyShader;

    public PlanetData PlanetData
    {
        get => _planetData;
        set
        {
      
                // if (_planetData != null)
                    // _planetData.Changed -= UpdateMulitMesh;
            _planetData = value;
            CleanupGPU();
            SetupComputeShader();
            UpdateMulitMesh();
                // if (_planetData != null)
                    // _planetData.Changed += UpdateMulitMesh;
            
        }
    }
    private PlanetData _planetData;
    public bool Processing { get; set; }
    private Vector4[] _trianglePoints;

    public Camera3D Camera { get; set; }

    private RenderingDevice _rd;

    private Rid _uniformSet_CC;
    private Rid _shader_CC;
    private Rid _pipeline_CC;

    private Rid _uniformSet_C;
    private Rid _shader_C;
    private Rid _pipeline_C;

    private Array<RDUniform> _bindings_CC = new();
    private Array<RDUniform> _bindings_C = new();

    private Rid _atomicCounterBuffer;
    private Rid _indicesBlockBuffer;
    private Rid _readList;
    private Rid _writeFullList;
    private Rid _writeCulledList;
    private Rid _positions;
    private Rid _cameraData;
    private Rid _debug;
    private Rid _dispatchIndirectBuffer;

    #region MAIN LOOP

    public override void _Ready()
    {
        CreateTrianglePoints();
    }



    public void CreateTrianglePoints()
    {
        _trianglePoints = new Vector4[30];
        Vector3[] normals = new Vector3[]
        {
            Vector3.Up,
            Vector3.Down,
            Vector3.Right,
            Vector3.Left,
            Vector3.Forward,
            Vector3.Back,
        };

        for (int i = 0; i < 6; i++)
        {
            Vector3 normal = normals[i];
            Vector3 axisA = new Vector3(normal.Y, normal.Z, normal.X);
            Vector3 axisB = normal.Cross(axisA);

            _trianglePoints[5 * i + 0] = Vector3Utils.toVector4(normal, 1);
            _trianglePoints[5 * i + 1] = Vector3Utils.toVector4(-axisA + axisB + normal, 1);
            _trianglePoints[5 * i + 2] = Vector3Utils.toVector4(-axisA - axisB + normal, 1);
            _trianglePoints[5 * i + 3] = Vector3Utils.toVector4(axisA + axisB + normal, 1);
            _trianglePoints[5 * i + 4] = Vector3Utils.toVector4(axisA - axisB + normal, 1);
        }

    }


    public void SetMaterialParameters()
    {
        _material.SetShaderParameter("position_list", _trianglePoints);
        _material.SetShaderParameter("height_gradient", _planetData.HeightGradient);
        _material.SetShaderParameter("radius", _planetData.Radius);
        _material.SetShaderParameter("albedo_map", _planetData.AlbedoMap);
        _material.SetShaderParameter("height_map", _planetData.HeightMap);
        _material.SetShaderParameter("height_scale", _planetData.HeightScale);
        _material.SetShaderParameter("is_debug", _planetData.DebugMode);
        _material.SetShaderParameter("is_cube", _planetData.CubeMode);
        _material.SetShaderParameter("resolution", _planetData.Resolution);
        _material.SetShaderParameter("normal_strength", _planetData.NormalStrength);
    }

    public void UpdateMulitMesh()
    {
        Vector3[] vertices = new Vector3[_planetData.Resolution * (_planetData.Resolution + 1) / 2];
        Vector3[] normals = new Vector3[_planetData.Resolution * (_planetData.Resolution + 1) / 2];
        Vector2[] uvs = new Vector2[_planetData.Resolution * (_planetData.Resolution + 1) / 2];
        int[] triangles = new int[(_planetData.Resolution - 1) * (_planetData.Resolution - 1) * 6 / 2];
        Vector3 normal = Vector3.Back;
        Vector3 axisA = new Vector3(normal.Y, normal.Z, normal.X);
        Vector3 axisB = normal.Cross(axisA).Abs();
        int triIndex = 0;
        int vertexIndex = 0;

        for (int y = 0; y < _planetData.Resolution; y++)
        {
            for (int x = 0; x < _planetData.Resolution - y; x++)
            {
                int currentIndex = vertexIndex++;
                Vector2 percentage = new Vector2(x, y) / (_planetData.Resolution - 1);
                vertices[currentIndex] = normal + (percentage.X * axisA + percentage.Y * axisB);
                uvs[currentIndex] = new Vector2(x, y);
                normals[currentIndex] = normal;

                if (x != _planetData.Resolution - y - 1)
                {
                    if (x == _planetData.Resolution - y - 2)
                    {
                        triangles[triIndex++] = currentIndex;
                        triangles[triIndex++] = currentIndex + 1;
                        triangles[triIndex++] = currentIndex + _planetData.Resolution - y;
                    }
                    else
                    {
                        bool isXEven = x % 2 == 0;
                        bool isYEven = y % 2 == 0;

                        if ((isXEven && isYEven) || (!isXEven && !isYEven))
                        {
                            triangles[triIndex++] = currentIndex;
                            triangles[triIndex++] = currentIndex + _planetData.Resolution - y + 1;
                            triangles[triIndex++] = currentIndex + _planetData.Resolution - y;
                            triangles[triIndex++] = currentIndex;
                            triangles[triIndex++] = currentIndex + 1;
                            triangles[triIndex++] = currentIndex + _planetData.Resolution - y + 1;
                        }
                        else
                        {
                            triangles[triIndex++] = currentIndex;
                            triangles[triIndex++] = currentIndex + 1;
                            triangles[triIndex++] = currentIndex + _planetData.Resolution - y;
                            triangles[triIndex++] = currentIndex + 1;
                            triangles[triIndex++] = currentIndex + _planetData.Resolution - y + 1;
                            triangles[triIndex++] = currentIndex + _planetData.Resolution - y;
                        }
                    }
                }
            }
        }

        ArrayMesh mesh = new();
        Array arrays = new();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Index] = triangles;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, _material);

        _multimesh.Multimesh = new MultiMesh()
        {
            Mesh = mesh,
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = 0,
            UseCustomData = true,
            UseColors = true
        };
        _multimesh.ExtraCullMargin = 2 * _planetData.Radius;


        SetMaterialParameters();


    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest || what == NotificationPredelete)
            CleanupGPU();

    }
    #endregion

    #region SHADER SETUP

    public void SetupComputeShader()
    {
        CreateRenderingDevice();

        // Compute & Cull
        _shader_CC = CreateShader(_computeCullShader);
        _pipeline_CC = CreatePipeline(_shader_CC);

        // Copy
        _shader_C = CreateShader(_copyShader);
        _pipeline_C = CreatePipeline(_shader_C);

        CreateUniforms();
    }

    private void CreateRenderingDevice()
    {
        _rd = RenderingServer.CreateLocalRenderingDevice();
    }

    private Rid CreateShader(string path)
    {
        RDShaderFile shaderFile = GD.Load<RDShaderFile>(path);
        RDShaderSpirV spirV = shaderFile.GetSpirV();
        return _rd.ShaderCreateFromSpirV(spirV);
    }

    private Rid CreatePipeline(Rid shader)
    {
        return _rd.ComputePipelineCreate(shader);
    }

    #region CREATE UNIFORMS
    private (RDUniform, Rid) CreateUniformFromData(byte[] data, int binding, int indirect = 0)
    {
        Rid buffer = _rd.StorageBufferCreate((uint)data.Length, data, usage: (RenderingDevice.StorageBufferUsage)indirect);
        RDUniform uniform = new()
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = binding
        };

        uniform.AddId(buffer);
        return (uniform, buffer);
    }

    private void CreateUniforms()
    {
        if (_rd == null) return;
        CreateAtomicCounter(0);
        CreateIndicesBlock(1);
        CreateReadList(2);
        CreateWriteFullList(3);
        CreateWriteCulledList(4);
        CreatePositionList(5);
        CreateCameraData(6);
        CreateDebugList(7);
        CreateDispatchOutBuffer(2);

        _uniformSet_C = _rd.UniformSetCreate(_bindings_C, _shader_C, 0);
        _uniformSet_CC = _rd.UniformSetCreate(_bindings_CC, _shader_CC, 0);
    }

    private void CreateAtomicCounter(int binding)
    {
        uint[] primCountFullAndCull = new uint[2 * 16];
        primCountFullAndCull[0] = 12 * 4;
        byte[] data = Utilities.ToBytes<uint>(primCountFullAndCull).ToArray();

        (RDUniform uniform, _atomicCounterBuffer) = CreateUniformFromData(data, binding);
        _bindings_C.Add(uniform);
        _bindings_CC.Add(uniform);
    }

    private void CreateIndicesBlock(int binding)
    {
        uint[] indices = new uint[] { 0, 1, 8, MaximumNodes };
        byte[] data = Utilities.ToBytes<uint>(indices).ToArray();

        (RDUniform uniform, _indicesBlockBuffer) = CreateUniformFromData(data, binding);
        _bindings_C.Add(uniform);
        _bindings_CC.Add(uniform);
    }

    private void CreateReadList(int binding)
    {
        Vector4I[] readList = new Vector4I[MaximumNodes];

        // Generate cube

        // key = uvec4(nodeID_MSB, nodeID_LSB, meshPolygonID, rootID)
        for (int i = 0; i < 12; i++)
        {
            readList[4 * i + 0] = new Vector4I(0, 1, i, 0);
            readList[4 * i + 1] = new Vector4I(0, 1, i, 1);
            readList[4 * i + 2] = new Vector4I(0, 1, i, 2);
            readList[4 * i + 3] = new Vector4I(0, 1, i, 3);
        }

        byte[] data = Utilities.ToBytes<Vector4I>(readList).ToArray();

        (RDUniform uniform, _readList) = CreateUniformFromData(data, binding);
        _bindings_CC.Add(uniform);
    }

    private void CreatePositionList(int binding)
    {
        byte[] data = Utilities.ToBytes<Vector4>(_trianglePoints).ToArray();

        (RDUniform uniform, _positions) = CreateUniformFromData(data, binding);
        _bindings_CC.Add(uniform);
    }

    private void CreateWriteFullList(int binding)
    {
        Key[] writeList = new Key[MaximumNodes];

        byte[] data = Utilities.ToBytes<Key>(writeList).ToArray();

        (RDUniform uniform, _writeFullList) = CreateUniformFromData(data, binding);
        _bindings_CC.Add(uniform);
    }

    private void CreateWriteCulledList(int binding)
    {
        Vector4I[] writeList = new Vector4I[MaximumNodes];

        byte[] data = Utilities.ToBytes<Vector4I>(writeList).ToArray();

        (RDUniform uniform, _writeCulledList) = CreateUniformFromData(data, binding);
        _bindings_CC.Add(uniform);
    }

    private void CreateDebugList(int binding)
    {
        byte[] data = Utilities.ToBytes<Vector4>(new Vector4[MaximumNodes]).ToArray();
        (RDUniform uniform, _debug) = CreateUniformFromData(data, binding);
        _bindings_CC.Add(uniform);
    }

    private void CreateCameraData(int binding)
    {
        Transform3D transform;
        Projection projectionMatrix = new();
        Basis basis = Basis.Identity;
        Vector3 origin = new Vector3(0, 0, 100 * 2 * _planetData.Radius);
        float fov = 75;
        float far = 4000;
        float near = 0.05f;

        if (Camera is not null)
        {
            transform = Camera.GlobalTransform;
            basis = transform.Basis;
            origin = transform.Origin;
            projectionMatrix = Camera.GetCameraProjection();
            fov = Camera.Fov;
            far = Camera.Far;
            near = Camera.Near;
        }

        byte[] data = Utilities.ToBytes<float>(new float[]
        {
            basis.X.X, basis.X.Y, basis.X.Z, 1.0f,
            basis.Y.X, basis.Y.Y, basis.Y.Z, 1.0f,
            basis.Z.X, basis.Z.Y, basis.Z.Z, 1.0f,
            origin.X,  origin.Y,  origin.Z,  1.0f,

            projectionMatrix[0].X, projectionMatrix[0].Y, projectionMatrix[0].Z, projectionMatrix[0].W,
            projectionMatrix[1].X, projectionMatrix[1].Y, projectionMatrix[1].Z, projectionMatrix[1].W,
            projectionMatrix[2].X, projectionMatrix[2].Y, projectionMatrix[2].Z, projectionMatrix[2].W,
            projectionMatrix[3].X, projectionMatrix[3].Y, projectionMatrix[3].Z, projectionMatrix[3].W,

            Mathf.DegToRad(fov), far, near, _planetData.Radius, _planetData.SubFactor * _planetData.Radius
        }).ToArray();

        (RDUniform uniform, _cameraData) = CreateUniformFromData(data, binding);
        _bindings_CC.Add(uniform);
    }

    private void CreateDispatchOutBuffer(int binding)
    {
        uint[] workgroups = new uint[] { 1, 1, 1 };

        (RDUniform uniform, _dispatchIndirectBuffer) = CreateUniformFromData(Utilities.ToBytes<uint>(workgroups).ToArray(), binding, 1);
        _bindings_C.Add(uniform);
    }
    #endregion

    #region UPDATE UNIFORMS
    private void UpdateCameraData()
    {
        Transform3D transform;
        Projection projectionMatrix = new();
        Basis basis = Basis.Identity;
        Vector3 origin = new Vector3(0, 0, 100 * 2 * _planetData.Radius);
        float fov = 75;
        float far = 4000;
        float near = 0.05f;

        if (Camera != null)
        {
            transform = Camera.GlobalTransform;
            basis = transform.Basis;
            origin = transform.Origin;
            projectionMatrix = Camera.GetCameraProjection();
            fov = Camera.Fov;
            far = Camera.Far;
            near = Camera.Near;
        }

        byte[] data = Utilities.ToBytes<float>(new float[]
        {
            basis.X.X, basis.X.Y, basis.X.Z, 0.0f,
            basis.Y.X, basis.Y.Y, basis.Y.Z, 0.0f,
            basis.Z.X, basis.Z.Y, basis.Z.Z, 0.0f,
            origin.X,  origin.Y,  origin.Z,  1.0f,

            projectionMatrix[0].X, projectionMatrix[0].Y, projectionMatrix[0].Z, projectionMatrix[0].W,
            projectionMatrix[1].X, projectionMatrix[1].Y, projectionMatrix[1].Z, projectionMatrix[1].W,
            projectionMatrix[2].X, projectionMatrix[2].Y, projectionMatrix[2].Z, projectionMatrix[2].W,
            projectionMatrix[3].X, projectionMatrix[3].Y, projectionMatrix[3].Z, projectionMatrix[3].W,

            Mathf.DegToRad(fov), far, near, _planetData.Radius, _planetData.SubFactor * _planetData.Radius
        }).ToArray();

        _rd.BufferUpdate(_cameraData, 0, (uint)data.Length, data);
    }

    private void UpdateReadList()
    {
        byte[] data = _rd.BufferGetData(_writeFullList);
        _rd.BufferUpdate(_readList, 0, (uint)data.Length, data);
    }

    private void UpdateWriteFullList()
    {
        byte[] data = Utilities.ToBytes<Vector4I>(new Vector4I[MaximumNodes]).ToArray();
        _rd.BufferUpdate(_writeFullList, 0, (uint)data.Length, data);
    }

    private void UpdateWriteCulledList()
    {
        byte[] data = Utilities.ToBytes<Vector4I>(new Vector4I[MaximumNodes]).ToArray();
        _rd.BufferUpdate(_writeCulledList, 0, (uint)data.Length, data);
    }

    private void UpdateIndicesBlock()
    {
        uint[] indices = Utilities.FromBytes<uint>(_rd.BufferGetData(_indicesBlockBuffer)).ToArray();
        indices[0] = (indices[0] + 1) % 16; // Read Index
        indices[1] = (indices[1] + 1) % 16; // Write Index
        indices[2] = (indices[2] + 1) % 16; // Delete Index
        indices[3] = MaximumNodes;
        byte[] data = Utilities.ToBytes<uint>(indices).ToArray();

        _rd.BufferUpdate(_indicesBlockBuffer, 0, (uint)data.Length, data);
    }

    private void UpdateDebug()
    {
        byte[] data = Utilities.ToBytes<Vector4>(new Vector4[MaximumNodes]).ToArray();
        _rd.BufferUpdate(_writeFullList, 0, (uint)data.Length, data);
    }

    private void UpdateUniforms()
    {
        if (_rd == null) return;
        UpdateIndicesBlock();
        UpdateReadList();
        UpdateWriteFullList();
        UpdateWriteCulledList();
        UpdateCameraData();
        UpdateDebug();
    }


    #endregion

    #endregion

    #region PROCESSING

    async public void StartProcessLoop()
    {
        GD.Print("In start process loop");
        GD.Print(_planetData);
        if (_rd == null) { GD.PrintErr("Warning RD was null"); return; }
    
        while (Processing)
        {
            UpdateCopy();
            UpdateComputeCull();
            Render();
            UpdateUniforms();
            await Task.Delay(_updateFrequency);
        }
    }

    private void UpdateCopy()
    {
        if (_rd == null) return;
        long computeList = _rd.ComputeListBegin();
        _rd.ComputeListBindComputePipeline(computeList, _pipeline_C);
        _rd.ComputeListBindUniformSet(computeList, _uniformSet_C, 0);
        _rd.ComputeListDispatch(computeList, 1, 1, 1);
        _rd.ComputeListEnd();
        _rd.Submit();
        _rd.Sync();
    }

    private void UpdateComputeCull()
    {
        if (_rd == null) return;
        long computeList = _rd.ComputeListBegin();
        _rd.ComputeListBindComputePipeline(computeList, _pipeline_CC);
        _rd.ComputeListBindUniformSet(computeList, _uniformSet_CC, 0);
        _rd.ComputeListDispatchIndirect(computeList, _dispatchIndirectBuffer, 0);
        _rd.ComputeListEnd();
        _rd.Submit();
    }

    private void Render()
    {
        if (_rd == null) return;
        _rd.Sync();

        Key[] keys = Utilities.FromBytes<Key>(_rd.BufferGetData(_writeFullList)).ToArray();
        uint[] indices = Utilities.FromBytes<uint>(_rd.BufferGetData(_indicesBlockBuffer)).ToArray();
        uint[] primCounts = Utilities.FromBytes<uint>(_rd.BufferGetData(_atomicCounterBuffer)).ToArray();
        Vector4[] debugData = Utilities.FromBytes<Vector4>(_rd.BufferGetData(_debug)).ToArray();

        InstanceAllTriangles(keys, (int)primCounts[indices[1]], debugData);
    }

    public void InstanceAllTriangles(Key[] keys, int amount, Vector4[] debugData)
    {
        _multimesh.Multimesh.InstanceCount = 0;
        _multimesh.Multimesh.InstanceCount = amount;

        if (amount > keys.Length)
        {
            CreateUniforms();
            return;
        }

        for (int i = 0; i < amount; i++)
        {
            Transform3D transform = new Transform3D(Basis.Identity, Vector3.Zero);
            _multimesh.Multimesh.SetInstanceTransform(i, transform);
            _multimesh.Multimesh.SetInstanceCustomData(i, keys[i].ToColor());
            _multimesh.Multimesh.SetInstanceColor(i, new Color(debugData[i].X, debugData[i].Y, debugData[i].Z, 0));
        }
    }

    private void CleanupGPU()
    {
        if (_rd == null) return;

        _rd.FreeRid(_uniformSet_C);
        _rd.FreeRid(_pipeline_C);
        _rd.FreeRid(_shader_C);
        _rd.FreeRid(_uniformSet_CC);
        _rd.FreeRid(_pipeline_CC);
        _rd.FreeRid(_shader_CC);

        _rd.FreeRid(_atomicCounterBuffer);
        _rd.FreeRid(_indicesBlockBuffer);
        _rd.FreeRid(_readList);
        _rd.FreeRid(_writeFullList);
        _rd.FreeRid(_writeCulledList);
        _rd.FreeRid(_positions);
        _rd.FreeRid(_cameraData);
        _rd.FreeRid(_dispatchIndirectBuffer);
        _rd.FreeRid(_debug);

        _rd.Free();
        _rd = null;
    }

    #endregion
}
