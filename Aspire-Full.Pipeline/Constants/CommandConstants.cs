namespace Aspire_Full.Pipeline.Constants;

public static class CommandConstants
{
    public const string RootDescription = "Aspire-Full Pipeline Tool";

    public static class Discover
    {
        public const string Name = "discover";
        public const string Description = "Discover environment and tools";
    }

    public static class Infra
    {
        public const string Name = "infra";
        public const string Description = "Manage Docker infrastructure";
        public const string Init = "init";
        public const string InitDesc = "Initialize Docker volumes and network";
    }

    public static class Ci
    {
        public const string Name = "ci";
        public const string Description = "Manage CI/CD workflows";

        public const string Runner = "runner";
        public const string RunnerDesc = "Manage GitHub Actions runner";
        public const string Setup = "setup";
        public const string SetupDesc = "Setup and start the runner";
        public const string Start = "start";
        public const string StartDesc = "Start the runner service";
        public const string Stop = "stop";
        public const string StopDesc = "Stop the runner service";
        public const string Status = "status";
        public const string StatusDesc = "Check runner status";
        public const string Logs = "logs";
        public const string LogsDesc = "View runner logs";

        public const string Cache = "cache";
        public const string CacheDesc = "Manage GitHub Actions cache";
        public const string List = "list";
        public const string ListDesc = "List cache entries";
        public const string Delete = "delete";
        public const string DeleteDesc = "Delete a cache entry";
        public const string Clear = "clear";
        public const string ClearDesc = "Clear all cache entries";

        public const string Sbom = "sbom";
        public const string SbomDesc = "Generate SBOM";

        public const string RunLocal = "run-local";
        public const string RunLocalDesc = "Run GitHub Actions locally using gh-act";
    }

    public static class Dev
    {
        public const string Name = "dev";
        public const string Description = "Development workflow operations";

        public const string Start = "start";
        public const string StartDesc = "Start the Aspire AppHost";
        public const string Stop = "stop";
        public const string StopDesc = "Stop the Aspire AppHost";
        public const string Status = "status";
        public const string StatusDesc = "Check status of Aspire AppHost";
        public const string Build = "build";
        public const string BuildDesc = "Build the solution";
        public const string Test = "test";
        public const string TestDesc = "Run tests";
        public const string Cleanup = "cleanup";
        public const string CleanupDesc = "Clean up artifacts and containers";
    }

    public static class Ai
    {
        public const string Name = "ai";
        public const string Description = "AI and Agent workflows";

        public const string Provision = "provision";
        public const string ProvisionDesc = "Provision Aspire Agents";
        public const string Models = "models";
        public const string ModelsDesc = "Interact with GitHub Models";
        public const string Workflows = "workflows";
        public const string WorkflowsDesc = "Manage Agentic Workflows";
        public const string Copilot = "copilot";
        public const string CopilotDesc = "GitHub Copilot CLI";
    }

    public static class Docs
    {
        public const string Name = "docs";
        public const string Description = "Documentation management";

        public const string Generate = "generate";
        public const string GenerateDesc = "Generate documentation";
        public const string Changelog = "changelog";
        public const string ChangelogDesc = "Generate changelog";
    }

    public static class Pipeline
    {
        public const string Name = "pipeline";
        public const string Description = "Run the full build/test/run pipeline";

        public const string SolutionDesc = "Solution to build";
        public const string ProjectDesc = "AppHost project";
        public const string ConfigDesc = "Build configuration";
        public const string SkipRunDesc = "Skip the final dotnet run step";
        public const string VerifyOnlyDesc = "Run format in verify mode";
        public const string NoRestoreDesc = "Skip restore";
    }
}
