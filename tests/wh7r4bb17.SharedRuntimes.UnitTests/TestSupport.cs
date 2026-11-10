using Microsoft.Build.Framework;
using System.Reflection.Emit;
using System.Text;

namespace wh7r4bb17.SharedRuntimes.Tests;

internal static class MetadataAssembly
    {
    #region Internal Methods

    internal static Assembly Create(params (string Key, string Value)[] entries)
        {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Metadata_" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.RunAndCollect);
        var constructor = typeof(AssemblyMetadataAttribute).GetConstructor([typeof(string), typeof(string)])!;
        foreach (var (Key, Value) in entries)
            { assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, [Key, Value])); }

        return assembly;
        }

    internal static string Payload(string framework = "netstandard2.0", bool legacy = false, string native = "@default", string exact = "True")
        {
        var fields = new List<string> { "company", "Logging", "1.0.8", ".\nlib", native, exact, "False" };
        if (!legacy)
            { fields.Add(framework); }

        return (legacy ? "v1|" : "v2|") + string.Join("|", fields.Select(x => Convert.ToBase64String(Encoding.UTF8.GetBytes(x))));
        }

    #endregion Internal Methods
    }

internal sealed class TempDirectory : IDisposable
    {
    #region Properties
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wh7r4bb17.SharedRuntimes.Tests", Guid.NewGuid().ToString("N"));

    #endregion Properties

    #region Internal Constructors

    internal TempDirectory() => Directory.CreateDirectory(Root);

    #endregion Internal Constructors

    #region Public Methods

    public void Dispose()
        {
        try
            { Directory.Delete(Root, true); }
        catch (IOException)
            { System.Diagnostics.Trace.WriteLine("Fixture cleanup deferred: " + Root); }
        catch (UnauthorizedAccessException)
            { System.Diagnostics.Trace.WriteLine("Fixture cleanup deferred: " + Root); }
        }

    #endregion Public Methods

    #region Internal Methods

    internal static string CopyFixture(string directory, bool conflict = false)
        {
        const string name = "wh7r4bb17.SharedRuntimes.TestFixture.dll";
        string source = Path.Combine(AppContext.BaseDirectory, "fixtures", conflict ? "conflict" : "original", name);
        if (!File.Exists(source))
            { throw new FileNotFoundException("Build the test project to stage its fixture assemblies.", source); }

        string destination = Path.Combine(directory, name);
        File.Copy(source, destination, true);
        return destination;
        }

    internal string Create(string relative)
        {
        string path = Path.Combine(Root, relative);
        Directory.CreateDirectory(path);
        return path;
        }

    internal SharedRuntimeOptions Options(string relative = "set") => new()
        {
        RuntimeVersion = "1.0.0",
        RuntimeDirectory = Create(relative)
        };

    #endregion Internal Methods
    }

internal sealed class RecordingBuildEngine : IBuildEngine
    {
    #region Properties
    public int ColumnNumberOfTaskNode => 0;
    public bool ContinueOnError => false;
    public int LineNumberOfTaskNode => 0;
    public string ProjectFileOfTaskNode => "UnitTest.proj";
    internal List<BuildErrorEventArgs> Errors { get; } = [];

    #endregion Properties

    #region Public Methods

    public bool BuildProjectFile(string projectFileName, string[] targetNames, System.Collections.IDictionary globalProperties, System.Collections.IDictionary targetOutputs) => throw new NotSupportedException();

    public void LogCustomEvent(CustomBuildEventArgs e)
        { }

    public void LogErrorEvent(BuildErrorEventArgs e) => Errors.Add(e);

    public void LogMessageEvent(BuildMessageEventArgs e)
        { }

    public void LogWarningEvent(BuildWarningEventArgs e)
        { }

    #endregion Public Methods
    }