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
        public string DriverName => _driver.Name;

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

                    case "displayscale":
                        // Set display scale for current user and default user
                        await SetDisplayScaleAsync(_driver.InstallCmd);
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

        private async Task SetDisplayScaleAsync(string scaleValue)
        {
            _log.Info($"Setting display scale to: {scaleValue}%");
            
            try
            {
                // Parse the scale value (e.g., "175" for 175%)
                if (!int.TryParse(scaleValue, out int scale))
                {
                    _log.Error($"Invalid scale value: {scaleValue}. Expected a number like 100, 125, 150, 175, etc.");
                    throw new ArgumentException($"Invalid scale value: {scaleValue}");
                }

                // Get the registry value for the scaling percentage
                int registryValue = GetScalingRegistryValue(scale);
                _log.Info($"Using registry value {registryValue} for {scale}% scaling");

                // Step 1: Set display scale for current user
                _log.Info("Setting display scale for current user...");
                SetDisplayScaleForCurrentUser(registryValue);

                // Step 2: Set display scale for default user
                _log.Info("Setting display scale for default user...");
                await SetDisplayScaleForDefaultUserAsync(registryValue);

                _log.Info($"Successfully set display scale to {scale}% (registry value: {registryValue}) for current and default users");
            }
            catch (Exception ex)
            {
                _log.Error($"Error setting display scale: {ex.Message}");
                throw;
            }
        }

        private int GetScalingRegistryValue(int scalePercent)
        {
            // Windows uses specific registry values for scaling percentages
            // These are empirically determined values that Windows expects
            switch (scalePercent)
            {
                case 100:
                    return 96;   // 100% = 96 DPI (default)
                case 125:
                    return 120;  // 125% = 120
                case 150:
                    return 144;  // 150% = 144
                case 175:
                    return 140;  // User-tested: 140 works for 175% on this hardware
                case 200:
                    return 192;  // 200% = 192
                case 225:
                    return 216;  // 225% = 216
                case 250:
                    return 240;  // 250% = 240
                case 300:
                    return 288;  // 300% = 288
                case 350:
                    return 336;  // 350% = 336
                case 400:
                    return 384;  // 400% = 384
                case 450:
                    return 432;  // 450% = 432
                case 500:
                    return 480;  // 500% = 480
                default:
                    // For custom values, log a warning and return the value as-is
                    _log.Warn($"Non-standard scaling value: {scalePercent}%. Using value directly.");
                    return scalePercent;
            }
        }

        private void SetDisplayScaleForCurrentUser(int dpiValue)
        {
            try
            {
                _log.Info($"Setting DPI value to {dpiValue} for current user");

                // Set the LogPixels value in registry for current user
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
                {
                    if (key != null)
                    {
                        key.SetValue("LogPixels", dpiValue, Microsoft.Win32.RegistryValueKind.DWord);
                        key.SetValue("Win8DpiScaling", 1, Microsoft.Win32.RegistryValueKind.DWord);
                        _log.Info("Updated HKCU\\Control Panel\\Desktop");
                    }
                }

                // Also set in Desktop\WindowMetrics
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop\WindowMetrics", true))
                {
                    if (key != null)
                    {
                        key.SetValue("AppliedDPI", dpiValue, Microsoft.Win32.RegistryValueKind.DWord);
                        _log.Info("Updated HKCU\\Control Panel\\Desktop\\WindowMetrics");
                    }
                }

                _log.Info("Successfully set display scale for current user");
            }
            catch (Exception ex)
            {
                _log.Error($"Error setting display scale for current user: {ex.Message}");
                throw;
            }
        }

        private async Task SetDisplayScaleForDefaultUserAsync(int dpiValue)
        {
            try
            {
                string defaultUserHive = @"C:\Users\Default\NTUSER.DAT";
                string tempHiveKey = "TempDefaultUser";
                
                _log.Info($"Loading default user hive from: {defaultUserHive}");

                // Step 1: Load the default user hive
                var loadProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "reg.exe",
                        Arguments = $"load HKLM\\{tempHiveKey} \"{defaultUserHive}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                loadProcess.Start();
                string loadOutput = await loadProcess.StandardOutput.ReadToEndAsync();
                string loadError = await loadProcess.StandardError.ReadToEndAsync();
                await Task.Run(() => loadProcess.WaitForExit());

                if (loadProcess.ExitCode != 0)
                {
                    _log.Error($"Failed to load default user hive. Exit code: {loadProcess.ExitCode}");
                    _log.Error($"Error: {loadError}");
                    throw new Exception($"Failed to load default user hive: {loadError}");
                }

                _log.Info("Default user hive loaded successfully");

                try
                {
                    // Step 2: Modify registry values in the loaded hive
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey($@"{tempHiveKey}\Control Panel\Desktop", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("LogPixels", dpiValue, Microsoft.Win32.RegistryValueKind.DWord);
                            key.SetValue("Win8DpiScaling", 1, Microsoft.Win32.RegistryValueKind.DWord);
                            _log.Info($"Set LogPixels to {dpiValue} in default user hive");
                        }
                        else
                        {
                            _log.Warn("Could not open Control Panel\\Desktop key in default user hive");
                        }
                    }

                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey($@"{tempHiveKey}\Control Panel\Desktop\WindowMetrics", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("AppliedDPI", dpiValue, Microsoft.Win32.RegistryValueKind.DWord);
                            _log.Info($"Set AppliedDPI to {dpiValue} in default user hive");
                        }
                    }

                    _log.Info("Successfully modified default user registry");
                }
                finally
                {
                    // Step 3: Unload the hive (always do this, even if modification failed)
                    _log.Info("Unloading default user hive...");
                    
                    // Small delay to ensure all handles are released
                    await Task.Delay(100);
                    
                    var unloadProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "reg.exe",
                            Arguments = $"unload HKLM\\{tempHiveKey}",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        }
                    };

                    unloadProcess.Start();
                    string unloadOutput = await unloadProcess.StandardOutput.ReadToEndAsync();
                    string unloadError = await unloadProcess.StandardError.ReadToEndAsync();
                    await Task.Run(() => unloadProcess.WaitForExit());

                    if (unloadProcess.ExitCode != 0)
                    {
                        _log.Error($"Failed to unload default user hive. Exit code: {unloadProcess.ExitCode}");
                        _log.Error($"Error: {unloadError}");
                    }
                    else
                    {
                        _log.Info("Default user hive unloaded successfully");
                    }
                }

                _log.Info("Successfully set display scale for default user");
            }
            catch (Exception ex)
            {
                _log.Error($"Error setting display scale for default user: {ex.Message}");
                throw;
            }
        }
    }
}
