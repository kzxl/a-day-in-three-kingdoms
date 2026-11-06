using SimT.Contracts.Input;
using SimT.Contracts.Objects;
using SimT.Contracts.Primitives;
using SimT.Contracts.Scenes;
using SimT.Core.Camera;

namespace SimT.Core.Input;

/// <summary>
/// Sovereign Input &amp; Interaction Dispatcher.
/// Performs coordinate translation, layer-prioritized hit testing,
/// and routes pointer events without click-through.
/// </summary>
public sealed class InputManager
{
    private readonly ILayerManager _layerManager;
    private readonly CameraController _cameraController;
    private IInteractable? _hoveredInteractable;
    private IInteractable? _focusedInteractable;
    private VectorPoint2D _lastScreenPos;
    private bool _isDragging;

    public float ViewportWidth { get; set; } = 1920f;
    public float ViewportHeight { get; set; } = 1080f;

    public IInteractable? HoveredInteractable => _hoveredInteractable;
    public IInteractable? FocusedInteractable => _focusedInteractable;

    public InputManager(ILayerManager layerManager, CameraController cameraController)
    {
        _layerManager = layerManager ?? throw new ArgumentNullException(nameof(layerManager));
        _cameraController = cameraController ?? throw new ArgumentNullException(nameof(cameraController));
    }

    public void ProcessPointerMove(VectorPoint2D screenPos)
    {
        var delta = screenPos - _lastScreenPos;
        _lastScreenPos = screenPos;

        var evt = new PointerEvent(PointerEventType.Move, PointerButton.None, screenPos, delta)
        {
            WorldPosition = _cameraController.ScreenToWorldGround(screenPos.X, screenPos.Y, ViewportWidth, ViewportHeight)
        };

        if (_isDragging && _focusedInteractable is not null)
        {
            _focusedInteractable.OnDrag(evt);
            return;
        }

        var hit = HitTest(screenPos, evt.WorldPosition);

        if (!ReferenceEquals(hit, _hoveredInteractable))
        {
            _hoveredInteractable?.OnPointerExit(evt);
            _hoveredInteractable = hit;
            _hoveredInteractable?.OnPointerEnter(evt);
        }
    }

    public void ProcessPointerDown(VectorPoint2D screenPos, PointerButton button)
    {
        _lastScreenPos = screenPos;
        var worldPos = _cameraController.ScreenToWorldGround(screenPos.X, screenPos.Y, ViewportWidth, ViewportHeight);

        var evt = new PointerEvent(PointerEventType.Down, button, screenPos, VectorPoint2D.Zero)
        {
            WorldPosition = worldPos
        };

        var hit = HitTest(screenPos, worldPos);
        _focusedInteractable = hit;

        if (hit is not null)
        {
            hit.OnPointerDown(evt);
            _isDragging = true;
        }
    }

    public void ProcessPointerUp(VectorPoint2D screenPos, PointerButton button)
    {
        var worldPos = _cameraController.ScreenToWorldGround(screenPos.X, screenPos.Y, ViewportWidth, ViewportHeight);

        var evt = new PointerEvent(PointerEventType.Up, button, screenPos, VectorPoint2D.Zero)
        {
            WorldPosition = worldPos
        };

        var hit = HitTest(screenPos, worldPos);

        if (_focusedInteractable is not null)
        {
            _focusedInteractable.OnPointerUp(evt);

            if (ReferenceEquals(hit, _focusedInteractable))
            {
                var clickEvt = new PointerEvent(PointerEventType.Click, button, screenPos, VectorPoint2D.Zero)
                {
                    WorldPosition = worldPos
                };
                _focusedInteractable.OnPointerClick(clickEvt);
            }
        }

        _isDragging = false;
    }

    public void ProcessPointerWheel(VectorPoint2D screenPos, float wheelDelta)
    {
        var evt = new PointerEvent(PointerEventType.Wheel, PointerButton.None, screenPos, VectorPoint2D.Zero, wheelDelta)
        {
            WorldPosition = _cameraController.ScreenToWorldGround(screenPos.X, screenPos.Y, ViewportWidth, ViewportHeight)
        };

        // If no interactive element consumes the wheel event, zoom the camera
        var hit = HitTest(screenPos, evt.WorldPosition);
        if (hit is null)
        {
            var zoomRatio = wheelDelta > 0 ? 0.9f : 1.11f;
            _cameraController.ZoomAt(zoomRatio, screenPos.X, screenPos.Y, ViewportWidth, ViewportHeight);
        }
    }

    public IInteractable? HitTest(VectorPoint2D screenPos, Vec3 worldGroundPos)
    {
        var layers = _layerManager.Layers;

        // Traverse layers in reverse Z-order (Top-most layers first)
        for (var i = layers.Count - 1; i >= 0; i--)
        {
            var layer = layers[i];
            if (!layer.Visible || !layer.InputEnabled) continue;

            var targetPos = layer.CameraBinding
                ? new VectorPoint2D(worldGroundPos.X, worldGroundPos.Z)
                : screenPos;

            for (var j = layer.GameObjects.Count - 1; j >= 0; j--)
            {
                var go = layer.GameObjects[j];
                if (!go.Active) continue;

                if (go.TryGetComponent<Collider2D>(out var collider) && collider is not null)
                {
                    if (collider.ContainsPoint(targetPos.X, targetPos.Y))
                    {
                        if (go.TryGetComponent<IInteractable>(out var interactable) && interactable is not null && interactable.IsInteractive)
                        {
                            return interactable;
                        }
                    }
                }
            }
        }

        return null;
    }
}
