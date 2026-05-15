using Microsoft.Win32;
using ModernWpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Tidy
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer statsTimer = new DispatcherTimer();

        private readonly PerformanceCounter cpuCounter =
            new PerformanceCounter("Processor", "% Processor Time", "_Total");

        private readonly PerformanceCounter ramCounter =
            new PerformanceCounter("Memory", "% Committed Bytes In Use");

        private List<InstalledApp> installedApps =
            new List<InstalledApp>();

        public MainWindow()
        {
            InitializeComponent();

            ThemeManager.Current.ApplicationTheme =
                ApplicationTheme.Dark;

            InitializeStats();

            LoadInstalledApps();
            LoadStartupApps();
            LoadStorageStats();
            LoadAiInsights();

            statsTimer.Interval = TimeSpan.FromSeconds(1);
            statsTimer.Tick += StatsTimer_Tick;
            statsTimer.Start();

            ShowPage(DashboardPage);
        }

        // =========================
        // NAVIGATION
        // =========================

        private void ShowPage(UIElement page)
        {
            DashboardPage.Visibility = Visibility.Collapsed;

            page.Visibility = Visibility.Visible;
        }

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(DashboardPage);
        }

        private void AppsButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Installed Apps page preserved in reactor core.",
                "Tidy");
        }

        private void StartupButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Startup Apps manager online.",
                "Tidy");
        }

        private void CleanupButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Cleanup engine online.",
                "Tidy");
        }

        private void StorageButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Storage analyzer online.",
                "Tidy");
        }

        private void DuplicateButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Duplicate finder initializing.",
                "Tidy");
        }

        private void ThemesButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Theme engine activated.",
                "Tidy");
        }

        private void AiButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "AI insights loaded.",
                "Tidy");
        }

        private void ActivityButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Activity feed online.",
                "Tidy");
        }

        // =========================
        // SYSTEM STATS
        // =========================

        private void InitializeStats()
        {
            CpuUsageText.Text = "0%";
            RamUsageText.Text = "0%";
            DiskUsageText.Text = "0%";
            GpuUsageText.Text = "0%";
        }

        private void StatsTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                float cpu = cpuCounter.NextValue();
                float ram = ramCounter.NextValue();

                CpuUsageText.Text = $"{cpu:0}%";
                RamUsageText.Text = $"{ram:0}%";

                RamFigureText.Text =
                    $"{GetUsedRam():0.0} GB Used";

                DriveInfo drive = DriveInfo.GetDrives()
                    .FirstOrDefault(d =>
                        d.IsReady && d.Name == "C:\\");

                if (drive != null)
                {
                    double used =
                        ((double)(drive.TotalSize -
                        drive.AvailableFreeSpace)
                        / drive.TotalSize) * 100;

                    DiskUsageText.Text =
                        $"{used:0}%";
                }

                GpuUsageText.Text = "Active";
            }
            catch
            {

            }
        }

        private double GetUsedRam()
        {
            try
            {
                ObjectQuery query = new ObjectQuery(
                    "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");

                ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(query);

                foreach (ManagementObject obj in searcher.Get())
                {
                    double total =
                        Convert.ToDouble(obj["TotalVisibleMemorySize"])
                        / 1024 / 1024;

                    double free =
                        Convert.ToDouble(obj["FreePhysicalMemory"])
                        / 1024 / 1024;

                    return total - free;
                }
            }
            catch
            {

            }

            return 0;
        }

        // =========================
        // INSTALLED APPS
        // =========================

        private class InstalledApp
        {
            public string Name { get; set; }

            public string Version { get; set; }

            public string Publisher { get; set; }
        }

        private void LoadInstalledApps()
        {
            installedApps.Clear();

            string uninstallKey =
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

            RegistryKey registryKey =
                Registry.LocalMachine.OpenSubKey(uninstallKey);

            if (registryKey == null)
                return;

            foreach (string subkeyName in registryKey.GetSubKeyNames())
            {
                RegistryKey subkey =
                    registryKey.OpenSubKey(subkeyName);

                string name =
                    subkey?.GetValue("DisplayName")?.ToString();

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                installedApps.Add(new InstalledApp
                {
                    Name = name,

                    Version =
                        subkey.GetValue("DisplayVersion")?.ToString(),

                    Publisher =
                        subkey.GetValue("Publisher")?.ToString()
                });
            }
        }

        // =========================
        // STARTUP APPS
        // =========================

        private void LoadStartupApps()
        {

        }

        // =========================
        // STORAGE
        // =========================

        private void LoadStorageStats()
        {

        }

        // =========================
        // AI INSIGHTS
        // =========================

        private void LoadAiInsights()
        {

        }
    }
}
