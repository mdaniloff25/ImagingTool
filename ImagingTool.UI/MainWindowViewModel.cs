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
        private bool _isHardwareSupported = true;
        private bool _isHardwareCheckComplete = false;
        
        public ICommand StartCommand { get; }
        public ICommand ExitCommand { get; }

        private readonly ILog _log = LogManager.GetLogger(typeof(MainWindowViewModel));
        
        public MainWindowViewModel()
        {
            StartCommand = new AsyncRelayCommand(async () =>
            {
                await OnStartAsync();
            }, () => !IsRunning && IsHardwareSupported && IsHardwareCheckComplete);

            ExitCommand = new RelayCommand(x =>
                {
                    OnExit();
                },
                x => !IsRunning);

            // Initialize and check hardware on startup
            Task.Run(() => InitializeAndCheckHardware());
        }

        private async Task InitializeAndCheckHardware()
        {
            await Task.Delay(500);
            InitLogging();
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusMessage = "Detecting hardware...";
                IsHardwareCheckComplete = false; // Mark as not complete
            });
            
            InstallDrivers installer = new InstallDrivers(this);
            installer.InitializeDrivers();

            Application.Current.Dispatcher.Invoke(() =>
            {
                IsHardwareSupported = installer.IsHardwareSupported;
                IsHardwareCheckComplete = true; // Mark as complete

                if (IsHardwareSupported)
                {
                    StatusMessage = $"Ready to install drivers for {installer.DetectedModel} ({installer.DetectedCpu})";
                    _log.Info("Hardware is supported. Ready for installation.");
                }
                else
                {
                    StatusMessage = $"⚠ Unsupported Hardware Detected\n" +
                                   $"Model: {installer.DetectedModel}\n" +
                                   $"CPU: {installer.DetectedCpu}\n" +
                                   $"This terminal is not supported by the driver installation tool.";
                    _log.Error($"Unsupported hardware: Model={installer.DetectedModel}, CPU={installer.DetectedCpu}");
                }
            });
        }

        public async Task OnStartAsync()
        {
            IsRunning = true;
            CurrentProgressValue = 0;
            
            StatusMessage = "Inspecting system...";
            await Task.Yield();
            
            InstallDrivers installer = new InstallDrivers(this);
            installer.InitializeDrivers();

            if (!installer.IsHardwareSupported)
            {
                StatusMessage = $"Installation aborted: Unsupported hardware ({installer.DetectedModel}, {installer.DetectedCpu})";
                _log.Error("Installation aborted due to unsupported hardware.");
                IsRunning = false;
                return;
            }

            await installer.InstallHardwareDriversAsync();
            
            CurrentProgressValue = MaximumProgressValue;
            StatusMessage = "All drivers installed successfully!";
            
            await Task.Delay(1000);
            IsRunning = false;
        }

        private void OnExit()
        {
            Application.Current.Shutdown();
        }

        private void InitLogging()
        {
            log4net.Config.XmlConfigurator.Configure();
        }

        public bool IsHardwareCheckComplete
        {
            get { return _isHardwareCheckComplete; }
            set
            {
                if (value == _isHardwareCheckComplete)
                    return;
                _isHardwareCheckComplete = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsHardwareSupported
        {
            get { return _isHardwareSupported; }
            set
            {
                if (value == _isHardwareSupported)
                    return;
                _isHardwareSupported = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _isRunning = false;
        public bool IsRunning
        {
            get { return _isRunning; }
            set
            {
                if (value == _isRunning)
                    return;
                _isRunning = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

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

        private int _currentProgressValue = 0;
        private string _statusMessage = "Initializing...";

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

        public string VersionLabel => "Version 1.0.0";

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
