using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

                LoadRegistryApps(
                    Registry.LocalMachine,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

                LoadRegistryApps(
                    Registry.CurrentUser,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
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

            AddActivity("System scan completed");
        }

        private void LoadRegistryApps(RegistryKey root, string path)
        {
            using var key = root.OpenSubKey(path);

            if (key == null)
                return;

            foreach (var sub in key.GetSubKeyNames())
            {
                try
                {
                    using var sk = key.OpenSubKey(sub);

                    string? name =
                        sk?.GetValue("DisplayName") as string;

                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    string? uninstall =
                        sk?.GetValue("UninstallString") as string;

                    string? publisher =
                        sk?.GetValue("Publisher") as string;

                    object? sizeObj =
                        sk?.GetValue("EstimatedSize");

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
                        Name = name,
                        Publisher = publisher ?? "Unknown",
                        Size = sizeText,
                        SizeMb = sizeMb,
                        Command = uninstall ?? ""
                    });
                }
                catch
                {
                }
            }
        }

        private void CalculateCleanupScore(double totalGb)
        {
            int score = 100;

            if (apps.Count > 120)
                score -= 20;

            if (totalGb > 150)
                score -= 20;

            if (apps.Count(a => a.SizeMb > 1024) > 10)
                score -= 20;

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

        private void LoadStartupApps()
        {
            var startupApps = GetStartupApps();

            if (startupApps.Any())
            {
                StartupAppsText.Text =
                    string.Join(Environment.NewLine,
                        startupApps.Take(10));
            }
            else
            {
                StartupAppsText.Text =
                    "No startup apps detected.";
            }
        }

        private List<string> GetStartupApps()
        {
            List<string> startupApps = new();

            try
            {
                using var key =
                    Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Run");

                if (key != null)
                {
                    startupApps.AddRange(
                        key.GetValueNames());
                }
            }
            catch
            {
            }

            return startupApps;
        }

        private void AddActivity(string message)
        {
            string log =
                $"{DateTime.Now:t} — {message}";

            activityLogs.Insert(0, log);

            ActivityText.Text =
                string.Join(
                    Environment.NewLine + Environment.NewLine,
                    activityLogs.Take(8));
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            AppsGrid.ItemsSource = null;

            AddActivity("Refreshing installed apps");

            await LoadAppsAsync();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
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

        private void UninstallButton_Click(object sender, RoutedEventArgs e)
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

            var result = MessageBox.Show(
                $"Are you sure you want to uninstall:\n\n{app.Name} ?",
                "Confirm Uninstall",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                string command = app.Command.Trim();

                // MSI uninstall handling
                if (command.Contains("msiexec",
                    StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {command}",
                        UseShellExecute = true,
                        Verb = "runas"
                    });

                    AddActivity(
                        $"Started MSI uninstall: {app.Name}");

                    return;
                }

                // quoted executable
                if (command.StartsWith("\""))
                {
                    int secondQuote =
                        command.IndexOf('\"', 1);

                    if (secondQuote > 1)
                    {
                        string exe =
                            command.Substring(
                                1,
                                secondQuote - 1);

                        string args =
                            command.Substring(
                                secondQuote + 1);

                        Process.Start(new ProcessStartInfo
                        {
                            FileName = exe,
                            Arguments = args,
                            UseShellExecute = true,
                            Verb = "runas"
                        });

                        AddActivity(
                            $"Started uninstall: {app.Name}");

                        return;
                    }
                }

                // fallback
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

    public class AppInfo
    {
        public string Name { get; set; } = "";

        public string Publisher { get; set; } = "";

        public string Size { get; set; } = "";

        public double SizeMb { get; set; }

        public string Command { get; set; } = "";
    }
}
