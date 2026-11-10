using wh7r4bb17.SharedRuntimes;
using System.Reflection;
using Runtime = wh7r4bb17.SharedRuntimes.SharedRuntime;

namespace SmokeTest;

/// <summary>Runs the end-to-end checks against a staged runtime set.</summary>
internal sealed class SmokeTestRunner
    {
    #region Fields

    private const string _fixtureAssemblyFile = "wh7r4bb17.SharedRuntimes.TestFixture.dll";
    private const string _fixtureTypeName = "wh7r4bb17.SharedRuntimes.TestFixture.Marker, wh7r4bb17.SharedRuntimes.TestFixture";
    private const string _mockDirectoryKey = "SmokeSetDirectory";

    private int _failures;
    private bool _initialized;

    #endregion Fields

    #region Public Methods

    /// <summary>Runs all checks and returns 0 on success, otherwise 1.</summary>
    public int Run()
        {
        try
            {
            string setDirectory = ReadMockDirectory();
            var sets = ReadBuildMetadata();

            InitializeRuntime(sets, setDirectory);
            CheckNotCopiedLocally();
            CheckSharedType(setDirectory);
            }
        catch (Exception ex)
            { Check("Unexpected exception", false, ex.ToString()); }
        finally
            { ShutdownRuntime(); }

        Console.WriteLine(_failures == 0
            ? "\nSmoke test passed."
            : $"\nSmoke test failed ({_failures} check(s)).");

        return _failures == 0 ? 0 : 1;
        }

    #endregion Public Methods

    #region Private Methods

    private void Check(string name, bool ok, string detail = "")
        {
        Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine($"[{(ok ? "OK" : "FAIL")}] {name} {detail}");
        Console.ResetColor();

        if (!ok)
            { _failures++; }
        }

    private void CheckNotCopiedLocally()
        {
        string localCopy = Path.Combine(AppContext.BaseDirectory, _fixtureAssemblyFile);
        Check("Shared DLL is not copied to the output folder", !File.Exists(localCopy));
        }

    private void CheckSharedType(string setDirectory)
        {
        var markerType = Type.GetType(_fixtureTypeName);
        Check("Type resolved from the shared set", markerType is not null);

        if (markerType is null)
            { return; }

        string? value = markerType.GetProperty("Value")?.GetValue(null)?.ToString();
        Check("Marker.Value", value == "original", value ?? "null");

        Check("Assembly was loaded from the set directory", markerType.Assembly.Location.StartsWith(setDirectory, StringComparison.OrdinalIgnoreCase), markerType.Assembly.Location);
        }

    private void InitializeRuntime(IReadOnlyList<SharedRuntimeSet> sets, string setDirectory)
        {
        Runtime.Initialize(sets.Select(set => set with
            {
            Options = set.Options with
                {
                RuntimeDirectory = setDirectory,
                Log = message => Console.WriteLine($"   [Resolver] {message}")
                }
            }));

        _initialized = true;
        Check("Runtime initialized", true);
        }

    private IReadOnlyList<SharedRuntimeSet> ReadBuildMetadata()
        {
        var sets = SharedRuntimeOptions.FromAssemblyAll(Assembly.GetExecutingAssembly());

        Check("Single set read from build metadata", sets.Count == 1 && sets[0].Id == "TestSet");
        Check("Set framework", sets[0].Options.TargetFramework == "net10.0", sets[0].Options.TargetFramework);

        return sets;
        }

    private string ReadMockDirectory()
        {
        string? directory = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == _mockDirectoryKey)?.Value;

        Check("Mock directory found in assembly metadata", !string.IsNullOrEmpty(directory), directory ?? "");

        return string.IsNullOrEmpty(directory)
            ? throw new InvalidOperationException($"{_mockDirectoryKey} is missing in the assembly metadata.")
            : directory;
        }

    private void ShutdownRuntime()
        {
        if (_initialized)
            { Runtime.Registration.Dispose(); }
        }

    #endregion Private Methods
    }