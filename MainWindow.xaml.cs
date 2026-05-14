using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Tidy
{
    public partial class MainWindow : Window
    {
        private List<AppInfo> apps = new();

        private readonly List<string> activityLogs = new();

        public MainWindow()
        {
            InitializeComponent();

            Loaded += async (s, e) =>
            {
                await LoadAppsAsync();
            };
        }

        private async Task LoadAppsAsync()
        {
            await Task.Run(() =>
            {
                apps.Clear();

                // 64-bit machine installs
                LoadRegistryApps(
                    Registry.LocalMachine,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

                // 32-bit installs
                LoadRegistryApps(
                    Registry.LocalMachine,
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");

                // Current user installs
                LoadRegistryApps(
                    Registry.CurrentUser,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

                // Current user WOW6432Node
                LoadRegistryApps(
                    Registry.CurrentUser,
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");

                RemoveDuplicateApps();
            });

            var sortedApps = apps
                .OrderByDescending(a => a.SizeMb)
                .ToList();

            AppsGrid.ItemsSource = sortedApps;

            InstalledCountText.Text =
                sortedApps.Count.ToString();

            double totalGb =
                sortedApps.Sum(a => a.SizeMb) / 1024.0;

            DiskUsageText.Text =
                $"{totalGb:F2} GB";

            CalculateCleanupScore(totalGb);

            LoadStartupApps();

            LoadLargestApps();

            LoadCleanupSuggestions();

            AddActivity($"Detected {sortedApps.Count} installed applications");
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

                    // Skip hidden/system entries
                    object? systemComponent =
                        sk.GetValue("SystemComponent");

                    if (systemComponent != null &&
                        systemComponent.ToString() == "1")
                    {
                        continue;
                    }

                    string? releaseType =
                        sk.GetValue("ReleaseType") as string;

                    if (!string.IsNullOrWhiteSpace(releaseType))
                        continue;

                    string? parentKeyName =
                        sk.GetValue("ParentKeyName") as string;

                    if (!string.IsNullOrWhiteSpace(parentKeyName))
                        continue;

                    string? uninstall =
                        sk.GetValue("UninstallString") as string;

                    if (string.IsNullOrWhiteSpace(uninstall))
                        continue;

                    // Skip updates/hotfixes
                    if (name.Contains("Security Update",
                        StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Update for",
                        StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Hotfix",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string? publisher =
                        sk.GetValue("Publisher") as string;

                    string? location =
                        sk.GetValue("InstallLocation") as string;

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

                    string uninstallType = "EXE";

                    if (uninstall.Contains("msiexec",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        uninstallType = "MSI";
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
                        InstallLocation = string.IsNullOrWhiteSpace(location)
                            ? "Unknown"
                            : location,
                        UninstallType = uninstallType
                    });
                }
                catch
                {
                }
            }
        }

        private void RemoveDuplicateApps()
        {
            apps = apps
                .GroupBy(a => a.Name.ToLower())
                .Select(g =>
                    g.OrderByDescending(a => a.SizeMb)
                     .First())
                .ToList();
        }

        private void CalculateCleanupScore(double totalGb)
        {
            int score = 100;

            if (apps.Count > 150)
                score -= 10;

            if (totalGb > 200)
                score -= 15;

            if (apps.Count(a => a.SizeMb > 2048) > 8)
                score -= 15;

            if (GetStartupApps().Count > 15)
                score -= 15;

            if (score < 0)
                score = 0;

            CleanupScoreText.Text = score.ToString();

            if (score >= 80)
                CleanupStatusText.Text = "Excellent";
            else if (score >= 60)
                CleanupStatusText.Text = "Good";
            else if (score >= 40)
                CleanupStatusText.Text = "Needs cleanup";
            else
                CleanupStatusText.Text = "Heavy cleanup needed";
        }

        private void LoadLargestApps()
        {
            var largest = apps
                .OrderByDescending(a => a.SizeMb)
                .Take(10)
                .Select(a =>
                    $"{a.Name} — {a.Size}");

            LargestAppsText.Text =
                string.Join(
                    Environment.NewLine,
                    largest);
        }

        private void LoadCleanupSuggestions()
        {
            List<string> suggestions = new();

            foreach (var app in apps
                .OrderByDescending(a => a.SizeMb)
                .Take(5))
            {
                if (app.SizeMb > 2048)
                {
                    suggestions.Add(
                        $"Large app detected: {app.Name}");
                }

                if (app.Publisher == "Unknown")
                {
                    suggestions.Add(
                        $"Unknown publisher: {app.Name}");
                }
            }

            if (GetStartupApps().Count > 15)
            {
                suggestions.Add(
                    "Too many startup applications enabled.");
            }

            if (!suggestions.Any())
            {
                suggestions.Add(
                    "System looks clean.");
            }

            CleanupSuggestionsText.Text =
                string.Join(
                    Environment.NewLine +
                    Environment.NewLine,
                    suggestions);
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

            var result = MessageBox.Show(
                $"Uninstall {AppsGrid.SelectedItems.Count} selected apps?",
                "Batch Uninstall",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            foreach (var item in AppsGrid.SelectedItems)
            {
                if (item is AppInfo app)
                {
                    try
                    {
                        UninstallApp(app);
                    }
                    catch
                    {
                    }
                }
            }

            AddActivity(
                $"Started batch uninstall ({AppsGrid.SelectedItems.Count} apps)");
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

        private async void Refresh_Click(
            object sender,
            RoutedEventArgs e)
        {
            AppsGrid.ItemsSource = null;

            AddActivity("Refreshing dashboard");

            await LoadAppsAsync();
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
                    a.Publisher.ToLower().Contains(query))
                .OrderByDescending(a => a.SizeMb)
                .ToList();
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
            if (string.IsNullOrWhiteSpace(app.Command))
            {
                MessageBox.Show(
                    "No uninstall command found.",
                    "Tidy");

                AddActivity(
                    $"Failed uninstall: {app.Name}");

                return;
            }

            try
            {
                string command = app.Command.Trim();

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
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

                AddActivity(
                    $"Uninstall failed: {app.Name}");
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

        public string InstallLocation { get; set; } = "";

        public string UninstallType { get; set; } = "";
    }
}
