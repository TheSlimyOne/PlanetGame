using System;

namespace Uniform;

public class InvalidRenderingDeviceException : InvalidOperationException 
{
    public InvalidRenderingDeviceException()
        : base("The specified rendering device is not the main rendering device.") { }

    public InvalidRenderingDeviceException(string message)
        : base(message) { }

    public InvalidRenderingDeviceException(string message, Exception innerException)
        : base(message, innerException) { }
}