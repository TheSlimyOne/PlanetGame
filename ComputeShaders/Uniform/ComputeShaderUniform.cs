using Godot;
using System;
using Godot.Collections;
using Dispatcher;

namespace Uniform
{
    public abstract partial class ComputeShaderUniform : GodotObject
    {
        public Rid Rid { get; protected set; }
        public int Binding { get; protected set; }
        public RDUniform Uniform { get; protected set; }
        protected RenderingDevice _rd;
        public readonly int OwnerID;

        protected ComputeShaderUniform(RenderingDevice renderingDevice, int binding, IDispatchable owner)
        {
            _rd = renderingDevice;
            Binding = binding;
            OwnerID = owner?.GetHashCode() ?? -1;
        }

        // This is supposed to simplify the process of sharing buffers between 2 or more compute shaders
        // It will either share the data if the rd is the same or clone the buffer to another rd if the rds are different
        public abstract ComputeShaderUniform RebindUniform(IDispatchable owner, RenderingDevice rd, int binding);

        public abstract void UpdateUniform(byte[] data);

        public abstract Array<byte[]> GetByteData();

        public void FreeRid()
        {
            if (_rd == null) return;
            foreach (Rid rid in Uniform.GetIds())
            {
                _rd.FreeRid(rid);
            }
            Uniform.ClearIds();
            _rd = null;
        }

        public bool HasOwner()
        {
            return OwnerID != -1;
        }
    }

}