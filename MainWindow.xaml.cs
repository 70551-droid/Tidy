using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

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

                        string? name = sk?.GetValue("DisplayName") as string;
                        string? uninstall =
                            sk?.GetValue("UninstallString") as string;

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            apps.Add(new AppInfo
                            {
                                Name = name,
                                Command = uninstall ?? ""
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
                .Select(a => a.Name);

            Logs.AppendText($"Loaded {apps.Count} apps\n");
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            AppsList.ItemsSource = null;

            await LoadAppsAsync();

            Logs.AppendText("Refresh complete\n");
        }

        private void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            if (AppsList.SelectedItem == null)
            {
                MessageBox.Show("Select an app first.");
                return;
            }

            string selectedName = AppsList.SelectedItem.ToString() ?? "";

            var app = apps.FirstOrDefault(a => a.Name == selectedName);

            if (app == null || string.IsNullOrWhiteSpace(app.Command))
            {
                MessageBox.Show("Uninstall command not found.");
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
        public string Command { get; set; } = "";
    }
}
