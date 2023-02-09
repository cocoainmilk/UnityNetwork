
using System;

public class ConnectionException : Exception
{
    public ConnectionException(string message) : base(message) { }
}
public class ProtocolException : Exception
{
    public long StatusCode { get; private set; }
    public string Body { get; private set; }
    public ProtocolException(string message, long statusCode, string body = null) : base(message)
    {
        StatusCode = statusCode;
        Body = body;
    }
}
public class DataProcessingException : Exception
{
    public DataProcessingException(string message) : base(message) { }
}
