using Godot;
using Godot.Collections;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
[Tool]

public partial class Dispatcher : Node3D
{
    [ExportGroup("Settings")]
    [Export(PropertyHint.Range, "1, 1000")] private int _updateFrequency = 60;
    [Export] private bool _autoStart;

    [ExportGroup("Requirements")]
    [Export(PropertyHint.File)] private string _computeCullShader;
    [Export(PropertyHint.File)] private string _copyShader;

    // At the very least must be 24
    private uint MaximumNodes = 32768;

    private RenderingDevice _rd;

    private Rid _uniformSet_CC;
    private Rid _shader_CC;
    private Rid _pipeline_CC;

    private Rid _uniformSet_C;
    private Rid _shader_C;
    private Rid _pipeline_C;

    private RDUniform _inputUniform;
    private RDUniform _outputUniform;
    private Array<RDUniform> _bindings_CC = new();
    private Array<RDUniform> _bindings_C = new();

    private Rid _atomicCounterBuffer;
    private Rid _indicesBlockBuffer;
    private Rid _readList;
    private Rid _writeFullList;
    private Rid _writeCulledList;
    private Rid _positions;
    private Rid _data;
    private Rid _cameraData;
    private Rid _dispatchIndirectBuffer;
    private Rid _distanceValues;

    [Export] private MultiMeshInstance3D _multimesh;
    [Export] private MultiMeshInstance3D _multimesh2;
    [Export] private ShaderMaterial _material;
    [Export] private ShaderMaterial _OTHER;
    [Export] private Camera3D _camera;
    [Export] private float _subFactor = 4;

    [Export(PropertyHint.Range, "2,1000,")]
    public int Resolution
    {
        get => _resolution;
        set { _resolution = value; InitialMulitMesh(); }
    }

    bool _isReady;
    private bool _processing;
    private int _resolution;

    [Export] private float _radius = 500;

    #region MAIN LOOP
    public override void _Ready()
    {
        _isReady = true;
        Initialize();
    }

    public void Initialize()
    {
        SetupComputeShader();
        InitialMulitMesh();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("step"))
        {
            if (_processing)
            {
                _processing = false;
                GD.Print("she stop :C");
            }
            else
            {
                StartProcessLoop();
                GD.Print("she go :)");
            }
        }

        if (@event.IsActionPressed("IncreaseFactor"))
        {
            hideIndex += (hideIndex + 1) < 16 ? 1 : 0;
            GD.Print(hideIndex);
        }

        if (@event.IsActionPressed("DecreaseFactor"))
        {

            hideIndex -= (hideIndex - 1) >= 0 ? 1 : 0;
            GD.Print(hideIndex);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest || what == NotificationPredelete)
        {
            CleanupGPU();
        }
    }
    #endregion

    #region SHADER SETUP

    private void SetupComputeShader()
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
        _atomicCounterBuffer = CreateAtomicCounter(0);
        _indicesBlockBuffer = CreateIndicesBlock(1);
        _readList = CreateReadList(2);
        _writeFullList = CreateWriteFullList(3);
        _writeCulledList = CreateWriteCulledList(4);
        _positions = CreatePositionList(5);
        _cameraData = CreateCameraData(6);
        _data = CreateDataList(7);
        _dispatchIndirectBuffer = CreateDispatchOutBuffer(2);
        _distanceValues = CreateDistanceValues(8);

        _uniformSet_C = _rd.UniformSetCreate(_bindings_C, _shader_C, 0);
        _uniformSet_CC = _rd.UniformSetCreate(_bindings_CC, _shader_CC, 0);
    }

    private Rid CreateAtomicCounter(int binding)
    {
        uint[] primCountFullAndCull = new uint[2 * 16];
        primCountFullAndCull[0] = 12 * 4;
        byte[] data = Utilities.ToBytes<uint>(primCountFullAndCull).ToArray();

        (RDUniform uniform, Rid buffer) = CreateUniformFromData(data, binding);
        _bindings_C.Add(uniform);
        _bindings_CC.Add(uniform);

        return buffer;
    }

    private Rid CreateIndicesBlock(int binding)
    {
        uint[] indices = new uint[] { 0, 1, 8, MaximumNodes };
        byte[] data = Utilities.ToBytes<uint>(indices).ToArray();

        (RDUniform uniform, Rid buffer) = CreateUniformFromData(data, binding);
        _bindings_C.Add(uniform);
        _bindings_CC.Add(uniform);

        return buffer;
    }

    private Rid CreateReadList(int binding)
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

        (RDUniform uniform, Rid buffer) = CreateUniformFromData(data, binding);
        _bindings_CC.Add(uniform);

        return buffer;
    }

    private Rid CreatePositionList(int binding)
    {
        Vector3 normal = Vector3.Up;
        Vector3 axisA = new Vector3(normal.Y, normal.Z, normal.X);
        Vector3 axisB = normal.Cross(axisA);

        Vector4[] positions = new Vector4[] {

            Vector3Utils.toVector4(normal - axisB, 1),
            Vector3Utils.toVector4(normal + axisA - axisB, 1),
            Vector3Utils.toVector4(normal, 1),
            Vector3Utils.toVector4(-axisB, 1),
            Vector3Utils.toVector4(normal - axisA - axisB, 1),

            Vector3Utils.toVector4(normal + axisA, 1),
            Vector3Utils.toVector4(normal + axisA + axisB, 1),
            Vector3Utils.toVector4(normal, 1),
            Vector3Utils.toVector4(axisA, 1),
            Vector3Utils.toVector4(normal + axisA - axisB, 1),

            Vector3Utils.toVector4(normal - axisA, 1),
            Vector3Utils.toVector4(normal - axisA - axisB, 1),
            Vector3Utils.toVector4(normal, 1),
            Vector3Utils.toVector4(-axisA, 1),
            Vector3Utils.toVector4(normal - axisA + axisB, 1),

            Vector3Utils.toVector4(normal + axisB, 1),
            Vector3Utils.toVector4(normal - axisA + axisB, 1),
            Vector3Utils.toVector4(normal, 1),
            Vector3Utils.toVector4(axisB, 1),
            Vector3Utils.toVector4(normal + axisA + axisB, 1),

            Vector3Utils.toVector4(axisA + axisB, 1),
            Vector3Utils.toVector4(-normal + axisA + axisB, 1),
            Vector3Utils.toVector4(axisB, 1),
            Vector3Utils.toVector4(axisA, 1),
            Vector3Utils.toVector4(normal + axisA + axisB, 1),

            Vector3Utils.toVector4(-axisA + axisB, 1),
            Vector3Utils.toVector4(normal - axisA + axisB, 1),
            Vector3Utils.toVector4(axisB, 1),
            Vector3Utils.toVector4(-axisA, 1),
            Vector3Utils.toVector4(-normal - axisA + axisB, 1),

            Vector3Utils.toVector4(axisA - axisB, 1),
            Vector3Utils.toVector4(-normal + axisA - axisB, 1),
            Vector3Utils.toVector4(axisA, 1),
            Vector3Utils.toVector4(-axisB, 1),
            Vector3Utils.toVector4(normal + axisA - axisB, 1),

            Vector3Utils.toVector4(-axisA - axisB, 1),
            Vector3Utils.toVector4(normal - axisA - axisB, 1),
            Vector3Utils.toVector4(-axisA, 1),
            Vector3Utils.toVector4(-axisB, 1),
            Vector3Utils.toVector4(-normal - axisA - axisB, 1),

            Vector3Utils.toVector4(-normal + axisB, 1),
            Vector3Utils.toVector4(-normal + axisA + axisB, 1),
            Vector3Utils.toVector4(-normal, 1),
            Vector3Utils.toVector4(axisB, 1),
            Vector3Utils.toVector4(-normal - axisA + axisB, 1),

            Vector3Utils.toVector4(-normal + axisA, 1),
            Vector3Utils.toVector4(-normal + axisA + axisB, 1),
            Vector3Utils.toVector4(axisA, 1),
            Vector3Utils.toVector4(-normal, 1),
            Vector3Utils.toVector4(-normal + axisA - axisB, 1),

            Vector3Utils.toVector4(-normal - axisA, 1),
            Vector3Utils.toVector4(-normal - axisA - axisB, 1),
            Vector3Utils.toVector4(-axisA, 1),
            Vector3Utils.toVector4(-normal, 1),
            Vector3Utils.toVector4(-normal - axisA + axisB, 1),

            Vector3Utils.toVector4(-normal - axisB, 1),
            Vector3Utils.toVector4(-normal - axisA - axisB, 1),
            Vector3Utils.toVector4(-normal, 1),
            Vector3Utils.toVector4(-axisB, 1),
            Vector3Utils.toVector4(-normal + axisA - axisB, 1),


        };

        _material.SetShaderParameter("position_list", positions);

        byte[] data = Utilities.ToBytes<Vector4>(positions).ToArray();

        (RDUniform uniform, Rid buffer) = CreateUniformFromData(data, binding);
        _bindings_CC.Add(uniform);

        return buffer;
    }

    private Rid CreateWriteFullList(int binding)
    {
        Key[] writeList = new Key[MaximumNodes];

        byte[] data = Utilities.ToBytes<Key>(writeList).ToArray();

        (RDUniform uniform, Rid buffer) = CreateUniformFromData(data, binding);
        _bindings_CC.Add(uniform);

        return buffer;
    }

    private Rid CreateWriteCulledList(int binding)
    {
        Vector4I[] writeList = new Vector4I[MaximumNodes];

        byte[] data = Utilities.ToBytes<Vector4I>(writeList).ToArray();

        (RDUniform uniform, Rid buffer) = CreateUniformFromData(data, binding);
        _bindings_CC.Add(uniform);

        return buffer;
    }

    private Rid CreateDataList(int binding)
    {
        Projection[] dataList = new Projection[MaximumNodes];

        byte[] data = Utilities.ToBytes<Projection>(dataList).ToArray();

        (RDUniform uniform, Rid buffer) = CreateUniformFromData(data, binding);
        _bindings_CC.Add(uniform);

        return buffer;
    }

    private Rid CreateDistanceValues(int binding)
    {
        float[] dataList = new float[MaximumNodes];

        byte[] data = Utilities.ToBytes<float>(dataList).ToArray();

        (RDUniform uniform, Rid buffer) = CreateUniformFromData(data, binding);
        _bindings_CC.Add(uniform);

        return buffer;
    }

    private Rid CreateCameraData(int binding)
    {
        Transform3D transform = _camera.GlobalTransform;
        Basis basis = transform.Basis;
        Vector3 origin = transform.Origin;
        Projection projectionMatrix =  _camera.GetCameraProjection();

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

            Mathf.DegToRad(_camera.Fov), _camera.Far, _camera.Near, _radius, _subFactor * _radius
        }).ToArray();

        (RDUniform uniform, Rid buffer) = CreateUniformFromData(data, binding);
        _bindings_CC.Add(uniform);

        return buffer;
    }

    private Rid CreateDispatchOutBuffer(int binding)
    {
        uint[] workgroups = new uint[] { 1, 1, 1 };

        (RDUniform uniform, Rid buffer) = CreateUniformFromData(Utilities.ToBytes<uint>(workgroups).ToArray(), binding, 1);
        _bindings_C.Add(uniform);

        return buffer;
    }
    #endregion

    #region UPDATE UNIFORMS
    private void UpdateCameraData()
    {
        Transform3D transform = _camera.GlobalTransform;
        Basis basis = transform.Basis;
        Vector3 origin = transform.Origin;
        Projection projectionMatrix =  _camera.GetCameraProjection();

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

            Mathf.DegToRad(_camera.Fov), _camera.Far, _camera.Near, _radius, _subFactor * _radius
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

    private void UpdateDistanceValues()
    {
        byte[] data = Utilities.ToBytes<float>(new float[MaximumNodes]).ToArray();
        _rd.BufferUpdate(_distanceValues, 0, (uint)data.Length, data);
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

    private void UpdateUniforms()
    {
        UpdateIndicesBlock();
        UpdateReadList();
        UpdateWriteFullList();
        UpdateWriteCulledList();
        UpdateCameraData();
        UpdateDistanceValues();
    }

    #endregion

    #endregion


    private void InitialMulitMesh()
    {
        if (!_isReady) return;

        Vector3[] vertices = new Vector3[_resolution * (_resolution + 1) / 2];
        Vector2[] uvs = new Vector2[_resolution * (_resolution + 1) / 2];
        int[] triangles = new int[(_resolution - 1) * (_resolution - 1) * 6 / 2];
        Vector3 normal = Vector3.Back;
        Vector3 axisA = new Vector3(normal.Y, normal.Z, normal.X);
        Vector3 axisB = normal.Cross(axisA).Abs();
        int triIndex = 0;
        int vertexIndex = 0;

        for (int y = 0; y < _resolution; y++)
        {
            for (int x = 0; x < _resolution - y; x++)
            {
                int currentIndex = vertexIndex++;
                Vector2 percentage = new Vector2(x, y) / (_resolution - 1);
                vertices[currentIndex] = normal + (percentage.X * axisA + percentage.Y * axisB);
                uvs[currentIndex] = percentage;

                if (x != _resolution - y - 1)
                {
                    if (x == _resolution - y - 2)
                    {
                        triangles[triIndex++] = currentIndex;
                        triangles[triIndex++] = currentIndex + 1;
                        triangles[triIndex++] = currentIndex + _resolution - y;
                    }
                    else if (x % 2 == 0)
                    {
                        if (y % 2 == 0)
                        {
                            triangles[triIndex++] = currentIndex;
                            triangles[triIndex++] = currentIndex + _resolution - y + 1;
                            triangles[triIndex++] = currentIndex + _resolution - y;
                            triangles[triIndex++] = currentIndex;
                            triangles[triIndex++] = currentIndex + 1;
                            triangles[triIndex++] = currentIndex + _resolution - y + 1;
                        }
                        else if (y % 2 == 1)
                        {
                            triangles[triIndex++] = currentIndex;
                            triangles[triIndex++] = currentIndex + 1;
                            triangles[triIndex++] = currentIndex + _resolution - y;
                            triangles[triIndex++] = currentIndex + 1;
                            triangles[triIndex++] = currentIndex + _resolution - y + 1;
                            triangles[triIndex++] = currentIndex + _resolution - y;
                        }
                    }
                    else if (x % 2 == 1)
                    {
                        if (y % 2 == 0)
                        {
                            triangles[triIndex++] = currentIndex;
                            triangles[triIndex++] = currentIndex + 1;
                            triangles[triIndex++] = currentIndex + _resolution - y;
                            triangles[triIndex++] = currentIndex + 1;
                            triangles[triIndex++] = currentIndex + _resolution - y + 1;
                            triangles[triIndex++] = currentIndex + _resolution - y;
                        }
                        else if (y % 2 == 1)
                        {
                            triangles[triIndex++] = currentIndex;
                            triangles[triIndex++] = currentIndex + _resolution - y + 1;
                            triangles[triIndex++] = currentIndex + _resolution - y;
                            triangles[triIndex++] = currentIndex;
                            triangles[triIndex++] = currentIndex + 1;
                            triangles[triIndex++] = currentIndex + _resolution - y + 1;
                        }
                    }
                }
            }
        }

        ArrayMesh mesh = new ArrayMesh();
        Godot.Collections.Array arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Index] = triangles;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        _material.SetShaderParameter("radius", _radius);
        mesh.SurfaceSetMaterial(0, _material);

        _multimesh.Multimesh = new MultiMesh()
        {
            Mesh = mesh,
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = 0,
            UseCustomData = true,
            UseColors = true
        };
        _multimesh.ExtraCullMargin = 2 * _radius;
    }

    #region PROCESSING

    async private void StartProcessLoop()
    {

        _processing = true;

        while (_processing)
        {
            UpdateCopy();
            UpdateComputeCull();
            Render();
            UpdateUniforms();
            await Task.Delay(_updateFrequency);
            // break;

        }
    }

    private void UpdateCopy()
    {
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
        long computeList = _rd.ComputeListBegin();
        _rd.ComputeListBindComputePipeline(computeList, _pipeline_CC);
        _rd.ComputeListBindUniformSet(computeList, _uniformSet_CC, 0);
        _rd.ComputeListDispatchIndirect(computeList, _dispatchIndirectBuffer, 0);
        _rd.ComputeListEnd();
        _rd.Submit();
    }
    int k;
    private void Render()
    {
        _rd.Sync();


        Key[] keys = Utilities.FromBytes<Key>(_rd.BufferGetData(_writeFullList)).ToArray();
        uint[] indices = Utilities.FromBytes<uint>(_rd.BufferGetData(_indicesBlockBuffer)).ToArray();
        uint[] primCounts = Utilities.FromBytes<uint>(_rd.BufferGetData(_atomicCounterBuffer)).ToArray();
        float[] distanceValues = Utilities.FromBytes<float>(_rd.BufferGetData(_distanceValues)).ToArray();



        InstanceAllTriangles(keys, distanceValues, (int)primCounts[indices[1]]);
    }
    int hideIndex;
    public void InstanceAllTriangles(Key[] keys, float[] distanceValues, int amount)
    {
        _multimesh.Multimesh.InstanceCount = amount;

        for (int i = 0; i < amount; i++)
        {
            Transform3D transform = new Transform3D(Basis.Identity, Vector3.Zero);
            _multimesh.Multimesh.SetInstanceTransform(i, transform);
            _multimesh.Multimesh.SetInstanceCustomData(i, keys[i].ToColor());
            _multimesh.Multimesh.SetInstanceColor(i, new Color(distanceValues[i], 0, 0, 0));
        }
    }


    private void CleanupGPU()
    {
        if (_rd is null) return;
        _rd.FreeRid(_uniformSet_C);
        _rd.FreeRid(_pipeline_C);
        _rd.FreeRid(_shader_C);
        _rd.FreeRid(_uniformSet_CC);
        _rd.FreeRid(_pipeline_CC);
        _rd.FreeRid(_shader_CC);
        _rd.Free();
        _rd = null;
    }

    #endregion





}
