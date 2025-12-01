namespace Aspire_Full.Pipeline.Constants;

public static class PathConstants
{
    public const string PidFile = ".aspire.pid";
    public const string SolutionFilter = "Aspire-Full.slnf";
    public const string AppHostProject = "Aspire-Full/Aspire-Full.csproj";
    public const string AppHostProjectName = "Aspire-Full";

    public const string DevContainerEnv = ".devcontainer/.env";
    public const string DevContainerCompose = ".devcontainer/docker-compose.yml";

    public const string DocsDir = "docs";
    public const string ApiDocsDir = "docs/api";
    public const string LlmsTxt = "llms.txt";

    public const string TestResultsUnit = "./TestResults/Unit";
    public const string TestResultsE2E = "./TestResults/E2E";
    public const string TestResultsAspire = "./TestResults/Aspire";

    public const string UnitTestProject = "Aspire-Full.Tests.Unit";
    public const string E2ETestProject = "Aspire-Full.Tests.E2E";

    public const string AgentDockerfile = "Aspire-Full.Python/python-agents/Dockerfile.agent";

    public const string AspireFullDll = "Aspire-Full/bin/Release/net9.0/Aspire-Full.dll";
    public const string PythonProjectRoot = "Aspire-Full.Python";
    public const string SandboxesDir = "sandboxes";
    public const string ScriptsDir = "scripts";
    public const string ToolsDir = "tools";
    public const string PythonAgentsDir = "Aspire-Full.Python/python-agents";
    public const string PythonAgentsTestsDir = "Aspire-Full.Python/python-agents/tests";
    public const string ScriptsTestsDir = "scripts/tests";
    public const string RecommendedConfigDir = ".config/recommended";
    public const string EnvironmentYaml = "environment.yaml";
    public const string PythonConfigYaml = "python-config.yaml";
}
