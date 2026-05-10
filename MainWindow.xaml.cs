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
            Logs.AppendText("Loading installed apps...\n");

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

                        string? uninstall =
                            sk?.GetValue("UninstallString") as string;

                        string? publisher =
                            sk?.GetValue("Publisher") as string;

                        string? installDate =
                            sk?.GetValue("InstallDate") as string;

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            apps.Add(new AppInfo
                            {
                                Name = name,
                                Command = uninstall ?? "",
                                Publisher = publisher ?? "Unknown",
                                InstallDate = installDate ?? "Unknown"
                            });
                        }
                    }
                    catch
                    {
                    }
                }
            });

            AppsList.ItemsSource = apps
                .OrderBy(a => a.Name)
                .ToList();

            Logs.AppendText($"Loaded {apps.Count} apps\n");
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            AppsList.ItemsSource = null;

            await LoadAppsAsync();

            Logs.AppendText("Refresh complete\n");
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.ToLower();

            AppsList.ItemsSource = apps
                .Where(a => a.Name.ToLower().Contains(query))
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

                Logs.AppendText($"Started uninstall: {app.Name}\n");
            }
            catch (Exception ex)
            {
                Logs.AppendText($"Error: {ex.Message}\n");
            }
        }
    }

    public class AppInfo
    {
        public string Name { get; set; } = "";
        public string Publisher { get; set; } = "";
        public string InstallDate { get; set; } = "";
        public string Command { get; set; } = "";
    }
}
