using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DeSwitcher.Core;
using DeSwitcher.Patcher.Logic;

namespace DeSwitcher.Patcher.Views
{
    public enum DangerChoice
    {
        Quarantine,
        RemoveSelf,
        Trust
    }

    public partial class DangerDialog : UserControl
    {
        public event Action<DangerChoice> Chosen;
        private const int LockSeconds = 5;

        public DangerDialog(string modName, string folder, ModVerdict verdict)
        {
            InitializeComponent();
            Title.Text = Loc.T("danger.title");
            ModName.Text = modName + "  ·  Mods/" + System.IO.Path.GetFileName(folder);
            Body.Text = Loc.T("danger.body");
            SiteLine.Text = "deswitchstudio.com";

            foreach (string rule in verdict.Findings.Where(f => f.Severity == Severity.Dangerous).Select(f => f.RuleId).Distinct().Take(4))
                Findings.Children.Add(new TextBlock
                {
                    Text = "→ " + Loc.T("finding." + rule),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11.5,
                    Foreground = (Brush)FindResource("Red"),
                    Margin = new Thickness(0, 1, 0, 1)
                });

            BuildButtons(verdict.CanTrust);
            Loaded += (s, e) => StartLock();
        }

        private void BuildButtons(bool canTrust)
        {
            Buttons.Children.Add(Button(Loc.T("danger.quarantine"), DangerChoice.Quarantine, true));
            Buttons.Children.Add(Button(Loc.T("danger.removeself"), DangerChoice.RemoveSelf, false));
            if (canTrust)
                Buttons.Children.Add(Button(Loc.T("danger.trust"), DangerChoice.Trust, false));
        }

        private Button Button(string text, DangerChoice choice, bool primary)
        {
            var b = new Button
            {
                Content = text,
                Style = (Style)FindResource(primary ? "PrimaryButton" : "GhostButton"),
                Margin = new Thickness(0, 0, 10, 0),
                IsEnabled = false,
                Tag = choice
            };
            if (primary)
                b.FontSize = 13;
            b.Click += (s, e) => Chosen?.Invoke(choice);
            return b;
        }

        private void StartLock()
        {
            var anim = new DoubleAnimation(40, 476, TimeSpan.FromSeconds(LockSeconds));
            TimerBar.BeginAnimation(WidthProperty, anim);
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(LockSeconds) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                foreach (Button b in Buttons.Children)
                    b.IsEnabled = true;
            };
            timer.Start();
        }
    }
}
