using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
                        arguments = command.Replace("msiexec", "").Trim();
                        break;

                    case "inf":
                        fileName = "pnputil.exe";
                        arguments = command.Replace("pnputil", "").Trim();
                        break;

                    case "cmd":
                        if (command.StartsWith("cmd.exe"))
                        {
                            fileName = "cmd.exe";
                            arguments = command.Substring("cmd.exe".Length).Trim();
                        }
                        else
                        {
                            fileName = "cmd.exe";
                            arguments = "/c " + command;
                        }
                        break;

                    case "copy":
                        // Copy folder operation
                        await CopyFolderAsync(_driver.Path, _driver.InstallCmd);
                        return;

                    case "shortcut":
                        // Create shortcut operation
                        CreateShortcut(_driver.Path, _driver.InstallCmd);
                        return;

                    case "registry":
                        // Merge registry file operation
                        await MergeRegistryFileAsync(_driver.Path);
                        return;

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

        private async Task CopyFolderAsync(string sourceFolder, string destinationFolder)
        {
            _log.Info($"Copying folder from: {sourceFolder} to: {destinationFolder}");
            
            try
            {
                if (!Directory.Exists(sourceFolder))
                {
                    _log.Error($"Source folder does not exist: {sourceFolder}");
                    throw new DirectoryNotFoundException($"Source folder not found: {sourceFolder}");
                }

                if (!Directory.Exists(destinationFolder))
                {
                    _log.Info($"Creating destination directory: {destinationFolder}");
                    Directory.CreateDirectory(destinationFolder);
                }

                await Task.Run(() => CopyDirectory(sourceFolder, destinationFolder, true));

                _log.Info($"Successfully copied folder: {sourceFolder} to {destinationFolder}");
            }
            catch (UnauthorizedAccessException ex)
            {
                _log.Error($"Access denied copying folder. Make sure application runs as administrator: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                _log.Error($"Error copying folder from {sourceFolder} to {destinationFolder}: {ex.Message}");
                throw;
            }
        }

        private void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            var dir = new DirectoryInfo(sourceDir);

            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                _log.Info($"Copying file: {file.Name}");
                file.CopyTo(targetFilePath, true);
            }

            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    _log.Info($"Copying subdirectory: {subDir.Name}");
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }

        private void CreateShortcut(string targetPath, string shortcutPath)
        {
            _log.Info($"Creating shortcut at: {shortcutPath} -> Target: {targetPath}");

            try
            {
                // Ensure the shortcut directory exists
                string shortcutDir = Path.GetDirectoryName(shortcutPath);
                if (!Directory.Exists(shortcutDir))
                {
                    _log.Info($"Creating shortcut directory: {shortcutDir}");
                    Directory.CreateDirectory(shortcutDir);
                }

                // Create the shortcut using Windows Script Host
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Description = _driver.Name;
                shortcut.Save();

                System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);

                _log.Info($"Successfully created shortcut: {shortcutPath}");
            }
            catch (Exception ex)
            {
                _log.Error($"Error creating shortcut at {shortcutPath}: {ex.Message}");
                throw;
            }
        }

        private async Task MergeRegistryFileAsync(string registryFilePath)
        {
            _log.Info($"Merging registry file: {registryFilePath}");

            try
            {
                // First, check if the file exists
                if (!File.Exists(registryFilePath))
                {
                    _log.Error($"Registry file does not exist: {registryFilePath}");
                    throw new FileNotFoundException($"Registry file not found: {registryFilePath}");
                }

                _log.Info($"Registry file found. Size: {new FileInfo(registryFilePath).Length} bytes");

                // Use reg.exe instead of regedit.exe for better error reporting
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "reg.exe",
                        Arguments = $"import \"{registryFilePath}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                _log.Info($"Executing: reg.exe import \"{registryFilePath}\"");

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit());

                _log.Info($"Registry import process exited with code: {process.ExitCode}");
                
                if (!string.IsNullOrWhiteSpace(output))
                    _log.Info($"Output: {output}");
                if (!string.IsNullOrWhiteSpace(error))
                    _log.Error($"Error Output: {error}");

                if (process.ExitCode != 0)
                {
                    _log.Error($"Registry import failed with exit code {process.ExitCode}");
                    throw new Exception($"Registry import failed with exit code {process.ExitCode}");
                }
                else
                {
                    _log.Info($"Successfully merged registry file: {registryFilePath}");
                }
            }
            catch (FileNotFoundException ex)
            {
                _log.Error($"Registry file not found: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                _log.Error($"Error merging registry file {registryFilePath}: {ex.Message}");
                throw;
            }
        }
    }
}
