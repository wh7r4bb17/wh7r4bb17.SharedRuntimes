namespace wh7r4bb17.SharedRuntimes.Tests;

public sealed class MetadataTests
    {
    #region Public Methods

    [Fact]
    public void FromAssembly_MultipleSets_Throws_WhileFromAssemblyAllSucceeds()
        {
        var assembly = MetadataAssembly.Create(("wh7r4bb17.SharedRuntimes.Set.A", MetadataAssembly.Payload()), ("wh7r4bb17.SharedRuntimes.Set.B", MetadataAssembly.Payload()));
        Assert.Equal(2, SharedRuntimeOptions.FromAssemblyAll(assembly).Count);
        Assert.Throws<InvalidOperationException>(() => SharedRuntimeOptions.FromAssembly(assembly));
        }

    [Fact]
    public void FromAssemblyAll_CustomNativeDirectories_ArePreserved()
        {
        var assembly = MetadataAssembly.Create(("wh7r4bb17.SharedRuntimes.Set.Logging", MetadataAssembly.Payload(native: "native\nx64")));
        Assert.Equal(["native", "x64"], Assert.Single(SharedRuntimeOptions.FromAssemblyAll(assembly)).Options.NativeSubdirectories);
        }

    [Fact]
    public void FromAssemblyAll_DuplicateIdsIgnoringCase_Throws()
        {
        var assembly = MetadataAssembly.Create(("wh7r4bb17.SharedRuntimes.Set.Logging", MetadataAssembly.Payload()), ("wh7r4bb17.SharedRuntimes.Set.LOGGING", MetadataAssembly.Payload()));
        Assert.Throws<InvalidOperationException>(() => SharedRuntimeOptions.FromAssemblyAll(assembly));
        }

    [Fact]
    public void FromAssemblyAll_InvalidBase64_Throws()
        {
        var fields = MetadataAssembly.Payload().Split('|'); fields[1] = "!not-base64!";
        var assembly = MetadataAssembly.Create(("wh7r4bb17.SharedRuntimes.Set.Logging", string.Join("|", fields)));
        Assert.Throws<InvalidOperationException>(() => SharedRuntimeOptions.FromAssemblyAll(assembly));
        }

    [Fact]
    public void FromAssemblyAll_InvalidBoolean_Throws()
        {
        var assembly = MetadataAssembly.Create(("wh7r4bb17.SharedRuntimes.Set.Logging", MetadataAssembly.Payload(exact: "sometimes")));
        Assert.Throws<InvalidOperationException>(() => SharedRuntimeOptions.FromAssemblyAll(assembly));
        }

    [Theory]
    [InlineData("v9|bad")]
    [InlineData("v1|bad")]
    [InlineData("")]
    public void FromAssemblyAll_InvalidFormat_Throws(string payload)
        {
        var assembly = MetadataAssembly.Create(("wh7r4bb17.SharedRuntimes.Set.Logging", payload));
        Assert.Throws<InvalidOperationException>(() => SharedRuntimeOptions.FromAssemblyAll(assembly));
        }

    [Fact]
    public void FromAssemblyAll_LegacySingleSet_ReadsDefaults()
        {
        var assembly = MetadataAssembly.Create(("CompanySharedRuntimeVersion", "1.0.8"));
        var set = Assert.Single(SharedRuntimeOptions.FromAssemblyAll(assembly));
        Assert.Equal("Default", set.Id);
        Assert.Equal("company", set.Options.CompanyName);
        Assert.Equal("SharedRuntime", set.Options.RuntimeName);
        Assert.Equal("net10.0", set.Options.TargetFramework);
        }

    [Fact]
    public void FromAssemblyAll_MissingMetadata_Throws() => Assert.Throws<InvalidOperationException>(() => SharedRuntimeOptions.FromAssemblyAll(MetadataAssembly.Create()));

    [Fact]
    public void FromAssemblyAll_NullAssembly_Throws() => Assert.Throws<ArgumentNullException>(() => SharedRuntimeOptions.FromAssemblyAll(null!));

    [Fact]
    public void FromAssemblyAll_V1_PreservesNet10Semantics()
        {
        var assembly = MetadataAssembly.Create(("wh7r4bb17.SharedRuntimes.Set.Logging", MetadataAssembly.Payload(legacy: true)));
        var options = Assert.Single(SharedRuntimeOptions.FromAssemblyAll(assembly)).Options;
        Assert.Equal("net10.0", options.TargetFramework);
        Assert.Null(options.NativeSubdirectories);
        }

    [Theory]
    [InlineData("netstandard2.0")]
    [InlineData("net10.0-windows")]
    [InlineData("net10.0")]
    public void FromAssemblyAll_V2_ReadsFrameworkAndOptions(string framework)
        {
        var assembly = MetadataAssembly.Create(("wh7r4bb17.SharedRuntimes.Set.Logging", MetadataAssembly.Payload(framework)));
        var set = Assert.Single(SharedRuntimeOptions.FromAssemblyAll(assembly));
        Assert.Equal("Logging", set.Id);
        Assert.Equal(framework, set.Options.TargetFramework);
        Assert.Equal("Logging", set.Options.RuntimeName);
        Assert.Equal("1.0.8", set.Options.RuntimeVersion);
        Assert.Equal([".", "lib"], set.Options.ManagedSubdirectories);
        Assert.True(set.Options.RequireExactAssemblyVersion);
        Assert.False(set.Options.ResolveNativeForAnyAssembly);
        }

    #endregion Public Methods
    }