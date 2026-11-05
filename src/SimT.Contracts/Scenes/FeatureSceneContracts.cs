using System.Text.Json.Serialization;

namespace SimT.Contracts.Scenes;

/// <summary>
/// Interactive World Map scene payload (<c>BanDo.dc.html</c>) rendered in WebView2.
/// </summary>
public sealed record MapScene(
    [property: JsonPropertyName("currentCityId")] long CurrentCityId,
    [property: JsonPropertyName("currentCityName")] string CurrentCityName,
    [property: JsonPropertyName("dateText")] string DateText,
    [property: JsonPropertyName("cash")] long Cash,
    [property: JsonPropertyName("fatigue")] int Fatigue,
    [property: JsonPropertyName("mapImagePath")] string MapImagePath,
    [property: JsonPropertyName("playerPortraitPath")] string PlayerPortraitPath,
    [property: JsonPropertyName("recommendation")] string Recommendation,
    [property: JsonPropertyName("cities")] IReadOnlyList<MapCityView> Cities,
    [property: JsonPropertyName("routes")] IReadOnlyList<MapRouteView> Routes)
{
    [JsonPropertyName("kind")]
    public string Kind => "map";
}

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
    [property: JsonPropertyName("quotes")] IReadOnlyList<MapTravelQuoteView> Quotes);

public sealed record MapTravelQuoteView(
    [property: JsonPropertyName("modeKey")] string ModeKey,
    [property: JsonPropertyName("nameVi")] string NameVi,
    [property: JsonPropertyName("noteVi")] string NoteVi,
    [property: JsonPropertyName("baseDays")] int BaseDays,
    [property: JsonPropertyName("actualDays")] int ActualDays,
    [property: JsonPropertyName("fee")] long Fee,
    [property: JsonPropertyName("rations")] int Rations,
    [property: JsonPropertyName("fatigue")] int Fatigue,
    [property: JsonPropertyName("pathNodeIds")] IReadOnlyList<long> PathNodeIds);

public sealed record MapRouteView(
    [property: JsonPropertyName("fromId")] long FromId,
    [property: JsonPropertyName("toId")] long ToId,
    [property: JsonPropertyName("days")] int Days);

/// <summary>
/// Interactive Social Network scene payload (<c>NhanDuyen.dc.html</c>) rendered in WebView2.
/// </summary>
public sealed record SocialScene(
    [property: JsonPropertyName("currentCityName")] string CurrentCityName,
    [property: JsonPropertyName("dateText")] string DateText,
    [property: JsonPropertyName("cash")] long Cash,
    [property: JsonPropertyName("fatigue")] int Fatigue,
    [property: JsonPropertyName("playerPortraitPath")] string PlayerPortraitPath,
    [property: JsonPropertyName("recommendation")] string Recommendation,
    [property: JsonPropertyName("selectedNpcId")] long SelectedNpcId,
    [property: JsonPropertyName("nodes")] IReadOnlyList<SocialNodeView> Nodes,
    [property: JsonPropertyName("skills")] IReadOnlyList<SocialSkillView> Skills,
    [property: JsonPropertyName("letters")] IReadOnlyList<SocialLetterView> Letters,
    [property: JsonPropertyName("retainers")] IReadOnlyList<SocialRetainerView> Retainers,
    [property: JsonPropertyName("giftItems")] IReadOnlyList<SocialGiftItemView> GiftItems)
{
    [JsonPropertyName("kind")]
    public string Kind => "social";
}

public sealed record SocialNodeView(
    [property: JsonPropertyName("npcId")] long NpcId,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("nameVi")] string NameVi,
    [property: JsonPropertyName("roleVi")] string RoleVi,
    [property: JsonPropertyName("cityNameVi")] string CityNameVi,
    [property: JsonPropertyName("here")] bool Here,
    [property: JsonPropertyName("affinity")] int Affinity,
    [property: JsonPropertyName("tierKey")] string TierKey,
    [property: JsonPropertyName("tierTitleVi")] string TierTitleVi,
    [property: JsonPropertyName("facePath")] string FacePath,
    [property: JsonPropertyName("profilePath")] string ProfilePath,
    [property: JsonPropertyName("skillId")] long SkillId,
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
    [property: JsonPropertyName("skillId")] long SkillId,
    [property: JsonPropertyName("nameVi")] string NameVi,
    [property: JsonPropertyName("rank")] string Rank,
    [property: JsonPropertyName("teacherNameVi")] string TeacherNameVi,
    [property: JsonPropertyName("effectVi")] string EffectVi,
    [property: JsonPropertyName("needVi")] string NeedVi,
    [property: JsonPropertyName("learned")] bool Learned,
    [property: JsonPropertyName("equipped")] bool Equipped,
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
    [property: JsonPropertyName("npcs")] IReadOnlyList<DossierNpcView> Npcs)
{
    [JsonPropertyName("kind")]
    public string Kind => "dossier";
}

public sealed record DossierNpcView(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("nameVi")] string NameVi,
    [property: JsonPropertyName("courtesyVi")] string CourtesyVi,
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
    [property: JsonPropertyName("ldr")] int Ldr,
    [property: JsonPropertyName("war")] int War,
    [property: JsonPropertyName("intStat")] int IntStat,
    [property: JsonPropertyName("pol")] int Pol,
    [property: JsonPropertyName("cha")] int Cha,
    [property: JsonPropertyName("com")] int Com,
    [property: JsonPropertyName("prc")] int Prc,
    [property: JsonPropertyName("wil")] int Wil);
