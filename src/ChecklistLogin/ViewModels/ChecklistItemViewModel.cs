using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ChecklistLogin.ViewModels
{
    public class ChecklistItemViewModel : INotifyPropertyChanged
    {
        private readonly Action _onStateChanged;
        private string _name = string.Empty;
        private bool? _isOk = null;
        private string _problemDescription = string.Empty;

        public ChecklistItemViewModel(string name, Action onStateChanged)
        {
            _name = name;
            _onStateChanged = onStateChanged;
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool? IsOk
        {
            get => _isOk;
            set
            {
                if (_isOk != value)
                {
                    _isOk = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsOkTrue));
                    OnPropertyChanged(nameof(IsOkFalse));
                    OnPropertyChanged(nameof(ShowDescriptionField));
                    _onStateChanged?.Invoke();
                }
            }
        }

        public bool IsOkTrue
        {
            get => _isOk == true;
            set
            {
                if (value)
                {
                    IsOk = true;
                }
            }
        }

        public bool IsOkFalse
        {
            get => _isOk == false;
            set
            {
                if (value)
                {
                    IsOk = false;
                }
            }
        }

        public bool ShowDescriptionField => _isOk == false;

        public string ProblemDescription
        {
            get => _problemDescription;
            set
            {
                if (_problemDescription != value)
                {
                    _problemDescription = value;
                    OnPropertyChanged();
                    _onStateChanged?.Invoke();
                }
            }
        }

        public bool IsValid
        {
            get
            {
                if (_isOk == null) return false;
                if (_isOk == true) return true;
                return !string.IsNullOrWhiteSpace(_problemDescription);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
