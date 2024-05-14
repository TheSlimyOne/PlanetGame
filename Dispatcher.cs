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
    public uint MaximumNodes = 8192;

    private RenderingDevice _rd;
    private Random Random = new Random(123);

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
    [Export] private ShaderMaterial _material;
    [Export] private Camera3D _camera;
    [Export] private float _subFactor = 700;


    private bool _processing;

    [Export] private float _radius = 500;

    #region MAIN LOOP
    public override void _Ready()
    {
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
            hideIndex = (hideIndex + 1) % 15;
            GD.Print(hideIndex);
        }

        if (@event.IsActionPressed("DecreaseFactor"))
        {
            if (hideIndex - 1 >= 0)
                hideIndex = (hideIndex - 1) % 15;
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
        byte[] data = ToBytes<uint>(primCountFullAndCull).ToArray();

        (RDUniform uniform, Rid buffer) = CreateUniformFromData(data, binding);
        _bindings_C.Add(uniform);
        _bindings_CC.Add(uniform);

        return buffer;
    }

    private Rid CreateIndicesBlock(int binding)
    {
        uint[] indices = new uint[] { 0, 1, 8, MaximumNodes };
        byte[] data = ToBytes<uint>(indices).ToArray();

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

        byte[] data = ToBytes<Vector4I>(readList).ToArray();

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

        ((ShaderMaterial)_material).SetShaderParameter("position_list", positions);

        // GD.Print("T\\left(a,b,c,A,B,C\\right)=\\operatorname{triangle}\\left(P_{obj}\\left(a.x,a.y,A,B,C\\right),P_{obj}\\left(b.x,b.y,A,B,C\\right),P_{obj}\\left(c.x,c.y,A,B,C\\right)\\right)");
        // GD.Print("P_{obj}\\left(p_{x},p_{y},A,B,C\\right)=Ap_{x}+Bp_{y}+C\\left(1-p_{x}-p_{y}\\right)");

        byte[] data = ToBytes<Vector4>(positions).ToArray();

        (RDUniform uniform, Rid buffer) = CreateUniformFromData(data, binding);
        _bindings_CC.Add(uniform);

        return buffer;
    }

    private Rid CreateWriteFullList(int binding)
    {
        Vector4I[] writeList = new Vector4I[MaximumNodes];

        byte[] data = ToBytes<Vector4I>(writeList).ToArray();

        (RDUniform uniform, Rid buffer) = CreateUniformFromData(data, binding);
        _bindings_CC.Add(uniform);

        return buffer;
    }

    private Rid CreateWriteCulledList(int binding)
    {
        Vector4I[] writeList = new Vector4I[MaximumNodes];

        byte[] data = ToBytes<Vector4I>(writeList).ToArray();

        (RDUniform uniform, Rid buffer) = CreateUniformFromData(data, binding);
        _bindings_CC.Add(uniform);

        return buffer;
    }

    private Rid CreateDataList(int binding)
    {
        Projection[] dataList = new Projection[MaximumNodes];

        byte[] data = ToBytes<Projection>(dataList).ToArray();

        (RDUniform uniform, Rid buffer) = CreateUniformFromData(data, binding);
        _bindings_CC.Add(uniform);

        return buffer;
    }

    private Rid CreateDistanceValues(int binding)
    {
        Vector4[] dataList = new Vector4[MaximumNodes];

        byte[] data = ToBytes<Vector4>(dataList).ToArray();

        (RDUniform uniform, Rid buffer) = CreateUniformFromData(data, binding);
        _bindings_CC.Add(uniform);

        return buffer;
    }

    private Rid CreateCameraData(int binding)
    {
        Transform3D transform = _camera.GlobalTransform;
        Basis basis = transform.Basis;
        Vector3 origin = transform.Origin;

        byte[] data = ToBytes<float>(new float[]
        {
            basis.X.X, basis.X.Y, basis.X.Z, 1.0f,
            basis.Y.X, basis.Y.Y, basis.Y.Z, 1.0f,
            basis.Z.X, basis.Z.Y, basis.Z.Z, 1.0f,
            origin.X,  origin.Y,  origin.Z,  1.0f,
            Mathf.DegToRad(_camera.Fov), _camera.Far, _camera.Near, _radius, _subFactor * _radius
        }).ToArray();

        (RDUniform uniform, Rid buffer) = CreateUniformFromData(data, binding);
        _bindings_CC.Add(uniform);

        return buffer;
    }

    private Rid CreateDispatchOutBuffer(int binding)
    {
        uint[] workgroups = new uint[] { 1, 1, 1 };

        (RDUniform uniform, Rid buffer) = CreateUniformFromData(ToBytes<uint>(workgroups).ToArray(), binding, 1);
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

        byte[] data = ToBytes<float>(new float[]
        {
            basis.X.X, basis.X.Y, basis.X.Z, 1.0f,
            basis.Y.X, basis.Y.Y, basis.Y.Z, 1.0f,
            basis.Z.X, basis.Z.Y, basis.Z.Z, 1.0f,
            origin.X,  origin.Y,  origin.Z,  1.0f,
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
        byte[] data = ToBytes<Vector4I>(new Vector4I[MaximumNodes]).ToArray();
        _rd.BufferUpdate(_writeFullList, 0, (uint)data.Length, data);
    }

    private void UpdateWriteCulledList()
    {
        byte[] data = ToBytes<Vector4I>(new Vector4I[MaximumNodes]).ToArray();
        _rd.BufferUpdate(_writeCulledList, 0, (uint)data.Length, data);
    }

    private void UpdateDistanceValues()
    {
        byte[] data = ToBytes<Vector4I>(new Vector4I[MaximumNodes]).ToArray();
        _rd.BufferUpdate(_distanceValues, 0, (uint)data.Length, data);
    }

    private void UpdateIndicesBlock()
    {
        uint[] indices = FromBytes<uint>(_rd.BufferGetData(_indicesBlockBuffer)).ToArray();
        indices[0] = (indices[0] + 1) % 16; // Read Index
        indices[1] = (indices[1] + 1) % 16; // Write Index
        indices[2] = (indices[2] + 1) % 16; // Delete Index
        indices[3] = MaximumNodes;
        byte[] data = ToBytes<uint>(indices).ToArray();

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
        ArrayMesh mesh = new ArrayMesh();
        Godot.Collections.Array arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = new Vector3[] { new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(1, 0, 1) };
        arrays[(int)Mesh.ArrayType.Index] = new int[] { 2, 0, 1 };
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        _material.SetShaderParameter("radius", _radius);
        mesh.SurfaceSetMaterial(0, _material);



        _multimesh.Multimesh = new MultiMesh()
        {
            Mesh = mesh,
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = 0,
            UseCustomData = true,
        };
        _multimesh.ExtraCullMargin = 2 * _radius;

        // Transform3D transform = new Transform3D(Basis.Identity, Vector3.Zero);

        // _multimesh.Multimesh.InstanceCount = 4;
        // _multimesh.Multimesh.SetInstanceTransform(0, transform);
        // _multimesh.Multimesh.SetInstanceCustomData(0, new Color(0, 33554431, 0, 2));


        // _multimesh.Multimesh.SetInstanceTransform(1, transform);
        // _multimesh.Multimesh.SetInstanceCustomData(1, new Color(0, 33554428, 0, 2));

        // _multimesh.Multimesh.SetInstanceTransform(2, transform);
        // _multimesh.Multimesh.SetInstanceCustomData(2, new Color(0, 33554429, 0, 2));

        // _multimesh.Multimesh.SetInstanceTransform(3, transform);
        // _multimesh.Multimesh.SetInstanceCustomData(3, new Color(0, 33554430, 0, 2));

        // GD.Print(leafSpaceToWorldSpace(new Vector4I(0, 33554431, 0, 2)));
        // GD.Print(leafSpaceToWorldSpace(new Vector4I(0, 33554428, 0, 2)));
        // GD.Print(leafSpaceToWorldSpace(new Vector4I(0, 33554429, 0, 2)));
        // GD.Print(leafSpaceToWorldSpace(new Vector4I(0, 33554430, 0, 2)));

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


        Vector4I[] keys = FromBytes<Vector4I>(_rd.BufferGetData(_writeFullList)).ToArray();
        uint[] indices = FromBytes<uint>(_rd.BufferGetData(_indicesBlockBuffer)).ToArray();
        uint[] primCounts = FromBytes<uint>(_rd.BufferGetData(_atomicCounterBuffer)).ToArray();



        InstanceAllTriangles(keys, (int)primCounts[indices[1]]);
    }
    int hideIndex = 0;
    int j = 0;
    public void InstanceAllTriangles(Vector4I[] keys, int amount)
    {
        _multimesh.Multimesh.InstanceCount = amount;
        Transform3D transform = new Transform3D(Basis.Identity, Vector3.Zero);
        j = 0;
        for (int i = 0; i < amount; i++)
        {
            if (getLevelInKey(new Vector2I(keys[i].X, keys[i].Y)) == hideIndex)
            {
                j++;
                // Projection[] data = FromBytes<Projection>(_rd.BufferGetData(_data)).ToArray();
                Vector4 keyTransform = getTransformation(keys[i]);
                // GD.PrintS(j, keys[i]);
                // GD.Print($"{j}, {keyTransform.X,-4}, {keyTransform.Y,-4}, {keyTransform.Z,-15}, {keyTransform.W,-15}");
                // Vector2I keyA = new Vector2I((int)data[0].Y.X, (int)data[0].Y.Y);
                // GD.PrintS(Convert.ToString(keyA.X, 2).PadZeros(32), Convert.ToString(keyA.Y, 2).PadZeros(32));
                // GD.Print("================================================");
            }
            _multimesh.Multimesh.SetInstanceTransform(i, transform);
            _multimesh.Multimesh.SetInstanceCustomData(i, new Color(keys[i].X, keys[i].Y, keys[i].Z, keys[i].W));
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

    // Based discord user created these functions: idrmzit
    Span<byte> ToBytes<T>(Span<T> data) where T : unmanaged
    {
        return MemoryMarshal.Cast<T, byte>(data);
    }

    Span<T> FromBytes<T>(Span<byte> data) where T : unmanaged
    {
        int length = data.Length - (data.Length % Unsafe.SizeOf<T>());
        return MemoryMarshal.Cast<byte, T>(data[..length]);
    }

    public static int getLevelInKey(Vector2I key)
    {
        return findMSB64(key) / 2;
    }

    public static int findMSB64(Vector2I key)
    {
        return (key.X == 0) ? findMSB(key.Y) : (findMSB(key.X) + 32);
    }

    public static int findMSB(int n)
    {
        n |= n >> 1;
        n |= n >> 2;
        n |= n >> 4;
        n |= n >> 8;
        n |= n >> 16;
        n = (n + 1) >> 1;
        return (int)(Mathf.Log(n) / Mathf.Log(2));
    }

    Vector2 getTranslation(uint b1)
    {
        Vector2 translation = new Vector2(b1 & 0x1, b1 ^ 0x1);
        return translation * 0.5f;
    }

    uint getBranching(Vector2I key, int level, int msb)
    {
        // Create the mask that will be shifted based on level
        uint mask = (uint)((0x3 << (msb % 32)) >> (level * 2));

        // Domain index is used to see if we are in X or Y
        // the msb in the key or the lsb in the key
        int domain_index = (msb % 32) / 2;

        if (msb >= 32)
        {
            if (domain_index < level)
            {
                mask = (uint)((0x3 << 30) >> ((level - 1 - domain_index) * 2));
                return (uint)((key.Y & mask) >> (msb - (2 * level)));
            }
            return (uint)((key.X & mask) >> ((msb % 32) - (2 * level)));
        }

        return (uint)((key.Y & mask) >> (msb - (2 * level)));
    }

    Vector2 rotate(uint rotationIndex, Vector2 translation)
    {
        Vector2I trig = quickPI_2(rotationIndex);
        Vector2 r = new Vector2(
            trig.X * translation.X - trig.Y * translation.Y,
             trig.Y * translation.X + trig.X * translation.Y);

        return r;
    }

    uint getRotation(uint b1b2, uint b1, uint b2)
    {
        uint a = (b1b2 ^ 0x2);
        uint b = (a | 0x1);
        uint c = (b1 ^ b2);
        return (b * c);
    }

    Vector2I quickPI_2(uint a)
    {
        int b = (int)(a & 3);
        int b1 = b >> 1;
        int b2 = b & 1;
        int bn2 = b2 ^ 1;
        int c = bn2 - (2 * (b1 & bn2));
        int s = b2 - (2 * (b1 & b2));
        return new Vector2I(c, s);
    }

    Vector4 getTransformation(Vector4I key)
    {
        int msb = findMSB64(new Vector2I(key.X, key.Y));
        Vector2 translation = new Vector2(0, 0);
        Vector2 temp;
        uint theta = 0;
        float scale = 1.0f;

        for (int i = 0; i < msb / 2; i++)
        {
            uint b1b2 = getBranching(new Vector2I(key.X, key.Y), i, msb - 2);
            uint b1 = b1b2 >> 1;
            uint b2 = b1b2 & 1;
            temp = scale * getTranslation(b1);

            translation += rotate(theta, temp);
            theta += getRotation(b1b2, b1, b2);
            scale *= 0.5f;
        }

        return new Vector4(theta, scale, translation.X, translation.Y);
    }

     private Triangle leafSpaceToWorldSpace(Vector4I key)
    { 
        int msb = findMSB64(new Vector2I(key.X, key.Y));
        Vector2 translation = new Vector2(0, 0);
        Vector2 temp;
        uint theta = 0;
        float scale = 1.0f;

        for (int i = 0; i < msb / 2; i++)
        {
            uint b1b2 = getBranching(new Vector2I(key.X, key.Y), i, msb - 2);
            uint b1 = b1b2 >> 1;
            uint b2 = b1b2 & 0x01;
            temp = scale * getTranslation(b1);

            translation += rotate(theta, new Vector2(temp.X, temp.Y));
            theta += getRotation(b1b2, b1, b2);
            scale *= 0.5f;
        }

        Vector2I trig = quickPI_2(theta);
        Basis transform_matrix = new Basis(
            trig.X * scale, -trig.Y * scale, translation.X,
            trig.Y * scale, trig.X * scale, translation.Y,
            0.0f, 0.0f, 1.0f
        );
        return createTriangle(transform_matrix, (uint)key.Z, (uint)key.W);
    }

    Triangle createTriangle(Basis transform_matrix, uint meshPolygonID, uint rootID)
    {
        Vector4[] position_list = FromBytes<Vector4>(_rd.BufferGetData(_positions)).ToArray();

        Vector2 point_a = new Vector2((transform_matrix * new Vector3(0, 0, 1)).X, (transform_matrix * new Vector3(0, 0, 1)).Y);
        Vector2 point_b = new Vector2((transform_matrix * new Vector3(0, 1, 1)).X, (transform_matrix * new Vector3(0, 1, 1)).Y);
        Vector2 point_c = new Vector2((transform_matrix * new Vector3(1, 0, 1)).X, (transform_matrix * new Vector3(1, 0, 1)).Y);
        Vector2 point_d = new Vector2((transform_matrix * new Vector3(0.5f, 0.5f, 1)).X, (transform_matrix * new Vector3(0.5f, 0.5f, 1)).Y);

        uint vertexBaseIndex = meshPolygonID * 5;
        uint vertexKeyA = rootID;
        uint vertexKeyB = ((rootID >> 1) ^ 1) + ((rootID & 1) << 1);

        Vector3 base_Triangle_a = Vector3Utils.toVector3(position_list[vertexBaseIndex + vertexKeyA + 1]);
        Vector3 base_Triangle_b = Vector3Utils.toVector3(position_list[vertexBaseIndex + vertexKeyB + 1]);
        Vector3 base_Triangle_c = Vector3Utils.toVector3(position_list[vertexBaseIndex]);

        Vector3 point_A = localPointToWorldPoint(point_a, base_Triangle_a, base_Triangle_b, base_Triangle_c);
        Vector3 point_B = localPointToWorldPoint(point_b, base_Triangle_a, base_Triangle_b, base_Triangle_c);
        Vector3 point_C = localPointToWorldPoint(point_c, base_Triangle_a, base_Triangle_b, base_Triangle_c);
        Vector3 point_D = localPointToWorldPoint(point_d, base_Triangle_a, base_Triangle_b, base_Triangle_c);
        
		Triangle t = new Triangle(new Vector3[] { point_A, point_B, point_C });
		t.spawnPoint = point_D;
        return t;
    }

    Vector3 localPointToWorldPoint(Vector2 point, Vector3 vertexA, Vector3 vertexB, Vector3 vertexC)
    {
        return vertexA * point.X + vertexB * point.Y + vertexC * (1 - point.X - point.Y);
    }


}
