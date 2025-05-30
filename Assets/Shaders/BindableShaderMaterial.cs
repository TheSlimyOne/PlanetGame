using Godot;
using System.Collections.Generic;
using System;

namespace Shaders
{
    [Tool]
    [GlobalClass]

    public partial class BindableShaderMaterial : ShaderMaterial
    {

        private Dictionary<string, Callable> _parameters { get; set; } = [];
        private Dictionary<string, Callable> _frameDependentParameters { get; set; } = [];

        public void Bind<T>(string parameterName, Func<T> callable)
        {
            _parameters[parameterName] = Callable.From(callable);
        }

        public void FrameDependentBind<T>(string parameterName, Func<T> callable)
        {
            Bind(parameterName, callable);
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

        public void UpdateParameter(string parameterName)
        {
            if (_parameters.TryGetValue(parameterName, out Callable callable))
            {
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

        public void Unbind(string parameterName)
        {
            _parameters.Remove(parameterName);
        }
        
        public void UnbindFrameDependentBind(string parameterName)
        {
            _frameDependentParameters.Remove(parameterName);
        }

        public void UnbindAll()
        {
            _parameters.Clear();
            _frameDependentParameters.Clear();
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