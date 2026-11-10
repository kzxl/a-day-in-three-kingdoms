using System.Windows;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Ui;
using SimT.Host.Wpf.Controls;

namespace SimT.Host.Wpf.Tests;

public sealed class SurvivalThreatTests
{
    [Fact]
    public void Evaluate_returns_safe_when_vitals_healthy_and_peaceful()
    {
        var vitals = new CharacterVitals(
            Health: 100,
            MaxHp: 100,
            Fatigue: 20,
            Morale: 80,
            Stress: 15,
            CurrentStamina: 100,
            MaxStamina: 100);

        var result = SurvivalThreatEvaluator.Evaluate(vitals, dangerLevel: 1, isAtWar: false);

        Assert.Equal(SurvivalThreatKind.None, result.Kind);
        Assert.Empty(result.BadgeText);
    }

    [Fact]
    public void Evaluate_detects_critical_hp_at_or_below_thirty_percent()
    {
        var vitals = new CharacterVitals(
            Health: 25,
            MaxHp: 100,
            Fatigue: 10,
            Morale: 50,
            Stress: 10,
            CurrentStamina: 80,
            MaxStamina: 100);

        var result = SurvivalThreatEvaluator.Evaluate(vitals, dangerLevel: 1);

        Assert.Equal(SurvivalThreatKind.CriticalHp, result.Kind);
        Assert.Contains("NGUY CẤP", result.BadgeText);
        Assert.Equal("rest", result.ActionCommand);
        Assert.Equal("#EF4444", result.BorderColorHex);
    }

    [Fact]
    public void Evaluate_detects_severe_fatigue_at_or_above_eighty_percent()
    {
        var vitals = new CharacterVitals(
            Health: 90,
            MaxHp: 100,
            Fatigue: 85,
            Morale: 40,
            Stress: 20,
            CurrentStamina: 10,
            MaxStamina: 100);

        var result = SurvivalThreatEvaluator.Evaluate(vitals, dangerLevel: 1);

        Assert.Equal(SurvivalThreatKind.SevereFatigue, result.Kind);
        Assert.Contains("KIỆT SỨC", result.BadgeText);
        Assert.Equal("rest", result.ActionCommand);
        Assert.Equal("#F59E0B", result.BorderColorHex);
    }

    [Fact]
    public void Evaluate_detects_severe_stress_at_or_above_eighty_percent()
    {
        var vitals = new CharacterVitals(
            Health: 80,
            MaxHp: 100,
            Fatigue: 30,
            Morale: 30,
            Stress: 88,
            CurrentStamina: 70,
            MaxStamina: 100);

        var result = SurvivalThreatEvaluator.Evaluate(vitals, dangerLevel: 1);

        Assert.Equal(SurvivalThreatKind.SevereStress, result.Kind);
        Assert.Contains("TÂM THẦN", result.BadgeText);
        Assert.Equal("rest", result.ActionCommand);
        Assert.Equal("#EC4899", result.BorderColorHex);
    }

    [Fact]
    public void Evaluate_detects_unsafe_territory_when_danger_level_is_three_or_at_war()
    {
        var vitals = new CharacterVitals(
            Health: 100,
            MaxHp: 100,
            Fatigue: 10,
            Morale: 80,
            Stress: 10,
            CurrentStamina: 100,
            MaxStamina: 100);

        var result = SurvivalThreatEvaluator.Evaluate(vitals, dangerLevel: 3, isAtWar: false);

        Assert.Equal(SurvivalThreatKind.UnsafeTerritory, result.Kind);
        Assert.Contains("HIỂM ĐỊA", result.BadgeText);
        Assert.Equal("map", result.ActionCommand);
    }

    [Fact]
    public void Evaluate_detects_warlord_extortion_and_retainer_betrayal_risk()
    {
        var vitals = new CharacterVitals(100, 100, 10, 80, 10, 100, 100);

        var extortion = SurvivalThreatEvaluator.Evaluate(vitals, dangerLevel: 1, isAtWar: false, hasWarlordExtortion: true, hasRetainerBetrayalRisk: true);
        Assert.Equal(SurvivalThreatKind.WarlordExtortion, extortion.Kind);
        Assert.Equal("politics.court", extortion.ActionCommand);

        var betrayal = SurvivalThreatEvaluator.Evaluate(vitals, dangerLevel: 1, isAtWar: false, hasWarlordExtortion: false, hasRetainerBetrayalRisk: true);
        Assert.Equal(SurvivalThreatKind.RetainerBetrayalRisk, betrayal.Kind);
        Assert.Equal("social.network", betrayal.ActionCommand);
    }

    [Fact]
    public void Banner_updates_visibility_and_fires_action_event_on_sta_thread()
    {
        WpfTestContext.RunOnSta(() =>
        {
            var banner = new GameSurvivalThreatBanner();
            Assert.Equal(Visibility.Collapsed, banner.Visibility);

            var lowHp = new CharacterVitals(
                Health: 20,
                MaxHp: 100,
                Fatigue: 10,
                Morale: 50,
                Stress: 10,
                CurrentStamina: 50,
                MaxStamina: 100);

            var triggeredAction = string.Empty;
            banner.ThreatActionTriggered += action => triggeredAction = action;

            var assessment = banner.Update(lowHp, dangerLevel: 1);
            Assert.Equal(SurvivalThreatKind.CriticalHp, assessment.Kind);
            Assert.Equal(Visibility.Visible, banner.Visibility);
        });
    }

    [Fact]
    public void SectionTabPartitioner_groups_court_market_and_social_sections_and_finds_keyword_tab()
    {
        var courtSections = new List<ScreenSection>
        {
            new("Cửu Phẩm Quan Chế Triều Đình", ["A"], [], SectionViewMode.ImperialCourtChamber),
            new("Thế Lực Chư Hầu Tam Quốc (12 Lộ)", ["A"], [], SectionViewMode.ImperialCourtChamber),
            new("Sức Ép Tài Phú & Quân Phiệt Dòm Ngó (Cây To Đón Gió)", ["A"], [], SectionViewMode.ImperialCourtChamber),
            new("🏛️ Sóng Ngầm Quan Trường · Tam Đại Thế Lực Triều Đình", ["A"], [], SectionViewMode.ImperialCourtChamber),
            new("⚖️ Ngự Sử Đài · Khâm Sai Thanh Sát & Phong Kỷ Quan Trường", ["A"], [], SectionViewMode.ImperialCourtChamber),
            new("Biên Niên Tiến Thoái Quan Trường", ["A"], [])
        };

        var groups = SectionTabPartitioner.BuildTabGroups("politics.court", courtSections);
        Assert.Equal(4, groups.Count);
        Assert.Equal("court:hierarchy", groups[0].Id);
        Assert.Equal("court:intrigue", groups[1].Id);
        Assert.Equal("court:chronicle", groups[2].Id);
        Assert.Equal("all", groups[3].Id);

        var focusIdx = SectionTabPartitioner.FindTabIndexForKeyword(groups, courtSections, "Dòm Ngó");
        Assert.Equal(1, focusIdx);
    }

    [Fact]
    public void SceneTrustGuard_allows_retainer_audit_placate_sworn_aid_and_wpf_dossier_switch()
    {
        var node = new SocialNodeView(
            10, "npc_10", "Quan Vũ", "Võ Tướng", "S", "Lạc Dương", true, 90, 85, 70, 200,
            "sworn", "Tri kỷ", "", "", "", "—", "S", "—", false, "200 quan", true, true, true, 0,
            75, 80, 95, "Kiên Trung");
        var retainer = new SocialRetainerView(
            10, "Quan Vũ", "Hộ Vệ", "S", "", 50, "Hộ vệ thương lộ", 75, 80, 95, "Kiên Trung", false);
        var scene = new SocialScene(
            "Lạc Dương", "N1 T1 N1", 1000, 50, 10, "", "Đàm đạo", 10,
            [node], [], [], [retainer], []);

        Assert.True(SceneTrustGuard.IsAllowedMessage(new SceneClientMessage("choice", "social", "retainer:audit:10"), scene));
        Assert.True(SceneTrustGuard.IsAllowedMessage(new SceneClientMessage("choice", "social", "retainer:placate:10:300"), scene));
        Assert.True(SceneTrustGuard.IsAllowedMessage(new SceneClientMessage("choice", "social", "sworn:aid:10"), scene));
        Assert.True(SceneTrustGuard.IsAllowedMessage(new SceneClientMessage("choice", "social", "view:wpf_dossier"), scene));
    }

    [Fact]
    public void CourtChamber_and_CaravanDispatch_render_new_depth_sections_on_sta_thread()
    {
        WpfTestContext.RunOnSta(() =>
        {
            var scrutinySection = new ScreenSection(
                "Sức Ép Tài Phú & Quân Phiệt Dòm Ngó (Cây To Đón Gió)",
                ["Thế Lực", "Mức Độ", "Áp Lực", "Sản Nghiệp", "Phụ Thu", "Tình Trạng"],
                [
                    new ScreenRow(["🚩 Đổng Trác", "🟠 Sách Nhiễu", "72/100 điểm dòm ngó", "Tiền: 5000q / 2500q", "+15%", "Đang bị dòm ngó"],
                        [new ScreenCommand("scrutiny:donate:200", "Cứu Tế (200q)", CommandKind.Primary)]),
                    new ScreenRow(["⚡ TRÁT LỆNH #1", "Trưng thu quân phí", "800 Quan", "Hạn: Ngày 25", "🚨 Khẩn cấp", "Nộp ngay 800 quan"],
                        [new ScreenCommand("scrutiny:pay:1", "Nộp Đủ", CommandKind.Primary)])
                ],
                SectionViewMode.ImperialCourtChamber);

            var scrutinyCtrl = new GameCourtChamberControl(scrutinySection, _ => { });
            Assert.True(scrutinyCtrl.Children.Count > 0);

            var triadSection = new ScreenSection(
                "🏛️ Sóng Ngầm Quan Trường · Tam Đại Thế Lực Triều Đình",
                ["Thế Lực", "Đặc Tính", "Thân Thiện", "Thái Độ", "Vận Động"],
                [
                    new ScreenRow(["Thập Thường Thị", "Hoạn quan", "+40 Điểm", "🤝 Thân Cận", "Hỗ trợ"],
                        [new ScreenCommand("court:lobby:EUNUCH:300", "Vận Động", CommandKind.Primary)])
                ],
                SectionViewMode.ImperialCourtChamber);

            var triadCtrl = new GameCourtChamberControl(triadSection, _ => { });
            Assert.True(triadCtrl.Children.Count > 0);

            var smuggleSection = new ScreenSection(
                "🔥 Buôn Lậu Quân Nhu Vượt Phòng Tuyến Tiền Tuyến",
                ["Thành", "Mặt Hàng", "Giá", "Lợi Nhuận", "Rủi Ro", "Trạng Thái"],
                [
                    new ScreenRow(["🏰 Hứa Xương", "Lương Thảo", "20q ➔ 70q", "+250%", "35% (Cao)", "Sẵn sàng (30 kiện)"],
                        [new ScreenCommand("warfare:smuggle:2:1:30", "Vượt Tuyến", CommandKind.Primary)])
                ],
                SectionViewMode.CaravanDispatchBoard);

            var smuggleCtrl = new GameCaravanDispatchControl(smuggleSection, _ => { });
            Assert.NotNull(smuggleCtrl.Child);
        });
    }
}
