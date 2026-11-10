using Runtime = wh7r4bb17.SharedRuntimes.SharedRuntime;

namespace wh7r4bb17.SharedRuntimes.Tests;

public sealed class FacadeTests
    {
    #region Public Methods

    [Fact]
    public void Initialize_EnforcesLifecycleAndRequiresSetSelectionWhenAmbiguous()
        {
        using var temp = new TempDirectory();
        Assert.Throws<InvalidOperationException>(() => { _ = Runtime.Registration; });
        Runtime.Initialize([new("A", temp.Options("a")), new("B", temp.Options("b"))]);
        try
            {
            Assert.Equal(2, Runtime.Runtimes.Count);
            Assert.Throws<InvalidOperationException>(() => { _ = Runtime.Current; });
            Assert.Equal(Path.Combine(temp.Root, "a", "Data", "a.json"), Runtime.GetPathForSet("A", "Data", "a.json"));
            Assert.Throws<InvalidOperationException>(() => Runtime.Initialize(temp.Options("c")));
            }
        finally
            { Runtime.Registration.Dispose(); }

        Assert.Throws<InvalidOperationException>(() => { _ = Runtime.Registration; });
        Runtime.Initialize(temp.Options("single"));

        try
            {
            Assert.Same(Runtime.GetRuntime("Default"), Runtime.Current);
            Assert.Equal(Path.Combine(temp.Root, "single", "a.txt"), Runtime.GetPath("a.txt"));
            }
        finally
            { Runtime.Registration.Dispose(); }
        }

    #endregion Public Methods
    }