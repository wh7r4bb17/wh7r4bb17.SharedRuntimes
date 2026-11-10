// Copyright (C) 2026 wh7r4bb17.
// This file is part of wh7r4bb17.SharedRuntimes.
//
// wh7r4bb17.SharedRuntimes is licensed under the GNU General Public License version 3 only.
// See LICENSE.txt for details.

namespace wh7r4bb17.SharedRuntimes;
/// <summary>Associates a logical identifier with runtime options.</summary>
/// <param name="Id">The logical set identifier.</param>
/// <param name="Options">The runtime options.</param>
public sealed record SharedRuntimeSet(string Id, SharedRuntimeOptions Options);
