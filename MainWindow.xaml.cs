using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Tidy
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer statsTimer;

        public MainWindow()
        {
            InitializeComponent();

            LoadInstalledApps();
            LoadStartupApps();
            StartLiveStats();

            DashboardPage.Visibility = Visibility.Visible;
            AppsPage.Visibility = Visibility.Collapsed;
            CleanupPage.Visibility = Visibility.Collapsed;
            StoragePage.Visibility = Visibility.Collapsed;
            ActivityPage.Visibility = Visibility.Collapsed;
        }

        private void StartLiveStats()
        {
            statsTimer = new DispatcherTimer();
            statsTimer.Interval = TimeSpan.FromSeconds(1);
            statsTimer.Tick += StatsTimer_Tick;
            statsTimer.Start();
        }

        private void StatsTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue();
                System.Threading.Thread.Sleep(200);

                CpuUsageText.Text = $"{Math.Round(cpuCounter.NextValue())}%";

                var memoryInfo = new Microsoft.VisualBasic.Devices.ComputerInfo();

                double totalRam = memoryInfo.TotalPhysicalMemory / 1024d / 1024d / 1024d;
                double availableRam = memoryInfo.AvailablePhysicalMemory / 1024d / 1024d / 1024d;
                double usedRam = totalRam - availableRam;

                RamFigureText.Text = $"{usedRam:F1} GB Used";

                DriveInfo drive = DriveInfo.GetDrives()
                    .FirstOrDefault(d => d.IsReady && d.Name == "C:\\");

                if (drive != null)
                {
                    double used = (drive.TotalSize - drive.AvailableFreeSpace) / 1024d / 1024d / 1024d;
                    DiskFigureText.Text = $"{used:F1} GB Used";
                }
            }
            catch
            {

            }
        }

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
        }

        private void ActivityButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(ActivityPage);
        }

        private void ShowPage(Grid page)
        {
            DashboardPage.Visibility = Visibility.Collapsed;
            AppsPage.Visibility = Visibility.Collapsed;
            CleanupPage.Visibility = Visibility.Collapsed;
            StoragePage.Visibility = Visibility.Collapsed;
            ActivityPage.Visibility = Visibility.Collapsed;

            page.Visibility = Visibility.Visible;
        }

        private void LoadInstalledApps()
        {
            try
            {
                AppsGrid.Items.Clear();

                string registryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

                using RegistryKey key = Registry.LocalMachine.OpenSubKey(registryKey);

                if (key == null)
                    return;

                foreach (string subkeyName in key.GetSubKeyNames())
                {
                    using RegistryKey subkey = key.OpenSubKey(subkeyName);

                    string name = subkey?.GetValue("DisplayName")?.ToString() ?? "";
                    string version = subkey?.GetValue("DisplayVersion")?.ToString() ?? "";
                    string publisher = subkey?.GetValue("Publisher")?.ToString() ?? "";

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        AppsGrid.Items.Add(new AppItem
                        {
                            Name = name,
                            Version = version,
                            Publisher = publisher
                        });
                    }
                }

                InstalledCountText.Text = AppsGrid.Items.Count.ToString();
            }
            catch
            {

            }
        }

        private void LoadStartupApps()
        {
            try
            {
                StartupGrid.Items.Clear();

                using RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run");

                if (key == null)
                    return;

                foreach (string appName in key.GetValueNames())
                {
                    StartupGrid.Items.Add(new StartupItem
                    {
                        Name = appName,
                        Command = key.GetValue(appName)?.ToString() ?? ""
                    });
                }
            }
            catch
            {

            }
        }

        private void CleanTemp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string tempPath = Path.GetTempPath();

                foreach (string file in Directory.GetFiles(tempPath))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {

                    }
                }

                CleanupResultText.Text = "Temporary files cleaned.";
            }
            catch
            {
                CleanupResultText.Text = "Cleanup failed.";
            }
        }

        private void CleanRecycle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "Clear-RecycleBin -Force",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });

                CleanupResultText.Text = "Recycle Bin cleaned.";
            }
            catch
            {
                CleanupResultText.Text = "Recycle Bin cleanup failed.";
            }
        }

        private void BoostMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (Process proc in Process.GetProcesses())
                {
                    try
                    {
                        if (!proc.ProcessName.ToLower().Contains("system"))
                        {
                            proc.PriorityClass = ProcessPriorityClass.BelowNormal;
                        }
                    }
                    catch
                    {

                    }
                }

                CleanupResultText.Text = "Boost mode optimized background activity.";
            }
            catch
            {
                CleanupResultText.Text = "Boost mode failed.";
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.ToLower();

            AppsGrid.Items.Clear();

            string registryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

            using RegistryKey key = Registry.LocalMachine.OpenSubKey(registryKey);

            if (key == null)
                return;

            foreach (string subkeyName in key.GetSubKeyNames())
            {
                using RegistryKey subkey = key.OpenSubKey(subkeyName);

                string name = subkey?.GetValue("DisplayName")?.ToString() ?? "";
                string version = subkey?.GetValue("DisplayVersion")?.ToString() ?? "";
                string publisher = subkey?.GetValue("Publisher")?.ToString() ?? "";

                if (!string.IsNullOrWhiteSpace(name) &&
                    name.ToLower().Contains(query))
                {
                    AppsGrid.Items.Add(new AppItem
                    {
                        Name = name,
                        Version = version,
                        Publisher = publisher
                    });
                }
            }
        }
    }

    public class AppItem
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Publisher { get; set; } = "";
    }

    public class StartupItem
    {
        public string Name { get; set; } = "";
        public string Command { get; set; } = "";
    }
}
