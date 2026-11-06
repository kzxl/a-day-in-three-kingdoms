using SimT.Contracts.Primitives;

namespace SimT.Contracts.Rendering;

/// <summary>
/// Sovereign hardware-agnostic 2D rendering abstraction.
/// Decouples game logic and entity rendering from WPF, SkiaSharp, or DirectX implementations.
/// </summary>
public interface IRenderContext
{
    void Clear(float r, float g, float b, float a = 1f);

    void DrawSprite(
        string spriteId,
        float x,
        float y,
        float width,
        float height,
        float rotation = 0f,
        float opacity = 1f,
        bool flipX = false,
        bool flipY = false);

    void DrawText(
        string text,
        float x,
        float y,
        float fontSize = 12f,
        string colorHex = "#FFFFFF",
        string fontFamily = "Noto Serif",
        bool bold = false);

    void DrawRectangle(
        float x,
        float y,
        float width,
        float height,
        string fillColorHex,
        string? strokeColorHex = null,
        float strokeWidth = 0f);

    void DrawCircle(
        float centerX,
        float centerY,
        float radius,
        string fillColorHex,
        string? strokeColorHex = null,
        float strokeWidth = 0f);

    void DrawLine(
        float x1,
        float y1,
        float x2,
        float y2,
        string colorHex,
        float strokeWidth = 1f);

    void PushTransform(VectorPoint2D translation, float rotation = 0f, VectorPoint2D? scale = null);
    void PopTransform();

    void PushClip(BoundingBox2D clipRect);
    void PopClip();
}
