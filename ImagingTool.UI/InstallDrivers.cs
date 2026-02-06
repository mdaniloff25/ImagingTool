using ImagingTool.Core;
using ImagingTool.Models;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ImagingTool.UI
{
    public class InstallDrivers
    {
        MainWindowViewModel _viewModel;
        private readonly ILog _log = LogManager.GetLogger(typeof(MainWindowViewModel));
        private string _model;
        private string _cpu;
        private Terminal _systemDriversTerminal;
        private Terminal _peripheralDriversTerminal;
        private List<Driver> _commonDrivers = new List<Driver>();

        public InstallDrivers(MainWindowViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        // Property to check if hardware is supported
        public bool IsHardwareSupported => _systemDriversTerminal != null;

        // Properties to expose hardware info
        public string DetectedModel => _model;
        public string DetectedCpu => _cpu;

        public void InitializeDrivers()
        {
            GetHardwareInfo();
            string manifestPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "manifest.json");
            Manifest manifest = ManifestLoader.LoadManifest(manifestPath);
            _commonDrivers = manifest.CommonDrivers ?? new List<Driver>();

            // Find matching system drivers
            _systemDriversTerminal = manifest.SystemDrivers
                ?.FirstOrDefault(t => t.Model.Equals(_model, StringComparison.OrdinalIgnoreCase)
                                     && _cpu.IndexOf(t.Cpu, StringComparison.OrdinalIgnoreCase) >= 0);

            // Find matching peripheral drivers
            _peripheralDriversTerminal = manifest.PeripheralDrivers
                ?.FirstOrDefault(t => t.Model.Equals(_model, StringComparison.OrdinalIgnoreCase)
                                     && _cpu.IndexOf(t.Cpu, StringComparison.OrdinalIgnoreCase) >= 0);

            if (_systemDriversTerminal != null)
            {
                _log.Info($"Found system drivers for model: {_model}, CPU: {_cpu}");
                foreach (var driver in _systemDriversTerminal.Drivers)
                {
                    _log.Info($"System driver: {driver.Name}");
                }
            }
            else
            {
                _log.Warn($"No system drivers found for model: {_model}, CPU: {_cpu}");
                _log.Warn($"This hardware is NOT SUPPORTED. Installation will be skipped.");
            }

            if (_peripheralDriversTerminal != null)
            {
                _log.Info($"Found peripheral drivers for model: {_model}, CPU: {_cpu}");
                foreach (var driver in _peripheralDriversTerminal.Drivers)
                {
                    _log.Info($"Peripheral driver: {driver.Name}");
                }
            }
            else
            {
                _log.Info($"No peripheral drivers found for model: {_model}, CPU: {_cpu}");
            }
        }

        public async Task InstallHardwareDriversAsync()
        {
            // Check if hardware is supported before proceeding
            if (!IsHardwareSupported)
            {
                _log.Error($"Cannot install drivers: Hardware not supported (Model: {_model}, CPU: {_cpu})");
                _viewModel.StatusMessage = $"Installation aborted: Unsupported hardware";
                return;
            }

            _viewModel.CurrentProgressValue += 1;
            List<DriverInstaller> installers = new List<DriverInstaller>();
            string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Step 1: Install system drivers first (chipset, serial IO, etc.)
            if (_systemDriversTerminal != null)
            {
                _log.Info("=== Installing System Drivers ===");
                foreach (var driver in _systemDriversTerminal.Drivers)
                {
                    driver.Path = Path.Combine(assemblyDirectory, driver.Path);
                    _log.Info($"Preparing to install system driver: {driver.Name} path: {driver.Path}");
                    installers.Add(new DriverInstaller(driver));
                }

                foreach (var installer in installers)
                {
                    _viewModel.StatusMessage = $"Installing system driver [{installer.DriverName}]: {Path.GetFileName(installer.DriverPath)}";
                    await installer.InstallDriverAsync();
                    _viewModel.CurrentProgressValue += 2;
                }

                installers.Clear();
            }

            // Step 2: Install common drivers
            if (_commonDrivers.Any())
            {
                _log.Info("=== Installing Common Drivers ===");
                foreach (var driver in _commonDrivers)
                {
                    driver.Path = Path.Combine(assemblyDirectory, driver.Path);
                    _log.Info($"Preparing to install common driver: {driver.Name} path: {driver.Path}");
                    installers.Add(new DriverInstaller(driver));
                }

                foreach (var installer in installers)
                {
                    _viewModel.StatusMessage = $"Installing common driver [{installer.DriverName}]: {Path.GetFileName(installer.DriverPath)}";
                    await installer.InstallDriverAsync();
                    _viewModel.CurrentProgressValue += 2;
                }

                installers.Clear();
            }

            // Step 3: Install peripheral drivers last
            if (_peripheralDriversTerminal != null)
            {
                _log.Info("=== Installing Peripheral Drivers ===");
                foreach (var driver in _peripheralDriversTerminal.Drivers)
                {
                    driver.Path = Path.Combine(assemblyDirectory, driver.Path);
                    _log.Info($"Preparing to install peripheral driver: {driver.Name} path: {driver.Path}");
                    installers.Add(new DriverInstaller(driver));
                }

                foreach (var installer in installers)
                {
                    _viewModel.StatusMessage = $"Installing peripheral driver [{installer.DriverName}]: {Path.GetFileName(installer.DriverPath)}";
                    await installer.InstallDriverAsync();
                    _viewModel.CurrentProgressValue += 2;
                }
            }
        }

        private void GetHardwareInfo()
        {
            _model = HardwareInfo.GetModel();
            _cpu = HardwareInfo.GetCpu();
            _log.Info($"Model: {_model}");
            _log.Info($"CPU: {_cpu}");
        }
    }
}
