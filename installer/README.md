# Parries and Blocks Installer

The installer is a small Windows Forms front end for a separately generated `.pbpkg` ZIP container. It detects Steam libraries, accepts a manually selected Sea of Stars directory, validates BepInEx, verifies package hashes, installs through a staging-directory swap, and removes only `BepInEx/plugins/GenerousBlock`.

The package builder accepts exactly `GenerousBlock.dll` and `README.txt`. It rejects every additional file so another mod, a local configuration file, a game binary, or a generated dependency cannot enter the release by accident.

See [Building](../docs/BUILDING.md) for the expected payload layout and commands.
