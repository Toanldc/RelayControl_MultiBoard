# Relay Control (WPF)

Modern WPF desktop app to control 5 relays over UART, matching the `relay_control.ino`
command protocol (`R1ON`, `R1OFF`, ... `R5ON`, `R5OFF`).

## Requirements

- Windows 10/11
- .NET 7 SDK (or adjust `TargetFramework` in the `.csproj` to `net6.0-windows` if you have .NET 6 instead)
- Visual Studio 2022 (recommended) or the `dotnet` CLI

## How to run

**Option A — Visual Studio**
1. Open `RelayControlWPF.csproj` in Visual Studio.
2. Press F5 to build and run.

**Option B — Command line**
```
cd RelayControlWPF
dotnet restore
dotnet run
```

## Notes

- `System.IO.Ports` (used for Serial communication) is part of the .NET runtime on Windows;
  no extra NuGet package is required for `net7.0-windows`.
- This project was written and syntax-reviewed in a Linux sandbox that cannot compile
  WPF apps (WPF is Windows-only), so please report any build errors and they can be
  fixed quickly.
- Baud rate is set to 9600 to match `relay_control.ino`. Change `BaudRate` in
  `MainWindow.xaml.cs` if you change it on the Arduino side too.
