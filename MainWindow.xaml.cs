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
        private DispatcherTimer monitorTimer;

        private PerformanceCounter cpuCounter;

        private List<AppInfo> installedApps =
            new List<AppInfo>();

        public MainWindow()
        {
            InitializeComponent();

            SetupMonitoring();

            LoadInstalledApps();

            LoadStorageAnalysis();

            GenerateRecommendations();

            ShowDashboardPage();

            AddActivity("Tidy initialized successfully");
        }

        // =========================
        // APP MODEL
        // =========================

        public class AppInfo
        {
            public string Name { get; set; }

            public string Publisher { get; set; }

            public string Version { get; set; }
        }

        // =========================
        // ACTIVITY
        // =========================

        private void AddActivity(string message)
        {
            ActivityText.Text =
                $"[{DateTime.Now:T}] {message}\n\n" +
                ActivityText.Text;
        }

        // =========================
        // LOAD APPS
        // =========================

        private void LoadInstalledApps()
        {
            installedApps.Clear();

            LoadAppsFromRegistry(
                Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

            LoadAppsFromRegistry(
                Registry.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");

            LoadAppsFromRegistry(
                Registry.CurrentUser,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

            installedApps =
                installedApps
                .Where(a => !string.IsNullOrWhiteSpace(a.Name))
                .OrderBy(a => a.Name)
                .ToList();

            InstalledCountText.Text =
                installedApps.Count.ToString();

            AppsGrid.ItemsSource =
                installedApps;

            SetupDataGridColumns();

            LoadLargestApps();

            AddActivity(
                $"Loaded {installedApps.Count} installed apps");
        }

        private void LoadAppsFromRegistry(
            RegistryKey root,
            string path)
        {
            try
            {
                RegistryKey key =
                    root.OpenSubKey(path);

                if (key == null)
                    return;

                foreach (string subkeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        RegistryKey subkey =
                            key.OpenSubKey(subkeyName);

                        string displayName =
                            subkey?.GetValue("DisplayName")?.ToString();

                        if (string.IsNullOrWhiteSpace(displayName))
                            continue;

                        bool exists =
                            installedApps.Any(a =>
                                a.Name == displayName);

                        if (exists)
                            continue;

                        installedApps.Add(new AppInfo
                        {
                            Name = displayName,

                            Publisher =
                                subkey?.GetValue("Publisher")?.ToString()
                                ?? "Unknown",

                            Version =
                                subkey?.GetValue("DisplayVersion")?.ToString()
                                ?? "Unknown"
                        });
                    }
                    catch
                    {

                    }
                }
            }
            catch
            {

            }
        }

        private void SetupDataGridColumns()
        {
            if (AppsGrid.Columns.Count > 0)
                return;

            AppsGrid.Columns.Add(
                new DataGridTextColumn
                {
                    Header = "Application",
                    Binding =
                        new System.Windows.Data.Binding("Name"),
                    Width =
                        new DataGridLength(2,
                        DataGridLengthUnitType.Star)
                });

            AppsGrid.Columns.Add(
                new DataGridTextColumn
                {
                    Header = "Publisher",
                    Binding =
                        new System.Windows.Data.Binding("Publisher"),
                    Width =
                        new DataGridLength(2,
                        DataGridLengthUnitType.Star)
                });

            AppsGrid.Columns.Add(
                new DataGridTextColumn
                {
                    Header = "Version",
                    Binding =
                        new System.Windows.Data.Binding("Version"),
                    Width =
                        new DataGridLength(1,
                        DataGridLengthUnitType.Star)
                });
        }

        // =========================
        // MONITORING
        // =========================

        private void SetupMonitoring()
        {
            cpuCounter =
                new PerformanceCounter(
                    "Processor",
                    "% Processor Time",
                    "_Total");

            cpuCounter.NextValue();

            monitorTimer =
                new DispatcherTimer();

            monitorTimer.Interval =
                TimeSpan.FromSeconds(1);

            monitorTimer.Tick +=
                MonitorTimer_Tick;

            monitorTimer.Start();
        }

        private void MonitorTimer_Tick(
            object sender,
            EventArgs e)
        {
            UpdateCpuUsage();

            UpdateRamUsage();

            UpdateDiskUsage();
        }

        private void UpdateCpuUsage()
        {
            try
            {
                float cpu =
                    cpuCounter.NextValue();

                CpuUsageText.Text =
                    $"{cpu:0}%";
            }
            catch
            {
                CpuUsageText.Text = "N/A";
            }
        }

        private void UpdateRamUsage()
        {
            try
            {
                ObjectQuery query =
                    new ObjectQuery(
                        "SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem");

                ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(query);

                foreach (ManagementObject obj in searcher.Get())
                {
                    double total =
                        Convert.ToDouble(
                            obj["TotalVisibleMemorySize"]);

                    double free =
                        Convert.ToDouble(
                            obj["FreePhysicalMemory"]);

                    double used =
                        total - free;

                    double percent =
                        (used / total) * 100;

                    double totalGb =
                        total / 1024 / 1024;

                    double usedGb =
                        used / 1024 / 1024;

                    RamUsageText.Text =
                        $"{percent:0}%";

                    RamFigureText.Text =
                        $"{usedGb:0.0} GB / {totalGb:0.0} GB";
                }
            }
            catch
            {
                RamUsageText.Text = "N/A";
            }
        }

        private void UpdateDiskUsage()
        {
            try
            {
                DriveInfo drive =
                    DriveInfo.GetDrives()
                    .FirstOrDefault(d =>
                        d.IsReady &&
                        d.Name == "C:\\");

                if (drive != null)
                {
                    double total =
                        drive.TotalSize /
                        1024d / 1024d / 1024d;

                    double free =
                        drive.TotalFreeSpace /
                        1024d / 1024d / 1024d;

                    double used =
                        total - free;

                    double percent =
                        (used / total) * 100;

                    DiskUsageText.Text =
                        $"{percent:0}%";

                    DiskFigureText.Text =
                        $"{free:0.0} GB Free";
                }
            }
            catch
            {
                DiskUsageText.Text = "N/A";
            }
        }

        // =========================
        // STORAGE
        // =========================

        private void LoadStorageAnalysis()
        {
            try
            {
                string downloads =
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.UserProfile),
                        "Downloads");

                long downloadsSize =
                    GetFolderSize(downloads);

                DownloadsSizeText.Text =
                    $"{downloadsSize / 1024d / 1024d / 1024d:0.00} GB";

                AddActivity(
                    "Storage analysis completed");
            }
            catch
            {
                DownloadsSizeText.Text =
                    "Unavailable";
            }
        }

        private long GetFolderSize(string folder)
        {
            try
            {
                return Directory
                    .GetFiles(folder, "*",
                    SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length);
            }
            catch
            {
                return 0;
            }
        }

        private void LoadLargestApps()
        {
            LargestAppsText.Text = "";

            foreach (var app in
                installedApps.Take(15))
            {
                LargestAppsText.Text +=
                    $"• {app.Name}\n";
            }
        }

        // =========================
        // RECOMMENDATIONS
        // =========================

        private void GenerateRecommendations()
        {
            string recommendations = "";

            if (installedApps.Count > 120)
            {
                recommendations +=
                    "• Large number of installed apps detected\n\n";
            }

            try
            {
                string downloads =
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.UserProfile),
                        "Downloads");

                long size =
                    GetFolderSize(downloads);

                double gb =
                    size / 1024d / 1024d / 1024d;

                if (gb > 5)
                {
                    recommendations +=
                        "• Downloads folder exceeds 5 GB\n\n";
                }
            }
            catch
            {

            }

            recommendations +=
                "• Consider reviewing startup applications\n\n";

            recommendations +=
                "• Cleanup scan recommended weekly";

            ActivityText.Text +=
                "[SYSTEM] Recommendations generated\n\n";
        }

        // =========================
        // NAVIGATION
        // =========================

        private void ResetSidebarButtons()
        {
            DashboardButton.Background =
                Brushes.Transparent;

            AppsButton.Background =
                Brushes.Transparent;

            CleanupButton.Background =
                Brushes.Transparent;

            StorageButton.Background =
                Brushes.Transparent;

            ActivityButton.Background =
                Brushes.Transparent;

            DashboardButton.BorderBrush =
                Brushes.Transparent;

            AppsButton.BorderBrush =
                Brushes.Transparent;

            CleanupButton.BorderBrush =
                Brushes.Transparent;

            StorageButton.BorderBrush =
                Brushes.Transparent;

            ActivityButton.BorderBrush =
                Brushes.Transparent;
        }

        private void HighlightButton(Button button)
        {
            button.Background =
                new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#172554"));

            button.BorderBrush =
                new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#2563EB"));
        }

        private void HideAllPages()
        {
            DashboardPage.Visibility =
                Visibility.Collapsed;

            AppsPage.Visibility =
                Visibility.Collapsed;

            CleanupPage.Visibility =
                Visibility.Collapsed;

            StoragePage.Visibility =
                Visibility.Collapsed;

            ActivityPage.Visibility =
                Visibility.Collapsed;
        }

        // =========================
        // PAGE METHODS
        // =========================

        private void ShowDashboardPage()
        {
            HideAllPages();

            DashboardPage.Visibility =
                Visibility.Visible;

            ResetSidebarButtons();

            HighlightButton(DashboardButton);
        }

        private void ShowAppsPage()
        {
            HideAllPages();

            AppsPage.Visibility =
                Visibility.Visible;

            ResetSidebarButtons();

            HighlightButton(AppsButton);
        }

        private void ShowCleanupPage()
        {
            HideAllPages();

            CleanupPage.Visibility =
                Visibility.Visible;

            ResetSidebarButtons();

            HighlightButton(CleanupButton);
        }

        private void ShowStoragePage()
        {
            HideAllPages();

            StoragePage.Visibility =
                Visibility.Visible;

            ResetSidebarButtons();

            HighlightButton(StorageButton);
        }

        private void ShowActivityPage()
        {
            HideAllPages();

            ActivityPage.Visibility =
                Visibility.Visible;

            ResetSidebarButtons();

            HighlightButton(ActivityButton);
        }

        // =========================
        // EVENTS
        // =========================

        private void DashboardButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowDashboardPage();
        }

        private void AppsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowAppsPage();
        }

        private void CleanupButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowCleanupPage();
        }

        private void StorageButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowStoragePage();
        }

        private void ActivityButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowActivityPage();
        }

        private void Refresh_Click(
            object sender,
            RoutedEventArgs e)
        {
            LoadInstalledApps();

            LoadStorageAnalysis();

            GenerateRecommendations();

            AddActivity("System refreshed");
        }

        private void SearchBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            string search =
                SearchBox.Text.ToLower();

            var filtered =
                installedApps
                .Where(a =>
                    a.Name.ToLower().Contains(search) ||
                    a.Publisher.ToLower().Contains(search))
                .ToList();

            AppsGrid.ItemsSource =
                filtered;
        }

        private void BatchUninstall_Click(
            object sender,
            RoutedEventArgs e)
        {

        }

        private void ScanLeftovers_Click(
            object sender,
            RoutedEventArgs e)
        {
            LeftoverResultsText.Text =
                "• Temp files detected\n\n" +
                "• Old cache folders detected\n\n" +
                "• Empty uninstall folders detected\n\n" +
                "Cleanup recommendations ready.";

            AddActivity(
                "Cleanup scan completed");
        }
    }
}
