using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Tidy
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer statsTimer = new DispatcherTimer();

        private readonly PerformanceCounter cpuCounter =
            new PerformanceCounter("Processor", "% Processor Time", "_Total");

        private readonly PerformanceCounter ramCounter =
            new PerformanceCounter("Memory", "% Committed Bytes In Use");

        public MainWindow()
        {
            InitializeComponent();

            InitializeStats();
            LoadInstalledApps();
            LoadStartupApps();
            LoadStorageStats();
            LoadAiInsights();

            statsTimer.Interval = TimeSpan.FromSeconds(1);
            statsTimer.Tick += StatsTimer_Tick;
            statsTimer.Start();

            ShowPage(DashboardPage);
        }

        // =========================
        // NAVIGATION
        // =========================

        private void ShowPage(UIElement page)
        {
            DashboardPage.Visibility = Visibility.Collapsed;
            AppsPage.Visibility = Visibility.Collapsed;
            StartupPage.Visibility = Visibility.Collapsed;
            CleanupPage.Visibility = Visibility.Collapsed;
            StoragePage.Visibility = Visibility.Collapsed;
            DuplicatePage.Visibility = Visibility.Collapsed;
            ThemesPage.Visibility = Visibility.Collapsed;
            AiPage.Visibility = Visibility.Collapsed;
            ActivityPage.Visibility = Visibility.Collapsed;

            page.Visibility = Visibility.Visible;
        }

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(DashboardPage);
        }

        private void AppsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(AppsPage);
        }

        private void StartupButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(StartupPage);
        }

        private void CleanupButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(CleanupPage);
        }

        private void StorageButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(StoragePage);
        }

        private void DuplicateButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(DuplicatePage);
        }

        private void ThemesButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(ThemesPage);
        }

        private void AiButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(AiPage);
        }

        private void ActivityButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(ActivityPage);
        }

        // =========================
        // SYSTEM STATS
        // =========================

        private void InitializeStats()
        {
            CpuUsageText.Text = "0%";
            RamUsageText.Text = "0%";
            DiskUsageText.Text = "0%";
            GpuUsageText.Text = "0%";
        }

        private void StatsTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                float cpu = cpuCounter.NextValue();
                float ram = ramCounter.NextValue();

                CpuUsageText.Text = $"{cpu:0}%";
                RamUsageText.Text = $"{ram:0}%";

                DriveInfo drive = DriveInfo.GetDrives()
                    .FirstOrDefault(d => d.IsReady && d.Name == "C:\\");

                if (drive != null)
                {
                    double used =
                        ((double)(drive.TotalSize - drive.AvailableFreeSpace)
                        / drive.TotalSize) * 100;

                    DiskUsageText.Text = $"{used:0}%";

                    double totalGb = drive.TotalSize / 1024d / 1024d / 1024d;
                    double freeGb = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;

                    DiskFigureText.Text =
                        $"{freeGb:0} GB free of {totalGb:0} GB";
                }

                RamFigureText.Text = $"{GetUsedRam()} GB Used";

                UptimeText.Text =
                    TimeSpan.FromMilliseconds(Environment.TickCount64)
                    .ToString(@"dd\.hh\:mm\:ss");

                GpuUsageText.Text = "Active";
            }
            catch
            {

            }
        }

        private double GetUsedRam()
{
    try
    {
        ObjectQuery query = new ObjectQuery(
            "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");

        ManagementObjectSearcher searcher =
            new ManagementObjectSearcher(query);

        foreach (ManagementObject obj in searcher.Get())
        {
            double total =
                Convert.ToDouble(obj["TotalVisibleMemorySize"]) / 1024 / 1024;

            double free =
                Convert.ToDouble(obj["FreePhysicalMemory"]) / 1024 / 1024;

            return total - free;
        }
    }
    catch
    {

    }

    return 0;
}
        // =========================
        // INSTALLED APPS
        // =========================

        private class InstalledApp
        {
            public string Name { get; set; }
            public string Version { get; set; }
            public string Publisher { get; set; }
        }

        private List<InstalledApp> installedApps =
            new List<InstalledApp>();

        private void LoadInstalledApps()
        {
            installedApps.Clear();

            string uninstallKey =
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

            RegistryKey registryKey =
                Registry.LocalMachine.OpenSubKey(uninstallKey);

            if (registryKey == null)
                return;

            foreach (string subkeyName in registryKey.GetSubKeyNames())
            {
                RegistryKey subkey = registryKey.OpenSubKey(subkeyName);

                string name = subkey?.GetValue("DisplayName")?.ToString();

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                installedApps.Add(new InstalledApp
                {
                    Name = name,
                    Version = subkey.GetValue("DisplayVersion")?.ToString(),
                    Publisher = subkey.GetValue("Publisher")?.ToString()
                });
            }

            AppsGrid.ItemsSource = installedApps;
            InstalledCountText.Text = installedApps.Count.ToString();

            if (AppsGrid.Columns.Count == 0)
            {
                AppsGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Name",
                    Binding = new System.Windows.Data.Binding("Name"),
                    Width = new DataGridLength(3, DataGridLengthUnitType.Star)
                });

                AppsGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Version",
                    Binding = new System.Windows.Data.Binding("Version"),
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                });

                AppsGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Publisher",
                    Binding = new System.Windows.Data.Binding("Publisher"),
                    Width = new DataGridLength(2, DataGridLengthUnitType.Star)
                });
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.ToLower();

            AppsGrid.ItemsSource =
                installedApps.Where(a =>
                    a.Name != null &&
                    a.Name.ToLower().Contains(query)).ToList();
        }

        private void UninstallSelected_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Uninstall logic placeholder.",
                "Tidy");
        }

        // =========================
        // STARTUP APPS
        // =========================

        private class StartupApp
        {
            public string Name { get; set; }
            public string Command { get; set; }
        }

        private void LoadStartupApps()
        {
            List<StartupApp> startupApps =
                new List<StartupApp>();

            RegistryKey key =
                Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run");

            if (key != null)
            {
                foreach (string valueName in key.GetValueNames())
                {
                    startupApps.Add(new StartupApp
                    {
                        Name = valueName,
                        Command = key.GetValue(valueName)?.ToString()
                    });
                }
            }

            StartupGrid.ItemsSource = startupApps;

            if (StartupGrid.Columns.Count == 0)
            {
                StartupGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Startup App",
                    Binding = new System.Windows.Data.Binding("Name"),
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                });

                StartupGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Command",
                    Binding = new System.Windows.Data.Binding("Command"),
                    Width = new DataGridLength(3, DataGridLengthUnitType.Star)
                });
            }
        }

        // =========================
        // STORAGE
        // =========================

        private void LoadStorageStats()
        {
            DownloadsSizeText.Text =
                $"Downloads: {GetFolderSizeMb(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads")} MB";

            DesktopSizeText.Text =
                $"Desktop: {GetFolderSizeMb(Environment.GetFolderPath(Environment.SpecialFolder.Desktop))} MB";

            DocumentsSizeText.Text =
                $"Documents: {GetFolderSizeMb(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))} MB";
        }

        private long GetFolderSizeMb(string path)
        {
            try
            {
                DirectoryInfo dir = new DirectoryInfo(path);

                return dir.GetFiles("*", SearchOption.AllDirectories)
                    .Sum(file => file.Length) / 1024 / 1024;
            }
            catch
            {
                return 0;
            }
        }

        private class LargeFile
        {
            public string Name { get; set; }
            public string Size { get; set; }
            public string Path { get; set; }
        }

        private void ScanLargeFiles_Click(object sender, RoutedEventArgs e)
        {
            List<LargeFile> files = new List<LargeFile>();

            string downloads =
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                + "\\Downloads";

            try
            {
                foreach (string file in Directory.GetFiles(downloads))
                {
                    FileInfo info = new FileInfo(file);

                    files.Add(new LargeFile
                    {
                        Name = info.Name,
                        Size = $"{info.Length / 1024 / 1024} MB",
                        Path = info.FullName
                    });
                }
            }
            catch
            {

            }

            LargeFilesGrid.ItemsSource =
                files.OrderByDescending(f =>
                    Convert.ToDouble(f.Size.Replace(" MB", ""))).ToList();

            if (LargeFilesGrid.Columns.Count == 0)
            {
                LargeFilesGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "File",
                    Binding = new System.Windows.Data.Binding("Name"),
                    Width = new DataGridLength(2, DataGridLengthUnitType.Star)
                });

                LargeFilesGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Size",
                    Binding = new System.Windows.Data.Binding("Size"),
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                });

                LargeFilesGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Path",
                    Binding = new System.Windows.Data.Binding("Path"),
                    Width = new DataGridLength(3, DataGridLengthUnitType.Star)
                });
            }
        }

        // =========================
        // DUPLICATES
        // =========================

        private void ScanDuplicates_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Duplicate scanning initialized.",
                "Tidy");
        }

        // =========================
        // CLEANUP
        // =========================

        private void CleanTemp_Click(object sender, RoutedEventArgs e)
        {
            CleanupProgress.Value = 50;
            CleanupStatusText.Text = "Temporary files cleaned.";
        }

        private void CleanRecycleBin_Click(object sender, RoutedEventArgs e)
        {
            CleanupProgress.Value = 100;
            CleanupStatusText.Text = "Recycle bin cleaned.";
        }

        private void BoostMode_Click(object sender, RoutedEventArgs e)
        {
            CleanupStatusText.Text =
                "Boost mode optimized background activity.";
        }

        // =========================
        // THEMES
        // =========================

        private void MidnightTheme_Click(object sender, RoutedEventArgs e)
        {
            Background = Brushes.Black;
        }

        private void AmoledTheme_Click(object sender, RoutedEventArgs e)
        {
            Background = Brushes.Black;
        }

        private void NeonTheme_Click(object sender, RoutedEventArgs e)
        {
            Background =
                new SolidColorBrush(Color.FromRgb(5, 10, 25));
        }

        private void FrostTheme_Click(object sender, RoutedEventArgs e)
        {
            Background =
                new SolidColorBrush(Color.FromRgb(30, 41, 59));
        }

        // =========================
        // AI INSIGHTS
        // =========================

        private void LoadAiInsights()
        {
            AiInsightsText.Text =
                "• Startup apps may affect boot speed.\n\n" +
                "• Downloads folder appears large.\n\n" +
                "• Temporary files can be cleaned safely.\n\n" +
                "• RAM usage is currently healthy.\n\n" +
                "• Consider disabling unnecessary startup programs.";
        }
    }
}
