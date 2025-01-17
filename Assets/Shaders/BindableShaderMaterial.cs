using Godot;
using System.Collections.Generic;
using System;

namespace Shaders
{
    [Tool]
    [GlobalClass]

    public partial class BindableShaderMaterial : ShaderMaterial
    {

        private Dictionary<string, Callable> _parameters { get; set; } = new();
        private Dictionary<string, Callable> _frameDependentParameters { get; set; } = new();

        public void Bind<T>(string parameterName, Func<T> callable)
        {
            _parameters[parameterName] = Callable.From(callable);
        }

        public void FrameDependentBind<T>(string parameterName, Func<T> callable)
        {
            _parameters[parameterName] = Callable.From(callable);
            _frameDependentParameters[parameterName] = Callable.From(callable);
        }
    
        public void UpdateAllParameters()
        {
            foreach (var parameter in _parameters)
            {
                string parameterName = parameter.Key;
                Callable callable = parameter.Value;

                SetShaderParameter(parameterName, callable.Call());
            }
        }

        public void UpdateFrameDependentParameters()
        {
            foreach (var parameter in _frameDependentParameters)
            {
                string parameterName = parameter.Key;
                Callable callable = parameter.Value;

                SetShaderParameter(parameterName, callable.Call());
            }
        }


        public void ConnectChanged(Action action)
        {
            if (!IsConnected("changed", Callable.From(action)))
            {
                Changed += action;
            }
        }
        public void DisconnectChanged(Action action)
        {
            if (IsConnected("changed", Callable.From(action)))
            {
                Changed -= action;
            }
        }
    }
}