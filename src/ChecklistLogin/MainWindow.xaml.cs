using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using ChecklistLogin.ViewModels;

namespace ChecklistLogin
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            if (DataContext is MainViewModel vm)
            {
                vm.RequestClose += (s, e) =>
                {
                    // Allow window closing when view model signals request close
                    Close();
                };
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (DataContext is MainViewModel vm && !vm.CanCloseWindow)
            {
                e.Cancel = true;
            }
            base.OnClosing(e);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Intercept Alt+F4 and Escape key combinations
            if (e.Key == Key.System && e.SystemKey == Key.F4)
            {
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
            }
        }
    }
}
