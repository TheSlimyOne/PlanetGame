using Godot;
using Godot.Collections;

public partial class IndirectDrawing : Node3D
{
	Vector3[] Vertices = new Vector3[]{
		new Vector3( 0.0f,  0.1f, 0.0f),
		new Vector3(-0.1f, -0.1f, 0.0f),
		new Vector3( 0.1f, -0.1f, 0.0f)
	};

	[Export]
	public int InstanceCount
	{
		get => _instanceCount;
		set
		{
			if (_instanceCount != value)
			{
				_instanceCount = Mathf.Clamp(value, 0, 1000);
				byte[] args = Utilities.ToBytesSingle(new IndirectArgs() { vertexCount = Vertices.Length, instanceCount = InstanceCount}).ToArray();
				rd.BufferUpdate(indirectArgs, 0, (uint)args.Length, args);
			}
		}
	}
	private int _instanceCount;


	[Export(PropertyHint.File, "*.glsl")] private string vertex;
	[Export(PropertyHint.File, "*.glsl")] private string fragment;

	Rid indirectArgs;
	Rid shader;
	Rid pipeline;
	long vertexFormat;
	Rid vertexBuffer;
	Rid vertexArray;

	RenderingDevice rd;

	RDShaderFile fragmentShaderFile;
	RDShaderFile vertexShaderFile;
	public struct IndirectArgs
	{
		public int vertexCount;
		public int instanceCount;
		public int firstVertex;
		public int firstInstance;
	}
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		rd = RenderingServer.GetRenderingDevice();
		
	 
		byte[] args = Utilities.ToBytesSingle(new IndirectArgs() { vertexCount = Vertices.Length, instanceCount = InstanceCount}).ToArray();
		indirectArgs = rd.StorageBufferCreate((uint)args.Length, args, RenderingDevice.StorageBufferUsage.DispatchIndirect);

		Array<RDVertexAttribute> attribute = new();
		RDVertexAttribute vertexAttribute = new() {
			Frequency = RenderingDevice.VertexFrequency.Vertex,
			Format = RenderingDevice.DataFormat.R32G32B32Sfloat,
			Stride = Utilities.SizeOf<Vector3>(),
			Offset = 0
		};
		attribute.Add(vertexAttribute);
		vertexFormat = rd.VertexFormatCreate(attribute);

		byte[] bytes = Utilities.ToBytes<Vector3>(Vertices).ToArray();
		vertexBuffer = rd.VertexBufferCreate((uint)bytes.Length, bytes);

		vertexArray = rd.VertexArrayCreate((uint)Vertices.Length, vertexFormat, new Array<Rid>() { vertexBuffer});

		vertexShaderFile = GD.Load<RDShaderFile>(vertex);
		fragmentShaderFile = GD.Load<RDShaderFile>(fragment);
        RDShaderSpirV bundle = new()
        {
            BytecodeVertex = vertexShaderFile.GetSpirV().BytecodeVertex,
			BytecodeFragment = fragmentShaderFile.GetSpirV().BytecodeFragment
        };
		shader = rd.ShaderCreateFromSpirV(bundle);

		long framebufferFormat = rd.ScreenGetFramebufferFormat();
		RenderingDevice.RenderPrimitive primative = RenderingDevice.RenderPrimitive.Triangles;
		RDPipelineRasterizationState rasterization = new();
		RDPipelineMultisampleState multisample = new();
		RDPipelineDepthStencilState depth = new();
		RDPipelineColorBlendState blend = new();
		blend.Attachments = new Array<RDPipelineColorBlendStateAttachment>() { new() };
		pipeline = rd.RenderPipelineCreate(shader, framebufferFormat, vertexFormat, primative, rasterization, multisample, depth, blend);

    }

    public override void _Process(double delta)
    {
		var dlist = rd.DrawListBeginForScreen(clearColor: new Color(0.2f, 0.2f, 0.2f));
		rd.DrawListBindRenderPipeline(dlist, pipeline);
		rd.DrawListBindVertexArray(dlist, vertexArray);
		rd.DrawListDrawIndirect(dlist, false, indirectArgs);
		rd.DrawListEnd();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
		{
			rd.FreeRid(indirectArgs);
			rd.FreeRid(shader);
			rd.FreeRid(vertexBuffer);
		}
    }
}
