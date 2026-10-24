# Publish on GitHub

Public repository: https://github.com/MhDTam/ShutdownSteam

The public screenshot is `assets/Dashboard.png`. It contains demonstration data
and is also the build-required visual skin. Raw source pictures, image-generation
notes, local previews, game caches, and build outputs are excluded from Git.

## First Upload

1. Create an empty GitHub repository named `ShutdownSteam` in your account.
2. Review the files, license, and artwork notices before committing.
3. From this project folder, run the following, replacing `YOUR_ACCOUNT`:

```powershell
git add .
git commit -m "Initial ShutdownSteam release"
git remote add origin https://github.com/YOUR_ACCOUNT/ShutdownSteam.git
git push -u origin main
```

If Git asks for your identity, configure your own author name and email. Generated EXEs, archives, previews, test fixtures, and old build outputs are excluded by `.gitignore`.

## Release

Check the build workflow passes, then create a version tag:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

The workflow builds on Windows, runs the artwork tests, creates a portable ZIP,
and publishes the ZIP, EXE, and SHA-256 checksum file to a GitHub Release.
The workflow needs permission to write repository contents for the release job;
the regular build job only has read permission.

For subsequent versions, update `src/AssemblyInfo.cs`, `app.manifest`, the default
version in `package.ps1`, and `CHANGELOG.md` before tagging.

## Manual Release

```powershell
.\test.ps1
.\package.ps1 -Version 1.0.0
```

Upload the ZIP and `SHA256SUMS.txt` from `release/`, plus the matching EXE from
`portable/`, to a new GitHub Release. Do not upload local Steam files or cached
game images. Only the EXE is required to run the app.
