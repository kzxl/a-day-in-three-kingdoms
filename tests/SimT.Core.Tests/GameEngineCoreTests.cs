using SimT.Contracts.Assets;
using SimT.Contracts.Diagnostics;
using SimT.Contracts.Input;
using SimT.Contracts.Objects;
using SimT.Contracts.Primitives;
using SimT.Contracts.Rendering;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;
using SimT.Core.Animation;
using SimT.Core.Assets;
using SimT.Core.Camera;
using SimT.Core.Input;
using SimT.Core.Rendering;
using SimT.Core.Scenes;
using SimT.Core.Time;

namespace SimT.Core.Tests;

public sealed class GameEngineCoreTests
{
    [Fact]
    public void TimeService_tracks_scaled_and_unscaled_delta_time()
    {
        var time = new TimeService();
        time.TimeScale = 2.0;

        time.Update(0.016);

        Assert.Equal(0.016, time.UnscaledDeltaTime);
        Assert.Equal(0.032, time.DeltaTime, precision: 4);
        Assert.Equal(0.032, time.TotalTime, precision: 4);
        Assert.Equal(1, time.FrameCount);
    }

    [Fact]
    public void EngineScheduler_dispatches_deferred_and_periodic_tasks()
    {
        var scheduler = new EngineScheduler();
        var deferredRan = false;
        var periodicCount = 0;

        scheduler.ScheduleAfter(0.05, () => deferredRan = true);
        scheduler.SchedulePeriodic(0.02, () => periodicCount++);

        scheduler.Update(0.02);
        Assert.False(deferredRan);
        Assert.Equal(1, periodicCount);

        scheduler.Update(0.03); // Total 0.05
        Assert.True(deferredRan);
        Assert.Equal(2, periodicCount);

        scheduler.CancelAll();
        scheduler.Update(0.05);
        Assert.Equal(2, periodicCount);
    }

    [Fact]
    public void TweenRunner_interpolates_values_with_easing()
    {
        var runner = new TweenRunner();
        var current = 0f;
        var completed = false;

        runner.AnimateFloat(0f, 100f, 0.1, val => current = val, Easing.Linear, () => completed = true);

        runner.Update(0.05);
        Assert.Equal(50f, current, tolerance: 0.1f);
        Assert.False(completed);

        runner.Update(0.05);
        Assert.Equal(100f, current, tolerance: 0.1f);
        Assert.True(completed);
    }

    [Fact]
    public void GameObject_and_Component_hierarchy_updates_coordinates()
    {
        var parent = new GameObject("Parent");
        parent.Transform.LocalPosition = new VectorPoint2D(100f, 100f);

        var child = new GameObject("GuanYu");
        child.Transform.LocalPosition = new VectorPoint2D(50f, 25f);
        parent.AddChild(child);

        var collider = child.AddComponent(new Collider2D
        {
            Size = new VectorPoint2D(40f, 40f)
        });

        // Child world position should be 100 + 50 = 150, 100 + 25 = 125
        Assert.Equal(new VectorPoint2D(150f, 125f), child.Transform.WorldPosition);

        // Collider should contain the world center point
        Assert.True(collider.ContainsPoint(150f, 125f));
        Assert.False(collider.ContainsPoint(0f, 0f));
    }

    [Fact]
    public void LayerManager_maintains_sorted_layers_and_camera_binding()
    {
        var manager = LayerManager.CreateDefaultThreeKingdomsSetup();
        Assert.Equal(6, manager.Layers.Count);
        Assert.Equal("BackgroundLayer", manager.Layers[0].Name);
        Assert.Equal("ScreenHudLayer", manager.Layers[^1].Name);

        Assert.True(manager.GetLayer("MapLayer")?.CameraBinding);
        Assert.False(manager.GetLayer("ScreenHudLayer")?.CameraBinding);
    }

    [Fact]
    public void CameraController_smoothly_pans_and_clamps_to_map_bounds()
    {
        var camera = new CameraController();
        camera.SetMapBounds(new BoundingBox2D(-500f, -500f, 500f, 500f));
        camera.Position = new Vec3(0f, 600f, 600f);

        // Huge pan attempt beyond bounds
        camera.Pan(10000f, 0f, 1920f, 1080f);
        camera.Update(1.0); // Step smoothing

        Assert.True(camera.Position.X <= 500f);
    }

    private sealed class MockInteractable : ComponentBase, IInteractable
    {
        public bool IsInteractive => true;
        public int ClickCount { get; private set; }
        public bool IsHovered { get; private set; }

        public void OnPointerEnter(PointerEvent e) => IsHovered = true;
        public void OnPointerExit(PointerEvent e) => IsHovered = false;
        public void OnPointerDown(PointerEvent e) { }
        public void OnPointerUp(PointerEvent e) { }
        public void OnPointerClick(PointerEvent e) => ClickCount++;
        public void OnDrag(PointerEvent e) { }
    }

    [Fact]
    public void InputManager_dispatches_clicks_to_topmost_layer_without_clickthrough()
    {
        var layers = new LayerManager();
        var camera = new CameraController();
        var input = new InputManager(layers, camera);

        var bottomLayer = new SceneLayer("MapLayer", zIndex: 10, cameraBinding: false);
        var topLayer = new SceneLayer("ScreenHudLayer", zIndex: 100, cameraBinding: false);

        layers.AddLayer(bottomLayer);
        layers.AddLayer(topLayer);

        var bottomObj = new GameObject("City");
        bottomObj.Transform.LocalPosition = new VectorPoint2D(100f, 100f);
        bottomObj.AddComponent(new Collider2D { Size = new VectorPoint2D(50f, 50f) });
        var bottomInteractable = bottomObj.AddComponent(new MockInteractable());
        bottomLayer.AddGameObject(bottomObj);

        var topObj = new GameObject("ModalButton");
        topObj.Transform.LocalPosition = new VectorPoint2D(100f, 100f);
        topObj.AddComponent(new Collider2D { Size = new VectorPoint2D(50f, 50f) });
        var topInteractable = topObj.AddComponent(new MockInteractable());
        topLayer.AddGameObject(topObj);

        // Click at (100, 100) where both objects overlap
        input.ProcessPointerDown(new VectorPoint2D(100f, 100f), PointerButton.Left);
        input.ProcessPointerUp(new VectorPoint2D(100f, 100f), PointerButton.Left);

        // Only the top layer object should receive the click!
        Assert.Equal(1, topInteractable.ClickCount);
        Assert.Equal(0, bottomInteractable.ClickCount);
    }

    private sealed class MockRenderContext : IRenderContext
    {
        public readonly List<string> DrawCalls = new();

        public void Clear(float r, float g, float b, float a = 1f) => DrawCalls.Add("Clear");
        public void DrawSprite(string spriteId, float x, float y, float width, float height, float rotation, float opacity, bool flipX, bool flipY) =>
            DrawCalls.Add($"Sprite:{spriteId}");
        public void DrawText(string text, float x, float y, float fontSize, string colorHex, string fontFamily, bool bold) =>
            DrawCalls.Add($"Text:{text}");
        public void DrawRectangle(float x, float y, float width, float height, string fillColorHex, string? strokeColorHex, float strokeWidth) =>
            DrawCalls.Add("Rect");
        public void DrawCircle(float centerX, float centerY, float radius, string fillColorHex, string? strokeColorHex, float strokeWidth) =>
            DrawCalls.Add("Circle");
        public void DrawLine(float x1, float y1, float x2, float y2, string colorHex, float strokeWidth) =>
            DrawCalls.Add("Line");
        public void PushTransform(VectorPoint2D translation, float rotation, VectorPoint2D? scale) { }
        public void PopTransform() { }
        public void PushClip(BoundingBox2D clipRect) { }
        public void PopClip() { }
    }

    [Fact]
    public void RenderQueue_sorts_commands_by_layer_depth_and_flushes_batches()
    {
        var queue = new RenderQueue();
        var ctx = new MockRenderContext();

        // Enqueue high Z text
        queue.EnqueueText(100, 0f, "HUD", 10f, 10f, 12f, "#FFF");
        // Enqueue background sprite
        queue.EnqueueSprite(0, 0f, "bg_map", 0f, 0f, 1920f, 1080f);
        // Enqueue unit sprite
        queue.EnqueueSprite(30, 200f, "guanyu_walk", 50f, 200f, 64f, 64f);

        var calls = queue.Flush(ctx);

        Assert.Equal(3, calls);
        Assert.Equal("Sprite:bg_map", ctx.DrawCalls[0]);
        Assert.Equal("Sprite:guanyu_walk", ctx.DrawCalls[1]);
        Assert.Equal("Text:HUD", ctx.DrawCalls[2]);
    }

    [Fact]
    public void AssetManager_shares_resource_handles_and_unloads_when_unused()
    {
        var assets = new AssetManager();
        var loadCount = 0;

        string Loader(string path)
        {
            loadCount++;
            return $"LoadedDataFrom:{path}";
        }

        var handle1 = assets.Load("art/guanyu.png", Loader);
        var handle2 = assets.Load("art/guanyu.png", Loader);

        // Both handles should reference the exact same object, only 1 physical load
        Assert.Same(handle1.Resource, handle2.Resource);
        Assert.Equal(1, loadCount);
        Assert.Equal(2, handle1.RefCount);

        handle1.Dispose();
        Assert.Equal(1, handle2.RefCount);

        // Should not unload while handle2 is still active
        var evicted = assets.UnloadUnused();
        Assert.Equal(0, evicted);

        handle2.Dispose();
        Assert.Equal(0, handle2.RefCount);

        // Now handle2 is disposed, should unload
        evicted = assets.UnloadUnused();
        Assert.Equal(1, evicted);
        Assert.Equal(0, assets.CachedResourceCount);
    }

    [Fact]
    public void FrameDiagnostics_measures_fps_and_frame_memory_allocation()
    {
        var diag = new FrameDiagnostics();
        diag.BeginFrame();

        // Deliberately allocate an object to verify GC delta detection
        var trash = new byte[1024];

        diag.EndFrame(0.016);

        Assert.True(diag.BytesAllocatedThisFrame >= 1024, $"Expected >= 1024 bytes allocated, got {diag.BytesAllocatedThisFrame}");
    }
}
