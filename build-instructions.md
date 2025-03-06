# CodeWalker Build Instructions

This document provides instructions for building and running the CodeWalker project on this specific Windows 10 computer.

## Prerequisites

- Windows 10 (version 10.0.22631)
- .NET SDK 9.0.200 or compatible version
- PowerShell 7

## Building the Project

1. Open PowerShell by running `pwsh.exe` from the Start menu or command prompt.

2. Navigate to the CodeWalker project directory:
   ```powershell
   cd C:\Users\thoma\Documents\Code\CodeWalker
   ```

3. Build the solution using the .NET SDK:
   ```powershell
   dotnet build CodeWalker.sln
   ```

   Note: The build may show a warning about a Visual Studio component and MSBuild.exe, but the main executables should still be built successfully.

## Running the Application

After building, you can run any of the following executables:

### Main CodeWalker Application
```powershell
Start-Process -FilePath "CodeWalker\bin\Debug\net48\CodeWalker.exe"
```

### RPF Explorer
```powershell
Start-Process -FilePath "CodeWalker.RPFExplorer\bin\Debug\net48\CodeWalker RPF Explorer.exe"
```

### Vehicle Viewer
```powershell
Start-Process -FilePath "CodeWalker.Vehicles\bin\Debug\net48\CodeWalker Vehicle Viewer.exe"
```

### Ped Viewer
```powershell
Start-Process -FilePath "CodeWalker.Peds\bin\Debug\net48\CodeWalker Ped Viewer.exe"
```

### Error Report Tool
```powershell
Start-Process -FilePath "CodeWalker.ErrorReport\bin\Debug\net48\CodeWalker Error Report Tool.exe"
```

## Running in Different Modes

The main CodeWalker application can be run in different modes using command-line arguments:

### Menu Mode
```powershell
Start-Process -FilePath "CodeWalker\bin\Debug\net48\CodeWalker.exe" -ArgumentList "menu"
```

### Explorer Mode
```powershell
Start-Process -FilePath "CodeWalker\bin\Debug\net48\CodeWalker.exe" -ArgumentList "explorer"
```

## First-Time Setup

On first startup, the app will prompt you to browse for the GTA:V game folder. If you have the Steam version installed in the default location (`C:\Program Files (x86)\Steam\SteamApps\common\Grand Theft Auto V`), this step will be skipped automatically.

## Troubleshooting

If you encounter build errors:

1. Make sure you have the correct .NET SDK version installed:
   ```powershell
   dotnet --version
   ```

2. If the build fails with specific errors, try cleaning the solution and rebuilding:
   ```powershell
   dotnet clean CodeWalker.sln
   dotnet build CodeWalker.sln
   ```

3. If you have Visual Studio installed, you can also try building the solution using Visual Studio instead of the command line. 