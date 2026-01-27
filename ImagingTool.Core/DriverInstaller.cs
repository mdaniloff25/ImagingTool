using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImagingTool.Models;
using log4net;

namespace ImagingTool.Core
{
    public class DriverInstaller
    {
        private readonly Driver _driver = null;
        private readonly ILog _log = LogManager.GetLogger(typeof(DriverInstaller));
        public DriverInstaller(Driver driver)
        {
            _driver = driver;
        }

        public string DriverPath => _driver.Path;

        public async Task InstallDriverAsync()
        {
            _log.Info($"Starting installation: {_driver.Name}");
            try
            {
                //string command = _driver.InstallCmd.Replace("{path}", _driver.Path);
                string exePath = _driver.Path;
                string exeDir = System.IO.Path.GetDirectoryName(exePath);
                string command = _driver.InstallCmd.Replace("{path}", exePath).Replace("{dir}", exeDir);
                string fileName, arguments;

                string workingDirectory = System.IO.Path.GetDirectoryName(_driver.Path);
                switch (_driver.Type.ToLower())
                {
                    case "exe":
                        // Remove outer quotes if present
                        if (command.StartsWith("\""))
                        {
                            int endQuote = command.IndexOf('\"', 1);
                            fileName = command.Substring(1, endQuote - 1);
                            arguments = command.Substring(endQuote + 1).Trim();
                        }
                        else
                        {
                            int firstSpace = command.IndexOf(' ');
                            if (firstSpace > 0)
                            {
                                fileName = command.Substring(0, firstSpace);
                                arguments = command.Substring(firstSpace + 1);
                            }
                            else
                            {
                                fileName = command;
                                arguments = "";
                            }
                        }
                        break;

                    case "msi":
                        fileName = "msiexec.exe";
                        // Remove msiexec from the command if present
                        arguments = command.Replace("msiexec", "").Trim();
                        break;

                    case "inf":
                        fileName = "pnputil.exe";
                        // Remove pnputil from the command if present
                        arguments = command.Replace("pnputil", "").Trim();
                        break;

                    case "cmd":
                        // For cmd, the command should already be in the correct format
                        // Example: cmd.exe /c "path\to\script.bat" or cmd.exe /c "path\to\exe" [args]
                        if (command.StartsWith("cmd.exe"))
                        {
                            fileName = "cmd.exe";
                            arguments = command.Substring("cmd.exe".Length).Trim();
                        }
                        else
                        {
                            // fallback: treat as a batch or command file
                            fileName = "cmd.exe";
                            arguments = "/c " + command;
                        }
                        break;

                    default:
                        throw new NotSupportedException($"Driver type '{_driver.Type}' is not supported.");
                }

                _log.Info($"Executing: {fileName} {arguments}");

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WorkingDirectory = workingDirectory
                    }
                };
                _log.Info($"Working directory: {workingDirectory}");
                _log.Info($"Exe dir {exeDir}");
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit());

                _log.Info($"Process exited with code: {process.ExitCode}");
                if (!string.IsNullOrWhiteSpace(output))
                    _log.Info($"Output: {output}");
                if (!string.IsNullOrWhiteSpace(error))
                    _log.Error($"Error Output: {error}");

                if (process.ExitCode == 3010)
                {
                    _log.Warn($"Reboot required for {_driver.Name}. Exit code 3010");
                }
                if (process.ExitCode != 0)
                {
                    _log.Error($"Installation failed for {_driver.Name} with exit code {process.ExitCode}");
                }
                else
                {
                    _log.Info($"Completed installation: {_driver.Name}");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error installing {_driver.Name}: {ex.Message}");
            }
        }


    }

}
