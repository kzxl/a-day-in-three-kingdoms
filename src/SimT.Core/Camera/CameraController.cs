using SimT.Contracts.Objects;
using SimT.Contracts.Primitives;

namespace SimT.Core.Camera;

/// <summary>
/// Sovereign Camera Controller designed for Three Kingdoms strategic maps.
/// Supports smooth panning, 2-tier zoom (Macro Realm vs Micro Focus),
/// cursor-anchored zoom, target following, and map bounds clamping.
/// </summary>
public sealed class CameraController
{
    private readonly Camera3D _camera;
    private BoundingBox2D? _mapBounds;
    private Transform2D? _followTarget;
    private Vec3 _targetPosition;
    private float _targetZoom;

    public Camera3D Camera => _camera;

    public float MinZoom { get; set; } = 250f;   // Micro: close local view
    public float MaxZoom { get; set; } = 3200f;  // Macro: realm overview
    public float SmoothSpeed { get; set; } = 12f; // Interpolation speed factor

    public Vec3 Position
    {
        get => _camera.Position;
        set
        {
            _camera.Position = value;
            _targetPosition = value;
        }
    }

    public Vec3 Target
    {
        get => _camera.Target;
        set => _camera.Target = value;
    }

    public float Zoom
    {
        get => _camera.OrthographicSize;
        set
        {
            var clamped = Math.Clamp(value, MinZoom, MaxZoom);
            _camera.OrthographicSize = clamped;
            _targetZoom = clamped;
        }
    }

    public CameraController(Camera3D? camera = null)
    {
        _camera = camera ?? new Camera3D
        {
            Projection = CameraProjectionMode.Orthographic,
            Position = new Vec3(0f, 600f, 600f),
            Target = new Vec3(0f, 0f, 0f),
            OrthographicSize = 1000f
        };

        _targetPosition = _camera.Position;
        _targetZoom = _camera.OrthographicSize;
    }

    public void SetMapBounds(BoundingBox2D bounds)
    {
        _mapBounds = bounds;
    }

    public void ClearMapBounds()
    {
        _mapBounds = null;
    }

    public void Follow(Transform2D? target)
    {
        _followTarget = target;
    }

    public void StopFollowing()
    {
        _followTarget = null;
    }

    public void Pan(float deltaScreenX, float deltaScreenY, float viewportWidth, float viewportHeight)
    {
        StopFollowing();

        if (viewportWidth <= 0 || viewportHeight <= 0) return;

        // Calculate world space movement proportional to current zoom
        var factor = _camera.OrthographicSize / Math.Max(viewportHeight, 1f);
        var moveX = -deltaScreenX * factor;
        var moveZ = deltaScreenY * factor;

        _targetPosition = new Vec3(_targetPosition.X + moveX, _targetPosition.Y, _targetPosition.Z + moveZ);
        ClampPositionToBounds();
    }

    public void ZoomAt(float zoomDeltaRatio, float cursorScreenX, float cursorScreenY, float viewportWidth, float viewportHeight)
    {
        var newZoom = Math.Clamp(_targetZoom * zoomDeltaRatio, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - _targetZoom) < 0.001f) return;

        // Anchor zoom toward cursor point on ground
        var cursorRay = _camera.ScreenPointToRay(cursorScreenX, cursorScreenY, viewportWidth, viewportHeight);
        if (Camera3D.RaycastGround(cursorRay, 0f, out var groundBefore))
        {
            _targetZoom = newZoom;
            _camera.OrthographicSize = newZoom;

            var newRay = _camera.ScreenPointToRay(cursorScreenX, cursorScreenY, viewportWidth, viewportHeight);
            if (Camera3D.RaycastGround(newRay, 0f, out var groundAfter))
            {
                var delta = groundBefore - groundAfter;
                _targetPosition = new Vec3(_targetPosition.X + delta.X, _targetPosition.Y, _targetPosition.Z + delta.Z);
            }
        }
        else
        {
            _targetZoom = newZoom;
        }

        ClampPositionToBounds();
    }

    public void FocusOn(VectorPoint2D worldPos, bool instant = false)
    {
        StopFollowing();
        var yDiff = _camera.Position.Y;
        var zOffset = _camera.Position.Z - _camera.Target.Z;

        _targetPosition = new Vec3(worldPos.X, yDiff, worldPos.Y + zOffset);
        ClampPositionToBounds();

        if (instant)
        {
            _camera.Position = _targetPosition;
            _camera.Target = new Vec3(worldPos.X, 0f, worldPos.Y);
        }
    }

    public VectorPoint2D WorldToScreen(Vec3 worldPos, float viewportWidth, float viewportHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0) return VectorPoint2D.Zero;

        var vp = _camera.GetViewProjectionMatrix();
        var clip = vp.TransformPoint(worldPos);

        // Normalize device coordinates [-1, 1] to screen [0, width]
        var screenX = (clip.X + 1f) * 0.5f * viewportWidth;
        var screenY = (1f - clip.Y) * 0.5f * viewportHeight;

        return new VectorPoint2D(screenX, screenY);
    }

    public Vec3 ScreenToWorldGround(float screenX, float screenY, float viewportWidth, float viewportHeight, float groundY = 0f)
    {
        var ray = _camera.ScreenPointToRay(screenX, screenY, viewportWidth, viewportHeight);
        return Camera3D.RaycastGround(ray, groundY, out var hit) ? hit : Vec3.Zero;
    }

    public void Update(double deltaTimeSeconds)
    {
        if (deltaTimeSeconds <= 0) return;

        if (_followTarget is not null)
        {
            var followPos = _followTarget.WorldPosition;
            var zOffset = _camera.Position.Z - _camera.Target.Z;
            _targetPosition = new Vec3(followPos.X, _camera.Position.Y, followPos.Y + zOffset);
            ClampPositionToBounds();
        }

        var t = (float)Math.Clamp(deltaTimeSeconds * SmoothSpeed, 0.0, 1.0);

        // Interpolate zoom
        _camera.OrthographicSize += (_targetZoom - _camera.OrthographicSize) * t;

        // Interpolate position
        var curPos = _camera.Position;
        var nextPos = curPos + (_targetPosition - curPos) * t;
        _camera.Position = nextPos;

        // Keep target aligned on the ground
        var forward = _camera.Forward;
        var groundDist = -nextPos.Y / (forward.Y != 0 ? forward.Y : -1f);
        _camera.Target = nextPos + (forward * groundDist);
    }

    private void ClampPositionToBounds()
    {
        if (_mapBounds is not { } b) return;

        var clampedX = Math.Clamp(_targetPosition.X, b.MinX, b.MaxX);
        var clampedZ = Math.Clamp(_targetPosition.Z, b.MinY, b.MaxY);
        _targetPosition = new Vec3(clampedX, _targetPosition.Y, clampedZ);
    }
}
