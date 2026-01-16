using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Annotations;
using System.Windows.Input;
using ImagingTool.Core;
using ImagingTool.Models;
using log4net;

namespace ImagingTool.UI
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private int _maximumProgressValue = 25;
        public ICommand StartCommand { get; }
        public ICommand ExitCommand { get; }

        private readonly ILog _log = LogManager.GetLogger(typeof(MainWindowViewModel));
        public MainWindowViewModel()
        {
            StartCommand = new AsyncRelayCommand(async () =>
            {
                await OnStartAsync();
            }, () => !IsRunning);

            ExitCommand = new RelayCommand(x =>
                {
                    OnExit();
                },
                x => !IsRunning);
        }

        public async Task OnStartAsync()
        {
            IsRunning = true;
            InitLogging();
            StatusMessage = "Inspecting system...";
            await Task.Yield(); // Allows UI to update
            InstallDrivers installer = new InstallDrivers(this);
            installer.InitializeDrivers();
            await installer.InstallHardwareDriversAsync();
            StatusMessage = "All drivers installed successfully.";
            IsRunning = false;
        }


        private void OnExit()
        {
            Application.Current.Shutdown();
        }

        private void InitLogging()
        {
            // Initialize logging here
            log4net.Config.XmlConfigurator.Configure();
        }

        public bool IsRunning { get; set; } = false;
        public int MaximumProgressValue
        {
            get { return _maximumProgressValue; }
            set
            {
                if (value == _maximumProgressValue)
                    return;
                _maximumProgressValue = value;
                OnPropertyChanged();
            }
        }

        private int _currentProgressValue = 3;
        private string _statusMessage;

        public int CurrentProgressValue
        {
            get { return _currentProgressValue; }
            set
            {
                if (value == _currentProgressValue)
                    return;
                _currentProgressValue = value;
                OnPropertyChanged();
            }
        }

        public string StatusMessage
        {
            get { return _statusMessage; }
            set
            {
                if (value == _statusMessage)
                    return;
                _statusMessage = value;
                OnPropertyChanged();
            }
        }



        #region override impelementations
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
