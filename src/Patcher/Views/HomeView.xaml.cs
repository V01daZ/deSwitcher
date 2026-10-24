using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DeSwitcher.Patcher.Logic;

namespace DeSwitcher.Patcher.Views
{
    public partial class HomeView : UserControl
    {
        public event Action EnableRequested;
        public event Action PickRequested;

        private const string ShieldCheck = "M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z M9 12 l2 2 4-4";
        private const string Ban = "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20 M4.93 4.93 19.07 19.07";
        private const string BadgeCheck = "M3.85 8.62a4 4 0 0 1 4.78-4.77 4 4 0 0 1 6.74 0 4 4 0 0 1 4.78 4.78 4 4 0 0 1 0 6.74 4 4 0 0 1-4.77 4.78 4 4 0 0 1-6.75 0 4 4 0 0 1-4.78-4.77 4 4 0 0 1 0-6.76Z M16 9 l-5.5 5.5L8 12";

        public HomeView()
        {
            InitializeComponent();
            Localize();
        }

        public void SetStatus(string gameVersion)
        {
            StatusLine.Text = Loc.T("mods.off") + "  ·  People Playground " + gameVersion;
        }

        public void Localize()
        {
            Subtitle.Text = Loc.T("subtitle") + "  ·  by deswitchstudio.com";
            EnableText.Text = Loc.T("enable");
            PickText.Text = Loc.T("pick.game");
            Banner.Text = Loc.T("banner");

            Reassure.Children.Clear();
            Reassure.Children.Add(ReassureRow(ShieldCheck, Loc.T("home.r1")));
            Reassure.Children.Add(ReassureRow(Ban, Loc.T("home.r2")));
            Reassure.Children.Add(ReassureRow(BadgeCheck, Loc.T("home.r3")));
        }

        private UIElement ReassureRow(string icon, string text)
        {
            var path = new Path
            {
                Style = (Style)FindResource("Icon"),
                Width = 15,
                Height = 15,
                Stroke = (Brush)FindResource("Dim"),
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse(icon)
            };
            var label = new TextBlock
            {
                Text = text,
                FontSize = 12.5,
                Foreground = (Brush)FindResource("Dim"),
                Margin = new Thickness(11, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 13) };
            row.Children.Add(path);
            row.Children.Add(label);
            return row;
        }

        private void OnBanner(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try { System.Diagnostics.Process.Start("https://deswitchstudio.com"); } catch { }
        }

        private void OnEnable(object sender, RoutedEventArgs e) => EnableRequested?.Invoke();

        private void OnPick(object sender, RoutedEventArgs e) => PickRequested?.Invoke();
    }
}
