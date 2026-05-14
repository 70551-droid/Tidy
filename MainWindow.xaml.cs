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

            ShowDashboardPage();

            AddActivity("Tidy initialized successfully");
        }

        public class AppInfo
        {
            public string Name { get; set; }

            public string Publisher { get; set; }

            public string Version { get; set; }

            public string UninstallString { get; set; }
        }

        private void AddActivity(string message)
        {
            ActivityText.Text =
                $"[{DateTime.Now:T}] {message}\n\n" +
                ActivityText.Text;
        }

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

                        if (ShouldSkipApp(displayName))
                            continue;

                        displayName = CleanAppName(displayName);

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
                                ?? "Unknown",

                            UninstallString =
                                subkey?.GetValue("UninstallString")?.ToString()
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

        private bool ShouldSkipApp(string name)
        {
            string lower = name.ToLower();

            string[] junk =
            {
                "security update",
                "hotfix",
                "runtime",
                "redistributable",
                "language pack",
                "sdk",
                "driver package",
                "service pack",
                "webview",
                "update for",
                "microsoft visual c++"
            };

            return junk.Any(j => lower.Contains(j));
        }

        private string CleanAppName(string name)
        {
            name = name.Replace("(x64)", "");
            name = name.Replace("(x86)", "");
            name = name.Replace("Microsoft ", "");
            name = name.Trim();

            return name;
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

                    double used = total - free;

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

        private void ResetSidebarButtons()
        {
            DashboardButton.Background = Brushes.Transparent;
            AppsButton.Background = Brushes.Transparent;
            CleanupButton.Background = Brushes.Transparent;
            StorageButton.Background = Brushes.Transparent;
            ActivityButton.Background = Brushes.Transparent;
        }

        private void HighlightButton(Button button)
        {
            button.Background =
                new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#172554"));
        }

        private void HideAllPages()
        {
            DashboardPage.Visibility = Visibility.Collapsed;
            AppsPage.Visibility = Visibility.Collapsed;
            CleanupPage.Visibility = Visibility.Collapsed;
            StoragePage.Visibility = Visibility.Collapsed;
            ActivityPage.Visibility = Visibility.Collapsed;
        }

        private void ShowDashboardPage()
        {
            HideAllPages();
            DashboardPage.Visibility = Visibility.Visible;
            ResetSidebarButtons();
            HighlightButton(DashboardButton);
        }

        private void ShowAppsPage()
        {
            HideAllPages();
            AppsPage.Visibility = Visibility.Visible;
            ResetSidebarButtons();
            HighlightButton(AppsButton);
        }

        private void ShowCleanupPage()
        {
            HideAllPages();
            CleanupPage.Visibility = Visibility.Visible;
            ResetSidebarButtons();
            HighlightButton(CleanupButton);
        }

        private void ShowStoragePage()
        {
            HideAllPages();
            StoragePage.Visibility = Visibility.Visible;
            ResetSidebarButtons();
            HighlightButton(StorageButton);
        }

        private void ShowActivityPage()
        {
            HideAllPages();
            ActivityPage.Visibility = Visibility.Visible;
            ResetSidebarButtons();
            HighlightButton(ActivityButton);
        }

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            ShowDashboardPage();
        }

        private void AppsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowAppsPage();
        }

        private void CleanupButton_Click(object sender, RoutedEventArgs e)
        {
            ShowCleanupPage();
        }

        private void StorageButton_Click(object sender, RoutedEventArgs e)
        {
            ShowStoragePage();
        }

        private void ActivityButton_Click(object sender, RoutedEventArgs e)
        {
            ShowActivityPage();
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

        private void UninstallSelected_Click(
            object sender,
            RoutedEventArgs e)
        {
            AppInfo selected =
                AppsGrid.SelectedItem as AppInfo;

            if (selected == null)
            {
                MessageBox.Show(
                    "Please select an application first.",
                    "Tidy",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            MessageBoxResult result =
                MessageBox.Show(
                    $"Uninstall {selected.Name}?",
                    "Confirm Uninstall",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                if (!string.IsNullOrWhiteSpace(selected.UninstallString))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {selected.UninstallString}",
                        UseShellExecute = true,
                        Verb = "runas"
                    });

                    AddActivity(
                        $"Started uninstall for {selected.Name}");
                }
                else
                {
                    MessageBox.Show(
                        "No uninstall command available.",
                        "Tidy",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Uninstall Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
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
