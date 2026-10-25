using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using DeSwitcher.Core;
using DeSwitcher.Patcher.Logic;
using DeSwitcher.Patcher.Views;

namespace DeSwitcher.Patcher
{
    public partial class MainWindow : Window
    {
        private readonly GameContext game;
        private List<ModScan> scans = new List<ModScan>();

        public MainWindow()
        {
            InitializeComponent();
            VersionLabel.Text = App.VersionText;
            UpdateLang();
            game = new GameContext(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "payload"));
            bool found = game.Locate();
            if (found && game.Installed && game.PayloadOutdated())
                DoPatchOp("--install");
            if (found && game.Installed)
                RunScan(false);
            else
                ShowHome(found);
        }

        private async void RunScan(bool afterInstall)
        {
            var view = new ScanningView();
            Swap(view);
            var progress = new Progress<(int done, int total)>(p => view.SetProgress(p.done, p.total));
            var report = (IProgress<(int, int)>)progress;
            await System.Threading.Tasks.Task.Run(() =>
            {
                game.LoadGuard();
                try
                {
                    scans = game.ScanMods((d, t) => report.Report((d, t)));
                }
                catch
                {
                    scans = new List<ModScan>();
                }
            });
            ShowDashboard();
            foreach (var s in scans.Where(s => s.Verdict != null && s.Verdict.NeedsDecision).ToList())
                pending.Enqueue(s);
            if (pending.Count > 0)
                ShowNextDanger();
        }


        private void ShowHome(bool found)
        {
            var home = new HomeView();
            home.SetStatus(found ? game.Version : "?");
            home.EnableRequested += OnEnable;
            home.PickRequested += OnPick;
            Swap(home);
        }

        private void OnEnable()
        {
            if (game.Game == null && !game.Locate())
            {
                OnPick();
                return;
            }
            if (!game.Installed)
            {
                if (game.DetectForeign().Count > 0)
                {
                    if (!AskCleanForeign() || !DoPatchOp("--clean"))
                        return;
                    if (game.DetectForeign().Count > 0)
                    {
                        MessageBox.Show(Loc.Current == Lang.Ru
                            ? "Не удалось убрать старые файлы. Проверьте целостность файлов игры в Steam и попробуйте снова."
                            : "Could not remove the old files. Verify game files in Steam and try again.",
                            "deSwitcher", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                if (!DoPatchOp("--install"))
                    return;
            }
            RunScan(true);
        }

        private bool AskCleanForeign()
        {
            string names = string.Join(", ", game.DetectForeign().ConvertAll(f => f.Id));
            string ru = "Найдены файлы другого патча модов (" + names + "). Их нужно убрать, чтобы deSwitcher встал без конфликта. Удалить эти лишние файлы? Файлы самой игры не трогаются.";
            string en = "Files from another mod patch were found (" + names + "). They must be removed so deSwitcher can install cleanly. Remove these leftover files? Your game's own files are not touched.";
            return MessageBox.Show(Loc.Current == Lang.Ru ? ru : en, "deSwitcher", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        private bool DoPatchOp(string op)
        {
            if (GameContext.GameRunning())
            {
                MessageBox.Show(Loc.Current == Lang.Ru
                    ? "Закройте People Playground и попробуйте снова."
                    : "Close People Playground and try again.",
                    "deSwitcher", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            string managed = DeSwitcher.Patcher.Logic.GameLocator.ManagedDir(game.Game);
            try
            {
                if (GameContext.CanWrite(managed))
                {
                    if (op == "--install")
                        game.Install();
                    else if (op == "--clean")
                        game.CleanForeign();
                    else
                        game.Remove();
                }
                else if (!Elevate(op))
                {
                    return false;
                }
                else
                {
                    game.RefreshInstalled();
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "deSwitcher", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private bool Elevate(string op)
        {
            var psi = new System.Diagnostics.ProcessStartInfo(System.Reflection.Assembly.GetEntryAssembly().Location)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = op + " \"" + game.Game + "\""
            };
            try
            {
                var p = System.Diagnostics.Process.Start(psi);
                p.WaitForExit();
                return p.ExitCode == 0;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        private void OnPick()
        {
            var dialog = new OpenFileDialog
            {
                Title = "People Playground.exe",
                Filter = "People Playground|People Playground.exe|exe|*.exe",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() != true)
                return;
            string dir = Path.GetDirectoryName(dialog.FileName);
            if (game.Locate(dir))
                ShowHome(true);
        }

        private void ShowDashboard()
        {
            var dash = new DashboardView
            {
                Mods = scans.Select(s => new ModRow { Name = s.Info.Name, Status = s.Verdict.Status }).ToList(),
                Quarantined = game.Game != null ? Quarantine.List(game.Game) : new List<QuarantinedMod>(),
                GameVersion = game.Version,
                LastCheck = DateTime.Now.ToString("HH:mm")
            };
            dash.RemovePatchRequested += OnRemovePatch;
            dash.OpenFolderRequested += () => OpenFolder(game.Game);
            dash.RestoreRequested += q => { Quarantine.Restore(game.Game, q.Path); ShowDashboard(); };
            dash.DeleteRequested += q => { Quarantine.Delete(q.Path); ShowDashboard(); };
            dash.Localize();
            Swap(dash);
        }

        private readonly Queue<ModScan> pending = new Queue<ModScan>();

        private void ShowNextDanger()
        {
            if (pending.Count == 0)
            {
                ShowDashboard();
                return;
            }
            var s = pending.Dequeue();
            var dialog = new DangerDialog(s.Info.Name, s.Info.Folder, s.Verdict);
            dialog.Chosen += choice =>
            {
                Root.Children.Remove(dialog);
                Act(choice, s);
                ShowNextDanger();
            };
            Root.Children.Add(dialog);
        }

        private void Act(DangerChoice choice, ModScan s)
        {
            if (choice == DangerChoice.Quarantine)
                Quarantine.Move(game.Game, s.Info.Folder);
            else if (choice == DangerChoice.Trust)
            {
                game.State.TrustedHashes.Add(s.Verdict.Hash);
                game.SaveState();
            }
        }

        private void OnRemovePatch()
        {
            if (!DoPatchOp("--remove"))
                return;
            scans = new List<ModScan>();
            ShowHome(true);
        }

        private static void OpenFolder(string game)
        {
            if (game != null && Directory.Exists(game))
                System.Diagnostics.Process.Start("explorer.exe", game);
        }

        private void Swap(UIElement view)
        {
            Root.Children.Clear();
            Root.Children.Add(view);
        }

        private void UpdateLang() => LangText.Text = Loc.Current == Lang.Ru ? "RU|en" : "ru|EN";

        private void OnToggleLang(object sender, RoutedEventArgs e)
        {
            Loc.Current = Loc.Current == Lang.Ru ? Lang.En : Lang.Ru;
            UpdateLang();
            if (Root.Children.Count > 0 && Root.Children[0] is HomeView)
                ShowHome(game.Game != null);
            else if (Root.Children.Count > 0 && Root.Children[0] is DashboardView dash)
                dash.Localize();
        }

        private void OnChromeDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
