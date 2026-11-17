# Windows installer

Install the .NET 8 SDK and Inno Setup 6. Run these commands from the repository root:

```powershell
dotnet publish src/FleetAutomate.Application/FleetAutomate.csproj -c Release -r win-x64 --self-contained false
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer/FleetAutomate.iss
```

The script finds the repository relative to its own location. It packages the published application and writes `FleetAutomate-0.1.0-setup.exe` to `website/releases/`.

The installed application requires the .NET 8 Desktop Runtime for Windows x64. The installer does not install the runtime.

When changing the release version, update `MyAppVersion` in `FleetAutomate.iss` and the download links and version text in all five website pages together. Generated installers are ignored by Git.
