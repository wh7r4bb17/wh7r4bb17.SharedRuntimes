// Copyright (C) 2026 wh7r4bb17.
// This file is part of wh7r4bb17.SharedRuntimes.
//
// wh7r4bb17.SharedRuntimes is licensed under the GNU General Public License version 3 only.
// See LICENSE.txt for details.

using System.Reflection;

namespace wh7r4bb17.SharedRuntimes;

/// <summary>Provides application-wide access to centrally installed runtime sets.</summary>
public static class SharedRuntime
{
    #region Fields
    private static readonly Lock _gate = new();
    private static volatile SharedRuntimeRegistration? _registration;

    #endregion Fields

    #region Properties
    /// <summary>Gets the sole active resolver. Throws when multiple sets are registered.</summary>
    public static SharedRuntimeResolver Current => Runtimes.Count == 1 ? Runtimes.Values.Single() : throw new InvalidOperationException("Multiple sets are active. Use GetRuntime(setId).");

    /// <summary>Gets the active registration. Throws before initialization.</summary>
    public static SharedRuntimeRegistration Registration
            => _registration is { IsDisposed: false } current ? current : throw new InvalidOperationException("Initialize SharedRuntime first.");

    /// <summary>Gets registered resolvers indexed by case-insensitive set identifiers.</summary>
    public static IReadOnlyDictionary<string, SharedRuntimeResolver> Runtimes => Registration.Runtimes;

    #endregion Properties

    #region Public Methods

    /// <summary>Returns a validated path inside the runtime directory.</summary>
    /// <param name="parts">The relative path components.</param>
    public static string GetPath(params string[] parts) => Current.GetPath(parts);

    /// <summary>Returns a validated path inside the specified runtime set.</summary>
    /// <param name="setId">The logical set identifier.</param>
    /// <param name="parts">The relative path components.</param>
    public static string GetPathForSet(string setId, params string[] parts) => GetRuntime(setId).GetPath(parts);

    /// <summary>Returns the resolver for the specified set identifier.</summary>
    /// <param name="setId">The logical set identifier.</param>
    public static SharedRuntimeResolver GetRuntime(string setId) => Registration.GetRuntime(setId);

    /// <summary>Registers runtime sets for the application lifetime. Rejects repeated initialization.</summary>
    /// <param name="options">The runtime configuration.</param>
    public static void Initialize(SharedRuntimeOptions options) => Initialize([new SharedRuntimeSet("Default", options)]);

    /// <summary>Registers runtime sets for the application lifetime. Rejects repeated initialization.</summary>
    /// <param name="sets">The runtime sets to register.</param>
    public static void Initialize(IEnumerable<SharedRuntimeSet> sets)
    {
        lock (_gate)
        {
            if (_registration is { IsDisposed: false })
            { throw new InvalidOperationException("SharedRuntime is already initialized."); }
            _registration = SharedRuntimeRegistration.Attach(sets);
        }
    }

    /// <summary>Registers the runtime sets stored in the supplied assembly metadata.</summary>
    /// <param name="assembly">The assembly containing runtime metadata.</param>
    /// <param name="log">An optional diagnostic callback.</param>
    public static void InitializeFor(Assembly assembly, Action<string>? log = null)
        => Initialize(SharedRuntimeOptions.FromAssemblyAll(assembly).Select(s => s with { Options = s.Options with { Log = log } }));

    #endregion Public Methods
}
