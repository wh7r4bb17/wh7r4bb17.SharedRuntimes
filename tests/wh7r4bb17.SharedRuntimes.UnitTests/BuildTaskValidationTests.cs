using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using wh7r4bb17.SharedRuntimes.Build;

namespace wh7r4bb17.SharedRuntimes.Tests;

public sealed class BuildTaskValidationTests
    {
    #region Fields
    private const string _fixture = "wh7r4bb17.SharedRuntimes.TestFixture";

    #endregion Fields

    #region Helpers

    private static ResolveSharedRuntimeSets Create(TempDirectory temp, bool designTime, params ITaskItem[] sets) => new()
        {
        BuildEngine = new RecordingBuildEngine(),
        CommonFiles = temp.Root,
        ConsumerTargetFramework = "net10.0-windows",
        CompanyName = "company",
        Sets = sets.Length == 0 ? [Set()] : sets,
        DesignTime = designTime
        };

    private static SharedRuntimeOptions DecodeSingle(ResolveSharedRuntimeSets task)
        {
        var entry = Assert.Single(task.Metadata);
        return SharedRuntimeOptions.FromAssembly(MetadataAssembly.Create((entry.ItemSpec, entry.GetMetadata("Value"))));
        }

    private static List<string> Errors(ResolveSharedRuntimeSets task) =>
        [.. ((RecordingBuildEngine)task.BuildEngine).Errors.Select(error => error.Message ?? "")];

    private static string ExpectedPath(TempDirectory temp, string framework, params string[] parts) =>
        Path.Combine([temp.Root, "company", "Logging", "1.0.8", framework, .. parts]);

    private static TaskItem Reference(string name, string? set = null, string? subdirectory = null)
        {
        var item = new TaskItem(name);
        if (set is not null)
            { item.SetMetadata("Set", set); }
        if (subdirectory is not null)
            { item.SetMetadata("Subdirectory", subdirectory); }
        return item;
        }

    private static TaskItem Set(string id = "Logging", string? framework = "netstandard2.0", params (string Key, string Value)[] metadata)
        {
        var item = new TaskItem(id);
        item.SetMetadata("RuntimeName", id);
        item.SetMetadata("RuntimeVersion", "1.0.8");
        if (framework is not null)
            { item.SetMetadata("TargetFramework", framework); }
        foreach (var (key, value) in metadata)
            { item.SetMetadata(key, value); }
        return item;
        }

    private static string SetDirectory(string id = "Logging", string framework = "netstandard2.0") =>
        Path.Combine("company", id, "1.0.8", framework);

    private static TaskItem SetWith(string key, string value) => Set("Logging", "netstandard2.0", (key, value));

    #endregion Helpers

    #region Framework validation

    [Fact]
    public void Execute_FrameworkWithSurroundingWhitespace_IsTrimmed()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, Set("Logging", "  net10.0  "));
        task.References = [Reference("Example")];
        Assert.True(task.Execute());
        Assert.Equal(ExpectedPath(temp, "net10.0", "Example.dll"), Assert.Single(task.ResolvedReferences).GetMetadata("HintPath"));
        }

    [Fact]
    public void Execute_NoFrameworkAnywhere_ReturnsFalse()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, Set("Logging", null));
        task.ConsumerTargetFramework = "";
        Assert.False(task.Execute());
        Assert.Contains("TargetFramework", Assert.Single(Errors(task)));
        }

    [Theory]
    [InlineData("netstandard2.0")]
    [InlineData("netstandard2.1")]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData("net10.0")]
    [InlineData("net11.0")]
    [InlineData("net10.0-windows")]
    [InlineData("net10.0-windows10.0.19041.0")]
    [InlineData("net11.0-windows")]
    [InlineData("NET10.0")]
    public void Execute_SupportedFramework_Succeeds(string framework)
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, Set("Logging", framework));
        Assert.True(task.Execute());
        Assert.Equal(framework, DecodeSingle(task).TargetFramework);
        }

    [Fact]
    public void Execute_UnsupportedFramework_MessageListsSupportedFrameworks()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, Set("Logging", "net7.0"));
        Assert.False(task.Execute());
        string message = Assert.Single(Errors(task));
        Assert.StartsWith("wh7r4bb17.SharedRuntimes:", message);
        Assert.Contains("netstandard2.0", message);
        Assert.Contains("netstandard2.1", message);
        Assert.Contains("net8.0", message);
        Assert.Contains("net11.0", message);
        }

    [Theory]
    [InlineData("netstandard")]
    [InlineData("netstandard1.6")]
    [InlineData("netstandard2.0-windows")]
    [InlineData("net")]
    [InlineData("net7.0")]
    [InlineData("net48")]
    [InlineData("netfoo")]
    [InlineData("net10.0-")]
    [InlineData("net10.0abc")]
    [InlineData("net10.00")]
    [InlineData("net10.0.1")]
    public void Execute_UnsupportedFramework_ReturnsFalse(string framework)
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, Set("Logging", framework));
        Assert.False(task.Execute());
        Assert.Contains("Unsupported target framework", Assert.Single(Errors(task)));
        }

    #endregion Framework validation

    #region Set and segment validation

    [Fact]
    public void Execute_EmptyGlobalCompanyName_FallsBackToDefault()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true);
        task.CompanyName = "";
        task.References = [Reference("Example")];
        Assert.True(task.Execute());
        Assert.Equal("company", DecodeSingle(task).CompanyName);
        Assert.Equal(ExpectedPath(temp, "netstandard2.0", "Example.dll"), Assert.Single(task.ResolvedReferences).GetMetadata("HintPath"));
        }

    [Theory]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a:b")]
    [InlineData("CON")]
    [InlineData("nul.txt")]
    [InlineData("invalid.")]
    public void Execute_InvalidRuntimeName_ReturnsFalse(string runtimeName)
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, SetWith("RuntimeName", runtimeName));
        Assert.False(task.Execute());
        Assert.Contains("RuntimeName", Assert.Single(Errors(task)));
        }

    [Theory]
    [InlineData("a b")]
    [InlineData("a/b")]
    [InlineData("ä")]
    [InlineData("Logging\n")]
    public void Execute_InvalidSetKey_ReturnsFalse(string id)
        {
        using var temp = new TempDirectory();
        Assert.False(Create(temp, true, Set(id)).Execute());
        }

    [Fact]
    public void Execute_MissingRuntimeVersion_NamesTheField()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, Set("Logging", "netstandard2.0", ("RuntimeVersion", "")));
        Assert.False(task.Execute());
        Assert.Contains("RuntimeVersion", Assert.Single(Errors(task)));
        }

    [Fact]
    public void Execute_RuntimeNameWithTrailingWhitespace_IsTrimmed()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, SetWith("RuntimeName", "Logging  "));
        task.References = [Reference("Example")];
        Assert.True(task.Execute());
        Assert.Equal(ExpectedPath(temp, "netstandard2.0", "Example.dll"), Assert.Single(task.ResolvedReferences).GetMetadata("HintPath"));
        }

    #endregion Set and segment validation

    #region Reference validation

    [Fact]
    public void Execute_DuplicateReferenceIgnoringCase_ReturnsFalse()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true);
        task.References = [Reference("Example"), Reference("EXAMPLE")];
        Assert.False(task.Execute());
        Assert.Contains("multiple times", Assert.Single(Errors(task)));
        }

    [Fact]
    public void Execute_InvalidReferenceName_ReturnsFalse()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true);
        task.References = [Reference("a/b")];
        Assert.False(task.Execute());
        }

    [Fact]
    public void Execute_ReferenceSetLookup_IgnoresCaseAndEmitsCanonicalId()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true);
        task.References = [Reference("Example", set: "logging")];
        Assert.True(task.Execute());
        Assert.Equal("Logging", Assert.Single(task.ResolvedReferences).GetMetadata("CompanySharedSet"));
        }

    [Fact]
    public void Execute_ReferenceSubdirectory_IsDeduplicatedAgainstConfiguredDirectories()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, SetWith("ManagedSubdirectories", "lib"));
        task.References = [Reference("Example", subdirectory: "./lib")];
        Assert.True(task.Execute());
        Assert.Equal("lib", string.Join("|", DecodeSingle(task).ManagedSubdirectories!));
        }

    [Fact]
    public void Execute_ReferenceSubdirectory_IsResolvedAndAddedToManagedDirectories()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true);
        task.References = [Reference("Example", subdirectory: "lib")];
        Assert.True(task.Execute());
        Assert.Equal(ExpectedPath(temp, "netstandard2.0", "lib", "Example.dll"), Assert.Single(task.ResolvedReferences).GetMetadata("HintPath"));
        Assert.Equal(".|lib", string.Join("|", DecodeSingle(task).ManagedSubdirectories!));
        }

    [Theory]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData("C:\\escape")]
    public void Execute_ReferenceSubdirectoryEscapingSet_ReturnsFalse(string subdirectory)
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true);
        task.References = [Reference("Example", subdirectory: subdirectory)];
        Assert.False(task.Execute());
        }

    [Fact]
    public void Execute_ReferenceToUnknownSet_ReturnsFalse()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true);
        task.References = [Reference("Example", set: "Missing")];
        Assert.False(task.Execute());
        Assert.Contains("Unknown set", Assert.Single(Errors(task)));
        }

    [Fact]
    public void Execute_ReferenceWithDllExtension_ReturnsFalse()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true);
        task.References = [Reference("Example.dll")];
        Assert.False(task.Execute());
        Assert.Contains("without the .dll", Assert.Single(Errors(task)));
        }

    #endregion Reference validation

    #region Managed subdirectories

    [Fact]
    public void Execute_DefaultManagedSubdirectories_AreTheSetRoot()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true);
        Assert.True(task.Execute());
        Assert.Equal(".", string.Join("|", DecodeSingle(task).ManagedSubdirectories!));
        }

    [Fact]
    public void Execute_EmptyManagedSubdirectoryList_ReturnsFalse()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, SetWith("ManagedSubdirectories", ";"));
        Assert.False(task.Execute());
        Assert.Contains("search folder", Assert.Single(Errors(task)));
        }

    [Theory]
    [InlineData("lib", "lib")]
    [InlineData("lib/", "lib")]
    [InlineData("./lib", "lib")]
    [InlineData("lib;lib/;./lib", "lib")]
    [InlineData(".;lib", ".|lib")]
    [InlineData("lib\\native", "lib/native")]
    [InlineData("./", ".")]
    [InlineData("lib/../lib", "lib")]
    [InlineData(" lib ; native ", "lib|native")]
    public void Execute_ManagedSubdirectories_AreNormalized(string configured, string expected)
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, SetWith("ManagedSubdirectories", configured));
        Assert.True(task.Execute());
        Assert.Equal(expected, string.Join("|", DecodeSingle(task).ManagedSubdirectories!));
        }

    [Theory]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData("lib/../../escape")]
    [InlineData("C:\\escape")]
    [InlineData("/escape")]
    [InlineData("\\\\server\\share")]
    [InlineData("lib:stream")]
    public void Execute_SubdirectoriesEscapingSet_ReturnFalse(string value)
        {
        foreach (string key in new[] { "ManagedSubdirectories", "NativeSubdirectories" })
            {
            using var temp = new TempDirectory();
            Assert.False(Create(temp, true, SetWith(key, value)).Execute());
            }
        }

    #endregion Managed subdirectories

    #region Native subdirectories

    [Fact]
    public void Execute_DefaultNative_IsEmittedAsDefault()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true);
        Assert.True(task.Execute());
        Assert.Null(DecodeSingle(task).NativeSubdirectories);
        }

    [Fact]
    public void Execute_DesignTime_MissingExplicitNativeDirectory_IsTolerated()
        {
        using var temp = new TempDirectory();
        Assert.True(Create(temp, true, SetWith("NativeSubdirectories", "native")).Execute());
        }

    [Fact]
    public void Execute_ExplicitNative_IsEmitted()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, SetWith("NativeSubdirectories", "native;runtimes/win-x64/native"));
        Assert.True(task.Execute());
        Assert.Equal("native|runtimes/win-x64/native", string.Join("|", DecodeSingle(task).NativeSubdirectories!));
        }

    [Fact]
    public void Execute_NativeListWithoutFolders_ReturnsFalse()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, SetWith("NativeSubdirectories", ";"));
        Assert.False(task.Execute());
        Assert.Contains("NativeSubdirectories", Assert.Single(Errors(task)));
        }

    [Theory]
    [InlineData("native;native/;./native", "native")]
    [InlineData("native\\x64", "native/x64")]
    [InlineData(" native ; native\\x64 ", "native|native/x64")]
    public void Execute_NativeSubdirectories_AreNormalizedAndDeduplicated(string configured, string expected)
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, SetWith("NativeSubdirectories", configured));
        Assert.True(task.Execute());
        Assert.Equal(expected, string.Join("|", DecodeSingle(task).NativeSubdirectories!));
        }

    [Fact]
    public void Execute_NormalBuild_DefaultNativeDirectory_IsNotRequired()
        {
        using var temp = new TempDirectory();
        temp.Create(SetDirectory());
        Assert.True(Create(temp, false).Execute());
        }

    [Fact]
    public void Execute_NormalBuild_ExistingExplicitNativeDirectory_Succeeds()
        {
        using var temp = new TempDirectory();
        temp.Create(Path.Combine(SetDirectory(), "native"));
        Assert.True(Create(temp, false, SetWith("NativeSubdirectories", "native")).Execute());
        }

    [Fact]
    public void Execute_NormalBuild_MissingExplicitNativeDirectory_ReturnsFalse()
        {
        using var temp = new TempDirectory();
        temp.Create(SetDirectory());
        var task = Create(temp, false, SetWith("NativeSubdirectories", "native"));
        Assert.False(task.Execute());
        Assert.Contains("Native search folder is missing", Assert.Single(Errors(task)));
        }

    #endregion Native subdirectories

    #region Options

    [Fact]
    public void Execute_BooleanOptions_AreEmitted()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, Set("Logging", "netstandard2.0",
            ("RequireExactAssemblyVersion", "false"),
            ("ResolveNativeForAnyAssembly", "true")));
        Assert.True(task.Execute());
        var options = DecodeSingle(task);
        Assert.False(options.RequireExactAssemblyVersion);
        Assert.True(options.ResolveNativeForAnyAssembly);
        }

    [Fact]
    public void Execute_Defaults_RequireExactVersionAndLimitNativeScope()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true);
        Assert.True(task.Execute());
        var options = DecodeSingle(task);
        Assert.True(options.RequireExactAssemblyVersion);
        Assert.False(options.ResolveNativeForAnyAssembly);
        }

    [Theory]
    [InlineData("RequireExactAssemblyVersion")]
    [InlineData("ResolveNativeForAnyAssembly")]
    public void Execute_InvalidBooleanOption_NamesTheOption(string key)
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, SetWith(key, "sometimes"));
        Assert.False(task.Execute());
        Assert.Contains(key, Assert.Single(Errors(task)));
        }

    #endregion Options

    #region Runtime directory resolution

    [Fact]
    public void Execute_GlobalRuntimeDirectoryWithTrailingSeparator_IsNormalized()
        {
        using var temp = new TempDirectory();
        string custom = temp.Create("custom");
        var task = Create(temp, true);
        task.RuntimeDirectory = custom + Path.DirectorySeparatorChar;
        task.References = [Reference("Example")];
        Assert.True(task.Execute());
        Assert.Equal(Path.Combine(custom, "Example.dll"), Assert.Single(task.ResolvedReferences).GetMetadata("HintPath"));
        }

    [Theory]
    [InlineData("")]
    [InlineData("relative")]
    public void Execute_MissingOrRelativeCommonFilesWithoutOverride_ReturnsFalse(string commonFiles)
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true);
        task.CommonFiles = commonFiles;
        Assert.False(task.Execute());
        Assert.Contains("Common Files", Assert.Single(Errors(task)));
        }

    [Fact]
    public void Execute_PerSetRuntimeDirectory_IsUsed()
        {
        using var temp = new TempDirectory();
        string custom = temp.Create("custom");
        var task = Create(temp, true, SetWith("RuntimeDirectory", custom));
        task.References = [Reference("Example")];
        Assert.True(task.Execute());
        Assert.Equal(Path.Combine(custom, "Example.dll"), Assert.Single(task.ResolvedReferences).GetMetadata("HintPath"));
        }

    [Fact]
    public void Execute_RelativeRuntimeDirectory_ReturnsFalse()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, SetWith("RuntimeDirectory", "relative"));
        Assert.False(task.Execute());
        Assert.Contains("absolute", Assert.Single(Errors(task)));
        }

    #endregion Runtime directory resolution

    #region Normal build validation

    [Fact]
    public void Execute_DesignTime_ConflictingDuplicates_AreNotScanned()
        {
        using var temp = new TempDirectory();
        TempDirectory.CopyFixture(temp.Create(SetDirectory("A")));
        TempDirectory.CopyFixture(temp.Create(SetDirectory("B")), conflict: true);
        Assert.True(Create(temp, true, Set("A"), Set("B")).Execute());
        }

    [Fact]
    public void Execute_DesignTime_MissingSetDirectory_IsTolerated()
        {
        using var temp = new TempDirectory();
        Assert.True(Create(temp, true).Execute());
        }

    [Fact]
    public void Execute_NormalBuild_AssemblyNameMismatch_ReturnsFalse()
        {
        using var temp = new TempDirectory();
        string directory = temp.Create(SetDirectory());
        string file = TempDirectory.CopyFixture(directory);
        File.Move(file, Path.Combine(directory, "Other.dll"));
        var task = Create(temp, false);
        task.References = [Reference("Other")];
        Assert.False(task.Execute());
        Assert.Contains("do not match", Assert.Single(Errors(task)));
        }

    [Fact]
    public void Execute_NormalBuild_ByteIdenticalDuplicatesAcrossSets_Succeed()
        {
        using var temp = new TempDirectory();
        TempDirectory.CopyFixture(temp.Create(SetDirectory("A")));
        TempDirectory.CopyFixture(temp.Create(SetDirectory("B")));
        Assert.True(Create(temp, false, Set("A"), Set("B")).Execute());
        }

    [Fact]
    public void Execute_NormalBuild_ConflictingDuplicatesAcrossSets_ReturnFalse()
        {
        using var temp = new TempDirectory();
        TempDirectory.CopyFixture(temp.Create(SetDirectory("A")));
        TempDirectory.CopyFixture(temp.Create(SetDirectory("B")), conflict: true);
        var task = Create(temp, false, Set("A"), Set("B"));
        Assert.False(task.Execute());
        string message = Assert.Single(Errors(task));
        Assert.Contains("Assembly conflict", message);
        Assert.Contains(_fixture, message);
        }

    [Fact]
    public void Execute_NormalBuild_ExistingSetDirectoryWithoutReferences_Succeeds()
        {
        using var temp = new TempDirectory();
        temp.Create(SetDirectory());
        Assert.True(Create(temp, false).Execute());
        }

    [Fact]
    public void Execute_NormalBuild_MissingSetDirectory_ReturnsFalse()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, false);
        Assert.False(task.Execute());
        Assert.Contains("Search folder is missing", Assert.Single(Errors(task)));
        }

    [Fact]
    public void Execute_NormalBuild_NonManagedDll_IsIgnored()
        {
        using var temp = new TempDirectory();
        string directory = temp.Create(SetDirectory());
        File.WriteAllText(Path.Combine(directory, "native.dll"), "not a managed PE");
        Assert.True(Create(temp, false).Execute());
        }

    [Fact]
    public void Execute_NormalBuild_ReferenceInSubdirectory_Succeeds()
        {
        using var temp = new TempDirectory();
        string directory = temp.Create(Path.Combine(SetDirectory(), "lib"));
        TempDirectory.CopyFixture(directory);
        var task = Create(temp, false);
        task.References = [Reference(_fixture, subdirectory: "lib")];
        Assert.True(task.Execute());
        Assert.Equal(".|lib", string.Join("|", DecodeSingle(task).ManagedSubdirectories!));
        }

    [Fact]
    public void Execute_NormalBuild_ReferenceOnlyInRoot_IsNotFoundInSubdirectory()
        {
        using var temp = new TempDirectory();
        string directory = temp.Create(SetDirectory());
        temp.Create(Path.Combine(SetDirectory(), "lib"));
        TempDirectory.CopyFixture(directory);
        var task = Create(temp, false);
        task.References = [Reference(_fixture, subdirectory: "lib")];
        Assert.False(task.Execute());
        }

    #endregion Normal build validation

    #region Outputs

    [Fact]
    public void Execute_ExcludeGlobs_CoverEverySetDirectory()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, Set("A"), Set("B"));
        Assert.True(task.Execute());
        char separator = Path.DirectorySeparatorChar;
        string Glob(string id) => Path.Combine(temp.Root, SetDirectory(id)) + "/**/*";
        Assert.Equal(2, task.ExcludeGlobs.Length);
        Assert.Contains(task.ExcludeGlobs, x => x.ItemSpec == Glob("A"));
        Assert.Contains(task.ExcludeGlobs, x => x.ItemSpec == Glob("B"));
        }

    [Fact]
    public void Execute_Failure_LeavesOutputsEmptyAndLogsPrefixedError()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, Set("Logging", "net7.0"));
        task.References = [Reference("Example")];
        Assert.False(task.Execute());
        Assert.Empty(task.ResolvedReferences);
        Assert.Empty(task.Metadata);
        Assert.Empty(task.ExcludeGlobs);
        Assert.StartsWith("wh7r4bb17.SharedRuntimes:", Assert.Single(Errors(task)));
        }

    [Fact]
    public void Execute_Metadata_UsesOneItemPerSet()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true, Set("A"), Set("B"));
        Assert.True(task.Execute());
        Assert.Equal(["wh7r4bb17.SharedRuntimes.Set.A", "wh7r4bb17.SharedRuntimes.Set.B"], task.Metadata.Select(x => x.ItemSpec).OrderBy(x => x));
        Assert.All(task.Metadata, entry => Assert.StartsWith("v2|", entry.GetMetadata("Value")));
        }

    [Fact]
    public void Execute_ResolvedReference_HasExpectedMetadata()
        {
        using var temp = new TempDirectory();
        var task = Create(temp, true);
        task.References = [Reference("Example")];
        Assert.True(task.Execute());
        var item = Assert.Single(task.ResolvedReferences);
        Assert.Equal("Example", item.ItemSpec);
        Assert.Equal("false", item.GetMetadata("Private"));
        Assert.Equal("Logging", item.GetMetadata("CompanySharedSet"));
        Assert.Equal(ExpectedPath(temp, "netstandard2.0", "Example.dll"), item.GetMetadata("HintPath"));
        }

    #endregion Outputs
    }