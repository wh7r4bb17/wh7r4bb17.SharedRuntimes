using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using wh7r4bb17.SharedRuntimes.Build;

namespace wh7r4bb17.SharedRuntimes.Tests;

public sealed class BuildTaskTests
    {
    #region Public Methods

    [Fact]
    public void Execute_DuplicateSetIds_ReturnsFalse()
        {
        using var temp = new TempDirectory();
        Assert.False(Create(temp.Root, [Set("Logging"), Set("LOGGING")]).Execute());
        }

    [Theory]
    [InlineData("netstandard2.0")]
    [InlineData("netstandard2.1")]
    [InlineData("net10.0")]
    public void Execute_ExplicitSetFramework_OverridesConsumerAndRoundTrips(string framework)
        {
        using var temp = new TempDirectory();
        var task = Create(temp.Root, [Set(framework: framework)]);
        task.References = [new TaskItem("wh7r4bb17.Logging")];
        Assert.True(task.Execute());
        var reference = Assert.Single(task.ResolvedReferences);
        Assert.Equal(Path.Combine(temp.Root, "company", "Logging", "1.0.8", framework, "wh7r4bb17.Logging.dll"), reference.GetMetadata("HintPath"));
        Assert.Equal("false", reference.GetMetadata("Private"));
        var metadata = Assert.Single(task.Metadata);
        Assert.StartsWith("v2|", metadata.GetMetadata("Value"));
        var assembly = MetadataAssembly.Create((metadata.ItemSpec, metadata.GetMetadata("Value")));
        Assert.Equal(framework, SharedRuntimeOptions.FromAssembly(assembly).TargetFramework);
        }

    [Fact]
    public void Execute_GlobalCompanyName_IsUsed()
        {
        using var temp = new TempDirectory();
        var task = Create(temp.Root); task.CompanyName = "OtherCompany";
        Assert.True(task.Execute());
        var entry = Assert.Single(task.Metadata);
        Assert.Equal("OtherCompany", SharedRuntimeOptions.FromAssembly(MetadataAssembly.Create((entry.ItemSpec, entry.GetMetadata("Value")))).CompanyName);
        }

    [Fact]
    public void Execute_GlobalDirectoryOverride_IsAmbiguousWithMultipleSets()
        {
        using var temp = new TempDirectory();
        var task = Create(temp.Root, [Set("A"), Set("B")]); task.RuntimeDirectory = temp.Root;
        Assert.False(task.Execute());
        }

    [Fact]
    public void Execute_GlobalDirectoryOverride_UsedForOneSet()
        {
        using var temp = new TempDirectory();
        var task = Create(temp.Root); task.RuntimeDirectory = temp.Create("custom");
        task.References = [new TaskItem("Example")];
        Assert.True(task.Execute());
        Assert.Equal(Path.Combine(task.RuntimeDirectory, "Example.dll"), Assert.Single(task.ResolvedReferences).GetMetadata("HintPath"));
        }

    [Theory]
    [InlineData("netstandard")]
    [InlineData("../net10.0")]
    [InlineData("C:\\net10.0")]
    public void Execute_InvalidFramework_ReturnsFalse(string framework)
        {
        using var temp = new TempDirectory();
        Assert.False(Create(temp.Root, [Set(framework: framework)]).Execute());
        }

    [Fact]
    public void Execute_MultipleSets_UseIndependentFrameworks()
        {
        using var temp = new TempDirectory();
        var task = Create(temp.Root, [Set("Logging", "netstandard2.0"), Set("Business", "net10.0")]);
        var first = new TaskItem("Logging.Api"); first.SetMetadata("Set", "Logging");
        var second = new TaskItem("Business.Api"); second.SetMetadata("Set", "Business");
        task.References = [first, second];
        Assert.True(task.Execute());
        Assert.Equal(2, task.ResolvedReferences.Length);
        var decoded = SharedRuntimeOptions.FromAssemblyAll(MetadataAssembly.Create([.. task.Metadata.Select(x => (x.ItemSpec, x.GetMetadata("Value")))]));
        Assert.Equal("netstandard2.0", decoded.Single(x => x.Id == "Logging").Options.TargetFramework);
        Assert.Equal("net10.0", decoded.Single(x => x.Id == "Business").Options.TargetFramework);
        }

    [Fact]
    public void Execute_MultipleSetsWithoutReferenceSet_LogsError()
        {
        using var temp = new TempDirectory();
        var task = Create(temp.Root, [Set("A"), Set("B")]);
        task.References = [new TaskItem("Example")];
        Assert.False(task.Execute());
        Assert.Contains(((RecordingBuildEngine)task.BuildEngine).Errors, x => x.Message?.Contains("Set metadata") == true);
        }

    [Fact]
    public void Execute_NoOverride_UsesConsumerFramework()
        {
        using var temp = new TempDirectory();
        var task = Create(temp.Root);
        task.References = [new TaskItem("wh7r4bb17.Logging")];
        Assert.True(task.Execute());
        Assert.Equal(Path.Combine(temp.Root, "company", "Logging", "1.0.8", "net10.0-windows", "wh7r4bb17.Logging.dll"), Assert.Single(task.ResolvedReferences).GetMetadata("HintPath"));
        }

    [Fact]
    public void Execute_NormalBuild_AcceptsRealFixtureAndEmitsExclusion()
        {
        using var temp = new TempDirectory();
        string directory = temp.Create(Path.Combine("company", "Logging", "1.0.8", "netstandard2.0"));
        TempDirectory.CopyFixture(directory);
        var task = Create(temp.Root, [Set(framework: "netstandard2.0")]); task.DesignTime = false;
        task.References = [new TaskItem("wh7r4bb17.SharedRuntimes.TestFixture")];
        Assert.True(task.Execute());
        Assert.Single(task.ResolvedReferences);
        Assert.Equal(directory + "/**/*", Assert.Single(task.ExcludeGlobs).ItemSpec);
        }

    [Fact]
    public void Execute_NormalBuild_RejectsMissingReference()
        {
        using var temp = new TempDirectory();
        var task = Create(temp.Root); task.DesignTime = false;
        task.References = [new TaskItem("Missing")];
        Assert.False(task.Execute());
        Assert.NotEmpty(((RecordingBuildEngine)task.BuildEngine).Errors);
        }

    [Fact]
    public void Execute_NoSets_ReturnsFalseAndLogsError()
        {
        using var temp = new TempDirectory();
        var task = Create(temp.Root, []);
        Assert.False(task.Execute());
        Assert.NotEmpty(((RecordingBuildEngine)task.BuildEngine).Errors);
        }

    [Fact]
    public void Execute_PerSetCompany_OverridesGlobalCompany()
        {
        using var temp = new TempDirectory();
        var set = Set(); set.SetMetadata("CompanyName", "ProductVendor");
        var task = Create(temp.Root, [set]); task.CompanyName = "GlobalVendor";
        Assert.True(task.Execute());
        var entry = Assert.Single(task.Metadata);
        Assert.Equal("ProductVendor", SharedRuntimeOptions.FromAssembly(MetadataAssembly.Create((entry.ItemSpec, entry.GetMetadata("Value")))).CompanyName);
        }

    #endregion Public Methods

    #region Private Methods

    private static ResolveSharedRuntimeSets Create(string commonFiles, ITaskItem[]? sets = null) => new()
        {
        BuildEngine = new RecordingBuildEngine(),
        CommonFiles = commonFiles,
        ConsumerTargetFramework = "net10.0-windows",
        CompanyName = "company",
        Sets = sets ?? [Set()],
        DesignTime = true
        };

    private static TaskItem Set(string id = "Logging", string? framework = null)
        {
        var item = new TaskItem(id);
        item.SetMetadata("RuntimeName", id);
        item.SetMetadata("RuntimeVersion", "1.0.8");
        if (framework is not null)
            { item.SetMetadata("TargetFramework", framework); }
        return item;
        }

    #endregion Private Methods
    }