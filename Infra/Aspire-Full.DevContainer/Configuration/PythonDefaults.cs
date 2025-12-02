namespace Aspire_Full.DevContainer.Configuration;

/// <summary>
/// Python runtime configuration defaults for the development environment.
/// </summary>
public static class PythonDefaults
{
    /// <summary>
    /// Python version used in the devcontainer.
    /// Uses free-threaded build with GIL disabled for true parallel threading.
    /// </summary>
    public const string PythonVersion = "3.15.0a2-free-threaded";

    /// <summary>
    /// Python runtime type identifier.
    /// </summary>
    public const string PythonRuntime = "cpython-free-threaded";
}
