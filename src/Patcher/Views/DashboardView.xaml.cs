using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DeSwitcher.Core;
using DeSwitcher.Patcher.Logic;

namespace DeSwitcher.Patcher.Views
{
    public sealed class ModRow
    {
        public string Name;
        public ModStatus Status;
    }

    public partial class DashboardView : UserControl
    {
        public event Action RemovePatchRequested;
        public event Action OpenFolderRequested;
        public event Action<QuarantinedMod> RestoreRequested;
        public event Action<QuarantinedMod> DeleteRequested;

        private readonly List<(string Key, TextBlock Label, TextBlock Badge, Path Icon)> nav = new List<(string, TextBlock, TextBlock, Path)>();
        private string page = "mods";

        private static readonly Dictionary<string, string> NavIcons = new Dictionary<string, string>
        {
            ["nav.home"] = "M15 21v-8a1 1 0 0 0-1-1h-4a1 1 0 0 0-1 1v8 M3 10a2 2 0 0 1 .709-1.528l7-6a2 2 0 0 1 2.582 0l7 6A2 2 0 0 1 21 10v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z",
            ["nav.mods"] = "M11 21.73a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73z M12 22V12 M3.29 7 12 12l8.71-5 M7.5 4.27 l9 5.15",
            ["nav.quarantine"] = "M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z M14.5 9.5 l-5 5 M9.5 9.5 l5 5",
            ["nav.settings"] = "M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915 M12 12 m-3 0 a3 3 0 1 0 6 0 a3 3 0 1 0 -6 0",
        };

        public List<ModRow> Mods = new List<ModRow>();
        public List<QuarantinedMod> Quarantined = new List<QuarantinedMod>();
        public string GameVersion = "?";
        public string LastCheck = "";

        public DashboardView()
        {
            InitializeComponent();
            BuildNav();
            Localize();
            Show("mods");
        }

        private void OnBanner(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try { System.Diagnostics.Process.Start("https://deswitchstudio.com"); } catch { }
        }

        public void Localize()
        {
            Banner.Text = Loc.T("banner");
            GuardLine.Text = Loc.T("guard.on");
            foreach (var item in nav)
                item.Label.Text = Loc.T(item.Key);
            Show(page);
        }

        private void BuildNav()
        {
            foreach (string key in new[] { "nav.mods", "nav.quarantine", "nav.settings" })
                Nav.Children.Add(NavItem(key));
        }

        private Border NavItem(string key)
        {
            var icon = new Path { Style = (Style)FindResource("Icon"), Width = 15, Height = 15, StrokeThickness = 1.8, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse(NavIcons[key]) };
            var label = new TextBlock { FontSize = 12.5, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var badge = new TextBlock { FontSize = 10, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("Dim") };
            var panel = new DockPanel();
            DockPanel.SetDock(badge, Dock.Right);
            DockPanel.SetDock(icon, Dock.Left);
            panel.Children.Add(badge);
            panel.Children.Add(icon);
            panel.Children.Add(label);
            var border = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 1, 0, 1), Cursor = System.Windows.Input.Cursors.Hand, Child = panel };
            string p = key.Substring(4);
            border.MouseLeftButtonUp += (s, e) => Show(p);
            nav.Add((key, label, badge, icon));
            return border;
        }

        private void Show(string which)
        {
            page = which;
            for (int i = 0; i < nav.Count; i++)
            {
                var border = (Border)Nav.Children[i];
                bool active = nav[i].Key.Substring(4) == which;
                border.Background = active ? new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1B)) : Brushes.Transparent;
                var fg = active ? (Brush)FindResource("Text") : (Brush)FindResource("Dim");
                nav[i].Label.Foreground = fg;
                nav[i].Icon.Stroke = fg;
            }

            ListHost.Children.Clear();
            if (which == "quarantine")
                ShowQuarantine();
            else if (which == "settings")
                ShowSettings();
            else
                ShowMods();
        }

        private void ShowMods()
        {
            PageKicker.Text = "People Playground " + GameVersion;
            PageTitle.Text = Loc.T("mods.on");
            PageSub.Text = Mods.Count + "  ·  " + Loc.T("lastcheck").ToLower() + ": " + LastCheck;
            if (Mods.Count == 0)
                ListHost.Children.Add(Empty(Loc.T("scan.none")));
            foreach (var m in Mods)
                ListHost.Children.Add(ModRowView(m));
        }

        private void ShowQuarantine()
        {
            PageKicker.Text = "";
            PageTitle.Text = Loc.T("nav.quarantine");
            PageSub.Text = Quarantined.Count.ToString();
            if (Quarantined.Count == 0)
                ListHost.Children.Add(Empty(Loc.T("quarantine.empty")));
            foreach (var q in Quarantined)
                ListHost.Children.Add(QuarantineRowView(q));
        }

        private void ShowSettings()
        {
            PageKicker.Text = "";
            PageTitle.Text = Loc.T("nav.settings");
            PageSub.Text = App.VersionText + "  ·  People Playground " + GameVersion;
            ListHost.Children.Add(SettingButton(Loc.T("remove.patch"), () => RemovePatchRequested?.Invoke()));
            ListHost.Children.Add(SettingButton(Loc.T("open.folder"), () => OpenFolderRequested?.Invoke()));
        }

        private UIElement ModRowView(ModRow m)
        {
            var name = new TextBlock { Text = m.Name, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center };
            var tag = Tag(m.Status);
            var panel = new DockPanel { Margin = new Thickness(14, 10, 14, 10) };
            DockPanel.SetDock(tag, Dock.Right);
            panel.Children.Add(tag);
            panel.Children.Add(name);
            return WithRule(panel);
        }

        private UIElement QuarantineRowView(QuarantinedMod q)
        {
            var name = new TextBlock { Text = q.Name, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center };
            var restore = SmallGhost(Loc.T("quarantine.restore"));
            restore.Click += (s, e) => RestoreRequested?.Invoke(q);
            var del = SmallGhost(Loc.T("quarantine.delete"));
            del.Click += (s, e) => DeleteRequested?.Invoke(q);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            buttons.Children.Add(restore);
            buttons.Children.Add(del);
            var panel = new DockPanel { Margin = new Thickness(14, 9, 14, 9) };
            DockPanel.SetDock(buttons, Dock.Right);
            panel.Children.Add(buttons);
            panel.Children.Add(name);
            return WithRule(panel);
        }

        private Border Tag(ModStatus status)
        {
            string text;
            Color fg, bg;
            switch (status)
            {
                case ModStatus.FromSite: text = Loc.T("tag.site"); fg = Hex("#3DE0FF"); break;
                case ModStatus.Trusted: text = Loc.T("tag.trusted"); fg = Hex("#3DE0FF"); break;
                case ModStatus.Suspicious: text = Loc.T("tag.suspicious"); fg = Hex("#FFB23D"); break;
                case ModStatus.Dangerous: text = Loc.T("tag.dangerous"); fg = Hex("#FF3D5A"); break;
                case ModStatus.Blocked: text = Loc.T("tag.blocked"); fg = Hex("#FF3D5A"); break;
                default: text = Loc.T("tag.clean"); fg = Hex("#3DFF9A"); break;
            }
            bg = Color.FromArgb(0x1A, fg.R, fg.G, fg.B);
            return new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(7, 2, 7, 2),
                Background = new SolidColorBrush(bg),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = text, FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(fg) }
            };
        }

        private Border WithRule(UIElement content)
        {
            return new Border { BorderBrush = Hex("#1B1B1E").ToBrush(), BorderThickness = new Thickness(0, 0, 0, 1), Child = content };
        }

        private TextBlock Empty(string text) =>
            new TextBlock { Text = text, Foreground = (Brush)FindResource("Dim"), FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 40) };

        private Button SmallGhost(string text)
        {
            var b = new Button { Content = text, Style = (Style)FindResource("GhostButton"), Margin = new Thickness(8, 0, 0, 0) };
            return b;
        }

        private UIElement SettingButton(string text, Action onClick)
        {
            var b = new Button { Content = text, Style = (Style)FindResource("GhostButton"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(14, 12, 0, 0) };
            b.Click += (s, e) => onClick();
            return b;
        }

        private static Color Hex(string s) => (Color)ColorConverter.ConvertFromString(s);
    }

    internal static class ColorExt
    {
        public static Brush ToBrush(this Color c) => new SolidColorBrush(c);
    }
}
