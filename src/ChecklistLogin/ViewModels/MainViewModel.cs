using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using ChecklistLogin.Models;
using ChecklistLogin.Services;

namespace ChecklistLogin.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly AppConfig _config;
        private bool _canCloseWindow = false;

        public event EventHandler? RequestClose;

        public string UserName { get; }
        public string MachineName { get; }

        public ObservableCollection<ChecklistItemViewModel> ChecklistItems { get; }

        public ICommand SubmitCommand { get; }

        public MainViewModel()
        {
            UserName = Environment.UserName;
            MachineName = Environment.MachineName;

            _config = ConfigService.LoadConfig();

            ChecklistItems = new ObservableCollection<ChecklistItemViewModel>
            {
                new ChecklistItemViewModel("Tela", OnItemStateChanged),
                new ChecklistItemViewModel("Teclado", OnItemStateChanged),
                new ChecklistItemViewModel("Mouse", OnItemStateChanged),
                new ChecklistItemViewModel("Internet", OnItemStateChanged),
                new ChecklistItemViewModel("Computador", OnItemStateChanged)
            };

            SubmitCommand = new RelayCommand(ExecuteSubmit, CanExecuteSubmit);
        }

        public bool CanCloseWindow
        {
            get => _canCloseWindow;
            private set
            {
                _canCloseWindow = value;
                OnPropertyChanged();
            }
        }

        public bool CanSubmit => ChecklistItems.All(i => i.IsValid);

        private void OnItemStateChanged()
        {
            OnPropertyChanged(nameof(CanSubmit));
            (SubmitCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private bool CanExecuteSubmit()
        {
            return CanSubmit;
        }

        private void ExecuteSubmit()
        {
            if (!CanSubmit) return;

            var logResult = LogService.SaveLog(_config, MachineName, UserName, ChecklistItems);

            if (logResult.Success)
            {
                if (logResult.UsedFallback)
                {
                    MessageBox.Show(
                        $"Checklist gravado com sucesso em local alternativo:\n{logResult.FilePath}",
                        "Checklist Concluído",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                CanCloseWindow = true;
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                MessageBox.Show(
                    $"Erro ao salvar log do checklist:\n{logResult.ErrorMessage}\n\nPor favor, tente novamente.",
                    "Erro ao Salvar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
