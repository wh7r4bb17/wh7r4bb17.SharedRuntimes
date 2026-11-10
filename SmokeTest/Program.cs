using System.Diagnostics;

namespace SmokeTest;

internal static class Program
    {
    #region Private Methods

    private static int Main()
        {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=== wh7r4bb17.SharedRuntimes Smoke Test ===");
        Console.ResetColor();

        int exitCode = new SmokeTestRunner().Run();

        if (Debugger.IsAttached)
            { Console.ReadKey(); }

        return exitCode;
        }

    #endregion Private Methods
    }