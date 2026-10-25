using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using DeSwitcher.Patcher.Logic;

namespace DeSwitcher.Patcher
{
    public partial class App : Application
    {
        internal static string VersionText
        {
            get
            {
                var v = typeof(App).Assembly.GetName().Version;
                return "deSwitcher " + v.Major + "." + v.Minor;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetDefaultDllDirectories(uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetDllDirectory(string path);

        private void OnStartup(object sender, StartupEventArgs e)
        {
            try
            {
                SetDefaultDllDirectories(0x00001000);
                SetDllDirectory("");
            }
            catch
            {
            }
            var args = e.Args;
            if (args.Length == 2 && (args[0] == "--install" || args[0] == "--remove" || args[0] == "--clean"))
            {
                Shutdown(RunHeadless(args[0], args[1]));
                return;
            }
            new MainWindow().Show();
        }

        private static int RunHeadless(string op, string game)
        {
            try
            {
                var ctx = new GameContext(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "payload"));
                if (!ctx.Locate(game))
                    return 2;
                if (op == "--install")
                    ctx.Install();
                else if (op == "--clean")
                    ctx.CleanForeign();
                else
                    ctx.Remove();
                return 0;
            }
            catch
            {
                return 1;
            }
        }
    }
}
