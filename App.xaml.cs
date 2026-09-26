using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DDay3.Services;
using DDay3.Views;

namespace DDay3
{
    public partial class App : Application
    {
        internal const string ProductName = "D-Day 3";
        internal const string Version = "3.1.6";
        private Mutex instanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool createdNew;
            instanceMutex = new Mutex(true, @"Local\MathTime.DDay3", out createdNew);
            if (!createdNew)
            {
                instanceMutex.Dispose();
                instanceMutex = null;
                MessageBox.Show("D-Day 3가 이미 실행 중입니다.", ProductName,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(0);
                return;
            }

            AppServices.Initialize();
            RegisterExceptionHandlers();
            AppServices.Log.Info("app.start",
                "version=" + Version + ", startup=" + HasArgument(e.Args, "--startup") +
                ", os=" + Environment.OSVersion.VersionString +
                ", clr=" + Environment.Version);

            try
            {
                ConfigLoadResult loadResult = AppServices.Configuration.Load();
                AppServices.Log.Info("config.load",
                    "status=" + loadResult.Status + ", items=" + loadResult.Settings.Items.Count);

                if (loadResult.Settings.AutoStart)
                {
                    StartupResult startupResult = AppServices.Startup.Sync(true);
                    if (!startupResult.Success)
                    {
                        AppServices.Log.Warn("startup.sync", startupResult.Message);
                    }
                }

                MainWindow window = new MainWindow(loadResult.Settings, loadResult);
                MainWindow = window;
                window.Show();
            }
            catch (Exception ex)
            {
                AppServices.Log.Error("app.start.failed", ex);
                MessageBox.Show("프로그램을 시작하지 못했습니다.\n\n" + ex.Message,
                    ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (AppServices.Log != null)
                AppServices.Log.Info("app.exit", "code=" + e.ApplicationExitCode);
            AppServices.Dispose();
            if (instanceMutex != null)
            {
                instanceMutex.ReleaseMutex();
                instanceMutex.Dispose();
                instanceMutex = null;
            }
            base.OnExit(e);
        }

        private void RegisterExceptionHandlers()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs args)
            {
                AppServices.Log.Error("appdomain.unhandled", args.ExceptionObject as Exception,
                    "terminating=" + args.IsTerminating);
            };
            TaskScheduler.UnobservedTaskException += delegate(object sender, UnobservedTaskExceptionEventArgs args)
            {
                AppServices.Log.Error("task.unobserved", args.Exception);
                args.SetObserved();
            };
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            AppServices.Log.Error("dispatcher.unhandled", e.Exception);
            MessageBox.Show("예기치 않은 오류가 발생했습니다. 아래 오류 내용을 확인해 주세요.\n\n" +
                e.Exception.Message, ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private static bool HasArgument(string[] args, string value)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, value, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
