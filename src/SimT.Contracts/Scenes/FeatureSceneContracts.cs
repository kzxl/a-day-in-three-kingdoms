using System.Text.Json.Serialization;

namespace SimT.Contracts.Scenes;

/// <summary>
/// Interactive World Map scene payload (<c>BanDo.dc.html</c>) rendered in WebView2.
/// </summary>
public sealed partial record MapScene(
    [property: JsonPropertyName("currentCityId")] long CurrentCityId,
    [property: JsonPropertyName("currentCityName")] string CurrentCityName,
    [property: JsonPropertyName("dateText")] string DateText,
    [property: JsonPropertyName("cash")] long Cash,
    [property: JsonPropertyName("fatigue")] int Fatigue,
    [property: JsonPropertyName("mapImagePath")] string MapImagePath,
    [property: JsonPropertyName("playerPortraitPath")] string PlayerPortraitPath,
    [property: JsonPropertyName("recommendation")] string Recommendation,
    [property: JsonPropertyName("cities")] IReadOnlyList<MapCityView> Cities,
    [property: JsonPropertyName("routes")] IReadOnlyList<MapRouteView> Routes,
    [property: JsonPropertyName("currentBag")] IReadOnlyList<MapBagItemView>? CurrentBag = null,
    [property: JsonPropertyName("season")] string Season = "spring",
    IReadOnlyList<MapFactionView>? Factions = null)
{
    [JsonPropertyName("kind")]
    public string Kind => "map";

    [JsonPropertyName("factions")]
    public IReadOnlyList<MapFactionView> Factions { get; init; } = Factions ?? Array.Empty<MapFactionView>();
}

public sealed record MapFactionView(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("nameVi")] string NameVi,
    [property: JsonPropertyName("color")] string Color,
    [property: JsonPropertyName("cityCount")] int CityCount);

public sealed record MapBagItemView(
    [property: JsonPropertyName("itemId")] long ItemId,
    [property: JsonPropertyName("nameVi")] string NameVi,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("basePrice")] long BasePrice);

public sealed record MapCityView(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("nameVi")] string NameVi,
    [property: JsonPropertyName("regionVi")] string RegionVi,
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("priceFactorPermille")] int PriceFactorPermille,
    [property: JsonPropertyName("dangerLevel")] int DangerLevel,
    [property: JsonPropertyName("biomeVi")] string BiomeVi,
    [property: JsonPropertyName("climateVi")] string ClimateVi,
    [property: JsonPropertyName("topographyVi")] string TopographyVi,
    [property: JsonPropertyName("primarySpecialtyVi")] string PrimarySpecialtyVi,
    [property: JsonPropertyName("secondarySpecialtyVi")] string SecondarySpecialtyVi,
    [property: JsonPropertyName("quotes")] IReadOnlyList<MapTravelQuoteView> Quotes,
    [property: JsonPropertyName("storedItemsSummaryVi")] string StoredItemsSummaryVi = "",
    [property: JsonPropertyName("factionId")] long FactionId = 0,
    [property: JsonPropertyName("factionNameVi")] string FactionNameVi = "",
    [property: JsonPropertyName("factionColor")] string FactionColor = "",
    [property: JsonPropertyName("governorNameVi")] string GovernorNameVi = "");

public sealed record MapTravelQuoteView(
    [property: JsonPropertyName("modeKey")] string ModeKey,
    [property: JsonPropertyName("nameVi")] string NameVi,
    [property: JsonPropertyName("noteVi")] string NoteVi,
    [property: JsonPropertyName("baseDays")] int BaseDays,
    [property: JsonPropertyName("actualDays")] int ActualDays,
    [property: JsonPropertyName("fee")] long Fee,
    [property: JsonPropertyName("rations")] int Rations,
    [property: JsonPropertyName("fatigue")] int Fatigue,
    [property: JsonPropertyName("pathNodeIds")] IReadOnlyList<long> PathNodeIds,
    [property: JsonPropertyName("carryCapacity")] int CarryCapacity = 40,
    [property: JsonPropertyName("danger")] int Danger = 0,
    [property: JsonPropertyName("blockedReasonVi")] string BlockedReasonVi = "");

public sealed record MapRouteView(
    [property: JsonPropertyName("fromId")] long FromId,
    [property: JsonPropertyName("toId")] long ToId,
    [property: JsonPropertyName("days")] int Days,
    [property: JsonPropertyName("danger")] int Danger = 0,
    [property: JsonPropertyName("blockedReasonVi")] string BlockedReasonVi = "");

/// <summary>
/// Interactive Social Network scene payload (<c>NhanDuyen.dc.html</c>) rendered in WebView2.
/// </summary>
public sealed record SocialScene(
    [property: JsonPropertyName("currentCityName")] string CurrentCityName,
    [property: JsonPropertyName("dateText")] string DateText,
    [property: JsonPropertyName("cash")] long Cash,
    [property: JsonPropertyName("fame")] long Fame,
    [property: JsonPropertyName("fatigue")] int Fatigue,
    [property: JsonPropertyName("playerPortraitPath")] string PlayerPortraitPath,
    [property: JsonPropertyName("recommendation")] string Recommendation,
    [property: JsonPropertyName("selectedNpcId")] long SelectedNpcId,
    [property: JsonPropertyName("nodes")] IReadOnlyList<SocialNodeView> Nodes,
    [property: JsonPropertyName("skills")] IReadOnlyList<SocialSkillView> Skills,
    [property: JsonPropertyName("letters")] IReadOnlyList<SocialLetterView> Letters,
    [property: JsonPropertyName("retainers")] IReadOnlyList<SocialRetainerView> Retainers,
    [property: JsonPropertyName("giftItems")] IReadOnlyList<SocialGiftItemView> GiftItems,
    [property: JsonPropertyName("world")] NpcBondScene? World = null)
{
    [JsonPropertyName("kind")]
    public string Kind => "social";

    public static IReadOnlyList<SocialSkillView> BuildSkillViews(
        SimT.Contracts.Playing.INpcRegistry reg,
        IReadOnlyList<SimT.Contracts.Playing.PlayerSkillProgressDef> progresses,
        IReadOnlyList<SimT.Contracts.Playing.NpcDef> localNpcs)
    {
        var list = new List<SocialSkillView>();
        var skillOwners = new Dictionary<string, (SimT.Contracts.Playing.NpcDef Npc, SimT.Contracts.Playing.NpcSkillDef Skill)>(StringComparer.OrdinalIgnoreCase);
        foreach (var npc in localNpcs.Concat(reg.All))
            foreach (var sk in npc.Skills)
                skillOwners.TryAdd(sk.Code, (npc, sk));

        foreach (var p in progresses)
        {
            skillOwners.TryGetValue(p.SkillCode, out var owner);
            var name = owner.Skill?.NameVi ?? p.SkillCode;
            var rank = p.Tier switch { >= 5 => "S", 4 => "A", 3 => "B", _ => "C" };
            var teacher = owner.Npc is not null ? RoleTitleVi.ShortName(owner.Npc.NameVi) : "Danh sư";
            list.Add(new SocialSkillView(
                p.SkillCode, name, name[..1], rank, owner.Npc?.Id ?? 0, teacher,
                owner.Skill?.EffectSummary ?? "Gia tăng năng lực", "Đã lĩnh hội",
                true, p.IsEquipped, p.EquippedSlot ?? 0, p.Level, $"Cấp {p.Level}"));
        }

        foreach (var (code, (npc, sk)) in skillOwners.Take(12))
        {
            if (list.Any(x => string.Equals(x.SkillCode, code, StringComparison.OrdinalIgnoreCase)))
                continue;
            var rank = sk.Tier switch { >= 5 => "S", 4 => "A", 3 => "B", _ => "C" };
            var reqAff = sk.Tier switch { >= 5 => 75, 4 => 55, _ => 35 };
            var teacher = RoleTitleVi.ShortName(npc.NameVi);
            list.Add(new SocialSkillView(
                code, sk.NameVi, sk.NameVi[..1], rank, npc.Id, teacher,
                sk.EffectSummary, $"Học từ {teacher}, cần hảo cảm {reqAff}",
                false, false, 0, 1, "Chưa lĩnh hội"));
            if (list.Count >= 9) break;
        }
        return list;
    }
}

public sealed record SocialNodeView(
    [property: JsonPropertyName("npcId")] long NpcId,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("nameVi")] string NameVi,
    [property: JsonPropertyName("roleVi")] string RoleVi,
    [property: JsonPropertyName("rankCode")] string RankCode,
    [property: JsonPropertyName("cityNameVi")] string CityNameVi,
    [property: JsonPropertyName("here")] bool Here,
    [property: JsonPropertyName("affinity")] int Affinity,
    [property: JsonPropertyName("trust")] int Trust,
    [property: JsonPropertyName("requiredAffinity")] int RequiredAffinity,
    [property: JsonPropertyName("signingFee")] int SigningFee,
    [property: JsonPropertyName("tierKey")] string TierKey,
    [property: JsonPropertyName("tierTitleVi")] string TierTitleVi,
    [property: JsonPropertyName("facePath")] string FacePath,
    [property: JsonPropertyName("profilePath")] string ProfilePath,
    [property: JsonPropertyName("skillCode")] string SkillCode,
    [property: JsonPropertyName("skillNameVi")] string SkillNameVi,
    [property: JsonPropertyName("skillRank")] string SkillRank,
    [property: JsonPropertyName("skillEffectVi")] string SkillEffectVi,
    [property: JsonPropertyName("skillLearned")] bool SkillLearned,
    [property: JsonPropertyName("recruitFeeText")] string RecruitFeeText,
    [property: JsonPropertyName("canRecruit")] bool CanRecruit,
    [property: JsonPropertyName("isRetainer")] bool IsRetainer,
    [property: JsonPropertyName("isSworn")] bool IsSworn,
    [property: JsonPropertyName("angleDeg")] double AngleDeg);

public sealed record SocialSkillView(
    [property: JsonPropertyName("skillCode")] string SkillCode,
    [property: JsonPropertyName("nameVi")] string NameVi,
    [property: JsonPropertyName("hanzi")] string Hanzi,
    [property: JsonPropertyName("rank")] string Rank,
    [property: JsonPropertyName("teacherNpcId")] long TeacherNpcId,
    [property: JsonPropertyName("teacherNameVi")] string TeacherNameVi,
    [property: JsonPropertyName("effectVi")] string EffectVi,
    [property: JsonPropertyName("needVi")] string NeedVi,
    [property: JsonPropertyName("learned")] bool Learned,
    [property: JsonPropertyName("equipped")] bool Equipped,
    [property: JsonPropertyName("equippedSlot")] int EquippedSlot,
    [property: JsonPropertyName("level")] int Level,
    [property: JsonPropertyName("levelTitleVi")] string LevelTitleVi);

public sealed record SocialLetterView(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("senderNameVi")] string SenderNameVi,
    [property: JsonPropertyName("senderFacePath")] string SenderFacePath,
    [property: JsonPropertyName("dateText")] string DateText,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("attachmentSummary")] string AttachmentSummary,
    [property: JsonPropertyName("isRead")] bool IsRead,
    [property: JsonPropertyName("hasClaimableGift")] bool HasClaimableGift);

public sealed record SocialRetainerView(
    [property: JsonPropertyName("npcId")] long NpcId,
    [property: JsonPropertyName("nameVi")] string NameVi,
    [property: JsonPropertyName("roleVi")] string RoleVi,
    [property: JsonPropertyName("rankCode")] string RankCode,
    [property: JsonPropertyName("facePath")] string FacePath,
    [property: JsonPropertyName("monthlyStipend")] long MonthlyStipend,
    [property: JsonPropertyName("bonusSummaryVi")] string BonusSummaryVi,
    [property: JsonPropertyName("loyalty")] int Loyalty);

public sealed record SocialGiftItemView(
    [property: JsonPropertyName("itemId")] long ItemId,
    [property: JsonPropertyName("nameVi")] string NameVi,
    [property: JsonPropertyName("heldQuantity")] int HeldQuantity);

/// <summary>
/// Interactive Market Stall scene payload (<c>Cho.dc.html</c>) rendered in WebView2.
/// </summary>
public sealed record MarketScene(
    [property: JsonPropertyName("currentCityId")] long CurrentCityId,
    [property: JsonPropertyName("currentCityName")] string CurrentCityName,
    [property: JsonPropertyName("regionVi")] string RegionVi,
    [property: JsonPropertyName("seasonVi")] string SeasonVi,
    [property: JsonPropertyName("priceFactorPermille")] int PriceFactorPermille,
    [property: JsonPropertyName("dateText")] string DateText,
    [property: JsonPropertyName("cash")] long Cash,
    [property: JsonPropertyName("bagUsed")] int BagUsed,
    [property: JsonPropertyName("bagCapacity")] int BagCapacity,
    [property: JsonPropertyName("bannerImagePath")] string BannerImagePath,
    [property: JsonPropertyName("recommendation")] string Recommendation,
    [property: JsonPropertyName("selectedItemId")] long SelectedItemId,
    [property: JsonPropertyName("items")] IReadOnlyList<MarketItemView> Items)
{
    [JsonPropertyName("kind")]
    public string Kind => "market";
}

public sealed record MarketItemView(
    [property: JsonPropertyName("itemId")] long ItemId,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("nameVi")] string NameVi,
    [property: JsonPropertyName("hanzi")] string Hanzi,
    [property: JsonPropertyName("categoryVi")] string CategoryVi,
    [property: JsonPropertyName("originVi")] string OriginVi,
    [property: JsonPropertyName("buyPrice")] long BuyPrice,
    [property: JsonPropertyName("sellPrice")] long SellPrice,
    [property: JsonPropertyName("basePrice")] long BasePrice,
    [property: JsonPropertyName("stock")] int Stock,
    [property: JsonPropertyName("held")] int Held,
    [property: JsonPropertyName("shelfLifeDays")] int ShelfLifeDays,
    [property: JsonPropertyName("iconPath")] string IconPath,
    [property: JsonPropertyName("historyPrices")] IReadOnlyList<long> HistoryPrices,
    [property: JsonPropertyName("bestSellCities")] IReadOnlyList<MarketArbitrageView> BestSellCities);

public sealed record MarketArbitrageView(
    [property: JsonPropertyName("cityId")] long CityId,
    [property: JsonPropertyName("cityNameVi")] string CityNameVi,
    [property: JsonPropertyName("travelDays")] int TravelDays,
    [property: JsonPropertyName("estimatedSellPrice")] long EstimatedSellPrice,
    [property: JsonPropertyName("diffPerUnit")] long DiffPerUnit);

/// <summary>
/// Interactive Danh Sĩ &amp; Hồ Sơ scene payload (<c>HoSo.dc.html</c>) rendered in WebView2.
/// </summary>
public sealed record DossierScene(
    [property: JsonPropertyName("currentCityName")] string CurrentCityName,
    [property: JsonPropertyName("metCount")] int MetCount,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("perceptionText")] string PerceptionText,
    [property: JsonPropertyName("recommendation")] string Recommendation,
    [property: JsonPropertyName("selectedNpcId")] long SelectedNpcId,
    [property: JsonPropertyName("npcs")] IReadOnlyList<DossierNpcView> Npcs,
    [property: JsonPropertyName("societyEnabled")] bool SocietyEnabled = false,
    [property: JsonPropertyName("historicalEvents")] IReadOnlyList<DossierHistoricalEventView>? HistoricalEvents = null,
    [property: JsonPropertyName("initialMode")] string? InitialMode = null)
{
    [JsonPropertyName("kind")]
    public string Kind => "dossier";
}

public sealed record DossierHistoricalEventView(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("year")] int Year,
    [property: JsonPropertyName("dateText")] string DateText,
    [property: JsonPropertyName("titleVi")] string TitleVi,
    [property: JsonPropertyName("citadelNameVi")] string CitadelNameVi,
    [property: JsonPropertyName("summaryVi")] string SummaryVi,
    [property: JsonPropertyName("status")] string Status);

public sealed record DossierNpcView(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("nameVi")] string NameVi,
    [property: JsonPropertyName("hanziName")] string HanziName,
    [property: JsonPropertyName("courtesyVi")] string CourtesyVi,
    [property: JsonPropertyName("moodVi")] string MoodVi,
    [property: JsonPropertyName("factionVi")] string FactionVi,
    [property: JsonPropertyName("roleVi")] string RoleVi,
    [property: JsonPropertyName("cityNameVi")] string CityNameVi,
    [property: JsonPropertyName("isLocal")] bool IsLocal,
    [property: JsonPropertyName("hasMet")] bool HasMet,
    [property: JsonPropertyName("affinity")] int Affinity,
    [property: JsonPropertyName("relationTitleVi")] string RelationTitleVi,
    [property: JsonPropertyName("facePath")] string FacePath,
    [property: JsonPropertyName("profilePath")] string ProfilePath,
    [property: JsonPropertyName("skillNameVi")] string SkillNameVi,
    [property: JsonPropertyName("skillRank")] string SkillRank,
    [property: JsonPropertyName("skillDescVi")] string SkillDescVi,
    [property: JsonPropertyName("bioVi")] string BioVi,
    [property: JsonPropertyName("war")] int War,
    [property: JsonPropertyName("ldr")] int Ldr,
    [property: JsonPropertyName("intStat")] int IntStat,
    [property: JsonPropertyName("pol")] int Pol,
    [property: JsonPropertyName("dip")] int Dip,
    [property: JsonPropertyName("com")] int Com,
    [property: JsonPropertyName("logStat")] int LogStat,
    [property: JsonPropertyName("cra")] int Cra,
    [property: JsonPropertyName("traits")] IReadOnlyList<SimT.Contracts.Playing.NpcTraitView>? Traits = null,
    [property: JsonPropertyName("roleCategoryVi")] string? RoleCategoryVi = null,
    [property: JsonPropertyName("firstMetDateText")] string? FirstMetDateText = null,
    [property: JsonPropertyName("metCitadelNameVi")] string? MetCitadelNameVi = null);
