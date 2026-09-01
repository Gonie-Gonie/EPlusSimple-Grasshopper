# Windows installer bundle template

`Install-Dragons.cmd` is the single, self-contained CMD and Windows PowerShell
5.1 installer copied into the generated Windows release bundle. It has no
repository, SDK, Python, administrator, or network dependency.

The release asset builder must produce this exact extracted layout for version
`0.1.0`:

```text
Install-Dragons.cmd
release-manifest.json
checksums.sha256
LICENSE.txt
NOTICE.md
README.txt
packages/
|-- rhino7/
|   |-- invisible-dragon-0.1.0-rh7-win.yak
|   `-- simple-dragon-0.1.0-rh7-win.yak
`-- rhino8/
    |-- invisible-dragon-0.1.0-rh8-win.yak
    `-- simple-dragon-0.1.0-rh8-win.yak
```

`release-manifest.json` uses schema
`dragons-grasshopper.windows-installer.v1`, version `0.1.0`, and an
ordered `products` array: `invisible-dragon` / `InvisibleDragon`, then
`simple-dragon` / `SimpleDragon`. Each product contains an ordered `packages`
array for `rhino7` then `rhino8`; every record has `target`, canonical relative
`path`, positive integer `bytes`, and lowercase `sha256`.

Before changing anything, the installer checks all four records, paths, byte
lengths, hashes, bundle containment, and reparse-point safety. It then uses the
`yak.exe` installed beside each selected `Rhino.exe` to replace both products.
The supported commands are:

```text
Install-Dragons.cmd
Install-Dragons.cmd --check
Install-Dragons.cmd rhino7
Install-Dragons.cmd rhino8 --check
```

The default `all` target installs into every detected Rhino 7 and Rhino 8
generation. An explicitly selected missing generation is an error; an absent
generation under `all` is skipped. Every Rhino process must be closed. Manual
Dragon GHAs outside Package Manager are reported but never removed.
