using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Tidy
{
    public sealed partial class MainWindow : Window
    {
        private List<AppInfo> apps = new();

        public MainWindow()
        {
            this.InitializeComponent();
            LoadApps();
        }

        private void LoadApps()
        {
            apps.Clear();

            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall");

            if (key == null) return;

            foreach (var sub in key.GetSubKeyNames())
            {
                using var sk = key.OpenSubKey(sub);
                string name = sk.GetValue("DisplayName") as string;
                string uninstall = sk.GetValue("UninstallString") as string;

                if (!string.IsNullOrEmpty(name))
                {
                    apps.Add(new AppInfo { Name = name, Command = uninstall });
                }
            }

            AppsList.ItemsSource = apps.Select(a => a.Name).ToList();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadApps();
            Log("Refreshed app list");
        }

        private void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            if (AppsList.SelectedItem == null) return;

            var selected = apps.First(a => a.Name == AppsList.SelectedItem.ToString());

            try
            {
                Process.Start("cmd.exe", "/c " + selected.Command);
                Log("Started uninstall: " + selected.Name);
            }
            catch
            {
                Log("Failed to uninstall");
            }
        }

        private void Log(string msg)
        {
            Logs.Text += msg + "\\n";
        }
    }

    public class AppInfo
    {
        public string Name { get; set; }
        public string Command { get; set; }
    }
}