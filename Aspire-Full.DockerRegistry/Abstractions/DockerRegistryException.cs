using System;

namespace Aspire_Full.DockerRegistry.Abstractions;

[Serializable]
public class DockerRegistryException : Exception
{
    public DockerRegistryErrorCode ErrorCode { get; } = DockerRegistryErrorCode.Unknown;

    public DockerRegistryException() { }

    public DockerRegistryException(string message) : base(message) { }

    public DockerRegistryException(string message, Exception innerException) : base(message, innerException) { }

    public DockerRegistryException(DockerRegistryErrorCode errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    public DockerRegistryException(DockerRegistryErrorCode errorCode, string message, Exception innerException) : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

public enum DockerRegistryErrorCode
{
    Unknown,
    NetworkError,
    ManifestNotFound,
    TagNotFound,
    AuthenticationFailed,
    BuildxError,
    GarbageCollectionFailed
}
