using System;
using System.Threading;
using System.Windows;

namespace ChecklistLogin
{
    public partial class App : Application
    {
        private static Mutex? _mutex = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "Global\\ChecklistLoginAppSingleInstanceMutex";
            _mutex = new Mutex(true, appName, out bool createdNew);

            if (!createdNew)
            {
                // Process is already running, prevent multiple instances
                Shutdown();
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                MessageBox.Show($"Ocurreu um erro inesperado: {ex?.Message}", "Erro do Sistema", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            base.OnStartup(e);
        }
    }
}
