namespace Aspire_Full.DockerRegistry;

[Serializable]
public class DockerRegistryException : Exception
{
    public DockerRegistryException() { }

    public DockerRegistryException(string message) : base(message) { }

    public DockerRegistryException(string message, Exception innerException) : base(message, innerException) { }
}
