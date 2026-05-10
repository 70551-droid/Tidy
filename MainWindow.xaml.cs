using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
            Log("Loading installed applications...");

            await Task.Run(() =>
            {
                apps.Clear();

                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall");

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

                        if (sizeObj != null &&
                            int.TryParse(sizeObj.ToString(), out int kb))
                        {
                            double mb = kb / 1024.0;
                            sizeText = $"{mb:F1} MB";
                        }

                        apps.Add(new AppInfo
                        {
                            Name = name,
                            Publisher = publisher ?? "Unknown",
                            InstallDate = installDate ?? "Unknown",
                            Size = sizeText,
                            Command = uninstall ?? ""
                        });
                    }
                    catch
                    {
                    }
                }
            });

            AppsList.ItemsSource = apps
                .OrderBy(a => a.Name)
                .ToList();

            Log($"Loaded {apps.Count} applications");
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            AppsList.ItemsSource = null;

            await LoadAppsAsync();

            Log("Refresh complete");
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.ToLower();

            AppsList.ItemsSource = apps
                .Where(a =>
                    a.Name.ToLower().Contains(query) ||
                    a.Publisher.ToLower().Contains(query))
                .OrderBy(a => a.Name)
                .ToList();
        }

        private void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            if (AppsList.SelectedItem is not AppInfo app)
            {
                MessageBox.Show("Select an app first.");
                return;
            }

            if (string.IsNullOrWhiteSpace(app.Command))
            {
                MessageBox.Show("No uninstall command found.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c " + app.Command,
                    Verb = "runas",
                    UseShellExecute = true
                });

                Log($"Started uninstall: {app.Name}");
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StringBuilder csv = new();

                csv.AppendLine("Name,Publisher,InstallDate,Size");

                foreach (var app in apps)
                {
                    csv.AppendLine(
                        $"\"{app.Name}\"," +
                        $"\"{app.Publisher}\"," +
                        $"\"{app.InstallDate}\"," +
                        $"\"{app.Size}\"");
                }

                string path = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.Desktop),
                    "Tidy_Export.csv");

                File.WriteAllText(path, csv.ToString());

                Log($"Exported app list to: {path}");

                MessageBox.Show("Export complete.");
            }
            catch (Exception ex)
            {
                Log($"Export failed: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            Dispatcher.Invoke(() =>
            {
                Logs.AppendText(
                    $"[{DateTime.Now:T}] {message}\n");

                Logs.ScrollToEnd();
            });
        }
    }

    public class AppInfo
    {
        public string Name { get; set; } = "";
        public string Publisher { get; set; } = "";
        public string InstallDate { get; set; } = "";
        public string Size { get; set; } = "";
        public string Command { get; set; } = "";
    }
}
