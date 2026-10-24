# Contributing

Build and test on Windows with Windows PowerShell 5.1 and .NET Framework 4.x:

```powershell
.\build.ps1
.\test.ps1
.\package.ps1
```

Keep the executable portable and avoid new runtime dependencies. Submit a pull
request with a concise description and verification results. Use generated
fixtures for tests; do not commit personal Steam manifests or cached game art.
Changes to source assets must preserve the licensing exclusions in
THIRD_PARTY_NOTICES.md.
