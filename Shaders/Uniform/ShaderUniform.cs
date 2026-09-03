using System.Collections.Generic;
using Godot;
using PlanetGame.Shaders;
using PlanetGame.Shaders.Dispatchers;

namespace Uniform
{
    public abstract class ShaderUniform : IGPUResource
    {
        public Rid Rid { get; protected set; }
        public int Binding { get; protected set; }
        public RDUniform Uniform { get; protected set; }
        public RenderingDevice RenderingDevice { get; private set; }
        public readonly bool UsingMainRenderingDevice;
        public IGPUResource Owner { get; protected set; }
        public bool Perserved { get; protected set; }

        public static List<ShaderUniform> Uniforms = [];

        protected ShaderUniform(int binding, IGPUResource owner, bool perserved = false) : this(RenderingServer.GetRenderingDevice(), binding, owner, perserved) { }

        protected ShaderUniform(RenderingDevice renderingDevice, int binding, IGPUResource owner, bool perserved = false)
        {
            RenderingDevice = renderingDevice;
            UsingMainRenderingDevice = RenderingDevice == RenderingServer.GetRenderingDevice();

            Binding = binding;
            Owner = owner;
            Perserved = perserved;

            Uniforms.Add(this);
        }

        // This is supposed to simplify the process of sharing buffers between 2 or more compute shaders
        // It will either share the data if the rd is the same or clone the buffer to another rd if the rds are different
        // Make sure that if the Uniform requires the main rd to throw error if rebinding to local rd
        public abstract ShaderUniform RebindUniform(IGPUResource owner, RenderingDevice rd, int binding);

        // TODO either delete or fix this because ever uniform updates differently
        public abstract void UpdateUniform(byte[] data);

        public abstract List<byte[]> GetByteData();

        public virtual void FreeRids()
        {
            if (RenderingDevice == null) return;

            foreach (Rid rid in Uniform.GetIds())
            {
                if (Rid == rid && Perserved)
                {
                    if (IGPUResource.Verbose) GD.Print($"Perserved {rid} of {GetType()} its owner is {Owner.GetID()} {Owner.GetType()}");
                    continue;
                }

                if (rid.IsValid)
                {
                    if (IGPUResource.Verbose) GD.Print($"Freed {rid} for a {GetType()}");
                    RenderingDevice.FreeRid(rid);
                }
                else
                {
                    if (IGPUResource.Verbose) GD.PrintErr($"Rid: {rid} is not valid for RenderingDevice: {RenderingDevice}.");
                }
            }
            Uniform.ClearIds();
            Uniform = null;
            Rid = new();
        }

        public bool HasOwner()
        {
            return Owner != null;
        }

        public override string ToString()
        {
            return $"Rid: {Rid}, Type: {GetType()} Binding: {Binding}, UsingMainRenderingDevice: {UsingMainRenderingDevice}, Owner: ({Owner.GetType()}, {Owner.GetID()}), Perserved: {Perserved}";
        }

        public int GetID() => Rid.GetHashCode() + Owner.GetHashCode();
        

    }
}