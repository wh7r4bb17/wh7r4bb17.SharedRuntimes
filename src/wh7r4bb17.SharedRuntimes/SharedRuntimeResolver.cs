// Copyright (C) 2026 wh7r4bb17.
// This file is part of wh7r4bb17.SharedRuntimes.
//
// wh7r4bb17.SharedRuntimes is licensed under the GNU General Public License version 3 only.
// See LICENSE.txt for details.

using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace wh7r4bb17.SharedRuntimes;

/// <summary>Provides managed and Windows native fallback resolution for one set.</summary>
public sealed class SharedRuntimeResolver : IDisposable
    {
    #region Fields

    private const uint _loadLibrarySearchDllLoadDir = 0x00000100;
    private const uint _loadLibrarySearchSystem32 = 0x00000800;
    private static readonly Lock _nativeGate = new();
    private static readonly Dictionary<string, nint> _nativeHandles = new(StringComparer.OrdinalIgnoreCase);
    private readonly AssemblyLoadContext _context;
    private readonly bool _exact;
    private readonly Action<string>? _log;
    private readonly string[] _managedDirectories;
    private readonly string[] _nativeDirectories;
    private readonly bool _nativeForAll;
    private int _disposed;

    #endregion Fields

    #region Properties

    /// <summary>Gets whether this instance has been disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Gets the resolved absolute runtime directory.</summary>
    public string RuntimeDirectory { get; }

    #endregion Properties

    #region Private Constructors

    private SharedRuntimeResolver(SharedRuntimeOptions options, AssemblyLoadContext context)
        {
        ArgumentNullException.ThrowIfNull(options);

        RuntimePaths.Segment(options.CompanyName, nameof(options.CompanyName));
        RuntimePaths.Segment(options.RuntimeName, nameof(options.RuntimeName));
        RuntimePaths.Segment(options.RuntimeVersion, nameof(options.RuntimeVersion));

        string directory = options.RuntimeDirectory ?? SharedRuntimePaths.GetRuntimeDirectory(options.CompanyName, options.RuntimeName, options.RuntimeVersion, targetFramework: options.TargetFramework);

        if (!Path.IsPathFullyQualified(directory))
            { throw new ArgumentException("RuntimeDirectory must be an absolute path.", nameof(options)); }

        RuntimeDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (!Directory.Exists(RuntimeDirectory))
            { throw new DirectoryNotFoundException($"Shared Runtime '{options.CompanyName}/{options.RuntimeName}/{options.RuntimeVersion}' is missing: {RuntimeDirectory}. Please install the Runtime-Installer."); }

        ArgumentNullException.ThrowIfNull(options.ManagedSubdirectories);

        _managedDirectories = [.. options.ManagedSubdirectories
            .Select(subdirectory => RuntimePaths.Child(RuntimeDirectory, subdirectory))
            .Select(Path.TrimEndingDirectorySeparator)
            .Distinct(OperatingSystem.IsWindows()                ? StringComparer.OrdinalIgnoreCase                : StringComparer.Ordinal)];

        if (_managedDirectories.Length == 0)
            { throw new ArgumentException("At least one managed search folder is required.", nameof(options)); }

        foreach (string path in _managedDirectories)
            {
            if (!Directory.Exists(path))
                { throw new DirectoryNotFoundException($"Searchfolder is missing: {path}"); }
            }

        string rid = "win-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        _nativeDirectories = [.. (options.NativeSubdirectories ??
            [$"runtimes/{rid}/native"])
            .Select(subdirectory => RuntimePaths.Child(RuntimeDirectory, subdirectory))
            .Select(Path.TrimEndingDirectorySeparator)
            .Distinct(OperatingSystem.IsWindows()                ? StringComparer.OrdinalIgnoreCase                : StringComparer.Ordinal)];

        _exact = options.RequireExactAssemblyVersion;
        _nativeForAll = options.ResolveNativeForAnyAssembly;
        _log = options.Log;
        _context = context;
        _context.Resolving += ResolveManaged;
        _context.ResolvingUnmanagedDll += ResolveNative;
        Log($"Activated: {RuntimeDirectory}; Context: {_context.Name ?? "Default"}");
        }

    #endregion Private Constructors

    #region Public Methods

    /// <summary>Validates configuration and attaches fallback handlers. Keep the result alive while using the sets.</summary>
    /// <param name="options">The runtime configuration.</param>
    /// <param name="context">The load context, or null for the default context.</param>
    public static SharedRuntimeResolver Attach(SharedRuntimeOptions options, AssemblyLoadContext? context = null)
        => new(options, context ?? AssemblyLoadContext.Default);

    /// <summary>Detaches handlers. Previously loaded assemblies and cached native libraries remain loaded.</summary>
    public void Dispose()
        {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            { return; }

        _context.Resolving -= ResolveManaged;
        _context.ResolvingUnmanagedDll -= ResolveNative;
        }

    /// <summary>Returns a validated path inside the runtime directory.</summary>
    /// <param name="relativeParts">The relative path components.</param>
    public string GetPath(params string[] relativeParts)
        {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(relativeParts);

        string relative = "";
        foreach (string part in relativeParts)
            {
            if (part is null || Path.IsPathRooted(part) || part.Contains(':') || part.StartsWith('\\'))
                { throw new ArgumentException("Only relative file paths are allowed.", nameof(relativeParts)); }

            relative = Path.Combine(relative, part.Replace('\\', Path.DirectorySeparatorChar));
            _ = RuntimePaths.Child(RuntimeDirectory, relative);
            }
        return RuntimePaths.Child(RuntimeDirectory, relative);
        }

    /// <summary>Loads or returns an assembly matching the requested identity.</summary>
    /// <param name="displayName">The requested assembly display name.</param>
    public Assembly LoadAssembly(string displayName)
        {
        ThrowIfDisposed();
        var requested = new AssemblyName(displayName);

        Assembly? loaded = _context.Assemblies.FirstOrDefault(assembly =>
             string.Equals(assembly.GetName().Name, requested.Name, StringComparison.OrdinalIgnoreCase) &&
             string.Equals(assembly.GetName().CultureName ?? "", requested.CultureName ?? "", StringComparison.OrdinalIgnoreCase));

        if (loaded is not null)
            {
            if (!Matches(requested, loaded.GetName()))
                { throw new FileLoadException($"Incompatible already loaded assembly: {loaded.FullName}; requested: {displayName}"); }

            return loaded;
            }
        return ResolveManaged(_context, requested) ?? throw new FileNotFoundException($"Assembly '{displayName}' is missing in the Runtime set.");
        }

    /// <summary>Loads a native Windows library from configured search directories.</summary>
    /// <param name="name">The native library name.</param>
    public nint LoadNativeLibrary(string name)
        {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!OperatingSystem.IsWindows())
            { throw new PlatformNotSupportedException("Native DLLs: only Windows."); }

        nint handle = FindNative(name);
        return handle != 0 ? handle : throw new DllNotFoundException($"Native DLL '{name}' is missing in: {string.Join("; ", _nativeDirectories)}");
        }

    #endregion Public Methods

    #region Private Methods

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern nint LoadLibraryExW(string fileName, nint file, uint flags);

    private nint FindNative(string name)
        {
        try
            {
            RuntimePaths.Segment(name, nameof(name));
            }
        catch (ArgumentException)
            { return 0; }

        string file = name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? name : name + ".dll";

        foreach (string directory in _nativeDirectories)
            {
            string path = Path.Combine(directory, file);
            if (!File.Exists(path))
                { continue; }

            // Multiple threads may load the same library concurrently before the
            // cache entry exists. Windows returns the same module handle and
            // increments its reference count, so this is safe without unloading.
            nint handle = LoadLibraryExW(path, 0, _loadLibrarySearchDllLoadDir | _loadLibrarySearchSystem32);

            if (handle == 0)
                {
                throw new DllNotFoundException($"Native DLL '{path}' or dependency not loadable.", new Win32Exception(Marshal.GetLastWin32Error()));
                }

            lock (_nativeGate)
                { _nativeHandles[path] = handle; }

            return handle;
            }

        return 0;
        }

    private bool IsInsideRuntimeDirectory(string path)
        {
        string fullPath = Path.GetFullPath(path);
        string prefix = Path.EndsInDirectorySeparator(RuntimeDirectory)
            ? RuntimeDirectory
            : RuntimeDirectory + Path.DirectorySeparatorChar;

        return fullPath.Equals(Path.TrimEndingDirectorySeparator(RuntimeDirectory), RuntimePaths.Comparison) ||
               fullPath.StartsWith(prefix, RuntimePaths.Comparison);
        }

    private void Log(string message)
        {
        try
            { _log?.Invoke(message); }
        catch { }
        }

    private bool Matches(AssemblyName requested, AssemblyName candidate)
        {
        if (!string.Equals(requested.Name, candidate.Name, StringComparison.OrdinalIgnoreCase) || !string.Equals(requested.CultureName ?? "", candidate.CultureName ?? "", StringComparison.OrdinalIgnoreCase))
            { return false; }

        byte[] token = requested.GetPublicKeyToken() ?? [];
        if (token.Length > 0 && !token.AsSpan().SequenceEqual(candidate.GetPublicKeyToken() ?? []))
            { return false; }

        if (requested.Version is null)
            { return true; }

        return candidate.Version is not null && (_exact ? candidate.Version.Equals(requested.Version) : candidate.Version.CompareTo(requested.Version) >= 0);
        }

    private Assembly? ResolveManaged(AssemblyLoadContext context, AssemblyName requested)
        {
        if (IsDisposed || string.IsNullOrWhiteSpace(requested.Name))
            { return null; }

        string culture = requested.CultureName ?? "";

        try
            {
            RuntimePaths.Segment(requested.Name, nameof(requested));

            if (culture.Length != 0)
                { RuntimePaths.Segment(culture, nameof(requested)); }
            }
        catch (ArgumentException)
            { return null; }

        foreach (string directory in _managedDirectories)
            {
            string path = culture.Length == 0 ? Path.Combine(directory, requested.Name + ".dll") : Path.Combine(directory, culture, requested.Name + ".dll");

            if (!File.Exists(path))
                { continue; }

            AssemblyName candidate;

            try
                {
                candidate = AssemblyName.GetAssemblyName(path);
                }
            catch (BadImageFormatException)
                {
                Log($"Not a managed assembly: {path}");
                continue;
                }

            if (!Matches(requested, candidate))
                {
                Log($"Identity/Version rejected: {path}; " + $"requested: {requested}; available: {candidate}");
                continue;
                }

            Log($"Managed: {requested} -> {path}");

            return context.LoadFromAssemblyPath(path);
            }

        return null;
        }

    private nint ResolveNative(Assembly requestingAssembly, string name)
        {
        if (IsDisposed || !OperatingSystem.IsWindows())
            { return 0; }

        if (!_nativeForAll)
            {
            if (requestingAssembly.IsDynamic ||
                string.IsNullOrEmpty(requestingAssembly.Location))
                { return 0; }

            if (!IsInsideRuntimeDirectory(requestingAssembly.Location))
                { return 0; }
            }

        return FindNative(name);
        }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);

    #endregion Private Methods
    }