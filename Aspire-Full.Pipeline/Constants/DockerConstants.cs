namespace Aspire_Full.Pipeline.Constants;

public static class DockerConstants
{
    public const string NetworkName = "aspire-network";
    public const string RunnerContainerName = "github-runner";
    public const string RegistryContainerName = "registry";

    public static class Volume
    {
        public const string NugetCache = "aspire-nuget-cache";
        public const string DotnetTools = "aspire-dotnet-tools";
        public const string AspireCli = "aspire-aspire-cli";
        public const string VscodeExtensions = "aspire-vscode-extensions";
        public const string Workspace = "aspire-workspace";
        public const string DashboardData = "aspire-dashboard-data";
        public const string DockerData = "aspire-docker-data";
        public const string DockerCerts = "aspire-docker-certs";
        public const string RunnerData = "aspire-runner-data";
        public const string RunnerWork = "aspire-runner-work";
        public const string RunnerNuget = "aspire-runner-nuget";
        public const string RunnerNpm = "aspire-runner-npm";
        public const string RunnerToolCache = "aspire-runner-toolcache";
        public const string GithubMcpData = "aspire-github-mcp-data";
        public const string GithubMcpLogs = "aspire-github-mcp-logs";
        public const string GithubMcpCache = "aspire-github-mcp-cache";
        public const string PostgresData = "aspire-postgres-data";
        public const string RedisData = "aspire-redis-data";
    }

    public static readonly (string Name, string Description)[] Volumes =
    [
        (Volume.NugetCache, "NuGet package cache"),
        (Volume.DotnetTools, ".NET global tools"),
        (Volume.AspireCli, "Aspire CLI data"),
        (Volume.VscodeExtensions, "VS Code server extensions"),
        (Volume.Workspace, "Workspace source code"),
        (Volume.DashboardData, "Aspire Dashboard data"),
        (Volume.DockerData, "Docker-in-Docker daemon data"),
        (Volume.DockerCerts, "Docker TLS certificates"),
        (Volume.RunnerData, "Runner configuration and state"),
        (Volume.RunnerWork, "Runner work directory"),
        (Volume.RunnerNuget, "Runner NuGet cache"),
        (Volume.RunnerNpm, "Runner npm cache"),
        (Volume.RunnerToolCache, "GitHub Actions tool cache"),
        (Volume.GithubMcpData, "GitHub MCP data"),
        (Volume.GithubMcpLogs, "GitHub MCP logs"),
        (Volume.GithubMcpCache, "GitHub MCP cache"),
        (Volume.PostgresData, "PostgreSQL database"),
        (Volume.RedisData, "Redis cache")
    ];
}
