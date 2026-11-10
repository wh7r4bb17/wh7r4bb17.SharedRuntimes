// Copyright (C) 2026 wh7r4bb17.
// This file is part of wh7r4bb17.SharedRuntimes.
//
// wh7r4bb17.SharedRuntimes is licensed under the GNU General Public License version 3 only.
// See LICENSE.txt for details.

using System.Text.RegularExpressions;

namespace wh7r4bb17.SharedRuntimes;

internal static class RuntimePaths
    {
    #region Fields
    private static readonly Regex _reservedDeviceNameRegex = new(@"^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    #endregion Fields

    #region Properties
    internal static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    #endregion Properties

    #region Internal Methods

    internal static string Child(string root, string relative)
        {
        ArgumentNullException.ThrowIfNull(relative);

        if (Path.IsPathRooted(relative) || relative.Contains(':') || relative.StartsWith('\\'))
            { throw new ArgumentException("Only relative paths without drive/UNC are allowed.", nameof(relative)); }

        string normalized = relative.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        string result = Path.GetFullPath(Path.Combine(root, normalized));
        string prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;

        if (!result.Equals(Path.TrimEndingDirectorySeparator(root), Comparison) && !result.StartsWith(prefix, Comparison))
            { throw new ArgumentException("Path leaves the runtime directory.", nameof(relative)); }

        return result;
        }

    internal static string Segment(string value, string parameter)
        {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        if (value is "." or ".." || value.IndexOfAny(['<', '>', ':', '"', '/', '\\', '|', '?', '*']) >= 0 || value.Any(c => c < 32) || value.EndsWith('.') || value.EndsWith(' '))
            { throw new ArgumentException("A single valid Windows path segment is required.", parameter); }

        string stem = value.Split('.')[0];

        if (_reservedDeviceNameRegex.IsMatch(stem))
            { throw new ArgumentException($"Reserved Windows device name: '{value}'.", parameter); }

        return value;
        }

    #endregion Internal Methods
    }