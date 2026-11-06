using SimT.Contracts.Primitives;

namespace SimT.Contracts.Input;

public enum PointerButton : byte
{
    None = 0,
    Left = 1,
    Right = 2,
    Middle = 3
}

public enum PointerEventType : byte
{
    Move = 0,
    Down = 1,
    Up = 2,
    Click = 3,
    DoubleClick = 4,
    Wheel = 5
}

/// <summary>
/// Unified pointer event payload supporting mouse, touch, and pen interactions.
/// </summary>
public sealed record PointerEvent(
    PointerEventType Type,
    PointerButton Button,
    VectorPoint2D ScreenPosition,
    VectorPoint2D DeltaScreen,
    float WheelDelta = 0f)
{
    public bool Handled { get; set; } = false;
    public Vec3 WorldPosition { get; set; } = Vec3.Zero;
}

/// <summary>
/// Interface implemented by interactive game components or UI items.
/// </summary>
public interface IInteractable
{
    bool IsInteractive { get; }
    void OnPointerEnter(PointerEvent e);
    void OnPointerExit(PointerEvent e);
    void OnPointerDown(PointerEvent e);
    void OnPointerUp(PointerEvent e);
    void OnPointerClick(PointerEvent e);
    void OnDrag(PointerEvent e);
}
