namespace SimT.Contracts.Playing;

/// <summary>
/// Custom item specification defined in an external JSON mod package (<c>mods/*.json</c>).
/// </summary>
public sealed record ModItemSpec(
    string Code,
    string NameVi,
    string NameEn,
    string Category,
    long BasePrice,
    int ShelfLifeDays,
    int Weight,
    string LoreVi = "",
    string SubCategoryVi = "Kỳ Vật Sáng Tạo (Mod)",
    string VisualIcon = "✨");

/// <summary>
/// Metadata summary of a discovered community mod package.
/// </summary>
public sealed record ModPackageSummary(
    string ModId,
    string TitleVi,
    string Author,
    string Version,
    int ItemCount,
    bool IsApplied,
    string SourcePath = "");

/// <summary>
/// External JSON modding loader and workshop template generator owned by <c>base.content</c>.
/// </summary>
public interface IModLoaderService
{
    IReadOnlyList<ModPackageSummary> DiscoverMods();

    string ApplyModPackage(string modId);

    string CreateSampleModTemplate();
}
