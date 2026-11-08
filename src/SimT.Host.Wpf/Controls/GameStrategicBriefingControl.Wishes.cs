using System.Windows;
using System.Windows.Controls;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Core.Kernel;
namespace SimT.Host.Wpf.Controls;
public sealed partial class GameStrategicBriefingControl
{
    public event Action<long>? RequestWishDestination;
    private void AddWishJournal(SimKernel kernel)
    {
        if (!kernel.Capabilities.TryGet<INpcWishes>(INpcWishes.CapabilityId,out var wishes) || wishes is null) return;
        var rows=wishes.Journal();
        var card=new GameSectionCard { Heading="Sổ nhiệm vụ — Ước nguyện",Subtitle="Những lời đã hứa với người tri kỷ",Margin=new Thickness(0,14,0,0) };
        var stack=new StackPanel();
        if (rows.Count==0) stack.Children.Add(new TextBlock { Text="Chưa nhận ước nguyện. Hãy hỏi chuyện người đạt bậc Tri kỷ.",Foreground=TextMuted,TextWrapping=TextWrapping.Wrap });
        foreach(var row in rows)
        {
            var line=new StackPanel { Margin=new Thickness(0,0,0,12) };
            line.Children.Add(new TextBlock { Text=row.NpcName,Foreground=AccentGold,FontWeight=FontWeights.Bold });
            line.Children.Add(new TextBlock { Text=row.RequestVi,Foreground=TextPrimary,TextWrapping=TextWrapping.Wrap });
            line.Children.Add(new TextBlock { Text=$"{row.ProgressVi} · Hạn {GameDateCodec.ShortVi(row.Wish.Deadline)}",Foreground=TextMuted,TextWrapping=TextWrapping.Wrap });
            var actions=new WrapPanel { Margin=new Thickness(0,6,0,0) };
            var go=new GameButton { Content="Đi tới",Margin=new Thickness(0,0,8,0) };
            go.Click+=(_,_)=>RequestWishDestination?.Invoke(row.DestinationCityId);
            actions.Children.Add(go);
            var talk=new GameButton { Content=row.Ready?"Trao":"Gặp NPC" };
            talk.Click+=(_,_)=>RequestTalkNpc?.Invoke(row.Wish.NpcId);
            actions.Children.Add(talk);
            line.Children.Add(actions);
            stack.Children.Add(line);
        }
        foreach(var notice in wishes.RecentNotices())
            stack.Children.Add(new TextBlock { Text=notice,Foreground=TextMuted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,0) });
        card.CardBody=stack;
        _rootContent.Children.Add(card);
    }
}
