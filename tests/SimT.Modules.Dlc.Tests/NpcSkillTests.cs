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
}
