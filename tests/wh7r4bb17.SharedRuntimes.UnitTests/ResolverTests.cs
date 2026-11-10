namespace wh7r4bb17.SharedRuntimes.Tests;

public sealed class ResolverTests
    {
    #region Public Methods

    [Fact]
    public void Attach_EmptyManagedDirectories_Throws()
        {
        using var temp = new TempDirectory();
        Assert.Throws<ArgumentException>(() => SharedRuntimeResolver.Attach(temp.Options() with { ManagedSubdirectories = [] }));
        }

    [Fact]
    public void Attach_LoggerException_DoesNotBreakInitialization()
        {
        using var temp = new TempDirectory();
        using var resolver = SharedRuntimeResolver.Attach(temp.Options() with { Log = _ => throw new InvalidOperationException("logger") });
        Assert.False(resolver.IsDisposed);
        }

    [Fact]
    public void Attach_NullOptions_Throws() => Assert.Throws<ArgumentNullException>(() => SharedRuntimeResolver.Attach(null!));

    [Fact]
    public void Attach_RelativeRoot_Throws()
        {
        Assert.Throws<ArgumentException>(() => SharedRuntimeResolver.Attach(new() { RuntimeVersion = "1", RuntimeDirectory = "relative" }));
        }

    [Fact]
    public void Dispose_RejectsFurtherOperations()
        {
        using var temp = new TempDirectory();
        var resolver = SharedRuntimeResolver.Attach(temp.Options());
        resolver.Dispose(); resolver.Dispose();
        Assert.True(resolver.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => resolver.GetPath("a"));
        Assert.Throws<ObjectDisposedException>(() => resolver.LoadAssembly("Missing"));
        Assert.Throws<ObjectDisposedException>(() => resolver.LoadNativeLibrary("Missing"));
        }

    [Fact]
    public void Dispose_RemovesFallbackHandler()
        {
        using var temp = new TempDirectory();
        var options = temp.Options(); TempDirectory.CopyFixture(options.RuntimeDirectory!);
        var context = new AssemblyLoadContext("test_" + Guid.NewGuid(), true);
        try
            {
            var resolver = SharedRuntimeResolver.Attach(options, context);
            resolver.Dispose();
            Assert.Throws<FileNotFoundException>(() => context.LoadFromAssemblyName(new AssemblyName("wh7r4bb17.SharedRuntimes.TestFixture")));
            }
        finally { context.Unload(); }
        }

    [Fact]
    public void GetPath_CombinesComponents_WithoutRequiringFileExistence()
        {
        using var temp = new TempDirectory();
        using var resolver = SharedRuntimeResolver.Attach(temp.Options());
        Assert.Equal(Path.Combine(resolver.RuntimeDirectory, "Data", "defaults.json"), resolver.GetPath("Data", "defaults.json"));
        Assert.Equal(resolver.RuntimeDirectory, resolver.GetPath());
        }

    [Fact]
    public void GetPath_NullArray_Throws()
        {
        using var temp = new TempDirectory();
        using var resolver = SharedRuntimeResolver.Attach(temp.Options());
        Assert.Throws<ArgumentNullException>(() => resolver.GetPath(null!));
        }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("..\\escape.txt")]
    [InlineData("C:\\escape.txt")]
    [InlineData("/escape.txt")]
    [InlineData("\\\\server\\share")]
    public void GetPath_RejectsEscapingOrRootedPaths(string relative)
        {
        using var temp = new TempDirectory();
        using var resolver = SharedRuntimeResolver.Attach(temp.Options());
        Assert.Throws<ArgumentException>(() => resolver.GetPath(relative));
        }

    [Theory]
    [InlineData(true, "0.5.0.0", false)]
    [InlineData(false, "0.5.0.0", true)]
    [InlineData(false, "2.0.0.0", false)]
    public void LoadAssembly_EnforcesCandidateVersionPolicy(bool exact, string requested, bool accepted)
        {
        using var temp = new TempDirectory();
        var options = temp.Options() with { RequireExactAssemblyVersion = exact };
        TempDirectory.CopyFixture(options.RuntimeDirectory!);
        var context = new AssemblyLoadContext("test_" + Guid.NewGuid(), true);
        try
            {
            using var resolver = SharedRuntimeResolver.Attach(options, context);
            string name = "wh7r4bb17.SharedRuntimes.TestFixture, Version=" + requested;
            if (accepted)
                { Assert.Equal(new Version(1, 0, 0, 0), resolver.LoadAssembly(name).GetName().Version); }
            else
                { Assert.Throws<FileNotFoundException>(() => resolver.LoadAssembly(name)); }
            }
        finally { context.Unload(); }
        }

    [Fact]
    public void LoadAssembly_Missing_Throws()
        {
        using var temp = new TempDirectory();
        using var resolver = SharedRuntimeResolver.Attach(temp.Options());
        Assert.Throws<FileNotFoundException>(() => resolver.LoadAssembly("Missing_" + Guid.NewGuid().ToString("N")));
        }

    [Fact]
    public void LoadAssembly_UsesCustomContext_AndReturnsSameInstance()
        {
        using var temp = new TempDirectory();
        var options = temp.Options();
        TempDirectory.CopyFixture(options.RuntimeDirectory!);
        var context = new AssemblyLoadContext("test_" + Guid.NewGuid(), isCollectible: true);
        try
            {
            using var resolver = SharedRuntimeResolver.Attach(options, context);
            var first = resolver.LoadAssembly("wh7r4bb17.SharedRuntimes.TestFixture, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            Assert.Same(context, AssemblyLoadContext.GetLoadContext(first));
            Assert.Same(first, resolver.LoadAssembly(first.FullName!));
            Assert.Equal("original", first.GetType("wh7r4bb17.SharedRuntimes.TestFixture.Marker")!.GetProperty("Value")!.GetValue(null));
            }
        finally { context.Unload(); }
        }

    [Fact]
    public void Registration_AttachFailure_RollsBackEarlierHandlers()
        {
        using var temp = new TempDirectory();
        var first = temp.Options("a"); TempDirectory.CopyFixture(first.RuntimeDirectory!);
        var second = temp.Options("b") with { ManagedSubdirectories = [] };
        var context = new AssemblyLoadContext("test_" + Guid.NewGuid(), true);
        try
            {
            Assert.Throws<ArgumentException>(() => SharedRuntimeRegistration.Attach([new("A", first), new("B", second)], context));
            Assert.Throws<FileNotFoundException>(() => context.LoadFromAssemblyName(new AssemblyName("wh7r4bb17.SharedRuntimes.TestFixture")));
            }
        finally { context.Unload(); }
        }

    #endregion Public Methods
    }