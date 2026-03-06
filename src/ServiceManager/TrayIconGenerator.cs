using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ServiceManager;

/// <summary>
/// Generates tray icons for different service states.
/// </summary>
public static class TrayIconGenerator
{
    private static readonly Dictionary<IconState, Icon> _iconCache = new();
    private static Icon? _appIconCache;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    // Default colors - can be customized
    public static Color PrimaryColor { get; set; } = Color.FromArgb(255, 102, 0);      // Orange
    public static Color SecondaryColor { get; set; } = Color.FromArgb(204, 82, 0);     // Dark Orange
    private static readonly Color Green = Color.FromArgb(76, 175, 80);
    private static readonly Color Yellow = Color.FromArgb(255, 193, 7);
    private static readonly Color Red = Color.FromArgb(244, 67, 54);
    private static readonly Color Gray = Color.FromArgb(158, 158, 158);

    public enum IconState
    {
        Running,
        Warning,
        Stopped,
        Error
    }

    public static Icon CreateIcon(IconState state, int size = 32)
    {
        if (_iconCache.TryGetValue(state, out var cachedIcon))
        {
            return cachedIcon;
        }

        using var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(Color.Transparent);

        var (primaryColor, secondaryColor) = GetColors(state);
        DrawFlameIcon(g, size, primaryColor, secondaryColor, state);

        var hIcon = bitmap.GetHicon();
        var icon = Icon.FromHandle(hIcon);
        var clonedIcon = (Icon)icon.Clone();
        DestroyIcon(hIcon);

        _iconCache[state] = clonedIcon;
        return clonedIcon;
    }

    private static (Color primary, Color secondary) GetColors(IconState state) => state switch
    {
        IconState.Running => (Green, Color.FromArgb(56, 142, 60)),
        IconState.Warning => (PrimaryColor, Yellow),
        IconState.Stopped => (Gray, Color.FromArgb(117, 117, 117)),
        IconState.Error => (Red, Color.FromArgb(198, 40, 40)),
        _ => (PrimaryColor, SecondaryColor)
    };

    private static void DrawFlameIcon(Graphics g, int size, Color primaryColor, Color secondaryColor, IconState state)
    {
        var padding = size * 0.1f;
        var flameWidth = size - (padding * 2);
        var flameHeight = size - (padding * 2);

        using var flamePath = CreateFlamePath(padding, padding, flameWidth, flameHeight);

        using var gradientBrush = new LinearGradientBrush(
            new PointF(size / 2f, padding),
            new PointF(size / 2f, size - padding),
            primaryColor,
            secondaryColor);

        g.FillPath(gradientBrush, flamePath);

        if (state == IconState.Running)
        {
            using var glowPen = new Pen(Color.FromArgb(100, primaryColor), 1.5f);
            g.DrawPath(glowPen, flamePath);
        }

        using var outlinePen = new Pen(Color.FromArgb(180, Color.FromArgb(
            Math.Max(0, primaryColor.R - 40),
            Math.Max(0, primaryColor.G - 40),
            Math.Max(0, primaryColor.B - 40))), 1f);
        g.DrawPath(outlinePen, flamePath);

        DrawInnerFlame(g, padding, flameWidth, flameHeight, primaryColor);
        DrawStatusDot(g, size, state);
    }

    private static GraphicsPath CreateFlamePath(float x, float y, float width, float height)
    {
        var path = new GraphicsPath();
        var centerX = x + width / 2f;
        var bottom = y + height;
        var top = y;

        path.AddBezier(
            centerX, bottom,
            x + width * 0.2f, bottom - height * 0.3f,
            x + width * 0.1f, bottom - height * 0.6f,
            x + width * 0.25f, top + height * 0.2f
        );

        path.AddBezier(
            x + width * 0.25f, top + height * 0.2f,
            x + width * 0.35f, top,
            x + width * 0.45f, top + height * 0.1f,
            centerX, top + height * 0.05f
        );

        path.AddBezier(
            centerX, top + height * 0.05f,
            x + width * 0.55f, top + height * 0.1f,
            x + width * 0.65f, top,
            x + width * 0.75f, top + height * 0.2f
        );

        path.AddBezier(
            x + width * 0.75f, top + height * 0.2f,
            x + width * 0.9f, bottom - height * 0.6f,
            x + width * 0.8f, bottom - height * 0.3f,
            centerX, bottom
        );

        path.CloseFigure();
        return path;
    }

    private static void DrawInnerFlame(Graphics g, float padding, float width, float height, Color baseColor)
    {
        var innerX = padding + width * 0.3f;
        var innerY = padding + height * 0.35f;
        var innerWidth = width * 0.4f;
        var innerHeight = height * 0.45f;

        using var innerPath = new GraphicsPath();

        var centerX = innerX + innerWidth / 2f;
        var bottom = innerY + innerHeight;
        var top = innerY;

        innerPath.AddBezier(
            centerX, bottom,
            innerX + innerWidth * 0.2f, bottom - innerHeight * 0.4f,
            innerX + innerWidth * 0.3f, top + innerHeight * 0.3f,
            centerX, top
        );

        innerPath.AddBezier(
            centerX, top,
            innerX + innerWidth * 0.7f, top + innerHeight * 0.3f,
            innerX + innerWidth * 0.8f, bottom - innerHeight * 0.4f,
            centerX, bottom
        );

        innerPath.CloseFigure();

        using var innerBrush = new SolidBrush(Color.FromArgb(120, 255, 255, 255));
        g.FillPath(innerBrush, innerPath);
    }

    private static void DrawStatusDot(Graphics g, int size, IconState state)
    {
        var dotSize = size * 0.3f;
        var dotX = size - dotSize - 1;
        var dotY = size - dotSize - 1;

        var dotColor = state switch
        {
            IconState.Running => Green,
            IconState.Warning => Yellow,
            IconState.Stopped => Gray,
            IconState.Error => Red,
            _ => PrimaryColor
        };

        using var bgBrush = new SolidBrush(Color.White);
        g.FillEllipse(bgBrush, dotX - 1, dotY - 1, dotSize + 2, dotSize + 2);

        using var dotBrush = new SolidBrush(dotColor);
        g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);

        using var dotPen = new Pen(Color.FromArgb(100, 0, 0, 0), 0.5f);
        g.DrawEllipse(dotPen, dotX, dotY, dotSize, dotSize);
    }

    public static Icon CreateApplicationIcon(int size = 48)
    {
        if (_appIconCache != null)
        {
            return _appIconCache;
        }

        using var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(Color.Transparent);

        var padding = size * 0.08f;
        var flameWidth = size - (padding * 2);
        var flameHeight = size - (padding * 2);

        using var flamePath = CreateFlamePath(padding, padding, flameWidth, flameHeight);

        using var gradientBrush = new LinearGradientBrush(
            new PointF(size / 2f, padding),
            new PointF(size / 2f, size - padding),
            PrimaryColor,
            SecondaryColor);

        g.FillPath(gradientBrush, flamePath);

        using var outlinePen = new Pen(Color.FromArgb(153, 61, 0), 1.5f);
        g.DrawPath(outlinePen, flamePath);

        DrawInnerFlame(g, padding, flameWidth, flameHeight, PrimaryColor);

        var hIcon = bitmap.GetHicon();
        var icon = Icon.FromHandle(hIcon);

        _appIconCache = (Icon)icon.Clone();
        DestroyIcon(hIcon);

        return _appIconCache;
    }

    /// <summary>
    /// Clears the icon cache. Call this if you change the primary/secondary colors.
    /// </summary>
    public static void ClearCache()
    {
        foreach (var icon in _iconCache.Values)
        {
            icon.Dispose();
        }
        _iconCache.Clear();

        _appIconCache?.Dispose();
        _appIconCache = null;
    }
}
