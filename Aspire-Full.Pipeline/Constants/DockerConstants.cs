namespace Aspire_Full.Pipeline.Constants;

public static class DockerConstants
{
    public const string NetworkName = "aspire-network";
    public const string RunnerContainerName = "github-runner";
    public const string RegistryContainerName = "registry";

    public static readonly (string Name, string Description)[] Volumes =
    [
        ("aspire-nuget-cache", "NuGet package cache"),
        ("aspire-dotnet-tools", ".NET global tools"),
        ("aspire-aspire-cli", "Aspire CLI data"),
        ("aspire-vscode-extensions", "VS Code server extensions"),
        ("aspire-workspace", "Workspace source code"),
        ("aspire-dashboard-data", "Aspire Dashboard data"),
        ("aspire-docker-data", "Docker-in-Docker daemon data"),
        ("aspire-docker-certs", "Docker TLS certificates"),
        ("aspire-runner-data", "Runner configuration and state"),
        ("aspire-runner-work", "Runner work directory"),
        ("aspire-runner-nuget", "Runner NuGet cache"),
        ("aspire-runner-npm", "Runner npm cache"),
        ("aspire-runner-toolcache", "GitHub Actions tool cache"),
        ("aspire-github-mcp-data", "GitHub MCP data"),
        ("aspire-github-mcp-logs", "GitHub MCP logs"),
        ("aspire-github-mcp-cache", "GitHub MCP cache"),
        ("aspire-postgres-data", "PostgreSQL database"),
        ("aspire-redis-data", "Redis cache")
    ];
}
