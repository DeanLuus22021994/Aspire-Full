namespace Aspire_Full.Configuration;

public static class AppHostConstants
{
    public const string NetworkName = "aspire-network";

    public static class Images
    {
        public const string DockerDebugger = "docker/debugger:latest";
        public const string DockerDind = "docker:27-dind";
        public const string AspireDashboard = "mcr.microsoft.com/dotnet/aspire-dashboard:latest";
        public const string Registry = "registry:2";
    }

    public static class Volumes
    {
        public const string DockerData = "aspire-docker-data";
        public const string DockerCerts = "aspire-docker-certs";
        public const string DashboardData = "aspire-dashboard-data";
        public const string PostgresData = "aspire-postgres-data";
        public const string RedisData = "aspire-redis-data";
        public const string QdrantData = "aspire-qdrant-data";
    }

    public static class Resources
    {
        public const string DockerDaemon = "docker";
        public const string DockerDebugger = "docker-debugger";
        public const string AspireDashboard = "aspire-dashboard";
        public const string Registry = "registry";
        public const string Postgres = "postgres";
        public const string Database = "aspiredb";
        public const string Redis = "redis";
        public const string Qdrant = "qdrant";
        public const string Api = "api";
        public const string Gateway = "gateway";
        public const string Frontend = "frontend";
        public const string WasmDocs = "frontend-docs";
        public const string WasmUat = "frontend-uat";
        public const string WasmProd = "frontend-prod";
        public const string PythonAgents = "python-agents";
    }

    public static class Ports
    {
        public const int DockerEngine = 2376;
        public const int DockerDebuggerUi = 9393;
        public const int DashboardUi = 18888;
        public const int DashboardOtlp = 18889;
        public const int Registry = 5000;
        public const int Api = 5000;
        public const int Gateway = 5001;
        public const int Frontend = 3000;
        public const int WasmDocs = 5175;
        public const int WasmUat = 5176;
        public const int WasmProd = 5177;
        public const int PythonAgents = 8000;
    }
}
