using Godot;
using System;
using Dispatcher;
using Godot.Collections;

namespace Uniform
{
    public abstract partial class ComputeShaderUniform : GodotObject
    {
        public Rid Rid { get; protected set; }
        public int Binding { get; protected set; }
        public RDUniform Uniform { get; protected set; }
        public RenderingDevice RenderingDevice { get; private set; }
        public readonly bool UsingMainRenderingDevice;
        public IDispatchable Owner { get; protected set; }

        protected ComputeShaderUniform(int binding, IDispatchable owner) : this(RenderingServer.GetRenderingDevice(), binding, owner) { }
       
        protected ComputeShaderUniform(RenderingDevice renderingDevice, int binding, IDispatchable owner)
        {
            RenderingDevice = renderingDevice;
            UsingMainRenderingDevice = RenderingDevice == RenderingServer.GetRenderingDevice(); 

            Binding = binding;
            Owner = owner;
        }

        // This is supposed to simplify the process of sharing buffers between 2 or more compute shaders
        // It will either share the data if the rd is the same or clone the buffer to another rd if the rds are different
        // Make sure that if the Uniform requires the main rd to throw error if rebinding to local rd
        public abstract ComputeShaderUniform RebindUniform(IDispatchable owner, RenderingDevice rd, int binding);

        // TODO either delete or fix this because ever uniform updates differently
        public abstract void UpdateUniform(byte[] data);

        public abstract Array<byte[]> GetByteData();

        public virtual void FreeRids()
        {
            if (RenderingDevice == null) return;
            foreach (Rid rid in Uniform.GetIds())
            {
                if (rid.IsValid)
                    RenderingDevice.FreeRid(rid);
                else
                    GD.PrintErr($"Rid: {rid} is not valid for RenderingDevice: {RenderingDevice}.");
            }
            Uniform.ClearIds();
        }

        public bool HasOwner()
        {
            return Owner != null;
        }
    }

}