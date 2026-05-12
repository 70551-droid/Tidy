using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Tidy
{
    public partial class MainWindow : Window
    {
        private List<AppInfo> apps = new();

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

                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

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

                        string? installDate =
                            sk?.GetValue("InstallDate") as string;

                        object? sizeObj =
                            sk?.GetValue("EstimatedSize");

                        string sizeText = "Unknown";

                        double sizeMb = 0;

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
                            InstallDate = installDate ?? "Unknown",
                            Size = sizeText,
                            SizeMb = sizeMb,
                            Command = uninstall ?? ""
                        });
                    }
                    catch
                    {
                    }
                }
            });

            var sortedApps = apps
                .OrderByDescending(a => a.SizeMb)
                .ToList();

            AppsList.ItemsSource = sortedApps;

            InstalledCountText.Text =
                sortedApps.Count.ToString();

            double totalGb =
                sortedApps.Sum(a => a.SizeMb) / 1024.0;

            DiskUsageText.Text =
                $"{totalGb:F2} GB";

            CalculateCleanupScore(totalGb);

            GenerateSuggestions(sortedApps);
        }

        private void CalculateCleanupScore(double totalGb)
        {
            int score = 100;

            if (apps.Count > 100)
                score -= 20;

            if (totalGb > 100)
                score -= 25;

            if (GetStartupApps().Count > 15)
                score -= 20;

            if (apps.Count(a => a.SizeMb > 1024) > 10)
                score -= 15;

            if (score < 0)
                score = 0;

            CleanupScoreText.Text =
                score.ToString();

            if (score >= 80)
                CleanupStatusText.Text = "Excellent";
            else if (score >= 60)
                CleanupStatusText.Text = "Good";
            else if (score >= 40)
                CleanupStatusText.Text = "Needs cleanup";
            else
                CleanupStatusText.Text = "Heavy cleanup needed";
        }

        private void GenerateSuggestions(List<AppInfo> sortedApps)
        {
            var largeApps = sortedApps
                .Where(a => a.SizeMb > 1024)
                .Take(5)
                .Select(a => a.Name)
                .ToList();

            if (largeApps.Any())
            {
                LargeAppsText.Text =
                    "Large apps detected: " +
                    string.Join(", ", largeApps);
            }
            else
            {
                LargeAppsText.Text =
                    "No unusually large apps detected.";
            }

            var startupApps = GetStartupApps();

            if (startupApps.Any())
            {
                StartupAppsText.Text =
                    "Startup-heavy apps: " +
                    string.Join(", ", startupApps.Take(5));
            }
            else
            {
                StartupAppsText.Text =
                    "No major startup apps detected.";
            }

            var reviewApps = sortedApps
                .Where(a => a.SizeMb > 500)
                .Take(5)
                .Select(a => a.Name)
                .ToList();

            if (reviewApps.Any())
            {
                UnusedAppsText.Text =
                    "Apps worth reviewing: " +
                    string.Join(", ", reviewApps);
            }
            else
            {
                UnusedAppsText.Text =
                    "No cleanup suggestions available.";
            }
        }

        private List<string> GetStartupApps()
        {
            List<string> startupApps = new();

            try
            {
                using var startupKey =
                    Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Run");

                if (startupKey != null)
                {
                    startupApps.AddRange(
                        startupKey.GetValueNames());
                }
            }
            catch
            {
            }

            return startupApps;
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            AppsList.ItemsSource = null;

            await LoadAppsAsync();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query =
                SearchBox.Text.ToLower();

            AppsList.ItemsSource = apps
                .Where(a =>
                    a.Name.ToLower().Contains(query) ||
                    a.Publisher.ToLower().Contains(query))
                .OrderByDescending(a => a.SizeMb)
                .ToList();
        }
    }

    public class AppInfo
    {
        public string Name { get; set; } = "";

        public string Publisher { get; set; } = "";

        public string InstallDate { get; set; } = "";

        public string Size { get; set; } = "";

        public double SizeMb { get; set; }

        public string Command { get; set; } = "";
    }
}
