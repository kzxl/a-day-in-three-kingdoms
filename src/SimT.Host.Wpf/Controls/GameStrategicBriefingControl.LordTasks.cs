using System.Windows;
using System.Windows.Controls;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Core.Kernel;
namespace SimT.Host.Wpf.Controls;
public sealed partial class GameStrategicBriefingControl
{
    private void AddLordTaskJournal(SimKernel kernel)
    {
        if (!kernel.Capabilities.TryGet<ILordTasks>(ILordTasks.CapabilityId,out var tasks) || tasks is null) return;
        var row=tasks.Current;
        if (row is null) return;
        var card=new GameSectionCard { Heading="Sổ nhiệm vụ — Công vụ chúa công",Subtitle="Quân lệnh mỗi tháng · Công trạng mở đường quan lộ",Margin=new Thickness(0,14,0,0) };
        var stack=new StackPanel();
        stack.Children.Add(new TextBlock { Text=row.RequestVi,Foreground=AccentGold,FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap });
        stack.Children.Add(new TextBlock { Text=$"{row.ProgressVi} · Hạn {GameDateCodec.ShortVi(row.Task.Deadline)}",Foreground=TextMuted,TextWrapping=TextWrapping.Wrap });
        stack.Children.Add(new TextBlock { Text=$"Thưởng {row.Task.Merit} danh vọng · {row.Task.Bonus} quan · Tín nhiệm {tasks.Trust}/100",Foreground=TextPrimary,TextWrapping=TextWrapping.Wrap });
        var actions=new WrapPanel { Margin=new Thickness(0,8,0,0) };
        var go=new GameButton { Content="Đi tới",Margin=new Thickness(0,0,8,0) };
        go.Click+=(_,_)=>RequestWishDestination?.Invoke(row.DestinationCityId);
        actions.Children.Add(go);
        if (row.Task.Template=="envoy" && !row.Task.Evidence)
        {
            var envoy=new GameButton { Content="Gặp thái thú",Margin=new Thickness(0,0,8,0) };
            envoy.Click+=(_,_)=>RequestTalkNpc?.Invoke(row.Task.TargetId);
            actions.Children.Add(envoy);
        }
        var court=kernel.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);
        var talk=new GameButton { Content="Bái kiến chúa công" };
        talk.Click+=(_,_)=>RequestTalkNpc?.Invoke(court.Factions.First(f=>f.Id==row.Task.FactionId).LeaderNpcId);
        actions.Children.Add(talk);
        stack.Children.Add(actions);
        card.CardBody=stack;
        _rootContent.Children.Add(card);
    }
}
