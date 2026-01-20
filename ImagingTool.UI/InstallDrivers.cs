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
        private Terminal _terminal;
        private List<Driver> _commonDrivers = new List<Driver>();

        public InstallDrivers(MainWindowViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public void InitializeDrivers()
        {
            GetHardwareInfo();
            string manifestPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "manifest.json");
            Manifest manifest = ManifestLoader.LoadManifest(manifestPath);
            _commonDrivers = manifest.CommonDrivers;

            _terminal = manifest.Terminals
                .FirstOrDefault(t => t.Model.Equals(_model, StringComparison.OrdinalIgnoreCase)
                                     && _cpu.IndexOf(t.Cpu, StringComparison.OrdinalIgnoreCase) >= 0);

            //Todo: Set the status that the suitable terminal is found or not
            if (_terminal != null)
            {
                foreach (var driver in _terminal.Drivers)
                {
                    
                    _log.Info($"Added driver: {driver.Name} for model: {_model}, CPU: {_cpu}");
                }
            }
        }

        public async Task InstallHardwareDriversAsync()
        {
            _viewModel.CurrentProgressValue += 1;
            List<DriverInstaller> installers = new List<DriverInstaller>();
            var driversToInstall = _terminal?.Drivers;
            string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            foreach (var driver in driversToInstall)
            {
                driver.Path = Path.Combine(assemblyDirectory, driver.Path);
                _log.Info($"Preparing to install driver: {driver.Name} path: {driver.Path} command: {driver.InstallCmd}");
                installers.Add(new DriverInstaller(driver));
            }

            foreach (var driver in _commonDrivers)
            {
                driver.Path = Path.Combine(assemblyDirectory, driver.Path);
                _log.Info($"Preparing to install common driver: {driver.Name} path: {driver.Path} command: {driver.InstallCmd}");
                installers.Add(new DriverInstaller(driver));
            }

            foreach (var installer in installers)
            {
                _viewModel.StatusMessage = $"Installing {installer.DriverPath}";
                await installer.InstallDriverAsync();
                _viewModel.CurrentProgressValue += 2;
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
