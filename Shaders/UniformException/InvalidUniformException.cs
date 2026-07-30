using System;

namespace UniformException;

public class InvalidUniformException : InvalidOperationException 
{
    public InvalidUniformException(string message)
        : base(message) { }

    public InvalidUniformException(string message, Exception innerException)
        : base(message, innerException) { }
}