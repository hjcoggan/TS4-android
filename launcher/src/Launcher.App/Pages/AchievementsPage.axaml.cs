using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Pages;

/// <summary>The Achievements page: every achievement the art folder holds, as a tile.</summary>
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

    /// <summary>Draws the tiles. <paramref name="picture"/> gives the drawn icon of an art file, or null when it cannot be drawn.</summary>
    public void Show(Showcase showcase, Func<string, IImage?> picture)
    {
        ListHost.Children.Clear();
        SummaryText.Text = showcase.AchievementsSummary;
        EmptyText.IsVisible = showcase.Achievements.Count == 0;
        foreach (var achievement in showcase.Achievements)
        {
            var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = achievement.Title, FontSize = 20, FontWeight = FontWeight.Bold, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap });
            var detail = new TextBlock { Text = achievement.Detail };
            detail.Classes.Add("small");
            text.Children.Add(detail);
            var points = new TextBlock
            {
                Text = $"{achievement.Points} G",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new(12, 0, 0, 0),
            };
            // The theme's brushes are recoloured in place (LauncherLook.Apply), so the brush itself is kept, not its colour.
            if (Avalonia.Application.Current?.FindResource("AccentTextBrush") is IBrush accent) points.Foreground = accent;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            row.Children.Add(new Image { Source = picture(achievement.ImagePath), Width = 64, Height = 64, Margin = new(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top });
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            Grid.SetColumn(points, 2);
            row.Children.Add(points);
            var tile = new Border { Child = row, Margin = new(0, 0, 12, 12) };
            tile.Classes.Add("card");
            ListHost.Children.Add(tile);
        }
    }
}
