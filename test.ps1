$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$testOutput = Join-Path $root 'test-output'
New-Item -ItemType Directory -Force -Path $testOutput | Out-Null
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path -LiteralPath $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
$testExe = Join-Path $testOutput 'ArtworkTests.exe'
& $csc /nologo /target:exe /optimize+ "/out:$testExe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll (Join-Path $root 'src\SteamArtwork.cs') (Join-Path $root 'tests\ArtworkTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
& $testExe $testOutput
if ($LASTEXITCODE -ne 0) { throw 'Artwork tests failed' }
