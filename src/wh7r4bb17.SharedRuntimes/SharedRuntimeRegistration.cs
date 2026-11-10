// Copyright (C) 2026 wh7r4bb17.
// This file is part of wh7r4bb17.SharedRuntimes.
//
// wh7r4bb17.SharedRuntimes is licensed under the GNU General Public License version 3 only.
// See LICENSE.txt for details.

using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;

namespace wh7r4bb17.SharedRuntimes;

/// <summary>Owns the validated runtime set resolvers.</summary>
public sealed class SharedRuntimeRegistration : IDisposable
    {
    #region Fields
    private const int _compareBufferSize = 8192;

    private static readonly Regex _setIdRegex = new(@"^[A-Za-z0-9_.-]+\z", RegexOptions.CultureInvariant);

    private readonly IReadOnlyDictionary<string, SharedRuntimeResolver> _runtimes;
    private int _disposed;

    #endregion Fields

    #region Properties

    /// <summary>Gets whether this instance has been disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Gets registered resolvers indexed by case-insensitive set identifiers.</summary>
    public IReadOnlyDictionary<string, SharedRuntimeResolver> Runtimes
        {
        get
            {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return _runtimes;
            }
        }

    #endregion Properties

    #region Private Constructors

    private SharedRuntimeRegistration(Dictionary<string, SharedRuntimeResolver> runtimes)
        => _runtimes = new ReadOnlyDictionary<string, SharedRuntimeResolver>(runtimes);

    #endregion Private Constructors

    #region Public Methods

    /// <summary>Validates configuration and attaches fallback handlers. Keep the result alive while using the sets.</summary>
    /// <param name="sets">The runtime sets to register.</param>
    /// <param name="context">The load context, or null for the default context.</param>
    public static SharedRuntimeRegistration Attach(IEnumerable<SharedRuntimeSet> sets, AssemblyLoadContext? context = null)
        {
        ArgumentNullException.ThrowIfNull(sets);
        var list = sets.ToArray();
        if (list.Length == 0)
            { throw new ArgumentException("At least one set is required.", nameof(sets)); }

        ValidateSetIds(list);
        ValidateConflicts(list);

        var attached = new Dictionary<string, SharedRuntimeResolver>(StringComparer.OrdinalIgnoreCase);

        try
            {
            foreach (var set in list)
                { attached.Add(set.Id, SharedRuntimeResolver.Attach(set.Options, context)); }
            return new(attached);
            }
        catch
            {
            foreach (var resolver in attached.Values.Reverse())
                {
                resolver.Dispose();
                }
            throw;
            }
        }

    /// <summary>Detaches handlers. Previously loaded assemblies and cached native libraries remain loaded.</summary>
    public void Dispose()
        {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            { return; }
        foreach (var resolver in _runtimes.Values.Reverse())
            { resolver.Dispose(); }
        }

    /// <summary>Returns the resolver for the specified set identifier.</summary>
    /// <param name="setId">The logical set identifier.</param>
    public SharedRuntimeResolver GetRuntime(string setId)
        {
        ArgumentException.ThrowIfNullOrWhiteSpace(setId);
        return Runtimes.TryGetValue(setId, out var resolver) ? resolver : throw new KeyNotFoundException($"Runtime set '{setId}' is unknown. Active: {string.Join(", ", Runtimes.Keys)}");
        }

    #endregion Public Methods

    #region Private Methods

    private static bool FilesAreIdentical(string first, string second)
        {
        var firstInfo = new FileInfo(first);
        var secondInfo = new FileInfo(second);

        if (firstInfo.Length != secondInfo.Length)
            { return false; }

        using var firstStream = firstInfo.OpenRead();
        using var secondStream = secondInfo.OpenRead();

        var firstBuffer = new byte[_compareBufferSize];
        var secondBuffer = new byte[_compareBufferSize];

        while (true)
            {
            int firstRead = firstStream.Read(firstBuffer, 0, firstBuffer.Length);
            int secondRead = secondStream.Read(secondBuffer, 0, secondBuffer.Length);

            if (firstRead != secondRead)
                { return false; }

            if (firstRead == 0)
                { return true; }

            for (int index = 0; index < firstRead; index++)
                {
                if (firstBuffer[index] != secondBuffer[index])
                    { return false; }
                }
            }
        }

    private static void ValidateConflicts(IReadOnlyList<SharedRuntimeSet> sets)
        {
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var set in sets)
            {
            var options = set.Options;

            RuntimePaths.Segment(options.CompanyName, nameof(options.CompanyName));
            RuntimePaths.Segment(options.RuntimeName, nameof(options.RuntimeName));
            RuntimePaths.Segment(options.RuntimeVersion, nameof(options.RuntimeVersion));

            string root = options.RuntimeDirectory ??
                SharedRuntimePaths.GetRuntimeDirectory(
                    options.CompanyName,
                    options.RuntimeName,
                    options.RuntimeVersion,
                    targetFramework: options.TargetFramework);

            if (!Path.IsPathFullyQualified(root))
                { throw new ArgumentException("RuntimeDirectory must be an absolute path.", nameof(sets)); }

            root = Path.GetFullPath(root);

            if (!Directory.Exists(root))
                { throw new DirectoryNotFoundException($"Runtime set '{set.Id}' is missing: {root}"); }

            ArgumentNullException.ThrowIfNull(options.ManagedSubdirectories);

            foreach (string relative in options.ManagedSubdirectories)
                {
                string directory = RuntimePaths.Child(root, relative);

                if (!Directory.Exists(directory))
                    { throw new DirectoryNotFoundException($"Set '{set.Id}': Search folder is missing: {directory}"); }

                var files = Directory.EnumerateFiles(directory)
                    .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    .Concat(Directory.EnumerateDirectories(directory)
                    .SelectMany(childDirectory => Directory.EnumerateFiles(childDirectory, "*.resources.dll")));

                foreach (string path in files)
                    {
                    AssemblyName assemblyName;

                    try
                        {
                        assemblyName = AssemblyName.GetAssemblyName(path);
                        }
                    catch (BadImageFormatException)
                        { continue; }

                    string key = assemblyName.Name + "|" + (assemblyName.CultureName ?? "");

                    if (!seen.TryGetValue(key, out string? previousPath))
                        {
                        seen.Add(key, path);
                        continue;
                        }

                    if (string.Equals(previousPath, path, RuntimePaths.Comparison))
                        { continue; }

                    if (!FilesAreIdentical(previousPath, path))
                        {
                        throw new FileLoadException(
                            $"Assembly conflict '{key}': " +
                            $"{previousPath} and {path}. " +
                            "Only byte-identical duplicates are allowed.");
                        }
                    }
                }
            }
        }

    private static void ValidateSetIds(IReadOnlyList<SharedRuntimeSet> sets)
        {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var set in sets)
            {
            ArgumentNullException.ThrowIfNull(set);
            ArgumentNullException.ThrowIfNull(set.Options);

            if (string.IsNullOrWhiteSpace(set.Id))
                { throw new ArgumentException("Set ID is required.", nameof(sets)); }

            if (!_setIdRegex.IsMatch(set.Id))
                { throw new ArgumentException($"Invalid set key '{set.Id}'.", nameof(sets)); }

            if (!ids.Add(set.Id))
                { throw new ArgumentException($"Duplicate set key '{set.Id}'.", nameof(sets)); }
            }
        }

    #endregion Private Methods
    }