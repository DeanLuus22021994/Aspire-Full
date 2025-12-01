using System.Reflection;
using System.Runtime.InteropServices;

namespace Aspire_Full.Tensor.Core.Native;

/// <summary>
/// Cross-platform native library resolver for AspireFullNative.
/// Handles library discovery across Windows, Linux, macOS and containerized environments.
/// Supports GPU bootstrapping via CUDA toolkit detection.
/// </summary>
public static class NativeLibraryResolver
{
    private const string LibraryName = "AspireFullNative";
    private static bool s_initialized;
    private static readonly object s_initLock = new();

    /// <summary>
    /// Gets whether the native library is loaded successfully.
    /// </summary>
    public static bool IsNativeLoaded { get; private set; }

    /// <summary>
    /// Gets the path of the loaded native library, if available.
    /// </summary>
    public static string? LoadedLibraryPath { get; private set; }

    /// <summary>
    /// Gets any error message from library loading.
    /// </summary>
    public static string? LoadError { get; private set; }

    /// <summary>
    /// Initializes the native library resolver.
    /// Call this early in application startup to ensure proper library discovery.
    /// </summary>
    public static void Initialize()
    {
        if (s_initialized) return;

        lock (s_initLock)
        {
            if (s_initialized) return;

            NativeLibrary.SetDllImportResolver(typeof(NativeTensorContext).Assembly, ResolveNativeLibrary);
            s_initialized = true;
        }
    }

    private static nint ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != LibraryName)
            return nint.Zero;

        var paths = GetSearchPaths();

        foreach (var path in paths)
        {
            if (TryLoadLibrary(path, out var handle))
            {
                IsNativeLoaded = true;
                LoadedLibraryPath = path;
                return handle;
            }
        }

        // Fallback: let the default loader try
        if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var defaultHandle))
        {
            IsNativeLoaded = true;
            LoadedLibraryPath = libraryName;
            return defaultHandle;
        }

        LoadError = $"Failed to load {libraryName} from any of the search paths: {string.Join(", ", paths)}";
        return nint.Zero;
    }

    private static IEnumerable<string> GetSearchPaths()
    {
        var assemblyLocation = Path.GetDirectoryName(typeof(NativeLibraryResolver).Assembly.Location) ?? ".";
        var appBase = AppContext.BaseDirectory;

        // Platform-specific library name
        var libName = GetPlatformLibraryName();

        // Priority ordered search paths
        var searchPaths = new List<string>
        {
            // 1. Same directory as the assembly
            Path.Combine(assemblyLocation, libName),

            // 2. Application base directory
            Path.Combine(appBase, libName),

            // 3. runtimes/{rid}/native/ structure (NuGet convention)
            Path.Combine(assemblyLocation, "runtimes", GetRuntimeIdentifier(), "native", libName),
            Path.Combine(appBase, "runtimes", GetRuntimeIdentifier(), "native", libName),
        };

        // 4. Docker/Container paths (common mount points)
        if (IsRunningInContainer())
        {
            searchPaths.Add($"/app/{libName}");
            searchPaths.Add($"/opt/aspire/{libName}");
            searchPaths.Add($"/usr/local/lib/{libName}");
            searchPaths.Add($"/usr/lib/{libName}");
        }

        // 5. CUDA toolkit library paths
        var cudaPaths = GetCudaLibraryPaths();
        searchPaths.AddRange(cudaPaths.Select(p => Path.Combine(p, libName)));

        // 6. Development build output paths
        searchPaths.Add(Path.Combine(assemblyLocation, "..", "..", "..", "build", libName));
        searchPaths.Add(Path.Combine(appBase, "..", "..", "..", "build", libName));

        // 7. LD_LIBRARY_PATH entries (Linux)
        if (OperatingSystem.IsLinux())
        {
            var ldPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH");
            if (!string.IsNullOrEmpty(ldPath))
            {
                foreach (var path in ldPath.Split(':'))
                {
                    searchPaths.Add(Path.Combine(path, libName));
                }
            }
        }

        return searchPaths.Distinct();
    }

    private static string GetPlatformLibraryName()
    {
        if (OperatingSystem.IsWindows())
            return "AspireFullNative.dll";
        if (OperatingSystem.IsMacOS())
            return "libAspireFullNative.dylib";
        return "libAspireFullNative.so";
    }

    private static string GetRuntimeIdentifier()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => "x64"
        };

        if (OperatingSystem.IsWindows())
            return $"win-{arch}";
        if (OperatingSystem.IsMacOS())
            return $"osx-{arch}";
        if (OperatingSystem.IsLinux())
            return $"linux-{arch}";

        return $"linux-{arch}";
    }

    private static bool IsRunningInContainer()
    {
        // Check common container indicators
        return File.Exists("/.dockerenv") ||
               Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ||
               Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST") != null;
    }

    private static IEnumerable<string> GetCudaLibraryPaths()
    {
        var paths = new List<string>();

        // CUDA_HOME / CUDA_PATH environment variables
        var cudaHome = Environment.GetEnvironmentVariable("CUDA_HOME") ??
                       Environment.GetEnvironmentVariable("CUDA_PATH");

        if (!string.IsNullOrEmpty(cudaHome))
        {
            paths.Add(Path.Combine(cudaHome, "lib64"));
            paths.Add(Path.Combine(cudaHome, "lib", "x64"));
            paths.Add(Path.Combine(cudaHome, "lib"));
        }

        // Standard CUDA installation paths
        if (OperatingSystem.IsLinux())
        {
            paths.Add("/usr/local/cuda/lib64");
            paths.Add("/usr/local/cuda-12/lib64");
            paths.Add("/usr/local/cuda-11/lib64");
            paths.Add("/opt/cuda/lib64");
        }
        else if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            paths.Add(Path.Combine(programFiles, "NVIDIA GPU Computing Toolkit", "CUDA", "v12.0", "bin"));
            paths.Add(Path.Combine(programFiles, "NVIDIA GPU Computing Toolkit", "CUDA", "v11.8", "bin"));
        }

        return paths;
    }

    private static bool TryLoadLibrary(string path, out nint handle)
    {
        handle = nint.Zero;

        if (!File.Exists(path))
            return false;

        try
        {
            return NativeLibrary.TryLoad(path, out handle);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets diagnostic information about the native library loading state.
    /// </summary>
    public static NativeLibraryDiagnostics GetDiagnostics()
    {
        return new NativeLibraryDiagnostics
        {
            IsInitialized = s_initialized,
            IsLoaded = IsNativeLoaded,
            LoadedPath = LoadedLibraryPath,
            Error = LoadError,
            RuntimeIdentifier = GetRuntimeIdentifier(),
            IsContainer = IsRunningInContainer(),
            CudaHome = Environment.GetEnvironmentVariable("CUDA_HOME"),
            CudaPath = Environment.GetEnvironmentVariable("CUDA_PATH"),
            SearchPaths = GetSearchPaths().ToArray()
        };
    }
}

/// <summary>
/// Diagnostic information for native library loading.
/// </summary>
public sealed class NativeLibraryDiagnostics
{
    public bool IsInitialized { get; init; }
    public bool IsLoaded { get; init; }
    public string? LoadedPath { get; init; }
    public string? Error { get; init; }
    public required string RuntimeIdentifier { get; init; }
    public bool IsContainer { get; init; }
    public string? CudaHome { get; init; }
    public string? CudaPath { get; init; }
    public required string[] SearchPaths { get; init; }
}
