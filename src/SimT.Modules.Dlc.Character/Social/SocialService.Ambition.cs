using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Character;

internal sealed partial class SocialService
{
    private void ProcessRetainerAmbitionFriction(RetainerDef ret, string name, string formattedDate, long dayIndex)
    {
        var currentRet = GetRetainer(ret.NpcId);
        if (currentRet is null) return;

        var currentLoyalty = currentRet.Loyalty;
        var psyche = GetPsycheProfile(ret.NpcId);
        var isSworn = GetSwornBrothers().Any(s => s.NpcId == ret.NpcId);

        // 1. Embezzlement risk for accountant / manager with high Ambition and low Integrity
        if (!isSworn && psyche.Ambition >= 70 && psyche.Integrity < 60 && currentLoyalty < 55
            && (ret.RoleAssigned == "accountant" || ret.RoleAssigned == "manager"))
        {
            if (_wallet.Liquid > 3000)
            {
                var embezzled = Math.Min(1500, (long)(_wallet.Liquid * 0.04));
                if (embezzled > 0)
                {
                    try
                    {
                        _wallet.Charge(embezzled, "social.embezzle", $"Thâm hụt quỹ bạc do {name} quản lý", $"embezzle:{ret.Id}:{formattedDate}");
                        currentLoyalty = Math.Max(0, currentLoyalty - 5);
                        _db.Execute(
                            "UPDATE kz_retainer SET loyalty = @loy WHERE id = @id",
                            DbArgs.One("@loy", currentLoyalty).Add("@id", ret.Id));

                        var narrative = $"Gia thần {name} phụ trách {RoleTitleVi(ret.RoleAssigned)}, do dã tâm cao ({psyche.Ambition}%) và bất mãn lòng trung ({currentLoyalty}%), đã bí mật bớt xén công quỹ {embezzled:N0} Quan chia cho thuộc hạ!";
                        RecordRetainerIncident(dayIndex, ret.Id, ret.NpcId, "EMBEZZLE", embezzled, -5, narrative);
                        _log.Warn($"[RetainerAmbition] {narrative}");
                    }
                    catch (Exception) { }
                }
            }
        }
        // 2. Ambition demand raise: if ambition >= 75 and loyalty < 45
        else if (!isSworn && psyche.Ambition >= 75 && currentLoyalty < 45 && currentLoyalty > 20)
        {
            currentLoyalty = Math.Max(0, currentLoyalty - 3);
            _db.Execute(
                "UPDATE kz_retainer SET loyalty = @loy WHERE id = @id",
                DbArgs.One("@loy", currentLoyalty).Add("@id", ret.Id));

            var narrative = $"{name} ỷ vào tài năng và tham vọng lớn, tỏ thái độ coi thường chủ công, lòng trung thành tiếp tục suy thoái.";
            RecordRetainerIncident(dayIndex, ret.Id, ret.NpcId, "DEMAND_RAISE", 0, -3, narrative);
        }

        // 3. Desertion risk: if loyalty <= 20 and not sworn brother
        if (!isSworn && currentLoyalty <= 20)
        {
            _db.Execute("DELETE FROM kz_retainer WHERE id = @id", DbArgs.One("@id", ret.Id));
            var narrative = $"Gia thần {name} lòng dạ bất phục (trung thành {currentLoyalty}%, dã tâm {psyche.Ambition}%), đã dứt áo đào tẩu khỏi phủ đệ, dứt đứt tình nghĩa!";
            RecordRetainerIncident(dayIndex, ret.Id, ret.NpcId, "DESERTION", 0, -currentLoyalty, narrative);
            _log.Warn($"[RetainerAmbition] Desertion: {narrative}");
        }
    }

    private void RecordRetainerIncident(long dayIndex, long retainerId, long npcId, string incidentType, long copperAmount, int loyaltyDelta, string narrativeVi)
    {
        _db.Execute(
            """
            INSERT INTO kz_retainer_audit_log (day_index, retainer_id, npc_id, incident_type, copper_amount, loyalty_delta, narrative_vi)
            VALUES (@day, @ret, @npc, @type, @cop, @loy, @nar)
            """,
            DbArgs.One("@day", dayIndex)
                .Add("@ret", retainerId)
                .Add("@npc", npcId)
                .Add("@type", incidentType)
                .Add("@cop", copperAmount)
                .Add("@loy", loyaltyDelta)
                .Add("@nar", narrativeVi));
    }

    public RetainerAuditResult AuditRetainer(long npcId)
    {
        var ret = GetRetainer(npcId);
        if (ret is null)
        {
            return new RetainerAuditResult(false, 0, 0, "Nhân sĩ này không phải là gia thần dưới trướng của bạn.");
        }

        var npc = _registry.Find(npcId);
        var name = npc?.NameVi ?? $"Gia thần #{npcId}";
        var psyche = GetPsycheProfile(npcId);

        var risk = Math.Clamp((psyche.Ambition * 2 + (100 - psyche.Integrity) + (100 - ret.Loyalty)) / 4, 5, 95);

        // Check if there was embezzlement recorded
        var lastEmbezzle = _db.Query(
            """
            SELECT id, copper_amount FROM kz_retainer_audit_log
            WHERE npc_id = @npc AND incident_type = 'EMBEZZLE'
            ORDER BY id DESC LIMIT 1
            """,
            r => (Id: r.GetInt64(0), Amount: r.GetInt64(1)),
            DbArgs.One("@npc", npcId)).FirstOrDefault();

        var today = _clock.Today.DayIndex;

        if (lastEmbezzle.Amount > 0)
        {
            var recovered = (long)(lastEmbezzle.Amount * 0.6);
            if (recovered > 0)
            {
                _wallet.Pay(recovered, "social.audit_recovery", $"Thu hồi tiền thâm hụt từ cuộc thẩm tra {name}");
            }

            var newLoyalty = Math.Clamp(ret.Loyalty + 10, 35, 100);
            _db.Execute("UPDATE kz_retainer SET loyalty = @loy WHERE id = @id", DbArgs.One("@loy", newLoyalty).Add("@id", ret.Id));

            var narrative = $"Đích thân thẩm tra sổ sách của {name}! Phát hiện dấu vết bớt xén công quỹ {lastEmbezzle.Amount:N0} Quan, nghiêm khắc thu hồi được {recovered:N0} Quan vào ngân khố. {name} kính phục uy nghiêm và mưu lược của chủ công, lòng trung thành chấn chỉnh lên {newLoyalty}/100.";
            RecordRetainerIncident(today, ret.Id, npcId, "AUDIT_CLEAR", recovered, 10, narrative);

            return new RetainerAuditResult(true, risk, lastEmbezzle.Amount, narrative);
        }
        else
        {
            var narrative = $"Đích thân thẩm định sổ sách và phong thái phục vụ của {name}. Sổ sách thu chi hoàn toàn minh bạch, không phát hiện tham nhũng hay manh tâm hai lòng (Chỉ số rủi ro dã tâm: {risk}%).";
            RecordRetainerIncident(today, ret.Id, npcId, "AUDIT_CLEAR", 0, 0, narrative);

            return new RetainerAuditResult(false, risk, 0, narrative);
        }
    }

    public RetainerPlacateResult PlacateRetainer(long npcId, long copperBonus)
    {
        if (copperBonus < 100)
        {
            return new RetainerPlacateResult(false, 0, 0, "Mức ban thưởng tối thiểu phải từ 100 Quan trở lên.");
        }

        if (_wallet.Liquid < copperBonus)
        {
            return new RetainerPlacateResult(false, 0, 0, $"Không đủ ngân lượng để ban thưởng (Cần {copperBonus:N0} Quan, hiện có {_wallet.Liquid:N0} Quan).");
        }

        var ret = GetRetainer(npcId);
        if (ret is null)
        {
            return new RetainerPlacateResult(false, 0, 0, "Nhân sĩ này không phải là gia thần dưới trướng của bạn.");
        }

        var npc = _registry.Find(npcId);
        var name = npc?.NameVi ?? $"Gia thần #{npcId}";

        _wallet.Charge(copperBonus, "social.placate", $"Ban thưởng trấn an gia thần {name}");

        var boost = Math.Clamp((int)(copperBonus / 10), 5, 30);
        var oldLoyalty = ret.Loyalty;
        var newLoyalty = Math.Min(100, oldLoyalty + boost);

        _db.Execute("UPDATE kz_retainer SET loyalty = @loy WHERE id = @id", DbArgs.One("@loy", newLoyalty).Add("@id", ret.Id));

        var today = _clock.Today.DayIndex;
        var narrative = $"Ban thưởng hậu hĩnh {copperBonus:N0} Quan cho {name}! {name} xúc động trước ân điển của chủ công, lòng trung thành tăng từ {oldLoyalty} lên {newLoyalty}/100, dã tâm được xoa dịu.";
        RecordRetainerIncident(today, ret.Id, npcId, "PLACATED", copperBonus, boost, narrative);

        return new RetainerPlacateResult(true, oldLoyalty, newLoyalty, narrative);
    }

    public SwornBrotherAidResult RequestSwornBrotherAid(long npcId)
    {
        var sworn = GetSwornBrothers().FirstOrDefault(s => s.NpcId == npcId);
        if (sworn is null)
        {
            return new SwornBrotherAidResult(false, 0, "Nhân sĩ này không phải là nghĩa huynh đệ kim lan cắt máu ăn thề của bạn.");
        }

        var npc = _registry.Find(npcId);
        var name = npc?.NameVi ?? $"Huynh đệ #{npcId}";

        if (_wallet.Liquid >= 3000)
        {
            return new SwornBrotherAidResult(false, 0, $"Ngân quỹ của bạn hiện vẫn còn {(_wallet.Liquid):N0} Quan, {name} khuyên bạn nên tự lập tự cường, để dành nghĩa trợ vào thời khắc nguy nan nhất.");
        }

        var aidAmount = 1500L;
        _wallet.Pay(aidAmount, "social.sworn_aid", $"Nghĩa huynh đệ kim lan {name} tương trợ ngân lượng");

        _db.Execute(
            "UPDATE kz_sworn_brother SET assist_count = assist_count + 1 WHERE npc_id = @id",
            DbArgs.One("@id", npcId));

        var today = _clock.Today.DayIndex;
        var narrative = $"{name} hay tin huynh đệ gặp bước sa cơ lỡ vận, lập tức thân chinh mang tới {aidAmount:N0} Quan tương trợ và nhắn nhủ: 'Đại trượng phu lúc cùng khốn chớ nản lòng, chút ngân lượng này đệ/huynh cứ giữ lấy làm lộ phí dựng lại cơ nghiệp!'";

        RecordRetainerIncident(today, 0, npcId, "SWORN_AID", aidAmount, 0, narrative);

        return new SwornBrotherAidResult(true, aidAmount, narrative);
    }

    public IReadOnlyList<RetainerAuditLogView> GetRecentRetainerIncidents(int limit = 10)
    {
        return _db.Query(
            """
            SELECT id, day_index, retainer_id, npc_id, incident_type, copper_amount, loyalty_delta, narrative_vi
            FROM kz_retainer_audit_log
            ORDER BY id DESC
            LIMIT @limit
            """,
            r => new RetainerAuditLogView(
                r.GetInt64(0),
                r.GetInt64(1),
                r.GetInt64(2),
                r.GetInt64(3),
                r.GetString(4),
                r.GetInt64(5),
                r.GetInt32(6),
                r.GetString(7)),
            DbArgs.One("@limit", limit));
    }
}
