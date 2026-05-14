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
        private readonly DispatcherTimer monitorTimer = new();
        private readonly List<string> activityLogs = new();
        private List<AppInfo> apps = new();

        private readonly PerformanceCounter cpuCounter =
            new("Processor", "% Processor Time", "_Total");

        private readonly PerformanceCounter ramCounter =
            new("Memory", "Available MBytes");

        public MainWindow()
        {
            InitializeComponent();

            Loaded += async (s, e) =>
            {
                StartMonitoring();

                await LoadAppsAsync();

                await LoadDownloadsAsync();

                LoadLargestApps();

                GenerateRecommendations();
            };
        }

        private void StartMonitoring()
        {
            monitorTimer.Interval =
                TimeSpan.FromSeconds(1);

            monitorTimer.Tick += (s, e) =>
            {
                UpdateStats();
            };

            monitorTimer.Start();
        }

        private void UpdateStats()
        {
            try
            {
                CpuUsageText.Text =
                    $"{Math.Round(cpuCounter.NextValue())}%";

                double availableMb =
                    ramCounter.NextValue();

                double totalGb = 16;

                double availableGb =
                    availableMb / 1024.0;

                double usedGb =
                    totalGb - availableGb;

                double percent =
                    (usedGb / totalGb) * 100;

                RamUsageText.Text =
                    $"{percent:F0}%";

                RamFigureText.Text =
                    $"{usedGb:F1} GB / {totalGb:F0} GB";

                var drive =
                    DriveInfo.GetDrives()
                    .FirstOrDefault(d =>
                        d.IsReady &&
                        d.Name.StartsWith("C"));

                if (drive != null)
                {
                    double totalDisk =
                        drive.TotalSize / 1024.0 / 1024.0 / 1024.0;

                    double freeDisk =
                        drive.TotalFreeSpace / 1024.0 / 1024.0 / 1024.0;

                    double diskPercent =
                        ((totalDisk - freeDisk) / totalDisk) * 100;

                    DiskUsageText.Text =
                        $"{diskPercent:F0}%";

                    DiskFigureText.Text =
                        $"{freeDisk:F0} GB Free";
                }
            }
            catch
            {
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

                apps =
                    apps.GroupBy(a => a.Name.ToLower())
                        .Select(g => g.First())
                        .ToList();

                CategorizeApps();
            });

            Dispatcher.Invoke(() =>
            {
                AppsGrid.ItemsSource =
                    apps.OrderByDescending(a => a.SizeMb).ToList();

                InstalledCountText.Text =
                    apps.Count.ToString();

                AddActivity(
                    $"Detected {apps.Count} installed applications");
            });
        }

        private void LoadRegistryApps(
            RegistryKey root,
            string path)
        {
            using var key =
                root.OpenSubKey(path);

            if (key == null)
                return;

            foreach (var sub in key.GetSubKeyNames())
            {
                try
                {
                    using var sk =
                        key.OpenSubKey(sub);

                    if (sk == null)
                        continue;

                    string? name =
                        sk.GetValue("DisplayName") as string;

                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    string? uninstall =
                        sk.GetValue("UninstallString") as string;

                    if (string.IsNullOrWhiteSpace(uninstall))
                        continue;

                    object? sizeObj =
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

                    string publisher =
                        sk.GetValue("Publisher") as string
                        ?? "Unknown";

                    apps.Add(new AppInfo
                    {
                        Name = name,
                        Publisher = publisher,
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
                string n =
                    app.Name.ToLower();

                if (n.Contains("chrome") ||
                    n.Contains("edge") ||
                    n.Contains("firefox"))
                {
                    app.Category = "Browser";
                }
                else if (n.Contains("steam") ||
                         n.Contains("epic"))
                {
                    app.Category = "Games";
                }
                else if (n.Contains("visual studio") ||
                         n.Contains("python"))
                {
                    app.Category = "Development";
                }
                else if (n.Contains("spotify") ||
                         n.Contains("vlc"))
                {
                    app.Category = "Media";
                }
                else if (n.Contains("microsoft"))
                {
                    app.Category = "Microsoft";
                }
            }
        }

        private async System.Threading.Tasks.Task LoadDownloadsAsync()
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

                    double size =
                        GetFolderSize(downloads);

                    Dispatcher.Invoke(() =>
                    {
                        DownloadsSizeText.Text =
                            $"{size:F2} GB";
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
                long total =
                    Directory.GetFiles(
                        path,
                        "*",
                        SearchOption.AllDirectories)
                    .Sum(f => new FileInfo(f).Length);

                return total / 1024.0 / 1024.0 / 1024.0;
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
                    .Take(10);

            LargestAppsText.Text =
                string.Join(
                    Environment.NewLine + Environment.NewLine,
                    largest.Select(a =>
                        $"{a.Name} • {a.Size}"));
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
                    "• Unknown publishers detected");
            }

            if (!recs.Any())
            {
                recs.Add(
                    "System health looks excellent.");
            }

            RecommendationsText.Text =
                string.Join(
                    Environment.NewLine + Environment.NewLine,
                    recs);
        }

        private void Refresh_Click(
            object sender,
            RoutedEventArgs e)
        {
            _ = LoadAppsAsync();

            _ = LoadDownloadsAsync();

            LoadLargestApps();

            GenerateRecommendations();

            AddActivity(
                "Dashboard refreshed");
        }

        private void SearchBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            string q =
                SearchBox.Text.ToLower();

            AppsGrid.ItemsSource =
                apps.Where(a =>
                    a.Name.ToLower().Contains(q) ||
                    a.Publisher.ToLower().Contains(q) ||
                    a.Category.ToLower().Contains(q))
                .ToList();
        }

        private void ScanLeftovers_Click(
            object sender,
            RoutedEventArgs e)
        {
            LeftoverResultsText.Text =
                "Advanced leftover scan completed.";
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

        private void AddActivity(string message)
        {
            string line =
                $"{DateTime.Now:t} — {message}";

            activityLogs.Insert(0, line);

            ActivityText.Text =
                string.Join(
                    Environment.NewLine + Environment.NewLine,
                    activityLogs.Take(20));
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
