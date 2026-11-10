namespace SimT.Contracts.Playing;

/// <summary>Read & action surface of <c>dlc.politics</c>, provided as the <c>politics.court</c> capability.</summary>
public interface IPoliticalCourt
{
    int Reputation { get; }

    int Merits => 0;

    long GovernorCityId => 0;

    FiefStatusView? PlayerFief => null;

    long OfficeId { get; }

    string? OfficeName { get; }

    long MonthlyStipend { get; }

    IReadOnlyList<OfficeView> Offices { get; }

    long AllegianceFactionId { get; }

    string? AllegianceFactionName { get; }

    IReadOnlyList<FactionView> Factions { get; }

    IReadOnlyList<CityGovernanceView> Governances { get; }

    IReadOnlyList<DecreeLogView> RecentDecrees(int limit = 10);

    IReadOnlyList<CareerLogView> RecentCareerLogs(int limit = 10) => [];

    IReadOnlyList<CourtMemorialView> RecentMemorials(int limit = 10) => [];

    CourtResult AppointGovernor(long citadelId) => CourtResult.Failure("Chưa được hỗ trợ.");

    CourtResult ResignGovernor() => CourtResult.Failure("Chưa được hỗ trợ.");

    CourtResult DevelopFief(string domainCode) => CourtResult.Failure("Chưa được hỗ trợ.");

    CourtResult AppointFiefAide(long npcId, string aideRole) => CourtResult.Failure("Chưa được hỗ trợ.");

    CourtResult SetFiefDirective(string directiveCode) => CourtResult.Failure("Chưa được hỗ trợ.");

    GovernanceMonthlyDigestView? LatestMonthlyDigest => null;

    IReadOnlyList<GovernanceMonthlyDigestView> RecentMonthlyDigests(int limit = 6) => [];

    QuarterlyEvalResult? EvaluateQuarterlyGovernance() => null;

    void SimulateMonthlyFiefAutonomy(SimT.Contracts.Time.GameDate date) { }

    IReadOnlyList<FiefMegaprojectView> GetFiefMegaprojects(long citadelId = 0) => [];

    CourtResult InvestFiefMegaproject(string projectCode, int copperAmount) => CourtResult.Failure("Chưa được hỗ trợ.");

    CourtResult LaunchFiefExpedition(string expeditionType) => CourtResult.Failure("Chưa được hỗ trợ.");

    IReadOnlyList<FiefExpeditionLogView> RecentExpeditionLogs(int limit = 5) => [];

    IReadOnlyList<FiefCrisisView> GetActiveFiefCrises(long citadelId = 0) => [];

    IReadOnlyList<FiefCrisisView> GetRecentFiefCrises(int limit = 5) => [];

    CourtResult ResolveFiefCrisis(long crisisId, string choiceCode) => CourtResult.Failure("Chưa được hỗ trợ.");

    CourtResult TriggerEmergentFiefCrisis() => CourtResult.Failure("Chưa được hỗ trợ.");

    CourtResult SimulateCitadelSiege(string attackerName, int enemyTroops, string tacticalStance) => CourtResult.Failure("Chưa được hỗ trợ.");

    IReadOnlyList<FiefSiegeLogView> RecentSiegeLogs(int limit = 5) => [];

    CourtResult SubmitMemorial(string category, string proposalVi) => CourtResult.Failure("Chưa được hỗ trợ.");

    CourtResult RequestPromotion() => CourtResult.Failure("Chưa được hỗ trợ.");

    CourtResult DemoteOffice(string reasonVi) => CourtResult.Failure("Chưa được hỗ trợ.");

    CourtResult JoinFaction(long factionId);

    CourtResult LeaveFaction();

    CourtResult Serve() => CourtResult.Failure("Chưa được hỗ trợ.");

    WarlordScrutinyView? CurrentScrutiny => null;

    IReadOnlyList<ScrutinyDemandView> ActiveScrutinyDemands => [];

    IReadOnlyList<PhilanthropyLogView> RecentPhilanthropyLogs(int limit = 5) => [];

    CourtResult ResolveScrutinyDemand(long demandId, string choiceCode) => CourtResult.Failure("Chưa được hỗ trợ.");

    CourtResult DonatePhilanthropy(int copperAmount) => CourtResult.Failure("Chưa được hỗ trợ.");

    void SimulateWarlordScrutiny(SimT.Contracts.Time.GameDate date) { }

    IReadOnlyList<CourtFactionStandingView> CourtFactionStandings => [];

    ImperialInspectionResult RespondToImperialInspection(string approachCode) => new(false, "NONE", 0, 0, "Không có cuộc thanh sát nào.");

    CourtResult ShiftCourtFactionStanding(string factionCode, int delta, string reasonVi) => CourtResult.Failure("Chưa được hỗ trợ.");

    CourtResult LobbyCourtFaction(string factionCode, int copperAmount) => CourtResult.Failure("Chưa được hỗ trợ.");

    IReadOnlyList<ImperialInspectionLogView> RecentImperialInspections(int limit = 5) => [];

    CourtResult TriggerImperialInspection() => CourtResult.Failure("Chưa được hỗ trợ.");

    CourtResult Apply(long officeId) => CourtResult.Failure("Chưa được hỗ trợ.");

    CourtResult Resign() => CourtResult.Failure("Chưa được hỗ trợ.");

    CourtResult EnactPolicy(string policyCode, long citadelId);

    void SetCityOwner(long citadelId, long factionId, long? governorNpcId = null) { }

    void AdjustCityStability(long citadelId, int unrestDelta, int defenseDelta) { }

    /// <summary>Boosts the prosperity score of a citadel and alleviates famine status.</summary>
    void BoostCityProsperity(long citadelId, int prosperityDelta, string famineStatusVi, int populationDelta = 0) { }

    CityDemographicsView? GetCityDemographics(long citadelId) => null;

    IReadOnlyList<CityDemographicsView> AllDemographics() => [];

    FamineSimulationResult? SimulateMonthlyCityFoodAndFamine(long citadelId, SimT.Contracts.Time.GameDate date) => null;
    IReadOnlyList<AideCandidateView> GetAideCandidates(long citadelId = 0) => [];
}

public sealed record CityDemographicsView(
    long CitadelId,
    int Population,
    int ProsperityScore,
    int StarvationMonths,
    int MonthlyFoodNeed,
    int MonthlyFoodGot,
    string FamineStatusVi,
    int UpdatedDayIndex);

public sealed record FamineSimulationResult(
    long CitadelId,
    int InitialPopulation,
    int FinalPopulation,
    int InitialProsperity,
    int FinalProsperity,
    int FoodNeeded,
    int FoodSupplied,
    int FoodDeficit,
    int StarvationMonths,
    int OldUnrest,
    int NewUnrest,
    int OldDefense,
    int NewDefense,
    string NarrativeVi);

public sealed record OfficeView(
    long Id,
    string Code,
    string NameVi,
    string NameEn,
    int Rank,
    long MonthlyStipend,
    int ReputationRequired);

public sealed record FactionView(
    long Id,
    string Code,
    string NameVi,
    long LeaderNpcId,
    long CapitalCityId,
    string IdealVi,
    int TroopCount,
    int TreasuryCopper,
    string ColorHex);

public sealed record CityGovernanceView(
    long CitadelId,
    long FactionId,
    long GovernorNpcId,
    int UnrestLevel,
    int DefenseStrength,
    int TaxRatePercent)
{
    public int Security => Math.Clamp(100 - UnrestLevel, 0, 100);
}

public sealed record DecreeLogView(
    long Id,
    long DayIndex,
    long CitadelId,
    string PolicyCode,
    int CostCopper,
    int ReputationGain,
    int UnrestDelta,
    string NarrativeVi);

public readonly record struct CourtResult(bool Ok, string Message)
{
    public static CourtResult Success(string message) => new(true, message);
    public static CourtResult Failure(string message) => new(false, message);
}

public sealed record FiefStatusView(
    long CitadelId,
    string CitadelNameVi,
    int Agriculture,
    int Commerce,
    int Fortification,
    int PublicOrder,
    long LocalTreasury,
    int AppointedDay,
    int Population = 50000,
    int GarrisonTroops = 1000,
    int WeaponsStock = 50,
    long CivilAideNpcId = 0,
    string CivilAideNameVi = "Chưa có",
    long MilitaryAideNpcId = 0,
    string MilitaryAideNameVi = "Chưa có",
    string PolicyDirective = "BALANCE",
    string LastMonthlyDigest = "");

public sealed record AideCandidateView(
    long NpcId,
    string NameVi,
    string Role,
    int Politics,
    int Intellect,
    int War,
    int Charm,
    IReadOnlyList<string> TraitsVi,
    string RecommendationVi,
    bool IsCurrentCivilAide,
    bool IsCurrentMilitaryAide);

public sealed record GovernanceMonthlyDigestView(
    long Id,
    long CitadelId,
    string CitadelNameVi,
    int Year,
    int Month,
    string Directive,
    int PopulationDelta,
    int TreasuryDelta,
    int FoodConsumed,
    int GarrisonDelta,
    int MeritsEarned,
    string CivilActionVi,
    string MilitaryActionVi,
    string SummaryVi);

public sealed record QuarterlyEvalResult(
    long CitadelId,
    string CitadelNameVi,
    string Grade,
    int MeritChange,
    long BonusStipend,
    string RankAction,
    string NarrativeVi);

public sealed record CareerLogView(
    long Id,
    long DayIndex,
    long OldOfficeId,
    string OldOfficeNameVi,
    long NewOfficeId,
    string NewOfficeNameVi,
    string ChangeType,
    string ReasonVi,
    int MeritsDelta);

public sealed record CourtMemorialView(
    long Id,
    long DayIndex,
    string Category,
    string TitleVi,
    string ProposalVi,
    string Outcome,
    int MeritAwarded,
    string ImpactNarrativeVi);

public sealed record FiefMegaprojectView(
    string ProjectCode,
    string NameVi,
    string DescriptionVi,
    string Status,
    int InvestedCopper,
    int RequiredCopper,
    int CompletedDay)
{
    public bool IsCompleted => Status == "COMPLETED" || InvestedCopper >= RequiredCopper;
    public int ProgressPercent => RequiredCopper > 0 ? Math.Clamp((int)(InvestedCopper * 100L / RequiredCopper), 0, 100) : 0;
}

public sealed record FiefExpeditionLogView(
    long Id,
    long CitadelId,
    int DayIndex,
    string ExpeditionType,
    int TroopsDispatched,
    int Casualties,
    int SpoilsCopper,
    int MeritsEarned,
    string NarrativeVi);

public sealed record FiefCrisisView(
    long Id,
    long CitadelId,
    string CrisisCode,
    string TitleVi,
    string DescriptionVi,
    int Severity,
    string Status,
    int CreatedDay,
    int ResolvedDay,
    string ResolutionChoice,
    string OutcomeVi);

public sealed record FiefSiegeLogView(
    long Id,
    long CitadelId,
    int DayIndex,
    string AttackerFactionName,
    int AttackerTroops,
    int DefenseTroops,
    string TacticalStance,
    string Outcome,
    int DefenseCasualties,
    int AttackerCasualties,
    int SpoilsCopper,
    int MeritsEarned,
    string BattlePhasesVi,
    string NarrativeVi);

public sealed record WarlordScrutinyView(
    long FactionId,
    string FactionNameVi,
    string LeaderNameVi,
    int ScrutinyScore,
    string ThreatLevel,
    string StatusSummaryVi,
    long LiquidWealth,
    long ShieldingThreshold,
    int SurchargePercent,
    bool IsEmbargoed,
    int EmbargoRemainingDays,
    IReadOnlyList<ScrutinyDemandView> ActiveDemands);

public sealed record ScrutinyDemandView(
    long Id,
    long FactionId,
    string FactionNameVi,
    string DemandType,
    string TitleVi,
    string DescriptionVi,
    int DemandCopper,
    int DueDayIndex,
    string Status);

public sealed record PhilanthropyLogView(
    long Id,
    long CitadelId,
    int DayIndex,
    int CopperDonated,
    int FameEarned,
    int VirtueEarned,
    int ScrutinyReduced,
    string NarrativeVi);

public sealed record CourtFactionStandingView(
    string FactionCode,
    string NameVi,
    int StandingScore,
    string DescriptionVi,
    string AttitudeVi);

public sealed record ImperialInspectionResult(
    bool Success,
    string Outcome,
    int MeritDelta,
    long CopperDelta,
    string NarrativeVi);

public sealed record ImperialInspectionLogView(
    long Id,
    long DayIndex,
    long CitadelId,
    string InspectorNameVi,
    string ApproachCode,
    string Outcome,
    int MeritDelta,
    long CopperDelta,
    string NarrativeVi);



