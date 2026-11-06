using SimT.Contracts.Primitives;
using SimT.Contracts.Rendering;

namespace SimT.Core.Rendering;

public enum RenderCommandType : byte
{
    Sprite = 0,
    Text = 1,
    Rectangle = 2,
    Circle = 3,
    Line = 4
}

/// <summary>
/// Sovereign sorted draw command record.
/// </summary>
public readonly record struct RenderCommand(
    RenderCommandType Type,
    int LayerZIndex,
    float DepthY,
    string ResourceOrText,
    float X,
    float Y,
    float WidthOrX2,
    float HeightOrY2,
    string ColorOrFont,
    float RotationOrFontSize,
    float OpacityOrStrokeWidth,
    bool FlipXOrBold,
    bool FlipY);

/// <summary>
/// Sovereign Render Command Buffer &amp; Batching Queue.
/// Enqueues draw commands and sorts them by Layer -> Material/Resource -> Depth Y before flushing.
/// </summary>
public sealed class RenderQueue
{
    private readonly List<RenderCommand> _commands = new(256);

    public int CommandCount => _commands.Count;

    public void EnqueueSprite(
        int layerZIndex,
        float depthY,
        string spriteId,
        float x,
        float y,
        float width,
        float height,
        float rotation = 0f,
        float opacity = 1f,
        bool flipX = false,
        bool flipY = false)
    {
        _commands.Add(new RenderCommand(
            RenderCommandType.Sprite,
            layerZIndex,
            depthY,
            spriteId,
            x,
            y,
            width,
            height,
            string.Empty,
            rotation,
            opacity,
            flipX,
            flipY));
    }

    public void EnqueueText(
        int layerZIndex,
        float depthY,
        string text,
        float x,
        float y,
        float fontSize,
        string colorHex,
        string fontFamily = "Noto Serif",
        bool bold = false)
    {
        _commands.Add(new RenderCommand(
            RenderCommandType.Text,
            layerZIndex,
            depthY,
            text,
            x,
            y,
            0f,
            0f,
            $"{colorHex}|{fontFamily}",
            fontSize,
            0f,
            bold,
            false));
    }

    public void EnqueueRectangle(
        int layerZIndex,
        float depthY,
        float x,
        float y,
        float width,
        float height,
        string fillColorHex,
        string? strokeColorHex = null,
        float strokeWidth = 0f)
    {
        _commands.Add(new RenderCommand(
            RenderCommandType.Rectangle,
            layerZIndex,
            depthY,
            strokeColorHex ?? string.Empty,
            x,
            y,
            width,
            height,
            fillColorHex,
            0f,
            strokeWidth,
            false,
            false));
    }

    public void Sort()
    {
        _commands.Sort((a, b) =>
        {
            // Primary: Layer Z-Index
            var zCmp = a.LayerZIndex.CompareTo(b.LayerZIndex);
            if (zCmp != 0) return zCmp;

            // Secondary: Y-depth for isometric Painter's sorting
            var yCmp = a.DepthY.CompareTo(b.DepthY);
            if (yCmp != 0) return yCmp;

            // Tertiary: Resource ID to minimize batch texture swaps
            return string.CompareOrdinal(a.ResourceOrText, b.ResourceOrText);
        });
    }

    public int Flush(IRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Sort();
        var drawCallCount = 0;
        string? lastResource = null;

        for (var i = 0; i < _commands.Count; i++)
        {
            var cmd = _commands[i];

            if (lastResource != cmd.ResourceOrText)
            {
                drawCallCount++;
                lastResource = cmd.ResourceOrText;
            }

            switch (cmd.Type)
            {
                case RenderCommandType.Sprite:
                    context.DrawSprite(
                        cmd.ResourceOrText,
                        cmd.X,
                        cmd.Y,
                        cmd.WidthOrX2,
                        cmd.HeightOrY2,
                        cmd.RotationOrFontSize,
                        cmd.OpacityOrStrokeWidth,
                        cmd.FlipXOrBold,
                        cmd.FlipY);
                    break;

                case RenderCommandType.Text:
                    var parts = cmd.ColorOrFont.Split('|');
                    var color = parts.Length > 0 ? parts[0] : "#FFFFFF";
                    var font = parts.Length > 1 ? parts[1] : "Noto Serif";
                    context.DrawText(
                        cmd.ResourceOrText,
                        cmd.X,
                        cmd.Y,
                        cmd.RotationOrFontSize,
                        color,
                        font,
                        cmd.FlipXOrBold);
                    break;

                case RenderCommandType.Rectangle:
                    context.DrawRectangle(
                        cmd.X,
                        cmd.Y,
                        cmd.WidthOrX2,
                        cmd.HeightOrY2,
                        cmd.ColorOrFont,
                        string.IsNullOrEmpty(cmd.ResourceOrText) ? null : cmd.ResourceOrText,
                        cmd.OpacityOrStrokeWidth);
                    break;
            }
        }

        _commands.Clear();
        return drawCallCount;
    }

    public void Clear() => _commands.Clear();
}
