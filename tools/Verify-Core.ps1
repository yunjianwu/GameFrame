param([string]$OutputRoot = 'E:\AI\_DownLoads\unity-gf-demos\build\core')
$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot 'CoreTests\CoreTests.csproj'
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
& dotnet build $projectPath --artifacts-path $OutputRoot --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Core compilation failed.' }
$runner = Join-Path $OutputRoot 'bin\CoreTests\debug\CoreTests.dll'
& dotnet $runner
if ($LASTEXITCODE -ne 0) { throw 'Core regression tests failed.' }
