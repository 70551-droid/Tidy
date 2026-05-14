using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Tidy
{
    public partial class MainWindow : Window
    {
        private List<AppInfo> apps = new();

        private readonly List<string> activityLogs = new();

        private readonly PerformanceCounter cpuCounter =
            new("Processor", "% Processor Time", "_Total");

        private readonly DispatcherTimer monitorTimer =
            new();

        public MainWindow()
        {
            InitializeComponent();

            Loaded += async (s, e) =>
            {
                StartSystemMonitor();

                await LoadAppsAsync();
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
                CpuUsageText.Text =
                    $"{Math.Round(cpuCounter.NextValue())}%";

                var memoryInfo =
                    GC.GetGCMemoryInfo();

                long total =
                    memoryInfo.TotalAvailableMemoryBytes;

                long used =
                    GC.GetTotalMemory(false);

                double usedPercent =
                    ((double)used / total) * 100;

                RamUsageText.Text =
                    $"{usedPercent:F0}%";

                StartupCountText.Text =
                    GetStartupApps().Count.ToString();
            }
            catch
            {
            }
        }

        private async Task LoadAppsAsync()
        {
            await Task.Run(() =>
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

            var sortedApps = apps
                .OrderByDescending(a => a.SizeMb)
                .ToList();

            AppsGrid.ItemsSource = sortedApps;

            InstalledCountText.Text =
                sortedApps.Count.ToString();

            LoadRecommendations();

            LoadDuplicateDetection();

            LoadStartupApps();

            AddActivity(
                $"Detected {sortedApps.Count} installed applications");
        }

        private void LoadRegistryApps(
            RegistryKey root,
            string path)
        {
            using var key = root.OpenSubKey(path);

            if (key == null)
                return;

            foreach (var sub in key.GetSubKeyNames())
            {
                try
                {
                    using var sk = key.OpenSubKey(sub);

                    if (sk == null)
                        continue;

                    string? name =
                        sk.GetValue("DisplayName") as string;

                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    object? systemComponent =
                        sk.GetValue("SystemComponent");

                    if (systemComponent != null &&
                        systemComponent.ToString() == "1")
                    {
                        continue;
                    }

                    string? uninstall =
                        sk.GetValue("UninstallString") as string;

                    if (string.IsNullOrWhiteSpace(uninstall))
                        continue;

                    string? publisher =
                        sk.GetValue("Publisher") as string;

                    object? sizeObj =
                        sk.GetValue("EstimatedSize");

                    double sizeMb = 0;

                    string sizeText = "Unknown";

                    if (sizeObj != null &&
                        int.TryParse(sizeObj.ToString(), out int kb))
                    {
                        sizeMb = kb / 1024.0;
                        sizeText = $"{sizeMb:F1} MB";
                    }

                    apps.Add(new AppInfo
                    {
                        Name = name.Trim(),
                        Publisher = string.IsNullOrWhiteSpace(publisher)
                            ? "Unknown"
                            : publisher.Trim(),
                        Size = sizeText,
                        SizeMb = sizeMb,
                        Command = uninstall,
                        Category = "Other"
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
                    name.Contains("firefox") ||
                    name.Contains("edge") ||
                    name.Contains("opera"))
                {
                    app.Category = "Browser";
                }
                else if (name.Contains("visual studio") ||
                         name.Contains("python") ||
                         name.Contains("node") ||
                         name.Contains("git"))
                {
                    app.Category = "Development";
                }
                else if (name.Contains("steam") ||
                         name.Contains("epic") ||
                         name.Contains("game"))
                {
                    app.Category = "Games";
                }
                else if (name.Contains("vlc") ||
                         name.Contains("spotify") ||
                         name.Contains("media"))
                {
                    app.Category = "Media";
                }
                else if (name.Contains("microsoft"))
                {
                    app.Category = "Microsoft";
                }
                else if (name.Contains("antivirus") ||
                         name.Contains("defender"))
                {
                    app.Category = "Security";
                }
                else
                {
                    app.Category = "Utility";
                }
            }
        }

        private void RemoveDuplicateApps()
        {
            apps = apps
                .GroupBy(a => a.Name.ToLower())
                .Select(g => g.First())
                .ToList();
        }

        private void LoadRecommendations()
        {
            List<string> recommendations = new();

            foreach (var app in apps
                .OrderByDescending(a => a.SizeMb)
                .Take(5))
            {
                if (app.SizeMb > 2048)
                {
                    recommendations.Add(
                        $"Large application detected: {app.Name}");
                }

                if (app.Publisher == "Unknown")
                {
                    recommendations.Add(
                        $"Unknown publisher: {app.Name}");
                }

                if (string.IsNullOrWhiteSpace(app.Command))
                {
                    recommendations.Add(
                        $"Broken uninstaller: {app.Name}");
                }
            }

            if (GetStartupApps().Count > 15)
            {
                recommendations.Add(
                    "Too many startup applications enabled.");
            }

            if (!recommendations.Any())
            {
                recommendations.Add(
                    "System looks healthy.");
            }

            RecommendationsText.Text =
                string.Join(
                    Environment.NewLine +
                    Environment.NewLine,
                    recommendations);
        }

        private void LoadDuplicateDetection()
        {
            List<string> duplicates = new();

            var grouped =
                apps.GroupBy(a =>
                    a.Name.Split(' ')[0]);

            foreach (var group in grouped)
            {
                if (group.Count() > 1)
                {
                    duplicates.Add(
                        $"{group.Key} ({group.Count()} related apps)");
                }
            }

            if (!duplicates.Any())
            {
                duplicates.Add(
                    "No duplicate groups detected.");
            }

            DuplicateAppsText.Text =
                string.Join(
                    Environment.NewLine +
                    Environment.NewLine,
                    duplicates.Take(10));
        }

        private void LoadStartupApps()
        {
            StartupListBox.Items.Clear();

            foreach (var app in GetStartupApps())
            {
                StartupListBox.Items.Add(
                    $"{app.Name} ({app.Impact})");
            }
        }

        private List<StartupAppInfo> GetStartupApps()
        {
            List<StartupAppInfo> startupApps = new();

            try
            {
                using var key =
                    Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Run");

                if (key != null)
                {
                    foreach (string name in key.GetValueNames())
                    {
                        string impact = "Low";

                        if (name.Length > 15)
                            impact = "Medium";

                        if (name.Length > 25)
                            impact = "High";

                        startupApps.Add(
                            new StartupAppInfo
                            {
                                Name = name,
                                Impact = impact
                            });
                    }
                }
            }
            catch
            {
            }

            return startupApps;
        }

        private void DisableStartup_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (StartupListBox.SelectedItem == null)
            {
                MessageBox.Show(
                    "Select a startup app first.",
                    "Tidy");

                return;
            }

            string selected =
                StartupListBox.SelectedItem.ToString() ?? "";

            string appName =
                selected.Split('(')[0].Trim();

            try
            {
                using var key =
                    Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Run",
                        true);

                key?.DeleteValue(appName, false);

                LoadStartupApps();

                AddActivity(
                    $"Disabled startup app: {appName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Tidy");
            }
        }

        private void CleanTemp_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                string tempPath =
                    Path.GetTempPath();

                int deletedFiles = 0;

                long freedBytes = 0;

                foreach (string file in Directory.GetFiles(
                    tempPath,
                    "*",
                    SearchOption.AllDirectories))
                {
                    try
                    {
                        FileInfo info = new(file);

                        freedBytes += info.Length;

                        File.Delete(file);

                        deletedFiles++;
                    }
                    catch
                    {
                    }
                }

                double freedMb =
                    freedBytes / 1024.0 / 1024.0;

                TempCleanerResultText.Text =
                    $"Removed {deletedFiles} files.\nFreed {freedMb:F2} MB.";

                AddActivity(
                    $"Cleaned temp files ({freedMb:F2} MB)");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Tidy");
            }
        }

        private void BatchUninstall_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (AppsGrid.SelectedItems.Count == 0)
            {
                MessageBox.Show(
                    "Select apps first.",
                    "Tidy");

                return;
            }

            foreach (var item in AppsGrid.SelectedItems)
            {
                if (item is AppInfo app)
                {
                    UninstallApp(app);
                }
            }

            AddActivity(
                $"Started batch uninstall ({AppsGrid.SelectedItems.Count} apps)");
        }

        private void SearchBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            string query =
                SearchBox.Text.ToLower();

            AppsGrid.ItemsSource = apps
                .Where(a =>
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

            AddActivity("Dashboard refreshed");
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
                    activityLogs.Take(15));
        }

        private void UninstallButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.DataContext is not AppInfo app)
                return;

            UninstallApp(app);
        }

        private void UninstallApp(AppInfo app)
        {
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
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Tidy");
            }
        }
    }

    public class StartupAppInfo
    {
        public string Name { get; set; } = "";

        public string Impact { get; set; } = "";
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
