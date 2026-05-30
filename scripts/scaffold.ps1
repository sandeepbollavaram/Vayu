#requires -Version 7.0
<#
.SYNOPSIS
  Scaffolds the Vayu solution and all projects (.NET 10).

.DESCRIPTION
  Run this ONCE from the repo root, after cloning. It creates:
    Vayu.slnx (or Vayu.sln if neither already exists)
    apps/Vayu.Desktop  (WinUI 3 packaged app)
    apps/Vayu.Tray     (Worker)
    apps/Vayu.Cli      (Console)
    src/Vayu.*         (15 class libraries)
    tests/Vayu.*.Tests (5 xUnit projects)

  Idempotent: skips anything that already exists.

.NOTES
  Requires .NET 10 SDK.
  Install WinUI templates first:
    dotnet new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates
#>

[CmdletBinding()]
param(
  [string]$Root = (Get-Location).Path,
  [string]$Tfm  = 'net10.0'
)

$ErrorActionPreference = 'Stop'

function New-Std {
  param([string]$Template, [string]$Path, [string]$Name)
  $full = Join-Path $Root $Path
  if (Test-Path (Join-Path $full "$Name.csproj")) {
    Write-Host "  exists  $Path/$Name.csproj" -ForegroundColor DarkGray
    return
  }
  New-Item -ItemType Directory -Force $full | Out-Null
  & dotnet new $Template --name $Name --output $full --framework $Tfm | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "dotnet new $Template failed for $Name" }
  Write-Host "  created $Path/$Name.csproj" -ForegroundColor Green
}

function New-WinUI {
  param([string]$Path, [string]$Name)
  $full = Join-Path $Root $Path
  if (Test-Path (Join-Path $full "$Name.csproj")) {
    Write-Host "  exists  $Path/$Name.csproj" -ForegroundColor DarkGray
    return
  }
  New-Item -ItemType Directory -Force $full | Out-Null
  & dotnet new winui --name $Name --output $full -tfm $Tfm | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "dotnet new winui failed for $Name" }
  Write-Host "  created $Path/$Name.csproj (WinUI 3)" -ForegroundColor Green
}

Write-Host "Vayu scaffold starting in: $Root (TFM: $Tfm)" -ForegroundColor Cyan

# 1. Solution
$slnxPath = Join-Path $Root 'Vayu.slnx'
$slnPath  = Join-Path $Root 'Vayu.sln'
if ((Test-Path $slnxPath) -or (Test-Path $slnPath)) {
  $existing = if (Test-Path $slnxPath) { 'Vayu.slnx' } else { 'Vayu.sln' }
  Write-Host "  exists  $existing" -ForegroundColor DarkGray
} else {
  & dotnet new sln --name Vayu --output $Root | Out-Null
  Write-Host "  created Vayu.sln" -ForegroundColor Green
}

# 2. Apps
New-WinUI -Path 'apps/Vayu.Desktop' -Name 'Vayu.Desktop'
New-Std   -Template 'worker'  -Path 'apps/Vayu.Tray' -Name 'Vayu.Tray'
New-Std   -Template 'console' -Path 'apps/Vayu.Cli'  -Name 'Vayu.Cli'

# 3. Libraries
$libs = @(
  'Vayu.Core',
  'Vayu.AgentRuntime',
  'Vayu.Permissions',
  'Vayu.Automation.Windows',
  'Vayu.Voice',
  'Vayu.AI.Local',
  'Vayu.AI.Gemini',
  'Vayu.Memory',
  'Vayu.Security',
  'Vayu.Logging',
  'Vayu.Connectors.Gmail',
  'Vayu.Connectors.VSCode',
  'Vayu.Connectors.GitHub',
  'Vayu.Connectors.Music',
  'Vayu.Connectors.Files'
)
foreach ($lib in $libs) {
  New-Std -Template 'classlib' -Path "src/$lib" -Name $lib
}

# 4. Tests
$tests = @(
  'Vayu.Core.Tests',
  'Vayu.Security.Tests',
  'Vayu.Permissions.Tests',
  'Vayu.AgentRuntime.Tests',
  'Vayu.Memory.Tests'
)
foreach ($t in $tests) {
  New-Std -Template 'xunit' -Path "tests/$t" -Name $t
}

# 5. Add everything to the solution (Vayu.slnx preferred; falls back to Vayu.sln)
$slnFile = if (Test-Path $slnxPath) { $slnxPath } else { $slnPath }
Write-Host "Adding projects to $(Split-Path -Leaf $slnFile)..." -ForegroundColor Cyan
$allCsproj = Get-ChildItem -Path $Root -Recurse -Filter '*.csproj' |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
foreach ($p in $allCsproj) {
  & dotnet sln $slnFile add $p.FullName 2>$null | Out-Null
}

Write-Host ""
Write-Host "Done. Next:" -ForegroundColor Cyan
Write-Host "  dotnet restore"
Write-Host "  dotnet build"
Write-Host "  dotnet test"
