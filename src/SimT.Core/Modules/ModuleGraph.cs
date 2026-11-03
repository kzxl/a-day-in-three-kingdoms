using SimT.Contracts;
using SimT.Contracts.Modules;

namespace SimT.Core.Modules;

/// <summary>
/// K5 — validates a composition and returns modules in deterministic dependency order. Every
/// failure here is a packaging bug, so all of them are hard errors with an actionable message.
/// </summary>
public static class ModuleGraph
{
    public static IReadOnlyList<ModuleDescriptor> Resolve(
        IReadOnlyList<ModuleDescriptor> discovered,
        IReadOnlyList<string> enabledIds)
    {
        var byId = new Dictionary<string, ModuleDescriptor>(StringComparer.Ordinal);
        foreach (var descriptor in discovered)
        {
            if (!byId.TryAdd(descriptor.Manifest.Id, descriptor))
            {
                throw new SimException("module.duplicate_id", $"Two module artefacts claim id '{descriptor.Manifest.Id}'.");
            }
        }

        var enabled = new List<ModuleDescriptor>();
        foreach (var id in enabledIds.Distinct(StringComparer.Ordinal))
        {
            if (!byId.TryGetValue(id, out var descriptor))
            {
                throw new SimException("module.not_found", $"Product manifest enables '{id}' but no such module artefact is present.");
            }

            if (descriptor.Manifest.ApiVersion != ModuleManifest.CurrentApiVersion)
            {
                throw new SimException(
                    "module.api_mismatch",
                    $"Module '{id}' targets API {descriptor.Manifest.ApiVersion}; kernel speaks {ModuleManifest.CurrentApiVersion}.");
            }

            enabled.Add(descriptor);
        }

        var enabledSet = enabled.Select(descriptor => descriptor.Manifest.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var descriptor in enabled)
        {
            foreach (var required in descriptor.Manifest.Requires)
            {
                if (!enabledSet.Contains(required))
                {
                    throw new SimException(
                        "module.missing_dependency",
                        $"Module '{descriptor.Manifest.Id}' requires '{required}', which the product manifest does not enable.");
                }
            }
        }

        RejectOwnershipClash(enabled);
        RejectCapabilityClash(enabled);
        return Sort(enabled);
    }

    private static void RejectOwnershipClash(IReadOnlyList<ModuleDescriptor> enabled)
    {
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in enabled)
        {
            foreach (var table in descriptor.Manifest.Owns.Tables)
            {
                if (owners.TryGetValue(table, out var owner))
                {
                    throw new SimException(
                        "module.table_ownership",
                        $"Table '{table}' is claimed by both '{owner}' and '{descriptor.Manifest.Id}'.");
                }

                owners[table] = descriptor.Manifest.Id;
            }
        }
    }

    private static void RejectCapabilityClash(IReadOnlyList<ModuleDescriptor> enabled)
    {
        var providers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var descriptor in enabled)
        {
            foreach (var capability in descriptor.Manifest.Provides)
            {
                if (providers.TryGetValue(capability, out var owner))
                {
                    throw new SimException(
                        "module.capability_ownership",
                        $"Capability '{capability}' is provided by both '{owner}' and '{descriptor.Manifest.Id}'.");
                }

                providers[capability] = descriptor.Manifest.Id;
            }
        }
    }

    private static IReadOnlyList<ModuleDescriptor> Sort(IReadOnlyList<ModuleDescriptor> enabled)
    {
        var byId = enabled.ToDictionary(descriptor => descriptor.Manifest.Id, StringComparer.Ordinal);
        var ordered = new List<ModuleDescriptor>(enabled.Count);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var done = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in byId.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            Visit(id);
        }

        return ordered;

        void Visit(string id)
        {
            if (done.Contains(id))
            {
                return;
            }

            if (!visiting.Add(id))
            {
                throw new SimException("module.cycle", $"Dependency cycle detected at module '{id}'.");
            }

            var descriptor = byId[id];
            foreach (var required in descriptor.Manifest.Requires.OrderBy(key => key, StringComparer.Ordinal))
            {
                Visit(required);
            }

            visiting.Remove(id);
            done.Add(id);
            ordered.Add(descriptor);
        }
    }
}
