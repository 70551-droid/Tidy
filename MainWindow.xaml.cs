using Microsoft.Win32;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace Tidy
{
    public partial class MainWindow : Window
    {
        private List<AppInfo> apps = new();

        public MainWindow()
        {
            InitializeComponent();
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

            AppsList.ItemsSource = apps.Select(a => a.Name);
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadApps();
            Log("Refreshed");
        }

        private void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            if (AppsList.SelectedItem == null) return;

            var app = apps.First(a => a.Name == AppsList.SelectedItem.ToString());

            Process.Start("cmd.exe", "/c " + app.Command);
            Log("Uninstall started: " + app.Name);
        }

        private void Log(string msg)
        {
            Logs.AppendText(msg + "\\n");
        }
    }

    public class AppInfo
    {
        public string Name { get; set; }
        public string Command { get; set; }
    }
}
