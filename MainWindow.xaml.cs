using Microsoft.Win32;
using System;
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

        public MainWindow()
        {
            InitializeComponent();

            statsTimer = new DispatcherTimer();
            statsTimer.Interval = TimeSpan.FromSeconds(1);
            statsTimer.Tick += StatsTimer_Tick;
            statsTimer.Start();

            LoadInstalledApps();
            LoadStartupApps();

            DashboardPage.Visibility = Visibility.Visible;

            AppsPage.Visibility = Visibility.Collapsed;
            CleanupPage.Visibility = Visibility.Collapsed;
            StoragePage.Visibility = Visibility.Collapsed;
            ActivityPage.Visibility = Visibility.Collapsed;
        }

        // =========================
        // LIVE STATS
        // =========================

        private void StatsTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                var cpuCounter =
                    new PerformanceCounter(
                        "Processor",
                        "% Processor Time",
                        "_Total");

                cpuCounter.NextValue();

                System.Threading.Thread.Sleep(100);

                CpuUsageText.Text =
                    $"{Math.Round(cpuCounter.NextValue())}%";

                double ramUsage =
                    Process.GetProcesses()
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

                RamFigureText.Text =
                    $"{ramUsage:F1} GB Used";

                DriveInfo drive = DriveInfo
                    .GetDrives()
                    .FirstOrDefault(d =>
                        d.IsReady &&
                        d.Name == "C:\\");

                if (drive != null)
                {
                    double used =
                        (drive.TotalSize -
                        drive.AvailableFreeSpace)
                        / 1024d / 1024d / 1024d;

                    DiskUsageText.Text =
                        $"{used:F1} GB Used";
                }
            }
            catch
            {

            }
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
        }

        private void ActivityButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(ActivityPage);
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

        private void LoadInstalledApps()
        {
            try
            {
                AppsGrid.Items.Clear();

                string uninstallKey =
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

                using RegistryKey key =
                    Registry.LocalMachine.OpenSubKey(uninstallKey);

                if (key == null)
                    return;

                foreach (string subkeyName in key.GetSubKeyNames())
                {
                    using RegistryKey subkey =
                        key.OpenSubKey(subkeyName);

                    string name =
                        subkey?.GetValue("DisplayName")?.ToString() ?? "";

                    string publisher =
                        subkey?.GetValue("Publisher")?.ToString() ?? "";

                    string version =
                        subkey?.GetValue("DisplayVersion")?.ToString() ?? "";

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        AppsGrid.Items.Add(new
                        {
                            Name = name,
                            Publisher = publisher,
                            Version = version
                        });
                    }
                }

                InstalledCountText.Text =
                    $"{AppsGrid.Items.Count} Apps Installed";
            }
            catch
            {

            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string search =
                    SearchBox.Text.ToLower();

                AppsGrid.Items.Clear();

                string uninstallKey =
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

                using RegistryKey key =
                    Registry.LocalMachine.OpenSubKey(uninstallKey);

                if (key == null)
                    return;

                foreach (string subkeyName in key.GetSubKeyNames())
                {
                    using RegistryKey subkey =
                        key.OpenSubKey(subkeyName);

                    string name =
                        subkey?.GetValue("DisplayName")?.ToString() ?? "";

                    string publisher =
                        subkey?.GetValue("Publisher")?.ToString() ?? "";

                    string version =
                        subkey?.GetValue("DisplayVersion")?.ToString() ?? "";

                    if (!string.IsNullOrWhiteSpace(name) &&
                        name.ToLower().Contains(search))
                    {
                        AppsGrid.Items.Add(new
                        {
                            Name = name,
                            Publisher = publisher,
                            Version = version
                        });
                    }
                }
            }
            catch
            {

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

                using RegistryKey key =
                    Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Run");

                if (key == null)
                    return;

                foreach (string appName in key.GetValueNames())
                {
                    string command =
                        key.GetValue(appName)?.ToString() ?? "";

                    StartupGrid.Items.Add(new
                    {
                        Name = appName,
                        Command = command
                    });
                }
            }
            catch
            {

            }
        }

        // =========================
        // CLEANUP ENGINE
        // =========================

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

                CleanupResultText.Text =
                    "Temporary files cleaned successfully.";
            }
            catch
            {
                CleanupResultText.Text =
                    "Cleanup failed.";
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

                CleanupResultText.Text =
                    "Recycle Bin cleaned.";
            }
            catch
            {
                CleanupResultText.Text =
                    "Recycle Bin cleanup failed.";
            }
        }

        private void BoostMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (Process process in Process.GetProcesses())
                {
                    try
                    {
                        if (!process.ProcessName
                            .ToLower()
                            .Contains("system"))
                        {
                            process.PriorityClass =
                                ProcessPriorityClass.BelowNormal;
                        }
                    }
                    catch
                    {

                    }
                }

                CleanupResultText.Text =
                    "Boost mode optimized background apps.";
            }
            catch
            {
                CleanupResultText.Text =
                    "Boost mode failed.";
            }
        }
    }
}
