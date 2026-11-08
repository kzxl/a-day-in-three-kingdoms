using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Contracts.Scenes;

public static class DossierSceneBuilder
{
    private static readonly string[] FeaturedProfileCodes =
        ["npc_0223", "npc_0205", "npc_0206", "npc_0207", "npc_0221", "npc_0222", "npc_0224", "npc_0225"];

    public static DossierScene? Build(
        ICapabilityBroker capabilities,
        string? recommendation = null,
        long selectedNpcId = 0,
        string? initialMode = null)
    {
        if (!capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var reg) || reg is null ||
            !capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) || map is null ||
            !capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) || playerState is null)
            return null;

        var cityId = playerState.CurrentCityId;
        var cityName = map.Find(cityId)?.NameVi ?? "Lạc Dương";
        capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social);
        capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var society);
        capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs);
        capabilities.TryGet<IPoliticalCourt>(CapabilityIds.CourtOffice, out var court);

        social?.CheckAndAwardRosterMilestones();

        var metMap = social?.GetAllMetNpcs().ToDictionary(m => m.NpcId) ?? new Dictionary<long, MetNpcRecord>();
        var relMap = social?.AllRelationships().ToDictionary(r => r.NpcId) ?? new Dictionary<long, RelationshipDef>();
        var factionsById = court?.Factions.ToDictionary(f => f.Id, f => f.NameVi) ?? new Dictionary<long, string>();
        var govFactionByCity = court?.Governances.ToDictionary(g => g.CitadelId, g => g.FactionId) ?? new Dictionary<long, long>();
        var leaderFactionByNpc = court?.Factions
            .Where(f => f.LeaderNpcId > 0)
            .GroupBy(f => f.LeaderNpcId)
            .ToDictionary(g => g.Key, g => g.First().Id) ?? new Dictionary<long, long>();

        var ordered = new List<NpcDef>();
        var seen = new HashSet<long>();

        foreach (var code in FeaturedProfileCodes)
            if (reg.FindByCode(code) is { } pNpc && seen.Add(pNpc.Id)) ordered.Add(pNpc);
        foreach (var lNpc in reg.All.Where(n => n.CurrentCitadelId(society) == cityId))
            if (seen.Add(lNpc.Id)) ordered.Add(lNpc);
        foreach (var aNpc in reg.All)
            if (seen.Add(aNpc.Id)) ordered.Add(aNpc);

        var views = ordered.Select(npc =>
        {
            relMap.TryGetValue(npc.Id, out var rel);
            metMap.TryGetValue(npc.Id, out var metRec);
            var hasMet = metRec is not null || (social?.HasMet(npc.Id) ?? (rel is not null && rel.Affinity != 0));
            var aff = hasMet ? (rel?.Affinity ?? 0) : 0;
            var (_, face, _) = SceneArtCatalog.ResolveNpcArt(npc);
            var profile = SceneArtCatalog.ResolveProfileArt(npc);
            var sk = npc.Skills.FirstOrDefault();
            var a = npc.Attributes;
            var isDeceased = npc.IsDeceased(society);
            var tier = isDeceased
                ? "Đã mất"
                : aff >= 80 ? "Tri kỷ" : aff >= 60 ? "Thân thiết" : aff >= 35 ? "Quen biết" : hasMet ? "Đã gặp" : "Chưa gặp";
            var npcCityId = npc.CurrentCitadelId(society);
            var isRetainer = social?.GetRetainer(npc.Id) is not null;
            var isLocal = !isDeceased && (npcCityId == cityId || isRetainer);
            var bioText = string.IsNullOrWhiteSpace(npc.BioVi)
                ? "Nhân sĩ danh tiếng thời Tam Quốc, hành trạng được dân gian ca tụng."
                : npc.BioVi;
            if (isDeceased && !bioText.Contains("Đã mất", StringComparison.OrdinalIgnoreCase))
                bioText = $"[Đã mất] {bioText}";

            var factionVi = ResolveFactionNameVi(npc, npcCityId, society, factionsById, govFactionByCity, leaderFactionByNpc);
            var roleCategoryVi = ClassifyRoleCategoryVi(npc);
            var firstMetDateText = metRec is not null ? GameDateCodec.Format(metRec.FirstMetDate) : null;
            var metCitadelNameVi = metRec is not null ? (map.Find(metRec.MetCitadelId)?.NameVi ?? cityName) : null;

            return new DossierNpcView(
                npc.Id, npc.Code, npc.NameVi, npc.NameHan ?? string.Empty, npc.StyleNameVi ?? "—",
                RoleTitleVi.FormatPersonality(npc.Personality), factionVi, RoleTitleVi.Format(npc.Role),
                isDeceased ? $"{map.Find(npcCityId)?.NameVi ?? cityName} (Đã mất)" : map.Find(npcCityId)?.NameVi ?? cityName,
                isLocal, hasMet,
                aff, tier, face, profile, sk?.NameVi ?? "Nội Chính Tinh Thông", "A",
                sk?.EffectSummary ?? "Gia tăng hiệu suất công việc và uy vọng tại thành trì.",
                bioText,
                a.War, a.Ldr, a.Int, a.Pol, a.Dip, a.Com, a.Log, a.Cra,
                social?.GetVisibleTraits(npc.Id),
                roleCategoryVi,
                firstMetDateText,
                metCitadelNameVi);
        }).ToList();

        var societyEnabled = society is not null;
        IReadOnlyList<DossierHistoricalEventView> historicalEvents = Array.Empty<DossierHistoricalEventView>();
        if (society is not null)
        {
            historicalEvents = society.GetHistoricalEvents(occurredOnly: false)
                .OrderBy(e => e.ScheduledDate.DayIndex)
                .ThenBy(e => e.Code, StringComparer.Ordinal)
                .Select(e =>
                {
                    var status = (e.Status ?? "pending").Trim().ToLowerInvariant();
                    var isPending = status != "viewed" && status != "occurred";
                    var citName = map.Find(e.PrimaryCitadelId)?.NameVi ?? "Trung Nguyên";
                    var summary = isPending
                        ? "Biến cố chưa xảy ra"
                        : status == "occurred"
                            ? "Không có mặt"
                            : (e.Impacts.FirstOrDefault()?.NoteVi ?? "Đã chứng kiến");
                    return new DossierHistoricalEventView(
                        e.Code,
                        e.ScheduledDate.Year,
                        isPending ? $"Năm {e.ScheduledDate.Year}" : GameDateCodec.Format(e.ScheduledDate),
                        isPending ? "???" : e.TitleVi,
                        isPending ? "???" : citName,
                        summary,
                        isPending ? "pending" : status);
                })
                .ToList();
        }

        var metCount = views.Count(v => v.HasMet);
        var prc = attrs?.EffectivePrc ?? 50;
        var rec = recommendation ?? $"Đang tại {cityName}: chọn danh sĩ để xem ảnh chân dung riêng, biểu đồ bát diện năng lực, bái phỏng hoặc so tài.";
        return new DossierScene(
            cityName,
            metCount,
            views.Count,
            $"{prc} điểm",
            rec,
            selectedNpcId,
            views,
            societyEnabled,
            historicalEvents,
            initialMode);
    }

    private static string ResolveFactionNameVi(
        NpcDef npc,
        long npcCityId,
        ISocietyChronicle? society,
        IReadOnlyDictionary<long, string> factionsById,
        IReadOnlyDictionary<long, long> govFactionByCity,
        IReadOnlyDictionary<long, long> leaderFactionByNpc)
    {
        var actFactionId = society?.GetNpcActivity(npc.Id)?.AllegianceFactionId ?? 0;
        if (actFactionId > 0 && factionsById.TryGetValue(actFactionId, out var actFactionName))
            return actFactionName;

        if (leaderFactionByNpc.TryGetValue(npc.Id, out var leaderFacId) &&
            factionsById.TryGetValue(leaderFacId, out var leaderFacName))
            return leaderFacName;

        if (govFactionByCity.TryGetValue(npcCityId, out var cityFacId) &&
            factionsById.TryGetValue(cityFacId, out var cityFacName))
            return cityFacName;

        return "Quần Hùng";
    }

    public static string ClassifyRoleCategoryVi(NpcDef npc)
    {
        var role = npc.Role ?? string.Empty;
        var a = npc.Attributes;
        if (role.Contains("Thương", StringComparison.OrdinalIgnoreCase) ||
            role.Contains("Phú", StringComparison.OrdinalIgnoreCase) ||
            role.Contains("Chưởng", StringComparison.OrdinalIgnoreCase) ||
            (a.Com >= 72 && a.Com >= a.War && a.Com >= a.Int))
            return "Thương nhân";

        if (a.War >= 75 && a.War >= a.Int)
            return "Võ tướng";

        if (a.Int >= 75)
            return "Mưu sĩ";

        if (a.Pol >= 70 ||
            role.Contains("Thái Thú", StringComparison.OrdinalIgnoreCase) ||
            role.Contains("Châu Mục", StringComparison.OrdinalIgnoreCase) ||
            role.Contains("Thượng Thư", StringComparison.OrdinalIgnoreCase) ||
            role.Contains("Thừa Tướng", StringComparison.OrdinalIgnoreCase))
            return "Quan văn";

        return "Danh sĩ";
    }
}
