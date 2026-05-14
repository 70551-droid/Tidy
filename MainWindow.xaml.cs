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

                await LoadStorageAnalysisAsync();

                LoadMicrosoftStoreApps();
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

            AppsGrid.ItemsSource = apps
                .OrderByDescending(a => a.SizeMb)
                .ToList();

            InstalledCountText.Text =
                apps.Count.ToString();

            LoadStartupApps();

            AddActivity(
                $"Detected {apps.Count} installed applications");
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
                         name.Contains("python") ||
                         name.Contains("git"))
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

        private async Task LoadStorageAnalysisAsync()
        {
            DownloadsSizeText.Text =
                "Scanning...";

            DesktopSizeText.Text =
                "Scanning...";

            DocumentsSizeText.Text =
                "Scanning...";

            await Task.Run(() =>
            {
                try
                {
                    string downloads =
                        Path.Combine(
                            Environment.GetFolderPath(
                                Environment.SpecialFolder.UserProfile),
                            "Downloads");

                    string desktop =
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.Desktop);

                    string documents =
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.MyDocuments);

                    double downloadsGb =
                        GetFolderSize(downloads);

                    double desktopGb =
                        GetFolderSize(desktop);

                    double documentsGb =
                        GetFolderSize(documents);

                    Dispatcher.Invoke(() =>
                    {
                        DownloadsSizeText.Text =
                            $"{downloadsGb:F2} GB";

                        DesktopSizeText.Text =
                            $"{desktopGb:F2} GB";

                        DocumentsSizeText.Text =
                            $"{documentsGb:F2} GB";
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
                DirectoryInfo dir = new(path);

                long size = dir
                    .GetFiles("*", SearchOption.AllDirectories)
                    .Sum(f => f.Length);

                return size / 1024.0 / 1024.0 / 1024.0;
            }
            catch
            {
                return 0;
            }
        }

        private void LoadMicrosoftStoreApps()
        {
            try
            {
                List<string> storeApps = new();

                string windowsApps =
                    @"C:\Program Files\WindowsApps";

                if (Directory.Exists(windowsApps))
                {
                    foreach (var dir in Directory.GetDirectories(windowsApps))
                    {
                        string name =
                            Path.GetFileName(dir);

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            storeApps.Add(name);
                        }
                    }
                }

                if (!storeApps.Any())
                {
                    storeApps.Add(
                        "No Microsoft Store apps detected.");
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
            try
            {
                List<string> leftovers = new();

                string programFiles =
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ProgramFiles);

                foreach (var dir in Directory.GetDirectories(programFiles))
                {
                    string name =
                        Path.GetFileName(dir);

                    bool exists =
                        apps.Any(a =>
                            a.Name.Contains(
                                name,
                                StringComparison.OrdinalIgnoreCase));

                    if (!exists)
                    {
                        leftovers.Add(
                            $"Possible leftover: {name}");
                    }
                }

                if (!leftovers.Any())
                {
                    leftovers.Add(
                        "No obvious leftovers detected.");
                }

                LeftoverResultsText.Text =
                    string.Join(
                        Environment.NewLine +
                        Environment.NewLine,
                        leftovers.Take(20));

                AddActivity(
                    "Scanned for leftover files");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Tidy");
            }
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

            _ = LoadStorageAnalysisAsync();

            LoadMicrosoftStoreApps();

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
