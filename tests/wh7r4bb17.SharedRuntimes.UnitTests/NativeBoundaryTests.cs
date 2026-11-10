namespace wh7r4bb17.SharedRuntimes.Tests;

public sealed class NativeBoundaryTests
    {
    #region Public Methods

    [Fact]
    public void LoadNativeLibrary_MissingLibrary_HasPlatformSpecificFailure()
        {
        using var temp = new TempDirectory();
        using var resolver = SharedRuntimeResolver.Attach(temp.Options());
        string missing = "Missing_" + Guid.NewGuid().ToString("N");
        
        if (OperatingSystem.IsWindows())
            { Assert.Throws<DllNotFoundException>(() => resolver.LoadNativeLibrary(missing)); }
        else
            { Assert.Throws<PlatformNotSupportedException>(() => resolver.LoadNativeLibrary(missing)); }
        }

    #endregion Public Methods
    }