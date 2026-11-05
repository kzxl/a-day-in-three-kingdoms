using System.IO;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    private void InitializeSceneHost()
    {
        SceneOverlay.ClientMessageReceived += OnSceneClientMessageReceived;
        Loaded += async (_, _) =>
        {
            var scenesDir = Path.Combine(AppContext.BaseDirectory, "assets", "scenes");
            await SceneOverlay.InitializeWarmAsync(scenesDir, skipForSmoke: _isSmoke);
        };
    }

    public bool TryOpenTalkScene(long npcId, string? feedbackVi = null)
    {
        if (!SceneOverlay.IsAvailable ||
            !_kernel.Capabilities.TryGet<IDialogueModule>(CapabilityIds.Dialogue, out var dialogue) ||
            dialogue is null)
        {
            return false;
        }

        try
        {
            var scene = dialogue.BuildTalkScene(npcId, feedbackVi);
            if (!SceneOverlay.ShowTalkScene(scene))
                return false;

            StatusText.Text = $"Đang diện kiến {scene.NameVi} ({scene.RoleVi}) tại {scene.LocationNameVi}.";
            return true;
        }
        catch (Exception ex)
        {
            StatusText.Text = Describe(ex);
            return false;
        }
    }

    public bool TryOpenDuelScene(NpcDef npc, string citadelName, TroopBranch playerBranch = TroopBranch.Infantry, DuelResult? result = null)
    {
        if (!SceneOverlay.IsAvailable ||
            !_kernel.Capabilities.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand, out var military) ||
            military is null ||
            !_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) ||
            attrs is null ||
            !_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) ||
            playerState is null)
        {
            return false;
        }

        var oppBranch = military.ResolveNpcBranch(npc.Id);
        var pMaxHp = 100 + (attrs.Attributes.Con / 2);
        var oMaxHp = 100 + (npc.Attributes.Con / 2);
        var pSp = attrs.Attributes.Wil >= 80 ? 10 : 0;
        var oSp = npc.Attributes.Wil >= 80 ? 10 : 0;
        var (_, npcFace, npcSprite) = SceneArtCatalog.ResolveNpcArt(npc.Code, npc.Role);

        var playerFighter = new DuelFighterView(
            NameVi: playerState.Name,
            SubVi: $"Võ lực {attrs.Attributes.War} · {TroopBranchHelper.NameVi(playerBranch)}",
            FacePath: SceneArtCatalog.PlayerPortraitFace,
            SpritePath: SceneArtCatalog.PlayerSprite,
            Branch: playerBranch,
            Hp: pMaxHp,
            MaxHp: pMaxHp,
            Spirit: pSp,
            MaxSpirit: 100);

        var oppFighter = new DuelFighterView(
            NameVi: npc.NameVi,
            SubVi: $"Võ lực {npc.Attributes.War} · {TroopBranchHelper.NameVi(oppBranch)}",
            FacePath: npcFace,
            SpritePath: npcSprite,
            Branch: oppBranch,
            Hp: oMaxHp,
            MaxHp: oMaxHp,
            Spirit: oSp,
            MaxSpirit: 100);

        IReadOnlyList<SceneChoice> choices;
        if (result is null)
        {
            choices =
            [
                new SceneChoice("branch:Infantry", "Xuất chiến Bộ Binh", "Khắc Cung · Kị Kỵ"),
                new SceneChoice("branch:Cavalry", "Xuất chiến Kỵ Binh", "Khắc Bộ · Kị Cung"),
                new SceneChoice("branch:Archers", "Xuất chiến Cung Binh", "Khắc Kỵ · Kị Bộ"),
                new SceneChoice("leave", "Rời lôi đài", "Cáo lui")
            ];
        }
        else
        {
            choices =
            [
                new SceneChoice("rematch", "Tái đấu hiệp mới", "Chọn lại binh chủng"),
                new SceneChoice("talk", "Trò chuyện sau trận", $"Gặp {npc.NameVi}"),
                new SceneChoice("leave", "Rời lôi đài", "Quay lại màn hình")
            ];
        }

        var scene = new DuelScene(
            SceneId: $"duel:{npc.Id}",
            NpcId: npc.Id,
            NpcCode: npc.Code,
            TitleVi: $"Tỷ võ cùng {npc.NameVi}",
            CitadelNameVi: citadelName,
            BackgroundPath: SceneArtCatalog.BackgroundForKey("arena"),
            Player: playerFighter,
            Opponent: oppFighter,
            MatchupSummaryVi: TroopBranchHelper.MatchupSummaryVi(playerBranch, oppBranch),
            Result: result,
            Choices: choices);

        if (!SceneOverlay.ShowDuelScene(scene))
            return false;

        StatusText.Text = result is null
            ? $"Chuẩn bị tỷ võ cùng {npc.NameVi} tại lôi đài {citadelName}."
            : result.CombatSummaryVi;
        return true;
    }

    private void OnSceneClientMessageReceived(SceneClientMessage message, object? activeScene)
    {
        var type = message.Type.Trim().ToLowerInvariant();
        if (type == "close")
        {
            UpdateHeader();
            RefreshBadges();
            RenderSelected();
            return;
        }

        if (activeScene is TalkScene talk)
        {
            HandleTalkSceneMessage(type, message.Id ?? string.Empty, talk);
        }
        else if (activeScene is DuelScene duel)
        {
            HandleDuelSceneMessage(type, message.Id ?? string.Empty, duel);
        }
    }

    private void HandleTalkSceneMessage(string type, string id, TalkScene talk)
    {
        if (type == "navigate" && id.StartsWith("npc:", StringComparison.Ordinal))
        {
            if (long.TryParse(id["npc:".Length..], out var targetNpcId))
            {
                TryOpenTalkScene(targetNpcId);
            }
            return;
        }

        if (type != "choice")
            return;

        if (!_kernel.Capabilities.TryGet<IDialogueModule>(CapabilityIds.Dialogue, out var dialogue) || dialogue is null)
            return;

        var outcome = dialogue.HandleChoice(talk.NpcId, id);
        StatusText.Text = outcome.MessageVi;
        _kernel.Save();
        UpdateHeader();
        RefreshBadges();

        if (outcome.OpenDuel)
        {
            if (_kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var reg) &&
                reg?.Find(talk.NpcId) is { } npc &&
                TryOpenDuelScene(npc, talk.LocationNameVi))
            {
                return;
            }

            SceneOverlay.HideScene();
            TryOpenDuelModal(talk.NpcId);
            return;
        }

        if (outcome.CloseScene)
        {
            SceneOverlay.HideScene();
            RenderSelected();
            return;
        }

        if (outcome.UpdatedScene is not null)
        {
            SceneOverlay.ShowTalkScene(outcome.UpdatedScene);
        }
    }

    private void HandleDuelSceneMessage(string type, string id, DuelScene duel)
    {
        if (type != "choice")
            return;

        if (id == "leave")
        {
            SceneOverlay.HideScene();
            UpdateHeader();
            RefreshBadges();
            RenderSelected();
            return;
        }

        if (id == "talk")
        {
            TryOpenTalkScene(duel.NpcId);
            return;
        }

        if (!_kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var reg) ||
            reg?.Find(duel.NpcId) is not { } npc)
        {
            return;
        }

        if (id == "rematch")
        {
            TryOpenDuelScene(npc, duel.CitadelNameVi);
            return;
        }

        if (id.StartsWith("branch:", StringComparison.Ordinal) &&
            Enum.TryParse<TroopBranch>(id["branch:".Length..], ignoreCase: true, out var branch) &&
            _kernel.Capabilities.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand, out var military) &&
            military is not null)
        {
            try
            {
                var result = military.ChallengeArena(npc.Id, branch);
                _kernel.Save();
                UpdateHeader();
                RefreshBadges();
                TryOpenDuelScene(npc, duel.CitadelNameVi, branch, result);
            }
            catch (Exception ex)
            {
                StatusText.Text = Describe(ex);
            }
        }
    }
}
