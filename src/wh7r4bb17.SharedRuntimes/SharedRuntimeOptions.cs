// Copyright (C) 2026 wh7r4bb17.
// This file is part of wh7r4bb17.SharedRuntimes.
//
// wh7r4bb17.SharedRuntimes is licensed under the GNU General Public License version 3 only.
// See LICENSE.txt for details.

using System.Reflection;

namespace wh7r4bb17.SharedRuntimes;
/// <summary>Configures one centrally installed runtime set.</summary>
public sealed record SharedRuntimeOptions
    {
    /// <summary>Gets the framework directory selected for this runtime set.</summary>
    public string TargetFramework { get; init; } = "net10.0";
    /// <summary>Gets the installed runtime set version.</summary>
    public required string RuntimeVersion { get; init; }
    /// <summary>Gets the company directory name.</summary>
    public string CompanyName { get; init; } = "company";

    /// <summary>Gets the product or runtime directory name.</summary>
    public string RuntimeName { get; init; } = "SharedRuntime";
    /// <summary>Gets an optional absolute directory override, bypassing conventional path construction.</summary>
    public string? RuntimeDirectory { get; init; }
    /// <summary>Gets the ordered relative managed search directories.</summary>
    public IReadOnlyList<string> ManagedSubdirectories { get; init; } = ["."];
    /// <summary>Gets optional relative native search directories. Null selects the Windows architecture default.</summary>
    public IReadOnlyList<string>? NativeSubdirectories { get; init; }
    /// <summary>Gets whether fallback candidates must have exactly the requested version.</summary>
    public bool RequireExactAssemblyVersion { get; init; } = true;
    /// <summary>Gets whether native fallback is also enabled for assemblies outside this set.</summary>
    public bool ResolveNativeForAnyAssembly { get; init; }
    /// <summary>Gets an optional diagnostic callback. Callback exceptions are ignored.</summary>
    public Action<string>? Log { get; init; }
    /// <summary>Reads exactly one set from assembly metadata. Throws for multiple sets.</summary>
    /// <param name="applicationAssembly">The assembly containing runtime metadata.</param>
    public static SharedRuntimeOptions FromAssembly(Assembly applicationAssembly)
        {
        var sets = FromAssemblyAll(applicationAssembly);
        if (sets.Count != 1)
            { throw new InvalidOperationException("Configured multiple sets. Use FromAssemblyAll."); }

        return sets[0].Options;
        }

    /// <summary>Reads all configured sets from assembly metadata.</summary>
    /// <param name="applicationAssembly">The assembly containing runtime metadata.</param>
    public static IReadOnlyList<SharedRuntimeSet> FromAssemblyAll(Assembly applicationAssembly)
        => SharedRuntimeMetadata.Read(applicationAssembly);
    }
