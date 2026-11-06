using SimT.Contracts.Localization;
using SimT.Contracts.Logging;
using SimT.Contracts.Modules;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;
using SimT.Core.Scenes;
using SimT.Core.Time;
using Xunit;

namespace SimT.Core.Tests;

public sealed class SceneManagerTests
{
    private sealed class TestLog : ILog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? error = null) { }
    }

    private sealed class MockScene : BaseScene
    {
        public override string SceneId { get; }
        public override string Title { get; }

        public int EnterCount { get; private set; }
        public int ExitCount { get; private set; }
        public int SuspendCount { get; private set; }
        public int ResumeCount { get; private set; }
        public double TotalTimeUpdated { get; private set; }

        public MockScene(string id, string title)
        {
            SceneId = id;
            Title = title;
        }

        public override void OnEnter(SceneRuntimeContext context)
        {
            base.OnEnter(context);
            EnterCount++;
        }

        public override void OnUpdate(double deltaTimeSeconds)
        {
            base.OnUpdate(deltaTimeSeconds);
            TotalTimeUpdated += deltaTimeSeconds;
        }

        public override void OnSuspend()
        {
            base.OnSuspend();
            SuspendCount++;
        }

        public override void OnResume()
        {
            base.OnResume();
            ResumeCount++;
        }

        public override void OnExit()
        {
            base.OnExit();
            ExitCount++;
        }
    }

    private sealed class MockClock : IGameClock
    {
        public GameDate Today { get; set; } = new GameDate(184, 1, 1);
        public GameDate Advance(int days) => Today = Today.AddDays(days);
    }

    private static SceneRuntimeContext CreateContext()
    {
        return new SceneRuntimeContext(
            null!,
            new MockClock(),
            new TestLog(),
            new Core.Localization.LocalizationService(),
            (_, _) => { },
            _ => { });
    }

    [Fact]
    public void SceneManager_handles_scene_change_lifecycle()
    {
        var context = CreateContext();
        var sm = new SceneManager(context);

        var scene1 = new MockScene("scene_1", "Stage 1");
        var scene2 = new MockScene("scene_2", "Stage 2");

        // 1. Initial scene
        sm.ChangeScene(scene1, SceneTransitionType.None);
        Assert.Equal(scene1, sm.CurrentScene);
        Assert.Equal(1, scene1.EnterCount);
        Assert.Equal(SceneLifecycleState.Active, scene1.State);

        // 2. Change to scene 2 directly
        sm.ChangeScene(scene2, SceneTransitionType.None);
        Assert.Equal(scene2, sm.CurrentScene);
        Assert.Equal(1, scene1.ExitCount);
        Assert.Equal(SceneLifecycleState.Inactive, scene1.State);
        Assert.Equal(1, scene2.EnterCount);
        Assert.Equal(SceneLifecycleState.Active, scene2.State);
    }

    [Fact]
    public void SceneManager_handles_hierarchical_push_and_pop_stack()
    {
        var context = CreateContext();
        var sm = new SceneManager(context);

        var cityScene = new MockScene("city", "Thành Đô");
        var talkScene = new MockScene("talk", "Đối thoại Gia Cát Lượng");

        // Push City Scene
        sm.PushScene(cityScene);
        Assert.Equal(cityScene, sm.CurrentScene);
        Assert.Equal(SceneLifecycleState.Active, cityScene.State);
        Assert.Single(sm.SceneStack);

        // Push Talk Scene on top of City Scene
        sm.PushScene(talkScene);
        Assert.Equal(talkScene, sm.CurrentScene);
        Assert.Equal(SceneLifecycleState.Suspended, cityScene.State);
        Assert.Equal(1, cityScene.SuspendCount);
        Assert.Equal(SceneLifecycleState.Active, talkScene.State);
        Assert.Equal(2, sm.SceneStack.Count);

        // Pop Talk Scene -> City resumes
        var popped = sm.PopScene();
        Assert.True(popped);
        Assert.Equal(cityScene, sm.CurrentScene);
        Assert.Equal(SceneLifecycleState.Active, cityScene.State);
        Assert.Equal(1, cityScene.ResumeCount);
        Assert.Equal(SceneLifecycleState.Inactive, talkScene.State);
        Assert.Equal(1, talkScene.ExitCount);
        Assert.Single(sm.SceneStack);
    }

    [Fact]
    public void SceneManager_updates_transition_progress_and_switches_at_midpoint()
    {
        var context = CreateContext();
        var sm = new SceneManager(context);

        var scene1 = new MockScene("scene_1", "Stage 1");
        var scene2 = new MockScene("scene_2", "Stage 2");

        sm.ChangeScene(scene1, SceneTransitionType.None);
        Assert.Equal(scene1, sm.CurrentScene);

        // Start 300ms transition to scene 2
        sm.ChangeScene(scene2, SceneTransitionType.Fade);
        Assert.True(sm.IsInTransition);
        Assert.Equal(0.0, sm.TransitionProgress);
        Assert.Equal(scene1, sm.CurrentScene); // Still scene 1 before midpoint

        // Advance 100ms (100 / 300 = 0.33 < 0.5)
        sm.Update(0.1);
        Assert.True(sm.IsInTransition);
        Assert.True(sm.TransitionProgress > 0.3 && sm.TransitionProgress < 0.4);
        Assert.Equal(scene1, sm.CurrentScene);

        // Advance another 100ms (200 / 300 = 0.67 >= 0.5 midpoint crossed!)
        sm.Update(0.1);
        Assert.Equal(scene2, sm.CurrentScene); // Switched to scene 2
        Assert.Equal(1, scene2.EnterCount);

        // Advance another 150ms (350 / 300 >= 1.0 transition completed)
        sm.Update(0.15);
        Assert.False(sm.IsInTransition);
        Assert.Equal(1.0, sm.TransitionProgress);
        Assert.Equal(scene2, sm.CurrentScene);
    }
}
