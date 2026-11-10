// Copyright (C) 2026 wh7r4bb17.
// This file is part of wh7r4bb17.SharedRuntimes.
//
// wh7r4bb17.SharedRuntimes is licensed under the GNU General Public License version 3 only.
// See LICENSE.txt for details.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace wh7r4bb17.SharedRuntimes.Build;

/// <summary>Resolves compiler references and emits versioned runtime metadata.</summary>
public sealed class ResolveSharedRuntimeSets : Microsoft.Build.Utilities.Task
    {
    #region Fields
    private const int _compareBufferSize = 8192;

    private const string _defaultCompanyName = "company";
    private const string _defaultNativeMarker = "@default";
    private const string _metadataFormatVersion = "v2";

    private static readonly StringComparison _pathComparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static readonly Regex _reservedDeviceNameRegex = new(@"^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex _setIdRegex = new(@"^[A-Za-z0-9_.-]+\z", RegexOptions.CultureInvariant);

    private static readonly string[] _supportedModernNetFamilies =
    [
    "net8.0",
    "net9.0",
    "net10.0",
    "net11.0"
    ];

    private static readonly string[] _supportedNetStandardFrameworks =
    [
    "netstandard2.0",
    "netstandard2.1"
    ];

    #endregion Fields

    #region Properties

    /// <summary>Gets or sets the absolute Common Files build root.</summary>
    public string CommonFiles { get; set; } = "";

    /// <summary>Gets or sets the company directory name.</summary>
    public string CompanyName { get; set; } = _defaultCompanyName;

    /// <summary>Gets or sets the consumer framework used when a set has no override.</summary>
    public string ConsumerTargetFramework { get; set; } = "";

    /// <summary>Gets or sets whether missing installed files are tolerated during an IDE design-time build.</summary>
    public bool DesignTime { get; set; }

    /// <summary>Gets or sets patterns excluded from local copy and publish output.</summary>
    [Output]
    public ITaskItem[] ExcludeGlobs { get; set; } = [];

    /// <summary>Gets or sets generated assembly metadata.</summary>
    [Output]
    public ITaskItem[] Metadata { get; set; } = [];

    /// <summary>Gets or sets declared shared assembly references.</summary>
    public ITaskItem[] References { get; set; } = [];

    /// <summary>Gets or sets compiler references with resolved hint paths.</summary>
    [Output]
    public ITaskItem[] ResolvedReferences { get; set; } = [];

    /// <summary>Gets or sets an optional absolute directory override, bypassing conventional path construction.</summary>
    public string RuntimeDirectory { get; set; } = "";

    /// <summary>Gets or sets the runtime set declarations.</summary>
    public ITaskItem[] Sets { get; set; } = [];

    #endregion Properties

    #region Public Methods

    /// <summary>Resolves references, validates sets and emits versioned assembly metadata.</summary>
    public override bool Execute()
        {
        try
            {
            if (Sets.Length == 0)
                { throw new ArgumentException("At least one CompanySharedRuntimeSet is required."); }

            var sets = ParseSets();
            var references = ResolveReferences(sets);

            if (!DesignTime)
                {
                Validate(sets.Values);
                }

            ResolvedReferences = [.. references];
            Metadata = [.. BuildMetadata(sets.Values)];
            ExcludeGlobs = [.. BuildExcludeGlobs(sets.Values)];

            return true;
            }
        catch (Exception ex)
            {
            Log.LogError("wh7r4bb17.SharedRuntimes: " + ex.Message);
            return false;
            }
        }

    #endregion Public Methods

    #region Private Methods

    private static string Child(string root, string relative)
        {
        if (Path.IsPathRooted(relative) || relative.IndexOf(':') >= 0 || relative.StartsWith("\\", StringComparison.Ordinal))
            { throw new ArgumentException("Only relative subfolders allowed."); }

        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('\\', Path.DirectorySeparatorChar)));

        if (!path.Equals(root, _pathComparison) && !path.StartsWith(WithTrailingSeparator(root), _pathComparison))
            { throw new ArgumentException("Subfolder leaves the runtime set."); }

        return path;
        }

    private static bool EndsWithSeparator(string path) => path.Length > 0 && (path[path.Length - 1] == Path.DirectorySeparatorChar || path[path.Length - 1] == Path.AltDirectorySeparatorChar);

    private static bool FilesAreIdentical(string first, string second)
        {
        var firstInfo = new FileInfo(first);
        var secondInfo = new FileInfo(second);

        if (firstInfo.Length != secondInfo.Length)
            {
            return false;
            }

        using var firstStream = firstInfo.OpenRead();
        using var secondStream = secondInfo.OpenRead();
        return StreamsAreIdentical(firstStream, secondStream);
        }

    private static bool FullyQualified(string path)
        {
        if (string.IsNullOrWhiteSpace(path))
            { return false; }

        if (Path.DirectorySeparatorChar != '\\')
            { return path.StartsWith("/", StringComparison.Ordinal); }

        return path.StartsWith(@"\\", StringComparison.Ordinal) ||
               (path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/'));
        }

    private static bool IsModernNetFamily(string value, string family)
        {
        if (value.Equals(family, StringComparison.OrdinalIgnoreCase))
            {
            return true;
            }

        return value.Length > family.Length + 1 &&
               value.StartsWith(family, StringComparison.OrdinalIgnoreCase) &&
               value[family.Length] == '-';
        }

    private static bool IsSupportedFramework(string? framework)
        {
        if (framework is null)
            { return false; }

        var value = framework.Trim();

        if (value.Length == 0)
            { return false; }

        return _supportedNetStandardFrameworks.Contains(value, StringComparer.OrdinalIgnoreCase) ||
       _supportedModernNetFamilies.Any(family => IsModernNetFamily(value, family));
        }

    private static string NormalizeSubdirectory(string root, string subdirectory)
        {
        string path = Child(root, subdirectory);
        string prefix = WithTrailingSeparator(root);

        // Child guarantees that path is the root itself or lies beneath it.
        string relative = path.StartsWith(prefix, _pathComparison)
            ? path.Substring(prefix.Length)
            : string.Empty;

        relative = relative
            .Replace('\\', '/')
            .TrimEnd('/');

        return relative.Length == 0 ? "." : relative;
        }

    private static void Segment(string value, string label)
        {
        if (string.IsNullOrWhiteSpace(value))
            { throw new ArgumentException($"{label} must not be empty."); }

        if (value is "." or ".." ||
            value.IndexOfAny(['<', '>', ':', '"', '/', '\\', '|', '?', '*']) >= 0 ||
            value.Any(character => character < 32) ||
            value.EndsWith(".", StringComparison.Ordinal) ||
            value.EndsWith(" ", StringComparison.Ordinal) ||
            _reservedDeviceNameRegex.IsMatch(value.Split('.')[0]))
            {
            throw new ArgumentException($"{label} contains an invalid Windows path segment: '{value}'.");
            }
        }

    private static string[] Split(string value) =>
        [.. value
            .Split([';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)];

    private static bool StreamsAreIdentical(Stream first, Stream second)
        {
        var firstBuffer = new byte[_compareBufferSize];
        var secondBuffer = new byte[_compareBufferSize];

        while (true)
            {
            int firstRead = first.Read(firstBuffer, 0, firstBuffer.Length);
            int secondRead = second.Read(secondBuffer, 0, secondBuffer.Length);

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

    private static string TrimEndingDirectorySeparator(string path)
        {
        if (!EndsWithSeparator(path))
            { return path; }

        string root = Path.GetPathRoot(path) ?? string.Empty;

        if (path.Length <= root.Length)
            { return path; }

        return path.Substring(0, path.Length - 1);
        }

    private static void Validate(IEnumerable<Set> sets)
        {
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var set in sets)
            {
            if (!set.DefaultNative)
                {
                foreach (string nativeDirectory in set.Native.Select(subdirectory => Child(set.Directory, subdirectory)))
                    {
                    if (!Directory.Exists(nativeDirectory))
                        { throw new DirectoryNotFoundException($"Set '{set.Id}': Native search folder is missing: {nativeDirectory}"); }
                    }
                }

            foreach (string directory in set.Managed.Select(subdirectory => Child(set.Directory, subdirectory)))
                {
                if (!Directory.Exists(directory))
                    { throw new DirectoryNotFoundException($"Set '{set.Id}': Search folder is missing: {directory}"); }

                var files = Directory.EnumerateFiles(directory)
                    .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    .Concat(Directory.EnumerateDirectories(directory)
                    .SelectMany(childDirectory => Directory.EnumerateFiles(childDirectory, "*.resources.dll")));

                foreach (string file in files)
                    {
                    AssemblyName assemblyName;

                    try
                        {
                        assemblyName = AssemblyName.GetAssemblyName(file);
                        }
                    catch (BadImageFormatException)
                        {
                        continue;
                        }

                    string key = assemblyName.Name + "|" + (assemblyName.CultureName ?? "");

                    if (!seen.TryGetValue(key, out var previousFile))
                        {
                        seen.Add(key, file);
                        continue;
                        }

                    if (!FilesAreIdentical(previousFile, file))
                        {
                        throw new FileLoadException(
                            $"Assembly conflict '{key}': " +
                            $"{previousFile} and {file}. " +
                            "Only byte-for-byte duplicates are allowed.");
                        }
                    }
                }
            }
        }

    private static string WithTrailingSeparator(string path) => EndsWithSeparator(path) ? path : path + Path.DirectorySeparatorChar;

    private IEnumerable<ITaskItem> BuildExcludeGlobs(IEnumerable<Set> sets)
        {
        foreach (var set in sets)
            {
            yield return new TaskItem(set.Directory + "/**/*");
            }
        }

    private IEnumerable<ITaskItem> BuildMetadata(IEnumerable<Set> sets)
        {
        foreach (var set in sets)
            {
            static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

            string[] fields =
            [
                set.Company,
                set.Name,
                set.Version,
                string.Join("\n", set.Managed),
                set.DefaultNative ? _defaultNativeMarker : string.Join("\n", set.Native),
                set.Exact.ToString(),
                set.NativeAll.ToString(),
                set.Framework
            ];

            var entry = new TaskItem("wh7r4bb17.SharedRuntimes.Set." + set.Id);

            entry.SetMetadata("Value", _metadataFormatVersion + "|" + string.Join("|", fields.Select(Encode)));

            yield return entry;
            }
        }

    private Set ParseSet(ITaskItem item)
        {
        string id = item.ItemSpec;

        if (!_setIdRegex.IsMatch(id))
            { throw new ArgumentException($"Invalid set key '{id}'."); }

        string Get(string key, string fallback)
            {
            var value = item.GetMetadata(key)?.Trim();

            if (value is null || value.Length == 0)
                { return fallback; }

            return value;
            }

        bool Bool(string key, bool fallback)
            {
            var value = Get(key, fallback ? "true" : "false");

            if (bool.TryParse(value, out bool result))
                { return result; }

            throw new ArgumentException($"Set '{id}': {key} must be true or false.");
            }

        var set = new Set
            {
            Id = id,
            Company = Get("CompanyName", string.IsNullOrWhiteSpace(CompanyName) ? _defaultCompanyName : CompanyName.Trim()),
            Name = Get("RuntimeName", id),
            Version = Get("RuntimeVersion", ""),
            Framework = Get("TargetFramework", ConsumerTargetFramework)
            };

        Segment(set.Company, $"Set '{id}': CompanyName");
        Segment(set.Name, $"Set '{id}': RuntimeName");
        Segment(set.Version, $"Set '{id}': RuntimeVersion");
        Segment(set.Framework, $"Set '{id}': TargetFramework");

        if (!IsSupportedFramework(set.Framework))
            {
            throw new ArgumentException(
                $"Set '{id}': Unsupported target framework '{set.Framework}'. " +
                $"Supported frameworks are {string.Join(", ", _supportedNetStandardFrameworks)} " +
                $"or {string.Join(", ", _supportedModernNetFamilies)} " +
                "including platform-specific variants.");
            }

        string directory = Get("RuntimeDirectory", "");

        if (string.IsNullOrEmpty(directory) && !string.IsNullOrWhiteSpace(RuntimeDirectory))
            {
            if (Sets.Length != 1)
                { throw new ArgumentException("The global RuntimeDirectory override requires exactly one set."); }

            directory = RuntimeDirectory.Trim();
            }

        if (string.IsNullOrEmpty(directory))
            {
            if (!FullyQualified(CommonFiles))
                { throw new ArgumentException("Common Files is missing. Set RuntimeDirectory to the absolute build path for each set."); }

            directory = Path.Combine(
                CommonFiles,
                set.Company,
                set.Name,
                set.Version,
                set.Framework);
            }

        if (!FullyQualified(directory))
            {
            throw new ArgumentException($"Set '{id}': RuntimeDirectory must be an absolute path.");
            }

        set.Directory = TrimEndingDirectorySeparator(Path.GetFullPath(directory));

        set.Managed = [.. Split(Get("ManagedSubdirectories", "."))];

        if (set.Managed.Count == 0)
            { throw new ArgumentException($"Set '{id}': At least one search folder is required."); }

        set.DefaultNative = string.IsNullOrWhiteSpace(item.GetMetadata("NativeSubdirectories"));
        set.Native = [.. Split(Get("NativeSubdirectories", ""))
                .Select(subdirectory => NormalizeSubdirectory(set.Directory, subdirectory))
                .Distinct(StringComparer.OrdinalIgnoreCase)];

        if (!set.DefaultNative && set.Native.Length == 0)
            { throw new ArgumentException($"Set '{id}': NativeSubdirectories contains no folder."); }

        foreach (string subdirectory in set.Managed)
            {
            _ = Child(set.Directory, subdirectory);
            }

        set.Exact = Bool("RequireExactAssemblyVersion", true);
        set.NativeAll = Bool("ResolveNativeForAnyAssembly", false);

        return set;
        }

    private Dictionary<string, Set> ParseSets()
        {
        var sets = new Dictionary<string, Set>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in Sets)
            {
            var set = ParseSet(item);

            if (sets.ContainsKey(set.Id))
                { throw new ArgumentException($"Invalid or duplicate set key '{set.Id}'."); }

            sets.Add(set.Id, set);
            }

        return sets;
        }

    private List<ITaskItem> ResolveReferences(Dictionary<string, Set> sets)
        {
        var resolved = new List<ITaskItem>();
        var referenceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in References)
            {
            string name = reference.ItemSpec;

            Segment(name, "Reference name");

            if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                { throw new ArgumentException($"Specify the reference '{name}' without the .dll extension."); }

            if (!referenceNames.Add(name))
                { throw new ArgumentException($"Reference '{name}' has been declared multiple times."); }

            string setId = reference.GetMetadata("Set")?.Trim() ?? "";

            if (string.IsNullOrEmpty(setId))
                {
                if (sets.Count == 1)
                    {
                    setId = sets.Keys.Single();
                    }
                else
                    {
                    throw new ArgumentException($"Reference '{name}': Set metadata is required when multiple sets are configured.");
                    }
                }

            if (!sets.TryGetValue(setId, out var set))
                { throw new ArgumentException($"Reference '{name}': Unknown set '{setId}'."); }

            string subdirectory = reference.GetMetadata("Subdirectory")?.Trim() ?? "";
            string path = Path.Combine(Child(set.Directory, subdirectory), name + ".dll");

            if (!DesignTime)
                {
                if (!File.Exists(path))
                    { throw new FileNotFoundException($"Shared DLL for set '{setId}' is missing: {path}"); }

                if (!string.Equals(AssemblyName.GetAssemblyName(path).Name, name, StringComparison.OrdinalIgnoreCase))
                    { throw new ArgumentException($"File and assembly names do not match: {path}"); }
                }

            set.Managed.Add(string.IsNullOrEmpty(subdirectory) ? "." : subdirectory);

            var output = new TaskItem(name);

            output.SetMetadata("HintPath", path);
            output.SetMetadata("Private", "false");
            output.SetMetadata("CompanySharedSet", set.Id);

            resolved.Add(output);
            }

        foreach (var set in sets.Values)
            {
            set.Managed = [.. set.Managed
                .Select(subdirectory => NormalizeSubdirectory(set.Directory, subdirectory))
                .Distinct(StringComparer.OrdinalIgnoreCase)];
            }

        return resolved;
        }

    #endregion Private Methods

    #region Classes

    private sealed class Set
        {
        #region Fields
        internal string Company = "";
        internal bool DefaultNative;
        internal string Directory = "";
        internal bool Exact;
        internal string Framework = "";
        internal string Id = "";
        internal List<string> Managed = [];
        internal string Name = "";
        internal string[] Native = [];
        internal bool NativeAll;
        internal string Version = "";

        #endregion Fields
        }

    #endregion Classes
    }