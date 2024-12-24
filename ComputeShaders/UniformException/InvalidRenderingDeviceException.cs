using System;

namespace UniformException;

public class InvalidRenderingDeviceException : InvalidOperationException 
{
    public InvalidRenderingDeviceException()
        : base("The specified rendering device is not the main rendering device.") { }

    public InvalidRenderingDeviceException(string message, Exception innerException)
        : base(message, innerException) { }
}