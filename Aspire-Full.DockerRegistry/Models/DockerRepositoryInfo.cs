namespace Aspire_Full.DockerRegistry.Models;

public sealed record DockerRepositoryInfo(string Repository, bool MatchesPattern, DockerImageDescriptor? Descriptor);
