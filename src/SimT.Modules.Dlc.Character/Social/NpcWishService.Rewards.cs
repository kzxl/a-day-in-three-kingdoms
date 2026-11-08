using SimT.Contracts.Scenes;
using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
namespace SimT.Modules.Dlc.Character;
internal sealed partial class NpcWishService
{
    public IReadOnlyList<string> RecentNotices() => context.Db.Query(
        "SELECT summary_vi FROM kz_memory WHERE topic='wish' ORDER BY id DESC LIMIT 5",r=>r.GetString(0));

    public string Give(long npcId)
    {
        var row=ForNpc(npcId);
        if (row is null || row.Wish.State!="accepted" || !row.Ready) return "Chưa đủ điều kiện trao; hãy xem tiến độ trong sổ nhiệm vụ.";
        if (row.Wish.Template=="mentor") return "Hãy chọn người thầy muốn giới thiệu.";
        if (row.Wish.Template=="home") return PayHomeFare(npcId);
        return Complete(row);
    }

    private string Complete(NpcWishProgress row)
    {
        var npcId=row.Wish.NpcId;
        var w=row.Wish;
        var npc=Registry.Find(npcId)!;
        // Choose the gift before charging; an unavailable catalog cannot consume resources.
        string giftCode=npc.Role.ToLowerInvariant() switch
        {
            "physician" or "healer" => "thuoc",
            "blacksmith" or "craftsman" or "artisan" => "sat",
            "farmer" or "peasant" => "gao",
            _ => RoleTitleVi.ToArchetypeKey(npc.Role) switch
            {
                "general" or "archer" or "rebel" => "sat",
                "merchant" => "lua",
                "scholar" or "elder" => "tra",
                _ => "ca_kho"
            }
        };
        var gift=Items.FindByCode(giftCode) ?? Items.All.OrderBy(i=>i.Id).FirstOrDefault();
        if (gift is null) return "Chưa có quà đáp lễ; hãy quay lại sau.";
        string message=$"Đã giúp {npc.NameVi} hoàn thành ước nguyện (+{NpcWishRules.RewardAffinity} thiện cảm), nhận {gift.NameVi}.";
        context.Db.Transaction(()=>
        {
            if (w.Template=="home")
            {
                if (!HomeReached(w)) Wallet.Charge(NpcWishRules.HomeFare,"social.wish",row.RequestVi,$"wish:{w.Id}");
                ReturnHome(w);
            }
            if (w.Template=="shop") Wallet.Charge(w.Quantity,"social.wish",row.RequestVi,$"wish:{w.Id}");
            if (w.Template=="rare" && !Bag.Remove(Player.CurrentCityId,w.ItemId,w.Quantity))
                throw new InvalidOperationException("Hành trang không đủ hàng.");
            if (w.Template=="mentor")
                context.Db.Execute("INSERT OR IGNORE INTO kz_npc_bond VALUES (@a,@b,'mentor',60,@d,'sim')",
                    new DbArgs().Add("@a",w.TargetNpcId).Add("@b",npcId).Add("@d",GameDateCodec.Format(context.Clock.Today)));
            context.Db.Execute("UPDATE kz_npc_wish SET state='completed',target_npc_id=@target WHERE id=@id",DbArgs.One("@id",w.Id).Add("@target",w.TargetNpcId));
            Bag.Add(Player.CurrentCityId,gift.Id,1);
            ChangeAffinity(w,NpcWishRules.RewardAffinity,message,context.Clock.Today);
            Record(w,message,context.Clock.Today);
        });
        return message;
    }
}
