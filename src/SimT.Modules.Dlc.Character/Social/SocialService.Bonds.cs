using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Character;

internal sealed partial class SocialService
{
    private RelationshipDef InitialBondRelationship(long npcId)
    {
        var intro = Introduction(npcId);
        return new RelationshipDef(npcId, intro.Length > 0 ? NpcBondRules.IntroductionBonus : 0, 50,
            intro.Length > 0 ? intro : "Sơ giao", 0, _clock.Today);
    }

    private string Introduction(long npcId)
    {
        if (_capabilities is null || !_capabilities.TryGet<INpcBonds>(INpcBonds.CapabilityId, out var bonds) || bonds is null) return "";
        var allies = AllRelationships().Where(r => r.Affinity >= NpcBondRules.CloseAffinity).Select(r => r.NpcId).ToHashSet();
        var friend = bonds.ForNpc(npcId).FirstOrDefault(b => b.Kind is "friend" or "sworn" or "sibling" && allies.Contains(b.Other(npcId)));
        return friend is null ? "" : $"Bạn của {_registry.Find(friend.Other(npcId))?.NameVi}";
    }

    private string ApplyBondConsequences(long npcId, int gain, string? introduction = null)
    {
        string notice = "";
        var intro = introduction?.StartsWith("Bạn của ", StringComparison.Ordinal) == true ? introduction : "";
        if (intro.Length > 0)
        {
            notice += " " + intro + ".";
            if (_db.ScalarLong("SELECT COUNT(*) FROM kz_memory WHERE npc_id=@id AND topic='introduction'", DbArgs.One("@id", npcId)) == 0)
                _db.Execute("INSERT INTO kz_memory (npc_id,date,topic,sentiment,affinity_delta,summary_vi) VALUES (@id,@d,'introduction','positive',@delta,@s)",
                    new DbArgs().Add("@id", npcId).Add("@d", GameDateCodec.Format(_clock.Today)).Add("@delta", NpcBondRules.IntroductionBonus).Add("@s", intro));
        }

        if (_capabilities is null || !_capabilities.TryGet<INpcBonds>(INpcBonds.CapabilityId, out var bonds) || bonds is null) return notice;

        if (gain > 0)
        {
            foreach (var bond in bonds.ForNpc(npcId).Where(b => b.Kind == "rival"))
            {
                long ally = bond.Other(npcId);
                if (!HasMet(ally) || GetRelationship(ally).Affinity < NpcBondRules.CloseAffinity) continue;
                int penalty = Math.Max(1, gain / NpcBondRules.RivalPenaltyDivisor);
                string message = $"{_registry.Find(ally)?.NameVi} không vui vì bạn kết thân với kẻ thù (-{penalty} Hảo cảm).";
                _db.Execute("UPDATE kz_relationship SET affinity=MAX(-100,affinity-@p) WHERE npc_id=@id", new DbArgs().Add("@p", penalty).Add("@id", ally));
                _db.Execute("INSERT INTO kz_memory (npc_id,date,topic,sentiment,affinity_delta,summary_vi) VALUES (@id,@d,'rival','negative',@delta,@s)",
                    new DbArgs().Add("@id", ally).Add("@d", GameDateCodec.Format(_clock.Today)).Add("@delta", -penalty).Add("@s", message));
                notice += " " + message;
            }

            if (gain >= 4)
            {
                var kinBonds = bonds.ForNpc(npcId).Where(b => b.Kind is "sworn" or "spouse" or "sibling" or "parent" or "mentor" or "lord");
                foreach (var bond in kinBonds)
                {
                    long kin = bond.Other(npcId);
                    if (kin == npcId) continue;
                    int bonus = Math.Max(1, gain / 3);
                    var kinName = _registry.Find(kin)?.NameVi ?? "Tri kỷ";
                    var targetName = _registry.Find(npcId)?.NameVi ?? "thân nhân";
                    string message = $"{kinName} ghi nhận bạn đối đãi tốt với {targetName} (+{bonus} Hảo cảm).";
                    _db.Execute(
                        "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
                        "VALUES (@id, @aff, 50, 'Thân thiết', 0, @d) " +
                        "ON CONFLICT(npc_id) DO UPDATE SET affinity = MIN(100, affinity + @b), last_interaction_date = @d",
                        new DbArgs().Add("@id", kin).Add("@aff", bonus).Add("@b", bonus).Add("@d", GameDateCodec.Format(_clock.Today)));
                    _db.Execute(
                        "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) VALUES (@id, @d, 'bond_gratitude', 'positive', @delta, @s)",
                        new DbArgs().Add("@id", kin).Add("@d", GameDateCodec.Format(_clock.Today)).Add("@delta", bonus).Add("@s", message));
                    notice += " " + message;
                }
            }
        }
        else if (gain <= -3)
        {
            var kinBonds = bonds.ForNpc(npcId).Where(b => b.Kind is "sworn" or "spouse" or "sibling" or "parent" or "mentor" or "lord");
            foreach (var bond in kinBonds)
            {
                long kin = bond.Other(npcId);
                if (kin == npcId) continue;
                int penalty = Math.Max(1, Math.Abs(gain) / 2);
                var kinName = _registry.Find(kin)?.NameVi ?? "Tri kỷ";
                var targetName = _registry.Find(npcId)?.NameVi ?? "thân nhân";
                string message = $"{kinName} bất bình vì bạn đắc tội với {targetName} (-{penalty} Hảo cảm).";
                _db.Execute(
                    "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
                    "VALUES (@id, @aff, 50, 'Hiềm khích', 0, @d) " +
                    "ON CONFLICT(npc_id) DO UPDATE SET affinity = MAX(-100, affinity - @p), last_interaction_date = @d",
                    new DbArgs().Add("@id", kin).Add("@aff", -penalty).Add("@p", penalty).Add("@d", GameDateCodec.Format(_clock.Today)));
                _db.Execute(
                    "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) VALUES (@id, @d, 'feud', 'negative', @delta, @s)",
                    new DbArgs().Add("@id", kin).Add("@d", GameDateCodec.Format(_clock.Today)).Add("@delta", -penalty).Add("@s", message));
                notice += " " + message;
            }
        }

        return notice;
    }
}
