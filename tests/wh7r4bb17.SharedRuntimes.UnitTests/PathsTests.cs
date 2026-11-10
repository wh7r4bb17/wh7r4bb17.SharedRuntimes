namespace wh7r4bb17.SharedRuntimes.Tests;

public sealed class PathsTests
    {
    #region Public Methods

    [Fact]
    public void GetRuntimeDirectory_LegacyDefault_IsNet10()
        {
        using var temp = new TempDirectory();
        Assert.Equal(Path.Combine(temp.Root, "company", "Logging", "1.0.8", "net10.0"), SharedRuntimePaths.GetRuntimeDirectory("company", "Logging", "1.0.8", temp.Root));
        }

    [Fact]
    public void GetRuntimeDirectory_RejectsFrameworkTraversal()
        {
        using var temp = new TempDirectory();
        Assert.Throws<ArgumentException>(() => SharedRuntimePaths.GetRuntimeDirectory("company", "Logging", "1", temp.Root, "../escape"));
        }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("CON")]
    [InlineData("invalid.")]
    [InlineData("invalid ")]
    public void GetRuntimeDirectory_RejectsInvalidCompanySegment(string company)
        {
        using var temp = new TempDirectory();
        Assert.Throws<ArgumentException>(() => SharedRuntimePaths.GetRuntimeDirectory(company, "Logging", "1", temp.Root));
        }

    [Theory]
    [InlineData("net10.0")]
    [InlineData("net10.0-windows")]
    [InlineData("netstandard2.0")]
    [InlineData("netstandard2.1")]
    public void GetRuntimeDirectory_UsesExplicitFramework(string framework)
        {
        using var temp = new TempDirectory();
        string actual = SharedRuntimePaths.GetRuntimeDirectory("company", "Logging", "1.0.8", temp.Root, framework);
        Assert.Equal(Path.Combine(temp.Root, "company", "Logging", "1.0.8", framework), actual);
        }

    #endregion Public Methods
    }