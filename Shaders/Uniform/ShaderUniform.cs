using System.Collections.Generic;
using Godot;
using PlanetGame.Shaders;

namespace Uniform
{
    public abstract class ShaderUniform : IGPUResource
    {
        public Rid Rid { get; protected set; }
        // public RDUniform Uniform { get; protected set; }
        public RenderingDevice.UniformType UniformType { get; protected set; }
        public RenderingDevice RenderingDevice { get; private set; }
        public readonly bool UsingMainRenderingDevice;
        public IGPUResource Owner { get; protected set; }
        public bool Perserved { get; protected set; }

        public string Name { get; private set; }

        //TODO need to implement
        public static List<ShaderUniform> Uniforms = [];
        protected ShaderUniform(string name, RenderingDevice renderingDevice, IGPUResource owner, bool perserved = false)
        {
            RenderingDevice = renderingDevice;
            UsingMainRenderingDevice = RenderingDevice == RenderingServer.GetRenderingDevice();

            Owner = owner;
            Perserved = perserved;
            Name = name;

            Uniforms.Add(this);
        }

        // This is supposed to simplify the process of sharing buffers between 2 or more compute shaders
        // It will either share the data if the rd is the same or clone the buffer to another rd if the rds are different
        // Make sure that if the Uniform requires the main rd to throw error if rebinding to local rd
        // public abstract ShaderUniform RebindUniform(IGPUResource owner, RenderingDevice rd, int binding);

        public abstract RDUniform CreateRDUniform(int binding);

        // TODO either delete or fix this because ever uniform updates differently
        public abstract void UpdateUniform(byte[] data);

        public abstract List<byte[]> GetByteData();

        public void FreeRid()
        {
            // GD.Print($"Freeing uniform: {this}");
            FreeRidInternal();
        }

        protected virtual void FreeRidInternal()
        {
            if (RenderingDevice == null) return;
            if (Rid.IsValid)
                RenderingDevice.FreeRid(Rid);
            Rid = new();
        }

        public bool HasOwner()
        {
            return Owner != null;
        }

        public override string ToString()
        {
            return $"Name: {Name}, Rid: {Rid}, Type: {GetType().Name}, UsingMainRenderingDevice: {UsingMainRenderingDevice}, Owner: ({Owner.GetType().Name}, {Owner.GetID()}), Perserved: {Perserved}";
        }

        public int GetID() => Rid.GetHashCode() + Owner.GetHashCode();
        

    }
}