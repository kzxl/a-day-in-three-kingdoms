using System.IO;
using System.Runtime.CompilerServices;
using SimT.Contracts.Scenes;
using System.Windows;
using System.Windows.Controls.Primitives;
using SimT.Contracts.Audio;

namespace SimT.Host.Wpf.Audio;

/// <summary>Presentation-only audio lifetime, shared across title, campaign and modal windows.</summary>
public static class GameAudio
{
    private sealed record EventAudio(bool HasWar);
    private static readonly ConditionalWeakTable<EventScene, EventAudio> Events = new();

    public static EventScene ConfigureEvent(EventScene scene, bool hasWar)
    {
        Events.Add(scene, new EventAudio(hasWar));
        return scene;
    }

    public static IAudioEngine Engine { get; private set; } = NullAudioEngine.Instance;
    public static AudioSettings Settings { get; private set; } = new();

    public static void Initialize(bool disabled)
    {
        Engine = NAudioEngine.Create(Path.Combine(AppContext.BaseDirectory, "assets", "audio"), disabled);
        Settings = AudioSettings.Load(AudioSettings.DefaultPath);
        Settings.Apply(Engine);
        EventManager.RegisterClassHandler(typeof(ButtonBase), ButtonBase.ClickEvent,
            new RoutedEventHandler((sender, _) =>
            {
                Engine.PlaySfx("ui.click");
                if (sender is Controls.GameSealButton) Engine.PlaySfx("ui.seal");
            }), true);
    }

    public static void ShowScene(object? scene)
    {
        var plan = AudioScenePolicy.Select(scene, scene is EventScene evt && Events.TryGetValue(evt, out var audio) && audio.HasWar);
        ApplyPlan(plan);
    }

    public static void PlayDuelMusic() => ApplyPlan(new("duel", []));

    private static void ApplyPlan(AudioScenePlan plan)
    {
        Engine.PlayMusic(plan.Music);
        if (Engine is NAudioEngine backend) backend.SetAmbience(plan.Ambience);
    }

    public static void SaveSettings()
    {
        Settings.Apply(Engine);
        Settings.Save(AudioSettings.DefaultPath);
    }

    public static void Dispose() { (Engine as IDisposable)?.Dispose(); Engine = NullAudioEngine.Instance; }
}
