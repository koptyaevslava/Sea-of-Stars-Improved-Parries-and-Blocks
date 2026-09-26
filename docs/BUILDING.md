# Building

## Plug-in

Install BepInEx 6 for IL2CPP into Sea of Stars and run the game once so BepInEx generates its interop assemblies. Set `SEA_OF_STARS_DIR` to the game directory, then build:

```powershell
$env:SEA_OF_STARS_DIR = "D:\SteamLibrary\steamapps\common\Sea of Stars"
dotnet build src\ParriesAndBlocks\ParriesAndBlocks.csproj -c Release
```

You can also pass the path directly:

```powershell
dotnet build src\ParriesAndBlocks\ParriesAndBlocks.csproj -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Sea of Stars"
```

The build never copies files into the game directory.

## Installer payload

Place these files under `payload/GenerousBlock`:

```text
payload/GenerousBlock/
  GenerousBlock.dll
  README.txt
```

The payload must contain exactly those two files. This prevents another mod, a game binary, or a local configuration file from entering the installer.

## Installer

Run:

```powershell
python installer\build_installer.py
```

Output is written to `dist/ParriesAndBlocks-Installer-2.2.4`. Keep the installer executable and `.pbpkg` file together. SHA-256 files are generated for both release artifacts.

The installer supports these command-line operations:

```text
ParriesAndBlocksInstaller.exe --verify-package
ParriesAndBlocksInstaller.exe --list-games
ParriesAndBlocksInstaller.exe --install "<game-folder>"
ParriesAndBlocksInstaller.exe --verify-installed "<game-folder>"
ParriesAndBlocksInstaller.exe --uninstall "<game-folder>"
```
