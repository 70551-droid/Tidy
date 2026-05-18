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
            LoadStorageInfo();

            AddActivity("Tidy started successfully.");
        }

        // =========================
        // LIVE STATS
        // =========================

        private void StatsTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                CpuText.Text =
    $"{GetCpuUsage():F0}%";

                RamText.Text =
                    $"{GetRamUsage():F1} GB";

                DiskText.Text =
                    $"{GetDiskUsage():F1} GB";
            }
            catch
            {

            }
        }

        private float GetCpuUsage()
        {
            try
            {
                using PerformanceCounter cpuCounter =
                    new PerformanceCounter(
                        "Processor",
                        "% Processor Time",
                        "_Total");

                cpuCounter.NextValue();

                System.Threading.Thread.Sleep(100);

                return cpuCounter.NextValue();
            }
            catch
            {
                return 0;
            }
        }

        private double GetRamUsage()
        {
            try
            {
                return Process.GetProcesses()
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
            }
            catch
            {
                return 0;
            }
        }

        private double GetDiskUsage()
        {
            try
            {
                DriveInfo drive =
                    DriveInfo.GetDrives()
                    .FirstOrDefault(d =>
                        d.IsReady &&
                        d.Name == "C:\\");

                if (drive == null)
                    return 0;

                return
                    (drive.TotalSize -
                    drive.AvailableFreeSpace)
                    / 1024d / 1024d / 1024d;
            }
            catch
            {
                return 0;
            }
        }

        // =========================
        // NAVIGATION
        // =========================

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(DashboardPage);

            AddActivity("Opened Dashboard.");
        }

        private void AppsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(AppsPage);

            AddActivity("Opened Installed Apps.");
        }

        private void CleanupButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(CleanupPage);

            AddActivity("Opened Cleanup Engine.");
        }

        private void StorageButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(StoragePage);

            AddActivity("Opened Storage Analyzer.");
        }

        private void ActivityButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(ActivityPage);

            AddActivity("Opened Activity Center.");
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

        string[] registryPaths =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (string path in registryPaths)
        {
            using RegistryKey key =
                Registry.LocalMachine.OpenSubKey(path);

            if (key == null)
                continue;

            foreach (string subkeyName in key.GetSubKeyNames())
            {
                using RegistryKey subkey =
                    key.OpenSubKey(subkeyName);

                string name =
                    subkey?.GetValue("DisplayName")?.ToString() ?? "";

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                string publisher =
                    subkey?.GetValue("Publisher")?.ToString() ?? "Unknown";

                string uninstall =
                    subkey?.GetValue("UninstallString")?.ToString() ?? "";

                AppsGrid.Items.Add(new
{
    Name = name,
    Publisher = publisher,
    Uninstall = uninstall
});
            }
        }

        AddActivity("Installed applications loaded.");
    }
    catch (Exception ex)
    {
        MessageBox.Show(ex.Message);
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

                    if (!string.IsNullOrWhiteSpace(name) &&
                        name.ToLower().Contains(search))
                    {
                        AppsGrid.Items.Add(new
                        {
                            Name = name,
                            Publisher = publisher
                        });
                    }
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
                string tempPath =
                    Path.GetTempPath();

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

                CleanupStatusText.Text =
                    "Temporary files cleaned successfully.";

                AddActivity("Temporary files cleaned.");
            }
            catch
            {
                CleanupStatusText.Text =
                    "Cleanup failed.";

                AddActivity("Temporary file cleanup failed.");
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

                CleanupStatusText.Text =
                    "Recycle Bin cleaned.";

                AddActivity("Recycle Bin cleaned.");
            }
            catch
            {
                CleanupStatusText.Text =
                    "Recycle Bin cleanup failed.";

                AddActivity("Recycle Bin cleanup failed.");
            }
        }

        // =========================
        // STORAGE
        // =========================

        private void LoadStorageInfo()
        {
            try
            {
                DriveInfo drive =
                    DriveInfo.GetDrives()
                    .FirstOrDefault(d =>
                        d.IsReady &&
                        d.Name == "C:\\");

                if (drive == null)
                    return;

                double used =
                    (drive.TotalSize -
                    drive.AvailableFreeSpace)
                    / 1024d / 1024d / 1024d;

                StorageText.Text =
                    $"{used:F1} GB Used";
            }
            catch
            {
                StorageText.Text =
                    "Storage scan failed.";
            }
        }

        // =========================
        // ACTIVITY SYSTEM
        // =========================
        
        private void UninstallButton_Click(object sender, RoutedEventArgs e)
{
    try
    {
        if (sender is not Button button)
            return;

        if (button.DataContext is not AppItem app)
            return;

        if (string.IsNullOrWhiteSpace(app.UninstallString))
        {
            MessageBox.Show(
                "No uninstall command found.");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {app.UninstallString}",
            UseShellExecute = true
        });

        AddActivity($"Started uninstall for {app.Name}");
    }
    catch
    {
        MessageBox.Show(
            "Failed to uninstall application.");
    }
}

        private void AddActivity(string message)
        {
            ActivityList.Items.Insert(
                0,
                $"{DateTime.Now:T}  •  {message}");
        }
    }
}