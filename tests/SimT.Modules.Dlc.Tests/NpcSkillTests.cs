using SimT.Contracts.Playing;
using SimT.Core.Kernel;

namespace SimT.Modules.Dlc.Tests;

public sealed class NpcSkillTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void NpcRegistry_loads_skills_for_historical_characters()
    {
        using var kernel = _host.Boot();
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);

        // Quan Vũ: Võ Thánh, Uy Chấn
        var guanYu = registry.FindByCode("npc_0003");
        Assert.NotNull(guanYu);
        Assert.NotEmpty(guanYu.Skills);
        Assert.Contains(guanYu.Skills, s => s.Code == "vo_thanh" && s.Category == "martial" && s.Tier == 5);
        Assert.Contains(guanYu.Skills, s => s.Code == "uy_chan");

        // Gia Cát Lượng: Thần Toán, Bát Trận, Ngọa Long
        var zhugeLiang = registry.FindByCode("npc_0025");
        Assert.NotNull(zhugeLiang);
        Assert.NotEmpty(zhugeLiang.Skills);
        Assert.Contains(zhugeLiang.Skills, s => s.Code == "than_toan" && s.Category == "strategist" && s.Tier == 5);

        // Lưu Bị: Nhân Đức, Độn Tẩu
        var liuBei = registry.FindByCode("npc_0001");
        Assert.NotNull(liuBei);
        Assert.NotEmpty(liuBei.Skills);
        Assert.Contains(liuBei.Skills, s => s.Code == "nhan_duc" && s.Category == "governance");

        // Triệu Vân: Long Đảm
        var zhaoYun = registry.FindByCode("npc_0024");
        Assert.NotNull(zhaoYun);
        Assert.Contains(zhaoYun.Skills, s => s.Code == "long_dam" && s.Tier == 5);

        // Bàng Thống: Phượng Sồ
        var pangTong = registry.FindByCode("npc_0026");
        Assert.NotNull(pangTong);
        Assert.Contains(pangTong.Skills, s => s.Code == "phuong_so" && s.Category == "strategist");

        // Hoa Đà: Thần Y
        var huaTuo = registry.FindByCode("npc_0061");
        Assert.NotNull(huaTuo);
        Assert.Contains(huaTuo.Skills, s => s.Code == "than_y" && s.Category == "special");
    }

    [Fact]
    public void Player_can_learn_and_track_skills()
    {
        using var kernel = _host.Boot();
        var attributes = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        Assert.False(attributes.HasSkill("vo_thanh"));
        Assert.DoesNotContain("vo_thanh", attributes.LearnedSkills);

        var learned = attributes.LearnSkill("vo_thanh");
        Assert.True(learned);
        Assert.True(attributes.HasSkill("vo_thanh"));
        Assert.Contains("vo_thanh", attributes.LearnedSkills);

        // Learning duplicate skill returns false
        var duplicate = attributes.LearnSkill("vo_thanh");
        Assert.False(duplicate);
    }

    [Fact]
    public void Social_mentorship_allows_learning_skills_when_affinity_is_high()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var attributes = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        // Find an NPC in current city
        var localNpcs = registry.FindByCitadel(player.CurrentCityId);
        Assert.NotEmpty(localNpcs);
        var targetNpc = localNpcs.First(n => n.Skills.Count > 0);
        var targetSkill = targetNpc.Skills[0];

        // 1. Initial attempt with low affinity (0) should be politely declined
        var lowAffinityResult = social.LearnSkillFromNpc(targetNpc.Id, targetSkill.Code);
        Assert.Contains("chưa đủ thâm sâu", lowAffinityResult);
        Assert.False(attributes.HasSkill(targetSkill.Code));

        // 2. Chat with NPC repeatedly to raise affinity >= 40
        for (var i = 0; i < 15; i++)
        {
            attributes.Rest(1); // restore stamina/fatigue
            social.ChatWithNpc(targetNpc.Id);
        }

        var rel = social.GetRelationship(targetNpc.Id);
        Assert.True(rel.Affinity >= 40);

        // 3. Mentorship succeeds!
        attributes.Rest(1);
        var successResult = social.LearnSkillFromNpc(targetNpc.Id, targetSkill.Code);
        Assert.Contains("lĩnh hội", successResult);
        Assert.True(attributes.HasSkill(targetSkill.Code));

        // 4. Learning again says already learned
        var alreadyLearnedResult = social.LearnSkillFromNpc(targetNpc.Id, targetSkill.Code);
        Assert.Contains("đã lĩnh hội", alreadyLearnedResult);
    }

    [Fact]
    public void Military_arena_activates_martial_skills_in_combat_summary()
    {
        using var kernel = _host.Boot();
        var military = kernel.Capabilities.Get<IMilitaryCommand>(CapabilityIds.MilitaryCommand);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var attributes = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        var guanYu = registry.FindByCode("npc_0003");
        Assert.NotNull(guanYu);

        var duelResult = military.ChallengeArena(guanYu.Id);
        Assert.NotNull(duelResult);
        Assert.Contains("Võ Thánh", duelResult.CombatSummaryVi);
    }

    [Fact]
    public void Skill_progression_levels_and_breakthrough_thresholds()
    {
        using var kernel = _host.Boot();
        var attributes = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        attributes.LearnSkill("vo_thanh");
        var p1 = attributes.GetSkillProgress("vo_thanh");
        Assert.NotNull(p1);
        Assert.Equal(1, p1.Level);

        // Train 60 EXP -> Still Lv.1
        var leveledUp = attributes.TrainSkill("vo_thanh", 60);
        Assert.False(leveledUp);
        var p2 = attributes.GetSkillProgress("vo_thanh");
        Assert.Equal(1, p2!.Level);

        // Train 40 EXP -> Hits 100+ -> Breakthrough to Lv.2!
        leveledUp = attributes.TrainSkill("vo_thanh", 40);
        Assert.True(leveledUp);
        var p3 = attributes.GetSkillProgress("vo_thanh");
        Assert.Equal(2, p3!.Level);

        // Train 250 EXP -> Breakthrough to Lv.3!
        leveledUp = attributes.TrainSkill("vo_thanh", 250);
        Assert.True(leveledUp);
        var p4 = attributes.GetSkillProgress("vo_thanh");
        Assert.Equal(3, p4!.Level);

        // Train 500 EXP -> Breakthrough to Lv.4!
        leveledUp = attributes.TrainSkill("vo_thanh", 500);
        Assert.True(leveledUp);
        var p5 = attributes.GetSkillProgress("vo_thanh");
        Assert.Equal(4, p5!.Level);

        // Train 1000 EXP -> Breakthrough to Lv.5!
        leveledUp = attributes.TrainSkill("vo_thanh", 1000);
        Assert.True(leveledUp);
        var p6 = attributes.GetSkillProgress("vo_thanh");
        Assert.Equal(5, p6!.Level);

        // Train extensively up to Lv.10 (Hóa Cảnh)
        for (int i = 0; i < 200; i++)
        {
            attributes.TrainSkill("vo_thanh", 100);
        }
        var p10 = attributes.GetSkillProgress("vo_thanh");
        Assert.Equal(10, p10!.Level);

        // Training past Lv.10 is capped and returns false
        leveledUp = attributes.TrainSkill("vo_thanh", 100);
        Assert.False(leveledUp);
    }

    [Fact]
    public void Active_skill_slots_enforce_6_slots_and_tier_caps()
    {
        using var kernel = _host.Boot();
        var attributes = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        // Learn multiple skills
        attributes.LearnSkill("vo_thanh");   // Tier 5
        attributes.LearnSkill("long_dam");   // Tier 5
        attributes.LearnSkill("uy_chan");    // Tier 4
        attributes.LearnSkill("van_nhan_dich"); // Tier 4
        attributes.LearnSkill("don_tau");    // Tier 4
        attributes.LearnSkill("tiep_te");    // Tier 3
        attributes.LearnSkill("moc_nghe");   // Tier 3

        // By default, vo_thanh was auto-equipped into Slot 1
        Assert.True(attributes.IsSkillEquipped("vo_thanh"));

        // Attempting to equip a 2nd Tier 5 skill (long_dam) into Slot 2 is rejected
        var rejectTier5 = attributes.EquipSkill("long_dam", 2, tier: 5);
        Assert.Contains("Chỉ được kích hoạt tối đa 1", rejectTier5);
        Assert.False(attributes.IsSkillEquipped("long_dam"));

        // Equip Tier 4 skills into Slot 2 and Slot 3 -> allowed (max 2)
        var eq1 = attributes.EquipSkill("uy_chan", 2, tier: 4);
        var eq2 = attributes.EquipSkill("van_nhan_dich", 3, tier: 4);
        Assert.Contains("thành công", eq1);
        Assert.Contains("thành công", eq2);

        // Attempting a 3rd Tier 4 skill (don_tau) into Slot 4 is rejected
        var rejectTier4 = attributes.EquipSkill("don_tau", 4, tier: 4);
        Assert.Contains("Chỉ được kích hoạt tối đa 2", rejectTier4);

        // Tier 3 skills can be equipped freely
        var eq3 = attributes.EquipSkill("tiep_te", 4, tier: 3);
        var eq4 = attributes.EquipSkill("moc_nghe", 5, tier: 3);
        Assert.Contains("thành công", eq3);
        Assert.Contains("thành công", eq4);

        // Unequipping frees up slot and tier cap
        var unequipVoThanh = attributes.UnequipSkill(1);
        Assert.Contains("Đã gỡ bỏ", unequipVoThanh);
        Assert.False(attributes.IsSkillEquipped("vo_thanh"));

        // Now long_dam (Tier 5) CAN be equipped into Slot 1
        var eqLongDam = attributes.EquipSkill("long_dam", 1, tier: 5);
        Assert.Contains("thành công", eqLongDam);
        Assert.True(attributes.IsSkillEquipped("long_dam"));
    }

    [Fact]
    public void Mentorship_training_with_npc_increases_proficiency()
    {
        using var kernel = _host.Boot();
        var social = kernel.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var attributes = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        var localNpcs = registry.FindByCitadel(player.CurrentCityId);
        var targetNpc = localNpcs.First(n => n.Skills.Count > 0);
        var targetSkill = targetNpc.Skills[0];

        // 1. Training before learning gives error
        var notLearnedResult = social.TrainSkillWithNpc(targetNpc.Id, targetSkill.Code);
        Assert.Contains("chưa lĩnh hội", notLearnedResult);

        // 2. Chat to get affinity
        for (var i = 0; i < 15; i++)
        {
            attributes.Rest(1);
            social.ChatWithNpc(targetNpc.Id);
        }

        // 3. Learn skill
        attributes.Rest(1);
        social.LearnSkillFromNpc(targetNpc.Id, targetSkill.Code);
        Assert.True(attributes.HasSkill(targetSkill.Code));

        // 4. Train skill with NPC
        attributes.Rest(1);
        var trainResult = social.TrainSkillWithNpc(targetNpc.Id, targetSkill.Code);
        Assert.Contains("thuần thục", trainResult);

        var prog = attributes.GetSkillProgress(targetSkill.Code);
        Assert.NotNull(prog);
        Assert.True(prog.Proficiency > 1);
    }
}
