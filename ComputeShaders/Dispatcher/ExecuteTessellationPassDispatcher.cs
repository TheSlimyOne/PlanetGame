using System;
using Uniform;
using Godot;
using Godot.Collections;
using PlanetGame.Util;
using System.Collections.Generic;
using System.Linq;

namespace PlanetGame.ComputeShaders.Dispatcher
{
	public class ExecuteTessellationPassDispatcher : ComputeShaderDispatcher<ExecuteTessellationPassDispatcher.BufferNames>
	{
		public PlanetController PlanetController { get; set; }
		public PrepareTessellationPassDispatcher PrepareTessellationPass { get; set; }
		public CustomCamera MainCamera { get; set; }
		public CustomCamera HelperCamera { get; set; }
		public MultiMeshRD PlanetMultiMesh { get; set; }
		public Texture2Drd MeshImageData { get; set; }

		public enum BufferNames
		{
			ATOMIC_COUNTER,
			KEY_INDICES,
			READ_LIST,
			WRITE_FULL_LIST,
			WRITE_CULL_LIST,
			EXTERNAL_DATA,
			MULTIMESH_BUFFER,
			GLOBAL_KEYS_DATA,
			BASE_MESH_DATA,
			// BASE_INDICES,
			// BASE_UVS,
		}

		public ExecuteTessellationPassDispatcher() : base(ShaderPaths.EXECUTE_TESSELLATION_PASS)
		{
			SetupComputeShader();
		}

		public override void CreateUniforms()
		{
			Image heightmap = SaveManager.GetBaseImages(SaveManager.CurrentSave)[SaveManager.SaveDataIdentifier.BASE_HEIGHT_MAP].GetImage();
			ArrayMesh mesh = GeneratePlanetMesh(heightmap, 1, 0.25f);
			Image meshData = MeshToImage(mesh);

			MeshImageData = new Texture2Drd()
			{
				TextureRdRid = RenderingServer.GetRenderingDevice().TextureCreate(
				new RDTextureFormat()
				{
					Width = (uint)meshData.GetWidth(),
					Height = (uint)meshData.GetHeight(),
					TextureType = RenderingDevice.TextureType.Type2D,
					Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
					UsageBits = RenderingDevice.TextureUsageBits.SamplingBit |
								RenderingDevice.TextureUsageBits.StorageBit

				}, new RDTextureView(), [meshData.GetData()]
			)
			};

			PlanetController.A.Texture =  MeshImageData;

			(int count, Key[] readList) = MeshToKeys(mesh, PlanetController.MaximumKeys);

			_computeShaderUniforms = new System.Collections.Generic.Dictionary<Enum, ComputeShaderUniform>()
			{
				// Full      list  0 - 2
				// Culling   list  3 - 5
				// Rendered  list  6 - 8
				[BufferNames.ATOMIC_COUNTER] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.ATOMIC_COUNTER,
					new Func<byte[]>(() =>
					{
						uint[] primCounts = new uint[3 * 3];
						primCounts[0] = (uint)count;
						// primCounts[0] = 6 * (uint)Mathf.Pow(4, PlanetController.StartingLod + 1);
						// GD.PrintS(PlanetController.StartingLod + 1, PlanetController.MaximumLod, PlanetController.MinimumLod);
						return Utilities.ToBytes<uint>(primCounts).ToArray();
					}).Invoke()
				),

				// 0 Read Index
				// 1 Write Index
				// 2 Delete Index
				// 3 Max nodes
				[BufferNames.KEY_INDICES] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.KEY_INDICES,
					Utilities.ToBytes<uint>([0, 1, 2, (uint)PlanetController.MaximumKeys]).ToArray()
				),

				// key = uvec4(nodeIdMSB, nodeIdLSB, meshPolygonId, flagsAndRootId)
				[BufferNames.READ_LIST] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.READ_LIST,
				// Utilities.ToBytes<Key>(readList).ToArray()
				new Func<byte[]>(() =>
				{


					// for (int i = 0; i < 6; i++)
					// {
					// 	Key[] faceData = Key.GenerateFullFace(PlanetController.StartingLod, i);
					// 	// GD.PrintS(PlanetController.StartingLod, faceData.Length);
					// 	System.Array.Copy(faceData, 0, readList, i * faceData.Length, faceData.Length);
					// }
					return Utilities.ToBytes<Key>(readList).ToArray();
				}).Invoke()
				),

				[BufferNames.WRITE_FULL_LIST] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.WRITE_FULL_LIST,
					Utilities.ToBytes<Key>(new Key[PlanetController.MaximumKeys]).ToArray()
				),

				[BufferNames.WRITE_CULL_LIST] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.WRITE_CULL_LIST,
					Utilities.ToBytes<Key>(new Key[PlanetController.MaximumKeys]).ToArray()
				),

				[BufferNames.EXTERNAL_DATA] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.EXTERNAL_DATA,
					GetExternalData()
				),

				[BufferNames.MULTIMESH_BUFFER] = new StorageBufferUniform(this, RenderingDevice, (int)BufferNames.MULTIMESH_BUFFER,
					PlanetMultiMesh.Buffer, perserve: true
				),

				[BufferNames.GLOBAL_KEYS_DATA] = new Texture2DUniform(this, RenderingDevice, (int)BufferNames.GLOBAL_KEYS_DATA,
					new RDTextureFormat()
					{
						Width = 10u,
						Height = 10u,
						TextureType = RenderingDevice.TextureType.Type2D,
						Format = RenderingDevice.DataFormat.R32Sfloat,
						UsageBits = RenderingDevice.TextureUsageBits.SamplingBit |
									RenderingDevice.TextureUsageBits.StorageBit |
									RenderingDevice.TextureUsageBits.CanUpdateBit |
									RenderingDevice.TextureUsageBits.CanCopyToBit |
									RenderingDevice.TextureUsageBits.CanCopyFromBit |
									RenderingDevice.TextureUsageBits.ColorAttachmentBit
					}, RenderingDevice.UniformType.Image
				),

				[BufferNames.BASE_MESH_DATA] = new Texture2DUniform(this, (int)BufferNames.BASE_MESH_DATA, MeshImageData.TextureRdRid, RenderingDevice.UniformType.Image, perserved: true)
				// [BufferNames.BASE_MESH_DATA] = new Texture2DUniform(this, RenderingDevice, (int)BufferNames.BASE_MESH_DATA,
				// 	new RDTextureFormat()
				// 	{
				// 		Width = (uint)meshData.GetWidth(),
				// 		Height = (uint)meshData.GetHeight(),
				// 		TextureType = RenderingDevice.TextureType.Type2D,
				// 		Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
				// 		UsageBits = RenderingDevice.TextureUsageBits.SamplingBit |
				// 					RenderingDevice.TextureUsageBits.StorageBit

				// 	}, RenderingDevice.UniformType.Image
				// )
			};
			CreateUniformSet();
		}

		public override void Invoke()
		{
			long computeList = RenderingDevice.ComputeListBegin();
			RenderingDevice.ComputeListBindComputePipeline(computeList, _pipeline);
			RenderingDevice.ComputeListBindUniformSet(computeList, _uniformSet, 0);
			RenderingDevice.ComputeListAddBarrier(computeList);
			RenderingDevice.ComputeListDispatchIndirect(computeList, PrepareTessellationPass[PrepareTessellationPassDispatcher.BufferNames.DISPATCH_BUFFER].Rid, 0);
			RenderingDevice.ComputeListEnd();
		}

		public override void UpdateUniforms()
		{
			_computeShaderUniforms[BufferNames.READ_LIST].UpdateUniform(
				_computeShaderUniforms[BufferNames.WRITE_FULL_LIST].GetByteData()[0]
			);

			_computeShaderUniforms[BufferNames.EXTERNAL_DATA].UpdateUniform(
				GetExternalData()
			);
		}

		//TODO make this push-constants
		private byte[] GetExternalData()
		{
			Array<byte> data =
			[
				.. Utilities.ToBytesSingle(HelperCamera.GetViewProjectionMatrix()),
				// .. Utilities.ToBytesSingle(HelperCamera.GetCameraProjection()),
				// .. Utilities.ToBytesSingle(Utilities.ToProjection(HelperCamera.GlobalTransform)),

				.. Utilities.ToBytesSingle(Utilities.ToProjection(PlanetController.GetPlanetTransformMatrix())),
				.. Utilities.ToBytesSingle(VectorUtils.ToVector4(MainCamera.GlobalPosition, 0)),
				.. Utilities.ToBytes(
				[
					Mathf.Tan(HelperCamera.GetCameraFov(true) / 2),
					PlanetController.SubFactor,
					PlanetController.HeightScale,
					PlanetController.Radius,

					PlanetController.Bias1,
					PlanetController.Bias2,
					PlanetController.IsCulling ? 1 : 0,

				]),
				.. Utilities.ToBytes([
					PlanetController.MaximumLod,
					PlanetController.MinimumLod
				])
			];
			return [.. data];
		}

		public Key[] GetKeys()
		{
			(int all, _, _) = GetPrimitiveCounts();
			return GetUniform<StorageBufferUniform>(BufferNames.WRITE_FULL_LIST).GetData<Key>(sizeBytes: (uint)all * Utilities.SizeOf<Key>());
		}
		public Key[] GetReadList()
		{
			return GetUniform<StorageBufferUniform>(BufferNames.READ_LIST).GetData<Key>(sizeBytes: 96u * Utilities.SizeOf<Key>());
		}

		public void ResizeReadList()
		{
			throw new NotImplementedException();
		}
		public void ResizeWriteList()
		{
			throw new NotImplementedException();
		}

		public (int fullPrimCount, int culledPrimCount, int renderedPrimCount) GetPrimitiveCounts()
		{
			uint[] indices = GetUniform<StorageBufferUniform>(BufferNames.KEY_INDICES).GetData<uint>();
			uint[] primCounts = GetUniform<StorageBufferUniform>(BufferNames.ATOMIC_COUNTER).GetData<uint>();

			return ((int)primCounts[indices[0]], (int)primCounts[indices[0] + 3], (int)primCounts[indices[0] + 6]);
		}

		public int GetCurrentLod()
		{
			return (int)GetUniform<Texture2DUniform>(BufferNames.GLOBAL_KEYS_DATA).GetPixel(0, 0).R - 1;
		}

		public void ClearGlobalKeyData()
		{
			GetUniform<Texture2DUniform>(BufferNames.GLOBAL_KEYS_DATA).ClearTexture(Colors.Black);
		}

		// TODO maybe finish this
		public void GetMultimeshBuffer()
		{
			float[] data = GetUniform<StorageBufferUniform>(BufferNames.MULTIMESH_BUFFER).GetData<float>();
			int instanceCount = PrepareTessellationPass.GetUniform<StorageBufferUniform>(PrepareTessellationPassDispatcher.BufferNames.MULTIMESH_COMMAND_BUFFER).GetData<int>()[1];

			for (int i = 0; i < instanceCount; i++)
			{
				int baseIndex = i * 20;
				Key key = new(data[baseIndex + 16], data[baseIndex + 17], data[baseIndex + 18], data[baseIndex + 19]);
			}
		}

		public static ArrayMesh GeneratePlanetMesh(Image heightmap, int resolution, float strength)
		{
			int faces = 6;

			Vector3[] vertices = new Vector3[faces * (resolution * resolution + (resolution + 1) * (resolution + 1))];
			Vector3[] normals = new Vector3[faces * (resolution * resolution + (resolution + 1) * (resolution + 1))];
			Vector2[] uvs = new Vector2[faces * (resolution * resolution + (resolution + 1) * (resolution + 1))];
			int[] triangles = new int[faces * 4 * resolution * resolution * 3];

			int triIndex = 0;
			int vertexIndex = 0;


			for (int i = 0; i < faces; i++)
			{
				for (int y = 0; y < resolution + 1; y++)
				{
					for (int x = 0; x < resolution + 1; x++)
					{
						int currentIndex = vertexIndex++;
						Vector2 percentage = new Vector2(x, y) / resolution;
						Vector3 cubePoint = VectorUtils.UVToPointOnCube(i, percentage);
						Vector3 spherePoint = VectorUtils.PointOnCubeToPointOnSphere(cubePoint);
						Vector2 uv = VectorUtils.PointOnSphereToUV(spherePoint);

						Color pixel = Sampler.SampleBilinear(heightmap, uv);
						// Vector3 vertex = spherePoint + spherePoint.Normalized() * pixel.R * strength;
						float h = Mathf.Clamp(pixel.R, 0f, 1f);
						Vector3 vertex = cubePoint;// + spherePoint.Normalized() * h * strength;

						vertices[currentIndex] = vertex;
						uvs[currentIndex] = uv;
						normals[currentIndex] = Vector3.Zero;

						if (x != resolution && y != resolution)
						{
							Vector2 diagonalVertexPercentage = new Vector2(x + 1, y + 1) / resolution;
							Vector2 centerVertexPercentage = (percentage + diagonalVertexPercentage) / 2;
							Vector3 centerVertexCubePoint = VectorUtils.UVToPointOnCube(i, centerVertexPercentage);
							Vector3 centerVertexSpherePoint = VectorUtils.PointOnCubeToPointOnSphere(centerVertexCubePoint);
							Vector2 centerVertexUv = VectorUtils.PointOnSphereToUV(centerVertexSpherePoint);

							Color centerPixel = Sampler.SampleBilinear(heightmap, centerVertexUv);
							float centerH = Mathf.Clamp(centerPixel.R, 0f, 1f);
							Vector3 centerVertex = centerVertexSpherePoint + centerVertexSpherePoint.Normalized() * centerH * strength;


							vertices[currentIndex + resolution + 1] = centerVertex;
							uvs[currentIndex + resolution + 1] = centerVertexUv;
							normals[currentIndex + resolution + 1] = Vector3.Zero;

							triangles[triIndex++] = currentIndex + resolution + 1;
							triangles[triIndex++] = currentIndex;
							triangles[triIndex++] = currentIndex + 1;

							triangles[triIndex++] = currentIndex + resolution + 1;
							triangles[triIndex++] = currentIndex + 1;
							triangles[triIndex++] = currentIndex + 2 * resolution + 2;

							triangles[triIndex++] = currentIndex + resolution + 1;
							triangles[triIndex++] = currentIndex + 2 * resolution + 2;
							triangles[triIndex++] = currentIndex + 2 * resolution + 1;

							triangles[triIndex++] = currentIndex + resolution + 1;
							triangles[triIndex++] = currentIndex + 2 * resolution + 1;
							triangles[triIndex++] = currentIndex;
						}
					}

					vertexIndex += resolution;
				}
				vertexIndex -= resolution;
			}

			CalculateNormals(vertices, triangles, normals, resolution);

			Godot.Collections.Array arrays = [];
			arrays.Resize((int)Mesh.ArrayType.Max);
			arrays[(int)Mesh.ArrayType.Vertex] = vertices;
			arrays[(int)Mesh.ArrayType.Index] = triangles;
			arrays[(int)Mesh.ArrayType.Normal] = normals;
			arrays[(int)Mesh.ArrayType.TexUV] = uvs;

			ArrayMesh mesh = new();
			mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

			return mesh;
		}

		public static Image MeshToImage(Mesh mesh)
		{

			Godot.Collections.Array surfaceArrays = mesh.SurfaceGetArrays(0);
			Vector3[] vertices = surfaceArrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
			int[] triangles = surfaceArrays[(int)Mesh.ArrayType.Index].AsInt32Array();
			Vector3[] normals = surfaceArrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
			Vector2[] uvs = surfaceArrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();

			int imageWidth = Math.Max(vertices.Length, triangles.Length);
			Image image = Image.CreateEmpty(imageWidth, 5, false, Image.Format.Rgbaf);

			// row 0 is counts, row 1 is vertices, row 2 is triangles, row 3 is normals, row 4 is uvs 
			image.SetPixel(0, 0, new Color(vertices.Length, 0, 0));
			image.SetPixel(1, 0, new Color(triangles.Length, 0, 0));
			image.SetPixel(2, 0, new Color(normals.Length, 0, 0));
			image.SetPixel(3, 0, new Color(uvs.Length, 0, 0));

			for (int index = 0; index < vertices.Length; index++)
			{
				Vector3 vertex = vertices[index];
				
				image.SetPixel(index, 1, new Color(vertex.X, vertex.Y, vertex.Z));
			}

			for (int index = 0; index < triangles.Length; index++)
			{

				image.SetPixel(index, 2, new Color(triangles[index], 0.0f, 0.0f));
			}

			for (int index = 0; index < normals.Length; index++)
			{
				Vector3 normal = normals[index];
				image.SetPixel(index, 3, new Color(normal.X, normal.Y, normal.Z));
			}

			for (int index = 0; index < uvs.Length; index++)
			{
				Vector2 uv = uvs[index];
				image.SetPixel(index, 4, new Color(uv.X, uv.Y, 0.0f));
			}

			return image;
		}

		private enum Direction
		{
			up, down, left, right,
			bottom_left, bottom_right, top_left, top_right,
			_
		}

		private static void CalculateNormals(Vector3[] vertices, int[] triangles, Vector3[] normals, int resolution)
		{
			(int faceA, int faceB, Direction directionA, Direction directionB, bool isReversedA, bool isReversedB)[] adjecencies = [
				(0, 2, Direction.down,  Direction.left,  false, false),
				(0, 3, Direction.up,    Direction.right, false, false),
				(0, 4, Direction.left,  Direction.right, false, false),
				(0, 5, Direction.right, Direction.left,  false, false),

				(1, 2, Direction.down,  Direction.right, true,  false),
				(1, 3, Direction.up,    Direction.left,  true,  false),
				(1, 4, Direction.right, Direction.left,  false, false),
				(1, 5, Direction.left,  Direction.right, false, false),

				(2, 4, Direction.down,  Direction.down,  false, true),
				(2, 5, Direction.up,    Direction.down,  false, false),
				(3, 4, Direction.down,  Direction.up,    true,  true),
				(3, 5, Direction.up,    Direction.up,    true,  false),
			];

			(int faceA, int faceB, int faceC, Direction directionA, Direction directionB, Direction directionC)[] corners = [
				(0, 2, 4, Direction.bottom_left,  Direction.bottom_left,  Direction.bottom_right),
				(1, 2, 4, Direction.bottom_right, Direction.bottom_right, Direction.bottom_left),
				(1, 3, 4, Direction.top_right,    Direction.bottom_left,  Direction.top_left),
				(1, 3, 5, Direction.top_left,     Direction.top_left,     Direction.top_right),
				(1, 2, 5, Direction.bottom_left,  Direction.top_right,    Direction.bottom_right),
				(0, 2, 5, Direction.bottom_right, Direction.top_left,     Direction.bottom_left),
				(0, 3, 4, Direction.top_left,     Direction.bottom_right, Direction.top_right),
				(0, 3, 5, Direction.top_right,    Direction.top_right,    Direction.top_left),

			];

			static float AngleBetween(Vector3 u, Vector3 v)
			{
				float dot = u.Normalized().Dot(v.Normalized()); // or Vector3.Dot(...) if that's your API
				dot = Math.Clamp(dot, -1f, 1f); // guard against acos domain errors from float precision
				return MathF.Acos(dot);
			}

			for (int i = 0; i < triangles.Length; i += 3)
			{
				int indexA = triangles[i];
				int indexB = triangles[i + 1];
				int indexC = triangles[i + 2];

				Vector3 posA = vertices[indexA];
				Vector3 posB = vertices[indexB];
				Vector3 posC = vertices[indexC];

				Vector3 edgeAB = posB - posA;
				Vector3 edgeAC = posC - posA;
				Vector3 edgeBC = posC - posB;

				// unchanged: magnitude ~ 2*area, keeps your existing winding
				Vector3 faceNormal = edgeAC.Cross(edgeAB).Normalized();

				// angle at each vertex, using the two edges that meet there
				float angleA = AngleBetween(edgeAB, edgeAC);
				float angleB = AngleBetween(-edgeAB, edgeBC);
				float angleC = AngleBetween(-edgeAC, -edgeBC);

				normals[indexA] += faceNormal * angleA;
				normals[indexB] += faceNormal * angleB;
				normals[indexC] += faceNormal * angleC;
			}

			static List<int> GetIndicesFromDirection(Direction direction, int resolution, bool isReversed)
			{
				IEnumerable<int> indices = direction switch
				{
					Direction.up => [.. Enumerable.Range(0, resolution + 1).Select(value => value + resolution * (2 * resolution + 1))],
					Direction.down => [.. Enumerable.Range(0, resolution + 1)],
					Direction.left => [.. Enumerable.Range(0, resolution + 1).Select(value => value * (2 * resolution + 1))],
					Direction.right => [.. Enumerable.Range(0, resolution + 1).Select(value => value * (2 * resolution + 1) + resolution)],
					_ => []
				};

				indices = indices.Skip(1).SkipLast(1);
				return [.. isReversed ? indices.Reverse() : indices];
			}


			static int GetCornerIndex(Direction direction, int resolution)
			{
				int indices = direction switch
				{
					Direction.bottom_left => 0,
					Direction.bottom_right => resolution,
					Direction.top_left => resolution * (2 * resolution + 1),
					Direction.top_right => resolution * (2 * resolution + 1) + resolution,

					_ => -1
				};


				return indices;
			}

			foreach ((int faceA, int faceB, Direction directionA, Direction directionB, bool isReversedA, bool isReversedB) in adjecencies)
			{
				List<int> faceAIndices = GetIndicesFromDirection(directionA, resolution, isReversedA);
				List<int> faceBIndices = GetIndicesFromDirection(directionB, resolution, isReversedB);

				for (int i = 0; i < faceAIndices.Count; i++)
				{
					int vertexIndexA = faceA * (resolution * resolution + (resolution + 1) * (resolution + 1)) + faceAIndices[i];
					int vertexIndexB = faceB * (resolution * resolution + (resolution + 1) * (resolution + 1)) + faceBIndices[i];

					Vector3 newNormal = normals[vertexIndexA] + normals[vertexIndexB];

					normals[vertexIndexA] = newNormal;
					normals[vertexIndexB] = newNormal;
				}
			}

			foreach ((int faceA, int faceB, int faceC, Direction directionA, Direction directionB, Direction directionC) in corners)
			{
				int localCornerAIndex = GetCornerIndex(directionA, resolution);
				int localCornerBIndex = GetCornerIndex(directionB, resolution);
				int localCornerCIndex = GetCornerIndex(directionC, resolution);

				int vertexIndexA = faceA * (resolution * resolution + (resolution + 1) * (resolution + 1)) + localCornerAIndex;
				int vertexIndexB = faceB * (resolution * resolution + (resolution + 1) * (resolution + 1)) + localCornerBIndex;
				int vertexIndexC = faceC * (resolution * resolution + (resolution + 1) * (resolution + 1)) + localCornerCIndex;

				Vector3 newNormal = normals[vertexIndexA] + normals[vertexIndexB] + normals[vertexIndexC];

				normals[vertexIndexA] = newNormal;
				normals[vertexIndexB] = newNormal;
				normals[vertexIndexC] = newNormal;
			}

			for (int i = 0; i < normals.Length; i++)
				normals[i] = normals[i].Normalized();
		}


		private static (int count, Key[] keys) MeshToKeys(ArrayMesh mesh, int maxSize)
		{
			Godot.Collections.Array surfaceArrays = mesh.SurfaceGetArrays(0);
			// Vector3[] vertices = surfaceArrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
			int[] triangles = surfaceArrays[(int)Mesh.ArrayType.Index].AsInt32Array();
			// Vector3[] normals = surfaceArrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
			// Vector2[] uvs = surfaceArrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();

			Key[] keys = new Key[maxSize];
			int count = triangles.Length / 3;

			// for (int i = 0; i < count; i++)
			// {
			// 	keys[4 * i + 0] = new Key(0, 1, i, 0);
			// 	keys[4 * i + 1] = new Key(0, 1, i, 1);
			// 	keys[4 * i + 2] = new Key(0, 1, i, 2);
			// 	keys[4 * i + 3] = new Key(0, 1, i, 3);
			// }

			for (int i = 0; i < count; i+=4)
			{
				int keyIndex = i / 4;
				keys[4 * keyIndex + 0] = new Key(0, 1, keyIndex, 0);
				keys[4 * keyIndex + 1] = new Key(0, 1, keyIndex, 1);
				keys[4 * keyIndex + 2] = new Key(0, 1, keyIndex, 2);
				keys[4 * keyIndex + 3] = new Key(0, 1, keyIndex, 3);

				GD.Print(4 * keyIndex);
				

			}

			// GD.Print(count);

			return (count, keys);
		}
	}
}