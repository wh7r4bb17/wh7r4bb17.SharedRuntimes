namespace wh7r4bb17.SharedRuntimes.Tests;

public sealed class RegistrationTests
    {
    #region Public Methods

    [Fact]
    public void Attach_ByteIdenticalDuplicates_AreAllowed()
        {
        using var temp = new TempDirectory();
        var a = temp.Options("a"); var b = temp.Options("b");
        TempDirectory.CopyFixture(a.RuntimeDirectory!);
        TempDirectory.CopyFixture(b.RuntimeDirectory!);
        using var registration = SharedRuntimeRegistration.Attach([new("A", a), new("B", b)]);
        Assert.Equal(2, registration.Runtimes.Count);
        }

    [Fact]
    public void Attach_EmptySets_Throws() => Assert.Throws<ArgumentException>(() => SharedRuntimeRegistration.Attach([]));

    [Fact]
    public void Attach_IdsDifferOnlyByCase_Throws()
        {
        using var temp = new TempDirectory();
        Assert.Throws<ArgumentException>(() => SharedRuntimeRegistration.Attach([new("Logging", temp.Options("a")), new("LOGGING", temp.Options("b"))]));
        }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a|b")]
    [InlineData("ä")]
    public void Attach_InvalidId_Throws(string id)
        {
        using var temp = new TempDirectory();
        Assert.Throws<ArgumentException>(() => SharedRuntimeRegistration.Attach([new(id, temp.Options())]));
        }

    [Fact]
    public void Attach_MissingDirectory_Throws()
        {
        using var temp = new TempDirectory();
        var options = temp.Options() with { RuntimeDirectory = Path.Combine(temp.Root, "missing") };
        Assert.Throws<DirectoryNotFoundException>(() => SharedRuntimeRegistration.Attach([new("A", options)]));
        }

    [Fact]
    public void Attach_MissingManagedSubdirectory_Throws()
        {
        using var temp = new TempDirectory();
        var options = temp.Options() with { ManagedSubdirectories = ["missing"] };
        Assert.Throws<DirectoryNotFoundException>(() => SharedRuntimeRegistration.Attach([new("A", options)]));
        }

    [Fact]
    public void Attach_MultipleSets_IndexesCaseInsensitively()
        {
        using var temp = new TempDirectory();
        using var registration = SharedRuntimeRegistration.Attach([new("Logging", temp.Options("a")), new("Reporting", temp.Options("b"))]);
        Assert.False(registration.IsDisposed);
        Assert.Equal(2, registration.Runtimes.Count);
        Assert.Same(registration.Runtimes["Logging"], registration.GetRuntime("LOGGING"));
        Assert.Throws<KeyNotFoundException>(() => registration.GetRuntime("Unknown"));
        }

    [Fact]
    public void Attach_NonManagedDll_IsIgnoredByManagedConflictScan()
        {
        using var temp = new TempDirectory();
        var options = temp.Options();
        File.WriteAllText(Path.Combine(options.RuntimeDirectory!, "native.dll"), "not a managed PE");
        using var registration = SharedRuntimeRegistration.Attach([new("A", options)]);
        Assert.Single(registration.Runtimes);
        }

    [Fact]
    public void Attach_NullOptions_Throws() => Assert.Throws<ArgumentNullException>(() => SharedRuntimeRegistration.Attach([new("A", null!)]));

    [Fact]
    public void Attach_NullSet_Throws() => Assert.Throws<ArgumentNullException>(() => SharedRuntimeRegistration.Attach([null!]));

    [Fact]
    public void Attach_NullSets_Throws() => Assert.Throws<ArgumentNullException>(() => SharedRuntimeRegistration.Attach(null!));

    [Fact]
    public void Attach_SameIdentityDifferentBytes_IsRejected()
        {
        using var temp = new TempDirectory();
        var a = temp.Options("a"); var b = temp.Options("b");
        string first = TempDirectory.CopyFixture(a.RuntimeDirectory!);
        string second = TempDirectory.CopyFixture(b.RuntimeDirectory!, conflict: true);
        Assert.Equal(AssemblyName.GetAssemblyName(first).FullName, AssemblyName.GetAssemblyName(second).FullName);
        var exception = Assert.Throws<FileLoadException>(() => SharedRuntimeRegistration.Attach([new("A", a), new("B", b)]));
        Assert.Contains("wh7r4bb17.SharedRuntimes.TestFixture", exception.Message);
        }

    [Fact]
    public void Dispose_IsIdempotent_AndDisposesEveryResolver()
        {
        using var temp = new TempDirectory();
        var registration = SharedRuntimeRegistration.Attach([new("A", temp.Options("a")), new("B", temp.Options("b"))]);
        var resolvers = registration.Runtimes.Values.ToArray();
        registration.Dispose();
        registration.Dispose();
        Assert.True(registration.IsDisposed);
        Assert.All(resolvers, resolver => Assert.True(resolver.IsDisposed));
        Assert.Throws<ObjectDisposedException>(() => { _ = registration.Runtimes; });
        Assert.Throws<ObjectDisposedException>(() => registration.GetRuntime("A"));
        }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GetRuntime_EmptyId_Throws(string id)
        {
        using var temp = new TempDirectory();
        using var registration = SharedRuntimeRegistration.Attach([new("A", temp.Options())]);
        Assert.Throws<ArgumentException>(() => registration.GetRuntime(id));
        }

    #endregion Public Methods
    }