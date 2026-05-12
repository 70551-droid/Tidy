using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

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

                        if (sizeObj != null &&
                            int.TryParse(sizeObj.ToString(), out int kb))
                        {
                            double mb = kb / 1024.0;
                            sizeText = $"{mb:F1} MB";
                        }

                        BitmapImage? icon = null;

                        try
                        {
                            string? iconPath =
                                sk?.GetValue("DisplayIcon") as string;

                            if (!string.IsNullOrWhiteSpace(iconPath))
                            {
                                iconPath = iconPath.Split(',')[0];

                                if (File.Exists(iconPath))
                                {
                                    icon = new BitmapImage();

                                    icon.BeginInit();
                                    icon.UriSource = new Uri(iconPath);
                                    icon.DecodePixelWidth = 32;
                                    icon.EndInit();
                                }
                            }
                        }
                        catch
                        {
                        }

                        apps.Add(new AppInfo
                        {
                            Name = name,
                            Publisher = publisher ?? "Unknown",
                            InstallDate = installDate ?? "Unknown",
                            Size = sizeText,
                            Command = uninstall ?? "",
                            Icon = icon
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

            InstalledCountText.Text = apps.Count.ToString();

            double totalGb = 0;

            foreach (var app in apps)
            {
                if (app.Size.Contains("MB"))
                {
                    string raw =
                        app.Size.Replace("MB", "").Trim();

                    if (double.TryParse(raw, out double mb))
                    {
                        totalGb += mb / 1024.0;
                    }
                }
            }

            DiskUsageText.Text = $"{totalGb:F2} GB";
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            AppsList.ItemsSource = null;

            await LoadAppsAsync();
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
    }

    public class AppInfo
    {
        public BitmapImage? Icon { get; set; }

        public string Name { get; set; } = "";

        public string Publisher { get; set; } = "";

        public string InstallDate { get; set; } = "";

        public string Size { get; set; } = "";

        public string Command { get; set; } = "";
    }
}
