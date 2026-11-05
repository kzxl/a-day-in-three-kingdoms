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
}
