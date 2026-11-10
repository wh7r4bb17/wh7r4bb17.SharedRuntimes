// Copyright (C) 2026 wh7r4bb17.
// This file is part of wh7r4bb17.SharedRuntimes.
//
// wh7r4bb17.SharedRuntimes is licensed under the GNU General Public License version 3 only.
// See LICENSE.txt for details.

using System.Reflection;

namespace wh7r4bb17.SharedRuntimes;

internal static class SharedRuntimeMetadata
{
    #region Fields
    private const string _prefix = "wh7r4bb17.SharedRuntimes.Set.";
    private const string _defaultNativeMarker = "@default";

    #endregion Fields

    #region Internal Methods

    internal static IReadOnlyList<SharedRuntimeSet> Read(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToArray();
        var entries = metadata.Where(x => x.Key.StartsWith(_prefix, StringComparison.Ordinal)).ToArray();

        if (entries.Length == 0)
        {
            string? Get(string key) => metadata.FirstOrDefault(x => x.Key == key)?.Value;
            return Array.AsReadOnly([ new SharedRuntimeSet("Default", new SharedRuntimeOptions
            {
                RuntimeVersion = Get("CompanySharedRuntimeVersion") ?? throw new InvalidOperationException("Runtime metadata is missing. Check UseCompanySharedRuntime=true and Build-Imports."),
                CompanyName = Get("CompanySharedCompanyName") ?? "company",
                RuntimeName = Get("CompanySharedRuntimeName") ?? "SharedRuntime"
            }) ]);
        }

        var result = new List<SharedRuntimeSet>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in entries)
        {
            string id = item.Key[_prefix.Length..];
            if (!ids.Add(id))
            { throw new InvalidOperationException($"Duplicate set metadata: {id}"); }

            string[] fields = (item.Value ?? "").Split('|');
            bool legacy = fields.Length == 8 && fields[0] == "v1";
            bool current = fields.Length == 9 && fields[0] == "v2";
            if (!legacy && !current)
            { throw new InvalidOperationException($"Invalid Runtime metadata format for '{id}'."); }

            string Decode(int index)
            {
                try
                { return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(fields[index])); }
                catch (FormatException ex)
                { throw new InvalidOperationException($"Invalid Runtime metadata for '{id}'.", ex); }
            }
            bool Boolean(int index) => bool.TryParse(Decode(index), out bool value) ? value : throw new InvalidOperationException($"Invalid Boolean in Runtime set '{id}'.");

            result.Add(new(id, new SharedRuntimeOptions
            {
                CompanyName = Decode(1),
                RuntimeName = Decode(2),
                RuntimeVersion = Decode(3),
                ManagedSubdirectories = Decode(4).Split('\n'),
                NativeSubdirectories = Decode(5) == _defaultNativeMarker ? null : Decode(5).Split('\n'),
                RequireExactAssemblyVersion = Boolean(6),
                ResolveNativeForAnyAssembly = Boolean(7),
                TargetFramework = legacy ? "net10.0" : Decode(8)
            }));
        }
        return result.AsReadOnly();
    }

    #endregion Internal Methods
}
