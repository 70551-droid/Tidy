using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Tidy
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer statsTimer;
        private readonly PerformanceCounter cpuCounter;
        private bool isDisposed = false;
        private DateTime lastUpdateTime;

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue(); // Initial read
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing CPU counter: {ex.Message}");
            }

            statsTimer = new DispatcherTimer();
            statsTimer.Interval = TimeSpan.FromSeconds(2);
            statsTimer.Tick += StatsTimer_Tick;
            statsTimer.Start();

            lastUpdateTime = DateTime.Now;

            LoadInstalledApps();
            LoadStartupApps();
            LoadStorageInfo();

            DashboardPage.Visibility = Visibility.Visible;

            AppsPage.Visibility = Visibility.Collapsed;
            CleanupPage.Visibility = Visibility.Collapsed;
            StoragePage.Visibility = Visibility.Collapsed;
            ActivityPage.Visibility = Visibility.Collapsed;

            this.Closed += (s, e) => Cleanup();
        }

        private void Cleanup()
        {
            if (isDisposed) return;
            isDisposed = true;

            try
            {
                statsTimer?.Stop();
                cpuCounter?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }

        // =========================
        // LIVE STATS
        // =========================

        private void StatsTimer_Tick(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                try
                {
                    // Get CPU usage
                    double cpuUsage = Math.Round(cpuCounter.NextValue());

                    // Get RAM usage
                    double ramTotal = GC.GetTotalMemory(false) / 1024d / 1024d / 1024d;
                    var ramUsage = Process.GetProcesses()
                        .AsParallel()
                        .Sum(p =>
                        {
                            try
                            {
                                return p.WorkingSet64;
                            }
                            catch
                            {
                                return 0;
                            }
                        }) / 1024d / 1024d / 1024d;

                    // Get disk usage
                    DriveInfo drive = DriveInfo
                        .GetDrives()
                        .FirstOrDefault(d =>
                            d.IsReady &&
                            d.Name == "C:\\");

                    double diskPercentage = 0;
                    if (drive != null)
                    {
                        diskPercentage = (drive.TotalSize - drive.AvailableFreeSpace) / (double)drive.TotalSize * 100;
                    }

                    // Update UI on dispatcher thread
                    Dispatcher.Invoke(() =>
                    {
                        CpuUsageText.Text = $"{cpuUsage}%";
                        CpuProgressBar.Value = cpuUsage;
                        
                        RamFigureText.Text = $"{ramUsage:F1} GB Used";
                        
                        // Estimate RAM percentage (assuming 16GB typical)
                        double ramPercentage = Math.Min(ramUsage / 16.0 * 100, 100);
                        RamProgressBar.Value = ramPercentage;

                        if (drive != null)
                        {
                            double used = (drive.TotalSize - drive.AvailableFreeSpace) / 1024d / 1024d / 1024d;
                            double total = drive.TotalSize / 1024d / 1024d / 1024d;
                            DiskUsageText.Text = $"{used:F1} GB / {total:F1} GB";
                            DiskProgressBar.Value = diskPercentage;
                        }

                        // Update last updated time
                        lastUpdateTime = DateTime.Now;
                        LastUpdatedText.Text = $"Last updated: {lastUpdateTime:HH:mm:ss}";
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating stats: {ex.Message}");
                }
            });
        }

        // =========================
        // NAVIGATION
        // =========================

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(DashboardPage);
        }

        private void AppsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(AppsPage);
        }

        private void CleanupButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(CleanupPage);
        }

        private void StorageButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(StoragePage);
            LoadStorageInfo();
        }

        private void ActivityButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(ActivityPage);
        }

        private void StartupButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(ActivityPage);
        }

        private void DuplicateButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Duplicate Finder feature coming soon!",
                "Feature in Development",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ThemesButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Themes feature coming soon!",
                "Feature in Development",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void AiButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "AI Tools feature coming soon!",
                "Feature in Development",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ShowPage(UIElement page)
        {
            DashboardPage.Visibility = Visibility.Collapsed;
            AppsPage.Visibility = Visibility.Collapsed;
            CleanupPage.Visibility = Visibility.Collapsed;
            StoragePage.Visibility = Visibility.Collapsed;
            ActivityPage.Visibility = Visibility.Collapsed;

            page.Visibility = Visibility.Visible;
        }

        // =========================
        // INSTALLED APPS
        // =========================

        private List<(string name, string publisher, string version)> GetInstalledApps(string searchFilter = "")
        {
            var apps = new List<(string, string, string)>();
            string uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

            try
            {
                using RegistryKey key = Registry.LocalMachine.OpenSubKey(uninstallKey);

                if (key == null)
                {
                    Debug.WriteLine("Warning: Could not open registry key for installed apps");
                    return apps;
                }

                foreach (string subkeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using RegistryKey subkey = key.OpenSubKey(subkeyName);

                        string name = subkey?.GetValue("DisplayName")?.ToString() ?? "";
                        string publisher = subkey?.GetValue("Publisher")?.ToString() ?? "";
                        string version = subkey?.GetValue("DisplayVersion")?.ToString() ?? "";

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            if (string.IsNullOrEmpty(searchFilter) || 
                                name.ToLower().Contains(searchFilter.ToLower()))
                            {
                                apps.Add((name, publisher, version));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error reading subkey {subkeyName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error accessing registry for installed apps: {ex.Message}");
            }

            return apps;
        }

        private void LoadInstalledApps()
        {
            try
            {
                AppsGrid.Items.Clear();

                var apps = GetInstalledApps();

                foreach (var app in apps)
                {
                    AppsGrid.Items.Add(new
                    {
                        Name = app.name,
                        Publisher = app.publisher,
                        Version = app.version
                    });
                }

                InstalledCountText.Text = $"{AppsGrid.Items.Count} Apps Installed";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading installed apps: {ex.Message}");
                InstalledCountText.Text = "Error loading apps";
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string search = SearchBox.Text;

                AppsGrid.Items.Clear();

                var apps = GetInstalledApps(search);

                foreach (var app in apps)
                {
                    AppsGrid.Items.Add(new
                    {
                        Name = app.name,
                        Publisher = app.publisher,
                        Version = app.version
                    });
                }

                InstalledCountText.Text = $"{AppsGrid.Items.Count} Apps Found";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during app search: {ex.Message}");
            }
        }

        // =========================
        // STARTUP APPS
        // =========================

        private void LoadStartupApps()
        {
            try
            {
                StartupGrid.Items.Clear();

                using RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run");

                if (key == null)
                {
                    Debug.WriteLine("Warning: Could not open registry key for startup apps");
                    return;
                }

                int count = 0;
                foreach (string appName in key.GetValueNames())
                {
                    try
                    {
                        string command = key.GetValue(appName)?.ToString() ?? "";

                        StartupGrid.Items.Add(new
                        {
                            Name = appName,
                            Command = command
                        });
                        count++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error reading startup app {appName}: {ex.Message}");
                    }
                }

                StartupCountText.Text = $"{count} startup item(s) loaded";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading startup apps: {ex.Message}");
            }
        }

        // =========================
        // STORAGE INFO
        // =========================

        private void LoadStorageInfo()
        {
            Task.Run(() =>
            {
                try
                {
                    // Get Downloads folder size
                    string downloadsPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "Downloads");

                    if (Directory.Exists(downloadsPath))
                    {
                        long downloadsSize = GetDirectorySize(downloadsPath);
                        double downloadsSizeGB = downloadsSize / 1024d / 1024d / 1024d;
                        int downloadFileCount = Directory.GetFiles(downloadsPath, "*", SearchOption.TopDirectoryOnly).Length;

                        Dispatcher.Invoke(() =>
                        {
                            DownloadsSizeText.Text = $"{downloadsSizeGB:F2} GB";
                            DownloadsDetailText.Text = $"{downloadFileCount} files in Downloads";
                            DownloadsProgressBar.Value = Math.Min(downloadsSizeGB / 100 * 100, 100);
                        });
                    }

                    // Get C: drive info
                    DriveInfo drive = DriveInfo
                        .GetDrives()
                        .FirstOrDefault(d => d.IsReady && d.Name == "C:\\");

                    if (drive != null)
                    {
                        double totalGB = drive.TotalSize / 1024d / 1024d / 1024d;
                        double usedGB = (drive.TotalSize - drive.AvailableFreeSpace) / 1024d / 1024d / 1024d;
                        double usedPercent = (drive.TotalSize - drive.AvailableFreeSpace) / (double)drive.TotalSize * 100;

                        Dispatcher.Invoke(() =>
                        {
                            CDriveText.Text = $"{usedGB:F1} GB / {totalGB:F1} GB";
                            CDriveDetailText.Text = $"{usedPercent:F1}% used - {drive.AvailableFreeSpace / 1024d / 1024d / 1024d:F1} GB free";
                            CDriveProgressBar.Value = usedPercent;
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading storage info: {ex.Message}");
                }
            });
        }

        private long GetDirectorySize(string path)
        {
            try
            {
                var dirInfo = new DirectoryInfo(path);
                return dirInfo.EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                    .AsParallel()
                    .Sum(file =>
                    {
                        try { return file.Length; }
                        catch { return 0; }
                    });
            }
            catch
            {
                return 0;
            }
        }

        // =========================
        // CLEANUP ENGINE
        // =========================

        private void CleanTemp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "This will delete temporary files. Continue?",
                    "Confirm Temp File Cleanup",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    CleanupResultText.Text = "Cleanup cancelled.";
                    return;
                }

                CleanupResultText.Text = "Cleaning temporary files...";
                CleanupProgressBar.Visibility = Visibility.Visible;

                Task.Run(() =>
                {
                    try
                    {
                        string tempPath = Path.GetTempPath();
                        var files = Directory.GetFiles(tempPath);
                        int deletedCount = 0;
                        int totalFiles = files.Length;

                        for (int i = 0; i < files.Length; i++)
                        {
                            try
                            {
                                File.Delete(files[i]);
                                deletedCount++;

                                int progress = (int)((i + 1) / (double)totalFiles * 100);
                                Dispatcher.Invoke(() =>
                                {
                                    CleanupProgressBar.Value = progress;
                                });
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Could not delete temp file {files[i]}: {ex.Message}");
                            }
                        }

                        Dispatcher.Invoke(() =>
                        {
                            CleanupResultText.Text =
                                $"✓ Temporary files cleaned successfully. ({deletedCount} files deleted)";
                            CleanupProgressBar.Visibility = Visibility.Collapsed;
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error during temp cleanup: {ex.Message}");
                        Dispatcher.Invoke(() =>
                        {
                            CleanupResultText.Text = $"✗ Cleanup failed: {ex.Message}";
                            CleanupProgressBar.Visibility = Visibility.Collapsed;
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CleanTemp_Click: {ex.Message}");
                CleanupResultText.Text = "Cleanup failed.";
            }
        }

        private void CleanRecycle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "This will permanently empty the Recycle Bin. This action cannot be undone. Continue?",
                    "Confirm Recycle Bin Cleanup",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    CleanupResultText.Text = "Cleanup cancelled.";
                    return;
                }

                CleanupResultText.Text = "Emptying Recycle Bin...";
                CleanupProgressBar.Visibility = Visibility.Visible;
                CleanupProgressBar.IsIndeterminate = true;

                Task.Run(() =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = "Clear-RecycleBin -Force -Confirm:$false",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });

                        Dispatcher.Invoke(() =>
                        {
                            CleanupResultText.Text = "✓ Recycle Bin cleaned successfully.";
                            CleanupProgressBar.Visibility = Visibility.Collapsed;
                            CleanupProgressBar.IsIndeterminate = false;
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error during recycle bin cleanup: {ex.Message}");
                        Dispatcher.Invoke(() =>
                        {
                            CleanupResultText.Text = $"✗ Recycle Bin cleanup failed: {ex.Message}";
                            CleanupProgressBar.Visibility = Visibility.Collapsed;
                            CleanupProgressBar.IsIndeterminate = false;
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CleanRecycle_Click: {ex.Message}");
                CleanupResultText.Text = "Cleanup failed.";
            }
        }

        private void BoostMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "This will reduce the priority of background processes to free up system resources. Continue?",
                    "Confirm Boost Mode",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    CleanupResultText.Text = "Boost mode cancelled.";
                    return;
                }

                CleanupResultText.Text = "Activating Boost Mode...";
                CleanupProgressBar.Visibility = Visibility.Visible;

                Task.Run(() =>
                {
                    try
                    {
                        var processes = Process.GetProcesses();
                        int optimizedCount = 0;

                        for (int i = 0; i < processes.Length; i++)
                        {
                            try
                            {
                                var process = processes[i];
                                if (!process.ProcessName.ToLower().Contains("system") &&
                                    !process.ProcessName.ToLower().Contains("svchost") &&
                                    !process.ProcessName.ToLower().Contains("csrss") &&
                                    !process.ProcessName.ToLower().Contains("lsass") &&
                                    process.ProcessName.ToLower() != "explorer" &&
                                    process.ProcessName.ToLower() != "dwm")
                                {
                                    process.PriorityClass = ProcessPriorityClass.BelowNormal;
                                    optimizedCount++;
                                }

                                int progress = (int)((i + 1) / (double)processes.Length * 100);
                                Dispatcher.Invoke(() =>
                                {
                                    CleanupProgressBar.Value = progress;
                                });
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Could not set priority: {ex.Message}");
                            }
                        }

                        Dispatcher.Invoke(() =>
                        {
                            CleanupResultText.Text =
                                $"✓ Boost mode optimized {optimizedCount} background processes.";
                            CleanupProgressBar.Visibility = Visibility.Collapsed;
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error during boost mode: {ex.Message}");
                        Dispatcher.Invoke(() =>
                        {
                            CleanupResultText.Text = $"✗ Boost mode failed: {ex.Message}";
                            CleanupProgressBar.Visibility = Visibility.Collapsed;
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in BoostMode_Click: {ex.Message}");
                CleanupResultText.Text = "Boost mode failed.";
            }
        }
    }
}
