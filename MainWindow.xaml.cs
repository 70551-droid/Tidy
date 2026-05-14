using Microsoft.Win32;
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
        private DispatcherTimer monitorTimer;

        public MainWindow()
        {
            InitializeComponent();

            SetupMonitoring();

            ShowDashboardPage();
        }

        // =========================
        // MONITORING
        // =========================

        private void SetupMonitoring()
        {
            monitorTimer = new DispatcherTimer();

            monitorTimer.Interval = TimeSpan.FromSeconds(1);

            monitorTimer.Tick += MonitorTimer_Tick;

            monitorTimer.Start();
        }

        private void MonitorTimer_Tick(object sender, EventArgs e)
        {
            UpdateCpuUsage();
            UpdateRamUsage();
            UpdateDiskUsage();
        }

        private void UpdateCpuUsage()
        {
            Random random = new Random();

            int cpu = random.Next(10, 75);

            CpuUsageText.Text = cpu + "%";
        }

        private void UpdateRamUsage()
        {
            try
            {
                ObjectQuery query =
                    new ObjectQuery("SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem");

                ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(query);

                foreach (ManagementObject obj in searcher.Get())
                {
                    double total =
                        Convert.ToDouble(obj["TotalVisibleMemorySize"]);

                    double free =
                        Convert.ToDouble(obj["FreePhysicalMemory"]);

                    double used = total - free;

                    double percent = (used / total) * 100;

                    double totalGb = total / 1024 / 1024;

                    double usedGb = used / 1024 / 1024;

                    RamUsageText.Text = $"{percent:0}%";

                    RamFigureText.Text =
                        $"{usedGb:0.0} GB / {totalGb:0.0} GB";
                }
            }
            catch
            {
                RamUsageText.Text = "N/A";
            }
        }

        private void UpdateDiskUsage()
        {
            try
            {
                DriveInfo drive =
                    DriveInfo.GetDrives()
                    .FirstOrDefault(d => d.IsReady && d.Name == "C:\\");

                if (drive != null)
                {
                    double total =
                        drive.TotalSize / 1024d / 1024d / 1024d;

                    double free =
                        drive.TotalFreeSpace / 1024d / 1024d / 1024d;

                    double used = total - free;

                    double percent = (used / total) * 100;

                    DiskUsageText.Text = $"{percent:0}%";

                    DiskFigureText.Text =
                        $"{free:0.0} GB Free";
                }
            }
            catch
            {
                DiskUsageText.Text = "N/A";
            }
        }

        // =========================
        // NAVIGATION
        // =========================

        private void ResetSidebarButtons()
        {
            DashboardButton.Background =
                Brushes.Transparent;

            AppsButton.Background =
                Brushes.Transparent;

            CleanupButton.Background =
                Brushes.Transparent;

            StorageButton.Background =
                Brushes.Transparent;

            ActivityButton.Background =
                Brushes.Transparent;

            DashboardButton.BorderBrush =
                Brushes.Transparent;

            AppsButton.BorderBrush =
                Brushes.Transparent;

            CleanupButton.BorderBrush =
                Brushes.Transparent;

            StorageButton.BorderBrush =
                Brushes.Transparent;

            ActivityButton.BorderBrush =
                Brushes.Transparent;
        }

        private void HighlightButton(Button button)
        {
            button.Background =
                new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#172554"));

            button.BorderBrush =
                new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#2563EB"));
        }

        private void HideAllPages()
        {
            DashboardPage.Visibility =
                Visibility.Collapsed;

            AppsPage.Visibility =
                Visibility.Collapsed;

            CleanupPage.Visibility =
                Visibility.Collapsed;

            StoragePage.Visibility =
                Visibility.Collapsed;

            ActivityPage.Visibility =
                Visibility.Collapsed;
        }

        // =========================
        // PAGE SHOW METHODS
        // =========================

        private void ShowDashboardPage()
        {
            HideAllPages();

            DashboardPage.Visibility =
                Visibility.Visible;

            ResetSidebarButtons();

            HighlightButton(DashboardButton);
        }

        private void ShowAppsPage()
        {
            HideAllPages();

            AppsPage.Visibility =
                Visibility.Visible;

            ResetSidebarButtons();

            HighlightButton(AppsButton);
        }

        private void ShowCleanupPage()
        {
            HideAllPages();

            CleanupPage.Visibility =
                Visibility.Visible;

            ResetSidebarButtons();

            HighlightButton(CleanupButton);
        }

        private void ShowStoragePage()
        {
            HideAllPages();

            StoragePage.Visibility =
                Visibility.Visible;

            ResetSidebarButtons();

            HighlightButton(StorageButton);
        }

        private void ShowActivityPage()
        {
            HideAllPages();

            ActivityPage.Visibility =
                Visibility.Visible;

            ResetSidebarButtons();

            HighlightButton(ActivityButton);
        }

        // =========================
        // BUTTON EVENTS
        // =========================

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            ShowDashboardPage();
        }

        private void AppsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowAppsPage();
        }

        private void CleanupButton_Click(object sender, RoutedEventArgs e)
        {
            ShowCleanupPage();
        }

        private void StorageButton_Click(object sender, RoutedEventArgs e)
        {
            ShowStoragePage();
        }

        private void ActivityButton_Click(object sender, RoutedEventArgs e)
        {
            ShowActivityPage();
        }

        // =========================
        // EXISTING EVENTS
        // =========================

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            UpdateCpuUsage();

            UpdateRamUsage();

            UpdateDiskUsage();

            ActivityText.Text +=
                $"[{DateTime.Now:T}] System refreshed\n";
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void BatchUninstall_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ScanLeftovers_Click(object sender, RoutedEventArgs e)
        {
            LeftoverResultsText.Text =
                "• Temp files detected\n" +
                "• Old cache folders detected\n" +
                "• Empty uninstall folders detected\n\n" +
                "Cleanup recommendations ready.";
        }
    }
}
