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
        var npcBonds = bonds.ForNpc(npcId);
        if (npcBonds.Count == 0) return "";
        var allies = _db.Query(
            "SELECT npc_id FROM kz_relationship WHERE affinity >= @min",
            r => r.GetInt64(0),
            DbArgs.One("@min", NpcBondRules.CloseAffinity)).ToHashSet();
        if (allies.Count == 0) return "";
        var friend = npcBonds.FirstOrDefault(b => b.Kind is "friend" or "sworn" or "sibling" && allies.Contains(b.Other(npcId)));
        return friend is null ? "" : $"Bạn của {_registry.Find(friend.Other(npcId))?.NameVi}";
    }

    private static bool IsKinBondKind(string kind) =>
        kind is "sworn" or "spouse" or "sibling" or "parent" or "mentor" or "lord";

    private string ApplyBondConsequences(long npcId, int gain, string? introduction = null)
    {
        string notice = "";
        var formattedToday = GameDateCodec.Format(_clock.Today);
        var intro = introduction?.StartsWith("Bạn của ", StringComparison.Ordinal) == true ? introduction : "";
        if (intro.Length > 0)
        {
            notice += " " + intro + ".";
            if (_db.ScalarLong("SELECT COUNT(*) FROM kz_memory WHERE npc_id=@id AND topic='introduction'", DbArgs.One("@id", npcId)) == 0)
                _db.Execute("INSERT INTO kz_memory (npc_id,date,topic,sentiment,affinity_delta,summary_vi) VALUES (@id,@d,'introduction','positive',@delta,@s)",
                    new DbArgs().Add("@id", npcId).Add("@d", formattedToday).Add("@delta", NpcBondRules.IntroductionBonus).Add("@s", intro));
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
                    new DbArgs().Add("@id", ally).Add("@d", formattedToday).Add("@delta", -penalty).Add("@s", message));
                notice += " " + message;
            }

            if (gain >= 4)
            {
                var visited = new HashSet<long> { npcId };
                var targetName = _registry.Find(npcId)?.NameVi ?? "thân nhân";
                var hop1Bonds = bonds.ForNpc(npcId).Where(b => IsKinBondKind(b.Kind)).ToList();
                var hop1Nodes = new List<(long KinId, int Bonus, string KinName)>();

                foreach (var bond in hop1Bonds)
                {
                    long kin = bond.Other(npcId);
                    if (!visited.Add(kin)) continue;
                    int bonus = Math.Max(1, gain / 3);
                    var kinName = _registry.Find(kin)?.NameVi ?? "Tri kỷ";
                    string message = $"{kinName} ghi nhận bạn đối đãi tốt với {targetName} (+{bonus} Hảo cảm).";
                    _db.Execute(
                        "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
                        "VALUES (@id, @aff, 50, 'Thân thiết', 0, @d) " +
                        "ON CONFLICT(npc_id) DO UPDATE SET affinity = MIN(100, affinity + @b), last_interaction_date = @d",
                        new DbArgs().Add("@id", kin).Add("@aff", bonus).Add("@b", bonus).Add("@d", formattedToday));
                    _db.Execute(
                        "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) VALUES (@id, @d, 'bond_gratitude', 'positive', @delta, @s)",
                        new DbArgs().Add("@id", kin).Add("@d", formattedToday).Add("@delta", bonus).Add("@s", message));
                    notice += " " + message;
                    hop1Nodes.Add((kin, bonus, kinName));
                }

                // 2-hop Spreading Activation across kin network for major favors (gain >= 9)
                if (gain >= 9 && hop1Nodes.Count > 0)
                {
                    foreach (var (hop1Id, hop1Bonus, hop1Name) in hop1Nodes)
                    {
                        int hop2Bonus = Math.Max(1, hop1Bonus / 2);
                        foreach (var bond2 in bonds.ForNpc(hop1Id).Where(b => IsKinBondKind(b.Kind)))
                        {
                            long hop2 = bond2.Other(hop1Id);
                            if (!visited.Add(hop2)) continue;
                            var hop2Name = _registry.Find(hop2)?.NameVi ?? "Bằng hữu";
                            string msg2 = $"{hop2Name} nghe {hop1Name} khen ngợi nghĩa khí của bạn với {targetName} (+{hop2Bonus} Hảo cảm).";
                            _db.Execute(
                                "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
                                "VALUES (@id, @aff, 50, 'Thiện cảm', 0, @d) " +
                                "ON CONFLICT(npc_id) DO UPDATE SET affinity = MIN(100, affinity + @b), last_interaction_date = @d",
                                new DbArgs().Add("@id", hop2).Add("@aff", hop2Bonus).Add("@b", hop2Bonus).Add("@d", formattedToday));
                            _db.Execute(
                                "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) VALUES (@id, @d, 'bond_gratitude', 'positive', @delta, @s)",
                                new DbArgs().Add("@id", hop2).Add("@d", formattedToday).Add("@delta", hop2Bonus).Add("@s", msg2));
                        }
                    }
                }
            }
        }
        else if (gain <= -3)
        {
            var visited = new HashSet<long> { npcId };
            var targetName = _registry.Find(npcId)?.NameVi ?? "thân nhân";
            var hop1Bonds = bonds.ForNpc(npcId).Where(b => IsKinBondKind(b.Kind)).ToList();
            var hop1Nodes = new List<(long KinId, int Penalty, string KinName)>();

            foreach (var bond in hop1Bonds)
            {
                long kin = bond.Other(npcId);
                if (!visited.Add(kin)) continue;
                int penalty = Math.Max(1, Math.Abs(gain) / 2);
                var kinName = _registry.Find(kin)?.NameVi ?? "Tri kỷ";
                string message = $"{kinName} bất bình vì bạn đắc tội với {targetName} (-{penalty} Hảo cảm).";
                _db.Execute(
                    "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
                    "VALUES (@id, @aff, 50, 'Hiềm khích', 0, @d) " +
                    "ON CONFLICT(npc_id) DO UPDATE SET affinity = MAX(-100, affinity - @p), last_interaction_date = @d",
                    new DbArgs().Add("@id", kin).Add("@aff", -penalty).Add("@p", penalty).Add("@d", formattedToday));
                _db.Execute(
                    "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) VALUES (@id, @d, 'feud', 'negative', @delta, @s)",
                    new DbArgs().Add("@id", kin).Add("@d", formattedToday).Add("@delta", -penalty).Add("@s", message));
                notice += " " + message;
                hop1Nodes.Add((kin, penalty, kinName));
            }

            if (gain <= -10 && hop1Nodes.Count > 0)
            {
                foreach (var (hop1Id, hop1Penalty, hop1Name) in hop1Nodes)
                {
                    int hop2Penalty = Math.Max(1, hop1Penalty / 2);
                    foreach (var bond2 in bonds.ForNpc(hop1Id).Where(b => IsKinBondKind(b.Kind)))
                    {
                        long hop2 = bond2.Other(hop1Id);
                        if (!visited.Add(hop2)) continue;
                        var hop2Name = _registry.Find(hop2)?.NameVi ?? "Thân tộc";
                        string msg2 = $"{hop2Name} nghe {hop1Name} kể chuyện bạn kết oán với {targetName} (-{hop2Penalty} Hảo cảm).";
                        _db.Execute(
                            "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
                            "VALUES (@id, @aff, 50, 'Hiềm khích', 0, @d) " +
                            "ON CONFLICT(npc_id) DO UPDATE SET affinity = MAX(-100, affinity - @p), last_interaction_date = @d",
                            new DbArgs().Add("@id", hop2).Add("@aff", -hop2Penalty).Add("@p", hop2Penalty).Add("@d", formattedToday));
                        _db.Execute(
                            "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) VALUES (@id, @d, 'feud', 'negative', @delta, @s)",
                            new DbArgs().Add("@id", hop2).Add("@d", formattedToday).Add("@delta", -hop2Penalty).Add("@s", msg2));
                    }
                }
            }
        }

        return notice;
    }
}
