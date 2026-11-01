using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Pages;

/// <summary>The Achievements page: every achievement the art folder holds, as a tile, earned ones first.</summary>
public partial class AchievementsPage : UserControl
{
    public AchievementsPage()
    {
        InitializeComponent();
        BackButton.Click += (_, _) => BackPressed?.Invoke();
    }

    public event Action? BackPressed;

    public Control EntryControl => BackButton;

    public void FocusFirst() => BackButton.Focus(NavigationMethod.Tab);

    /// <summary>
    /// Draws the tiles. <paramref name="picture"/> gives the drawn icon of an art file, and <paramref name="lockedPicture"/>
    /// its greyed form for a locked achievement; either is null when it cannot be drawn.
    /// </summary>
    public void Show(Showcase showcase, Func<string, IImage?> picture, Func<string, IImage?> lockedPicture)
    {
        ListHost.Children.Clear();
        SummaryText.Text = showcase.AchievementsSummary;
        EmptyText.IsVisible = showcase.Achievements.Count == 0;
        var total = showcase.TotalPoints;
        var earned = showcase.EarnedPoints;
        ScoreBar.IsVisible = showcase.UnlockedCount > 0 && total > 0;
        ScoreBar.Value = total == 0 ? 0 : (double)earned / total;
        // The theme's brushes are recoloured in place (LauncherLook.Apply), so the brush itself is kept, not its colour.
        var accent = Brush("AccentTextBrush") ?? Brushes.White;
        var muted = Brush("MutedBrush") ?? Brushes.Gray;
        foreach (var achievement in showcase.AchievementsInOrder)
            ListHost.Children.Add(Tile(achievement, showcase.IsUnlocked(achievement), showcase.UnlockedAt(achievement), picture, lockedPicture, accent, muted));
    }

    private static IBrush? Brush(string key) => Avalonia.Application.Current?.FindResource(key) as IBrush;

    private static Border Tile(DiscArtEntry achievement, bool unlocked, DateTime? when, Func<string, IImage?> picture,
        Func<string, IImage?> lockedPicture, IBrush accent, IBrush muted)
    {
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = achievement.Title,
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = unlocked ? Brushes.White : muted,
            TextWrapping = TextWrapping.Wrap,
        });
        // As the console does: what was done once it is earned, what to do while it is locked.
        var detail = new TextBlock { Text = unlocked || achievement.HowTo.Length == 0 ? achievement.Detail : achievement.HowTo };
        detail.Classes.Add("small");
        text.Children.Add(detail);
        var status = new TextBlock
        {
            Text = unlocked
                ? when is { } time ? "Unlocked " + time.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture) : "Unlocked"
                : "Locked",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = unlocked ? accent : muted,
            Margin = new(0, 2, 0, 0),
        };
        text.Children.Add(status);

        var points = new TextBlock
        {
            Text = $"{achievement.Points} G",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new(12, 0, 0, 0),
            Foreground = unlocked ? accent : muted,
        };

        var icon = new Grid { Width = 64, Height = 64, Margin = new(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top };
        var source = unlocked ? picture(achievement.ImagePath) : lockedPicture(achievement.ImagePath) ?? picture(achievement.ImagePath);
        icon.Children.Add(new Image { Source = source, Width = 64, Height = 64, Opacity = unlocked ? 1 : 0.5 });
        if (!unlocked)
        {
            var badge = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new(13),
                Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x10, 0x10, 0x10)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new(0, 0, -4, -4),
                Child = new ContentControl { Content = Icons.Make(Icons.Padlock, 16), Foreground = Brushes.White },
            };
            icon.Children.Add(badge);
        }

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        row.Children.Add(icon);
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        Grid.SetColumn(points, 2);
        row.Children.Add(points);
        var tile = new Border { Child = row, Margin = new(0, 0, 12, 12), Opacity = unlocked ? 1 : 0.85 };
        tile.Classes.Add("card");
        if (unlocked) tile.Classes.Add("earned");
        return tile;
    }
}
