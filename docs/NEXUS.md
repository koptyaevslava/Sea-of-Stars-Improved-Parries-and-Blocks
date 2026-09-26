# Nexus Mods packaging

Upload only the dedicated manual-install ZIP to Nexus Mods. It contains `BepInEx/plugins/GenerousBlock/GenerousBlock.dll` and a plain-text README.

Do not upload the installer bundle to Nexus Mods. That bundle contains an executable and a `.pbpkg` ZIP container. Placing either inside another ZIP introduces security-review triggers, and placing the package inside another ZIP creates a nested archive.

The Nexus package must meet all of these checks:

- Standard ZIP format
- No executable files
- No nested archives
- No password protection
- No game files, BepInEx binaries, generated interop assemblies, or unrelated mods
- SHA-256 digest published next to the download

Nexus Mods may still quarantine any upload for manual review. If that happens, keep the blocked file available and contact Nexus Mods support with the mod-page link.
