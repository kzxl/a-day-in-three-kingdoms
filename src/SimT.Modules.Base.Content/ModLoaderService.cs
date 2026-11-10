using System.Text.Json;
using SimT.Contracts.Data;
using SimT.Contracts.Playing;

namespace SimT.Modules.Base.Content;

/// <summary>
/// Discovers and applies external JSON mod packages (<c>mods/*.json</c>) into the runtime
/// Item Hub and catalog without altering base campaign determinism unless explicitly invoked.
/// </summary>
public sealed class ModLoaderService : IModLoaderService
{
    private readonly IDbSession _db;
    private readonly ItemHubService _hub;
    private readonly Action<ItemDef>? _onItemRegistered;
    private readonly HashSet<string> _appliedMods = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _modsDirectory;

    public ModLoaderService(
        IDbSession db,
        ItemHubService hub,
        Action<ItemDef>? onItemRegistered = null,
        string? modsDirectory = null)
    {
        _db = db;
        _hub = hub;
        _onItemRegistered = onItemRegistered;
        _modsDirectory = modsDirectory ?? Path.Combine(AppContext.BaseDirectory, "mods");
    }

    public IReadOnlyList<ModPackageSummary> DiscoverMods()
    {
        var list = new List<ModPackageSummary>();
        if (!Directory.Exists(_modsDirectory))
        {
            return list;
        }

        foreach (var file in Directory.EnumerateFiles(_modsDirectory, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var json = File.ReadAllText(file);
                var doc = JsonSerializer.Deserialize<ModPackageDto>(json, JsonOpts);
                if (doc is null || string.IsNullOrWhiteSpace(doc.ModId))
                {
                    continue;
                }

                list.Add(new ModPackageSummary(
                    doc.ModId,
                    string.IsNullOrWhiteSpace(doc.NameVi) ? doc.ModId : doc.NameVi,
                    string.IsNullOrWhiteSpace(doc.Author) ? "Cộng đồng Modder" : doc.Author,
                    string.IsNullOrWhiteSpace(doc.Version) ? "1.0.0" : doc.Version,
                    doc.Items?.Count ?? 0,
                    _appliedMods.Contains(doc.ModId)));
            }
            catch
            {
                // Ignore malformed JSON files in mods directory
            }
        }

        return list;
    }

    public string ApplyModPackage(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            return "Mã gói Mod không hợp lệ.";
        }

        if (!Directory.Exists(_modsDirectory))
        {
            CreateSampleModTemplate();
        }

        string? targetFile = null;
        ModPackageDto? targetDoc = null;

        if (Directory.Exists(_modsDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(_modsDirectory, "*.json"))
            {
                try
                {
                    var doc = JsonSerializer.Deserialize<ModPackageDto>(File.ReadAllText(file), JsonOpts);
                    if (doc is not null && string.Equals(doc.ModId, modId, StringComparison.OrdinalIgnoreCase))
                    {
                        targetFile = file;
                        targetDoc = doc;
                        break;
                    }
                }
                catch
                {
                    // Ignore malformed file
                }
            }
        }

        if (targetDoc is null || targetFile is null)
        {
            return $"Không tìm thấy gói Mod '{modId}' trong thư mục mods/.";
        }

        var addedCount = 0;
        var nextId = Math.Max(1000L, _db.ScalarLong("SELECT COALESCE(MAX(id), 100) FROM ct_item") + 1);

        _db.Transaction(() =>
        {
            foreach (var spec in targetDoc.Items ?? [])
            {
                if (string.IsNullOrWhiteSpace(spec.Code) || string.IsNullOrWhiteSpace(spec.NameVi))
                {
                    continue;
                }

                var existingId = _db.ScalarLong("SELECT COALESCE((SELECT id FROM ct_item WHERE code = @c), 0)", DbArgs.One("@c", spec.Code));
                var itemId = existingId > 0 ? existingId : nextId++;

                _db.Execute(
                    """
                    INSERT OR IGNORE INTO ct_item (id, code, name_vi, name_en, category, base_price, shelf_life_days, weight)
                    VALUES (@id, @code, @nvi, @nen, @cat, @price, @shelf, @weight)
                    """,
                    new DbArgs()
                        .Add("@id", itemId)
                        .Add("@code", spec.Code)
                        .Add("@nvi", spec.NameVi)
                        .Add("@nen", string.IsNullOrWhiteSpace(spec.NameEn) ? spec.NameVi : spec.NameEn)
                        .Add("@cat", string.IsNullOrWhiteSpace(spec.Category) ? "luxury" : spec.Category)
                        .Add("@price", Math.Max(1L, spec.BasePrice))
                        .Add("@shelf", Math.Max(0, spec.ShelfLifeDays))
                        .Add("@weight", Math.Max(1, spec.Weight)));

                var itemDef = new ItemDef(
                    itemId,
                    spec.Code,
                    spec.NameVi,
                    string.IsNullOrWhiteSpace(spec.NameEn) ? spec.NameVi : spec.NameEn,
                    string.IsNullOrWhiteSpace(spec.Category) ? "luxury" : spec.Category,
                    Math.Max(1L, spec.BasePrice),
                    Math.Max(0, spec.ShelfLifeDays),
                    Math.Max(1, spec.Weight));

                _onItemRegistered?.Invoke(itemDef);

                var hubEntry = new ItemHubEntry(
                    itemDef,
                    "Kỳ Vật Sáng Tạo (Mod)",
                    "✨",
                    $"[ MOD: {targetDoc.NameVi} ]\n  ★ {spec.NameVi} ({spec.BasePrice} Quan)",
                    string.IsNullOrWhiteSpace(spec.LoreVi) ? $"Kỳ vật mở rộng từ gói Mod '{targetDoc.NameVi}' của tác giả {targetDoc.Author}." : spec.LoreVi,
                    new Dictionary<string, string>
                    {
                        ["Nguồn Gói Mod"] = $"{targetDoc.NameVi} (v{targetDoc.Version})",
                        ["Tác Giả"] = targetDoc.Author ?? "Cộng đồng",
                        ["Giá Gốc"] = $"{itemDef.BasePrice} Quan",
                        ["Khối Lượng"] = $"{itemDef.Weight} cân"
                    },
                    "Tích hợp từ gói Mod JSON bên ngoài.",
                    "Trưng bày trong Bách Vật Đồ Giám và giao thương mở rộng.",
                    "Hỗ trợ mở rộng qua JSON Mod SDK.");

                _hub.RegisterCustomEntry(hubEntry);
                addedCount++;
            }
        });

        _appliedMods.Add(targetDoc.ModId);
        return $"[Mod SDK] Đã nạp thành công gói Mod '{targetDoc.NameVi}' (v{targetDoc.Version}) — bổ sung {addedCount} kỳ vật mới vào Bách Vật Đồ Giám!";
    }

    public string CreateSampleModTemplate()
    {
        Directory.CreateDirectory(_modsDirectory);
        var samplePath = Path.Combine(_modsDirectory, "sample-three-kingdoms-treasures.json");
        if (!File.Exists(samplePath))
        {
            var sample = new ModPackageDto
            {
                ModId = "tk.treasures.pack",
                NameVi = "Tam Quốc Kỳ Trân Dị Bảo Lục",
                Author = "SimT Modding SDK",
                Version = "1.0.0",
                Items =
                [
                    new ModItemSpec(
                        "yeu_dao_thanh_cong",
                        "Thanh Công Bảo Kiếm",
                        "Qinggang Sword",
                        "luxury",
                        850,
                        0,
                        4,
                        "Bảo kiếm chém sắt như chém bùn tương truyền từng thuộc về Hạ Hầu Ân và Triệu Tử Long."),
                    new ModItemSpec(
                        "bach_ngoc_long_boi",
                        "Bạch Ngọc Long Bội",
                        "White Jade Dragon Pendant",
                        "luxury",
                        620,
                        0,
                        1,
                        "Ngọc bội chạm rồng tinh xảo của vương công quý tộc Lạc Dương.")
                ]
            };

            File.WriteAllText(samplePath, JsonSerializer.Serialize(sample, JsonOpts));
        }

        return $"[Mod SDK] Đã khởi tạo gói Mod mẫu tại '{samplePath}'. Bạn có thể chỉnh sửa JSON hoặc nạp trực tiếp ngay trên Bách Vật Đồ Giám!";
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private sealed class ModPackageDto
    {
        public string ModId { get; set; } = "";
        public string NameVi { get; set; } = "";
        public string? Author { get; set; }
        public string? Version { get; set; }
        public List<ModItemSpec>? Items { get; set; }
    }
}
