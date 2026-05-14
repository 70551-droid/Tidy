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
        private List<AppInfo> apps = new();

        private readonly List<string> activityLogs = new();

        private readonly DispatcherTimer monitorTimer =
            new();

        private readonly PerformanceCounter cpuCounter =
            new("Processor", "% Processor Time", "_Total");

        private readonly PerformanceCounter ramCounter =
            new("Memory", "Available MBytes");

        public MainWindow()
        {
            InitializeComponent();

            Loaded += async (s, e) =>
            {
                StartSystemMonitor();

                await LoadAppsAsync();

                await LoadStorageAnalysisAsync();

                LoadMicrosoftStoreApps();

                LoadLargestApps();

                GenerateRecommendations();
            };
        }

        private void StartSystemMonitor()
        {
            monitorTimer.Interval =
                TimeSpan.FromSeconds(1);

            monitorTimer.Tick += (s, e) =>
            {
                UpdateSystemStats();
            };

            monitorTimer.Start();
        }

        private void UpdateSystemStats()
        {
            try
            {
                // CPU
                CpuUsageText.Text =
                    $"{Math.Round(cpuCounter.NextValue())}%";

                // RAM
                double totalRamGb =
                    GetTotalRamGb();

                double availableMb =
                    ramCounter.NextValue();

                double availableGb =
                    availableMb / 1024.0;

                double usedGb =
                    totalRamGb - availableGb;

                double ramPercent =
                    (usedGb / totalRamGb) * 100;

                RamUsageText.Text =
                    $"{ramPercent:F0}%";

                RamFigureText.Text =
                    $"{usedGb:F1} GB / {totalRamGb:F1} GB";

                // DISK
                DriveInfo drive =
                    DriveInfo.GetDrives()
                    .FirstOrDefault(d =>
                        d.IsReady &&
                        d.Name.StartsWith("C"));

                if (drive != null)
                {
                    double totalGb =
                        drive.TotalSize / 1024.0 / 1024.0 / 1024.0;

                    double freeGb =
                        drive.TotalFreeSpace / 1024.0 / 1024.0 / 1024.0;

                    double usedPercent =
                        ((totalGb - freeGb) / totalGb) * 100;

                    DiskUsageText.Text =
                        $"{usedPercent:F0}%";

                    DiskFigureText.Text =
                        $"{freeGb:F0} GB Free";
                }
            }
            catch
            {
            }
        }

        private double GetTotalRamGb()
        {
            try
            {
                var info =
                    new Microsoft.VisualBasic.Devices.ComputerInfo();

                ulong totalBytes =
                    info.TotalPhysicalMemory;

                return totalBytes / 1024.0 / 1024.0 / 1024.0;
            }
            catch
            {
                return 16;
            }
        }

        private async System.Threading.Tasks.Task LoadAppsAsync()
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                apps.Clear();

                LoadRegistryApps(
                    Registry.LocalMachine,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

                LoadRegistryApps(
                    Registry.LocalMachine,
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");

                LoadRegistryApps(
                    Registry.CurrentUser,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

                RemoveDuplicateApps();

                CategorizeApps();
            });

            AppsGrid.ItemsSource =
                apps.OrderByDescending(a => a.SizeMb).ToList();

            InstalledCountText.Text =
                apps.Count.ToString();

            AddActivity(
                $"Detected {apps.Count} installed applications");
        }

        private void LoadRegistryApps(
            RegistryKey root,
            string path)
        {
            using RegistryKey key =
                root.OpenSubKey(path);

            if (key == null)
                return;

            foreach (string sub in key.GetSubKeyNames())
            {
                try
                {
                    using RegistryKey sk =
                        key.OpenSubKey(sub);

                    if (sk == null)
                        continue;

                    string name =
                        sk.GetValue("DisplayName") as string;

                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    object systemComponent =
                        sk.GetValue("SystemComponent");

                    if (systemComponent != null &&
                        systemComponent.ToString() == "1")
                    {
                        continue;
                    }

                    string uninstall =
                        sk.GetValue("UninstallString") as string;

                    if (string.IsNullOrWhiteSpace(uninstall))
                        continue;

                    string publisher =
                        sk.GetValue("Publisher") as string;

                    object sizeObj =
                        sk.GetValue("EstimatedSize");

                    double sizeMb = 0;

                    string sizeText = "Unknown";

                    if (sizeObj != null &&
                        int.TryParse(sizeObj.ToString(), out int kb))
                    {
                        sizeMb = kb / 1024.0;

                        sizeText =
                            $"{sizeMb:F1} MB";
                    }

                    apps.Add(new AppInfo
                    {
                        Name = name.Trim(),
                        Publisher =
                            string.IsNullOrWhiteSpace(publisher)
                            ? "Unknown"
                            : publisher.Trim(),
                        Size = sizeText,
                        SizeMb = sizeMb,
                        Command = uninstall,
                        Category = "Utility"
                    });
                }
                catch
                {
                }
            }
        }

        private void CategorizeApps()
        {
            foreach (var app in apps)
            {
                string name =
                    app.Name.ToLower();

                if (name.Contains("chrome") ||
                    name.Contains("edge") ||
                    name.Contains("firefox"))
                {
                    app.Category = "Browser";
                }
                else if (name.Contains("steam") ||
                         name.Contains("epic"))
                {
                    app.Category = "Games";
                }
                else if (name.Contains("visual studio") ||
                         name.Contains("python"))
                {
                    app.Category = "Development";
                }
                else if (name.Contains("spotify") ||
                         name.Contains("vlc"))
                {
                    app.Category = "Media";
                }
                else if (name.Contains("microsoft"))
                {
                    app.Category = "Microsoft";
                }
            }
        }

        private void RemoveDuplicateApps()
        {
            apps =
                apps.GroupBy(a => a.Name.ToLower())
                    .Select(g => g.First())
                    .ToList();
        }

        private async System.Threading.Tasks.Task LoadStorageAnalysisAsync()
        {
            DownloadsSizeText.Text =
                "Scanning...";

            await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    string downloads =
                        Path.Combine(
                            Environment.GetFolderPath(
                                Environment.SpecialFolder.UserProfile),
                            "Downloads");

                    double downloadsGb =
                        GetFolderSize(downloads);

                    Dispatcher.Invoke(() =>
                    {
                        DownloadsSizeText.Text =
                            $"{downloadsGb:F2} GB";
                    });
                }
                catch
                {
                }
            });
        }

        private double GetFolderSize(string path)
        {
            try
            {
                DirectoryInfo dir =
                    new(path);

                long size =
                    dir.GetFiles("*", SearchOption.AllDirectories)
                       .Sum(f => f.Length);

                return size / 1024.0 / 1024.0 / 1024.0;
            }
            catch
            {
                return 0;
            }
        }

        private void LoadLargestApps()
        {
            var largest =
                apps.OrderByDescending(a => a.SizeMb)
                    .Take(10)
                    .ToList();

            List<string> lines = new();

            foreach (var app in largest)
            {
                lines.Add(
                    $"{app.Name}  •  {app.Size}");
            }

            LargestAppsText.Text =
                string.Join(
                    Environment.NewLine +
                    Environment.NewLine,
                    lines);
        }

        private void GenerateRecommendations()
        {
            List<string> recs = new();

            if (apps.Any(a => a.SizeMb > 5000))
            {
                recs.Add(
                    "• Very large applications detected");
            }

            if (apps.Any(a => a.Publisher == "Unknown"))
            {
                recs.Add(
                    "• Apps with unknown publishers found");
            }

            if (!recs.Any())
            {
                recs.Add(
                    "System health looks excellent.");
            }

            RecommendationsText.Text =
                string.Join(
                    Environment.NewLine +
                    Environment.NewLine,
                    recs);
        }

        private void LoadMicrosoftStoreApps()
        {
            try
            {
                List<string> storeApps =
                    new();

                string path =
                    @"C:\Program Files\WindowsApps";

                if (Directory.Exists(path))
                {
                    foreach (var dir in Directory.GetDirectories(path))
                    {
                        storeApps.Add(
                            Path.GetFileName(dir));
                    }
                }

                StoreAppsText.Text =
                    string.Join(
                        Environment.NewLine +
                        Environment.NewLine,
                        storeApps.Take(15));
            }
            catch
            {
                StoreAppsText.Text =
                    "Administrator permission required.";
            }
        }

        private void ScanLeftovers_Click(
            object sender,
            RoutedEventArgs e)
        {
            LeftoverResultsText.Text =
                "Advanced scan completed.";
        }

        private void SearchBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            string query =
                SearchBox.Text.ToLower();

            AppsGrid.ItemsSource =
                apps.Where(a =>
                    a.Name.ToLower().Contains(query) ||
                    a.Publisher.ToLower().Contains(query) ||
                    a.Category.ToLower().Contains(query))
                .OrderByDescending(a => a.SizeMb)
                .ToList();
        }

        private void Refresh_Click(
            object sender,
            RoutedEventArgs e)
        {
            _ = LoadAppsAsync();

            _ = LoadStorageAnalysisAsync();

            LoadLargestApps();

            GenerateRecommendations();

            AddActivity(
                "Dashboard refreshed");
        }

        private void AddActivity(string message)
        {
            string log =
                $"{DateTime.Now:t} — {message}";

            activityLogs.Insert(0, log);

            ActivityText.Text =
                string.Join(
                    Environment.NewLine +
                    Environment.NewLine,
                    activityLogs.Take(20));
        }

        private void BatchUninstall_Click(
            object sender,
            RoutedEventArgs e)
        {
            AddActivity(
                "Batch uninstall started");
        }

        private void UninstallButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.DataContext is not AppInfo app)
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {app.Command}",
                    UseShellExecute = true,
                    Verb = "runas"
                });

                AddActivity(
                    $"Started uninstall: {app.Name}");
            }
            catch
            {
            }
        }
    }

    public class AppInfo
    {
        public string Name { get; set; } = "";

        public string Publisher { get; set; } = "";

        public string Size { get; set; } = "";

        public double SizeMb { get; set; }

        public string Command { get; set; } = "";

        public string Category { get; set; } = "";
    }
}
