# Tidy

A powerful Windows system maintenance and cleanup utility built with WPF and .NET 8.

## Overview

Tidy is a comprehensive system cleanup tool designed for Windows 11. It helps users optimize their system by monitoring resources, managing applications, and cleaning up unnecessary files.

## Features

### Core Features (Current)
- **Dashboard**: Real-time monitoring of CPU, RAM, and disk usage
- **Installed Applications**: Browse, search, and manage installed programs with version information
- **Cleanup Engine**: 
  - Clean temporary files
  - Empty Recycle Bin permanently
  - Boost Mode - Optimize system by reducing background process priorities
- **Activity Center**: View startup applications and system processes
- **Storage Analyzer**: Monitor storage usage (Downloads folder tracking)

### Planned Features
- **Duplicate Finder**: Identify and remove duplicate files
- **Themes**: Customize application appearance
- **AI Tools**: Smart system optimization recommendations

## System Requirements

- **Operating System**: Windows 11 (Windows 10 may work but untested)
- **Framework**: .NET 8.0 Runtime
- **Permissions**: Administrator rights required for most cleanup operations
- **Disk Space**: Minimal (< 50MB)

## Installation

### Option 1: Download Pre-built Executable
1. Download the latest `Tidy-App.zip` from the [Releases](https://github.com/70551-droid/Tidy/releases) page
2. Extract the ZIP file
3. Run `Tidy.exe` with administrator privileges

### Option 2: Build from Source
```bash
git clone https://github.com/70551-droid/Tidy.git
cd Tidy
dotnet build -c Release
dotnet run
```

## Usage

### Dashboard
- View real-time system metrics
- CPU usage updates every 2 seconds
- RAM and disk usage monitoring

### Installed Apps
- Search through installed applications
- View publisher and version information
- Keep track of all software on your system

### Cleanup Engine
**⚠️ Warning**: These operations may affect system stability. Use with caution.
- **Clean Temp**: Removes temporary files from Windows temp folder
- **Clean Recycle Bin**: Permanently empties the Recycle Bin (cannot be undone)
- **Boost Mode**: Reduces priority of background processes (excludes system processes)

### Activity Center
- Monitor startup applications
- View commands associated with startup items

## Building & Deployment

### GitHub Actions Workflow
The project includes automated CI/CD pipeline:
- Builds on every push to `main` branch
- Creates self-contained Windows x64 executable
- Packages as single file for easy distribution
- Artifacts available in GitHub Actions

### Manual Build
```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## Configuration

The application uses Windows Registry for reading system information:
- Installed applications (from `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall`)
- Startup items (from `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`)

No configuration file needed - settings are stored in the system registry.

## Known Limitations

- Requires administrator privileges for cleanup operations
- Some system processes cannot have their priority modified
- Storage analyzer currently only shows Downloads folder
- Theme and AI features not yet implemented

## Troubleshooting

### "Access Denied" errors
- Run Tidy with administrator privileges
- Some operations require elevated permissions

### CPU Usage shows 0%
- Ensure adequate system resources
- Check Windows Performance Monitor permissions

### Cleanup operations fail silently
- Check Application logs in Event Viewer
- Some files may be in use by running processes

## Development

### Project Structure
```
Tidy/
├── App.xaml              # Application entry point
├── App.xaml.cs
├── MainWindow.xaml       # Main UI layout
├── MainWindow.xaml.cs    # Business logic
├── Tidy.csproj          # Project configuration
└── .github/workflows/   # CI/CD pipeline
```

### Technologies Used
- **Framework**: .NET 8.0
- **UI**: WPF (Windows Presentation Foundation)
- **Language**: C# 12
- **Performance Monitoring**: Windows Performance Counter API
- **Registry Access**: Microsoft.Win32 Registry API

## Contributing

Contributions are welcome! Areas for improvement:
- Duplicate file finder implementation
- Theme customization system
- AI-powered optimization recommendations
- Additional cleanup categories (browser cache, logs, etc.)

## License

This project is open source and available under the MIT License.

## Disclaimer

**Use at your own risk!** This application performs system-level operations that can affect your computer's functionality. Always backup important data before using cleanup features. The developers are not responsible for data loss or system issues caused by this application.

## Support

For issues, feature requests, or bug reports, please create an [issue](https://github.com/70551-droid/Tidy/issues) on GitHub.

---

**Version**: 1.0  
**Last Updated**: 2026-05-16  
**Maintainer**: [@70551-droid](https://github.com/70551-droid)
