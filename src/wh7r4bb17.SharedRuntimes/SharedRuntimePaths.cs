// Copyright (C) 2026 wh7r4bb17.
// This file is part of wh7r4bb17.SharedRuntimes.
//
// wh7r4bb17.SharedRuntimes is licensed under the GNU General Public License version 3 only.
// See LICENSE.txt for details.

namespace wh7r4bb17.SharedRuntimes;
/// <summary>Constructs conventional runtime installation paths.</summary>
public static class SharedRuntimePaths
    {
    #region Public Methods

    /// <summary>Builds the installation path using the selected framework directory.</summary>
    /// <param name="companyName">The company directory name.</param>
    /// <param name="runtimeName">The product directory name.</param>
    /// <param name="runtimeVersion">The installed set version.</param>
    /// <param name="commonFilesDirectory">An optional absolute Common Files root.</param>
    /// <param name="targetFramework">The exact framework directory name.</param>
    public static string GetRuntimeDirectory(string companyName, string runtimeName, string runtimeVersion, string? commonFilesDirectory = null, string targetFramework = "net10.0")
        {
        RuntimePaths.Segment(companyName, nameof(companyName));
        RuntimePaths.Segment(runtimeName, nameof(runtimeName));
        RuntimePaths.Segment(runtimeVersion, nameof(runtimeVersion));
        RuntimePaths.Segment(targetFramework, nameof(targetFramework));

        if (commonFilesDirectory is null)
            {
            if (!OperatingSystem.IsWindows())
                { throw new PlatformNotSupportedException("“Common Files” is a Windows installation location. On other systems, explicitly set the RuntimeDirectory.."); }

            commonFilesDirectory = Environment.GetEnvironmentVariable("CommonProgramW6432");
            if (string.IsNullOrWhiteSpace(commonFilesDirectory))
                { commonFilesDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles); }
            }

        if (string.IsNullOrWhiteSpace(commonFilesDirectory) || !Path.IsPathFullyQualified(commonFilesDirectory))
            { throw new ArgumentException("An absolute Common-Files base directory is required.", nameof(commonFilesDirectory)); }

        return Path.GetFullPath(Path.Combine(commonFilesDirectory, companyName, runtimeName, runtimeVersion, targetFramework));
        }

    #endregion Public Methods
    }
