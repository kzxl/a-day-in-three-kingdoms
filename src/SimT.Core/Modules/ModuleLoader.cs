using System.Reflection;
using System.Text.Json;
using SimT.Contracts;
using SimT.Contracts.Modules;

namespace SimT.Core.Modules;

/// <summary>A module assembly plus its embedded manifest and entry point.</summary>
public sealed record ModuleDescriptor(ModuleManifest Manifest, Assembly Assembly, Type EntryPoint);

/// <summary>
/// K5 — discovers modules from a directory of artefacts. A DLL without <c>module.json</c> is
/// skipped; a DLL with a manifest but no entry point is a hard error, because that is a
/// packaging bug that would otherwise fail silently.
/// </summary>
public static class ModuleLoader
{
    private const string ManifestResource = "module.json";

    public static IReadOnlyList<ModuleDescriptor> Discover(string modulesDirectory)
    {
        if (!Directory.Exists(modulesDirectory))
        {
            throw new SimException("module.directory_missing", $"Modules directory not found: {modulesDirectory}");
        }

        var descriptors = new List<ModuleDescriptor>();
        var files = Directory.GetFiles(modulesDirectory, "SimT.Modules.*.dll");
        Array.Sort(files, StringComparer.Ordinal);

        foreach (var file in files)
        {
            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(file);
            }
            catch (BadImageFormatException)
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(ManifestResource);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            var manifest = ParseManifest(reader.ReadToEnd(), assembly.GetName().Name ?? file);
            descriptors.Add(new ModuleDescriptor(manifest, assembly, FindEntryPoint(assembly, manifest.Id)));
        }

        return descriptors;
    }

    public static ModuleManifest ParseManifest(string json, string source)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new ModuleManifest(
            Id: Text(root, "id", source),
            Version: Text(root, "version", source),
            ApiVersion: Text(root, "apiVersion", source),
            Kind: OptionalText(root, "kind") ?? "module",
            Requires: Strings(root, "requires", source),
            Provides: Strings(root, "provides", source),
            Consumes: Strings(root, "consumes", source),
            Owns: Ownership(root, source),
            Screens: Strings(root, "screens", source));
    }

    private static Type FindEntryPoint(Assembly assembly, string moduleId)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException error)
        {
            throw new SimException("module.type_load_failed", $"Cannot inspect '{moduleId}': {error.Message}", error);
        }

        return types.FirstOrDefault(type =>
                   !type.IsAbstract
                   && type.IsClass
                   && typeof(ISimModule).IsAssignableFrom(type)
                   && type.GetConstructor(Type.EmptyTypes) is not null)
               ?? throw new SimException("module.no_entrypoint", $"Module '{moduleId}' has no public parameterless ISimModule implementation.");
    }

    private static ModuleOwnership Ownership(JsonElement root, string source)
    {
        if (!root.TryGetProperty("owns", out var owns))
        {
            return new ModuleOwnership(Array.Empty<string>(), Array.Empty<string>());
        }

        return new ModuleOwnership(Strings(owns, "tables", source), Strings(owns, "migrations", source));
    }

    private static string Text(JsonElement root, string name, string source) =>
        OptionalText(root, name) ?? throw new SimException("module.invalid_manifest", $"{source}: module.json is missing '{name}'.");

    private static string? OptionalText(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static IReadOnlyList<string> Strings(JsonElement root, string name, string source)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return Array.Empty<string>();
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new SimException("module.invalid_manifest", $"{source}: '{name}' must be an array.");
        }

        var items = new List<string>();
        foreach (var element in value.EnumerateArray())
        {
            var text = element.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                items.Add(text);
            }
        }

        return items;
    }
}
