using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace ArkBoard
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = new System.Globalization.CultureInfo("en-US");
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            if (args.Length > 0 && (args[0] == "--self-test" || args[0] == "--benchmark"))
            {
                string folder = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-output");
                app.Startup += async delegate
                {
                    try { if (args[0] == "--benchmark") await RenderBenchmarks.Run(folder); else await SelfTests.Run(folder); app.Shutdown(0); }
                    catch (Exception ex)
                    { Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, "FAILED.txt"), ex.ToString()); app.Shutdown(1); }
                };
            }
            else
            {
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                app.Startup += delegate
                {
                    MainWindow window = new MainWindow(false); app.MainWindow = window; window.Show();
                    if (args.Length > 0 && File.Exists(args[0])) window.OpenProject(args[0]);
                };
            }
            return app.Run();
        }
    }
}
