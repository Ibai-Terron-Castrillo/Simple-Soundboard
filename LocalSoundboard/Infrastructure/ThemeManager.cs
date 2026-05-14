using System.Windows;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace LocalSoundboard.Infrastructure;

public static class ThemeManager
{
    public static void Apply(Window window, bool isDarkMode)
    {
        var palette = isDarkMode ? DarkPalette : LightPalette;

        foreach (var (key, color) in palette)
        {
            window.Resources[key] = new SolidColorBrush(color);
        }
    }

    private static readonly Dictionary<string, MediaColor> LightPalette = new()
    {
        ["WindowBrush"] = MediaColor.FromRgb(244, 246, 248),
        ["TextBrush"] = MediaColor.FromRgb(23, 33, 43),
        ["MutedBrush"] = MediaColor.FromRgb(102, 112, 133),
        ["LineBrush"] = MediaColor.FromRgb(215, 222, 232),
        ["SurfaceBrush"] = MediaColor.FromRgb(255, 255, 255),
        ["SubtleBrush"] = MediaColor.FromRgb(238, 242, 246),
        ["ButtonBrush"] = MediaColor.FromRgb(231, 236, 242),
        ["ButtonHoverBrush"] = MediaColor.FromRgb(221, 229, 238),
        ["ButtonPressedBrush"] = MediaColor.FromRgb(203, 213, 225),
        ["AccentBrush"] = MediaColor.FromRgb(37, 99, 235),
        ["AccentHoverBrush"] = MediaColor.FromRgb(29, 78, 216),
        ["DangerBrush"] = MediaColor.FromRgb(220, 38, 38),
        ["DangerHoverBrush"] = MediaColor.FromRgb(185, 28, 28),
        ["FavoriteBrush"] = MediaColor.FromRgb(217, 119, 6),
        ["FavoriteBackgroundBrush"] = MediaColor.FromRgb(255, 244, 222),
        ["FavoriteBorderBrush"] = MediaColor.FromRgb(248, 216, 165),
        ["SelectedItemBrush"] = MediaColor.FromRgb(234, 242, 255),
        ["RemoteInfoBrush"] = MediaColor.FromRgb(239, 246, 255),
        ["RemoteInfoBorderBrush"] = MediaColor.FromRgb(191, 219, 254),
        ["IconBrush"] = MediaColor.FromRgb(23, 33, 43)
    };

    private static readonly Dictionary<string, MediaColor> DarkPalette = new()
    {
        ["WindowBrush"] = MediaColor.FromRgb(16, 23, 33),
        ["TextBrush"] = MediaColor.FromRgb(248, 250, 252),
        ["MutedBrush"] = MediaColor.FromRgb(190, 202, 218),
        ["LineBrush"] = MediaColor.FromRgb(49, 61, 78),
        ["SurfaceBrush"] = MediaColor.FromRgb(24, 34, 48),
        ["SubtleBrush"] = MediaColor.FromRgb(36, 48, 64),
        ["ButtonBrush"] = MediaColor.FromRgb(43, 56, 74),
        ["ButtonHoverBrush"] = MediaColor.FromRgb(55, 70, 92),
        ["ButtonPressedBrush"] = MediaColor.FromRgb(65, 83, 109),
        ["AccentBrush"] = MediaColor.FromRgb(96, 165, 250),
        ["AccentHoverBrush"] = MediaColor.FromRgb(59, 130, 246),
        ["DangerBrush"] = MediaColor.FromRgb(248, 113, 113),
        ["DangerHoverBrush"] = MediaColor.FromRgb(239, 68, 68),
        ["FavoriteBrush"] = MediaColor.FromRgb(251, 191, 36),
        ["FavoriteBackgroundBrush"] = MediaColor.FromRgb(67, 51, 23),
        ["FavoriteBorderBrush"] = MediaColor.FromRgb(120, 83, 28),
        ["SelectedItemBrush"] = MediaColor.FromRgb(30, 58, 95),
        ["RemoteInfoBrush"] = MediaColor.FromRgb(23, 42, 67),
        ["RemoteInfoBorderBrush"] = MediaColor.FromRgb(38, 75, 119),
        ["IconBrush"] = MediaColor.FromRgb(235, 239, 245)
    };
}
