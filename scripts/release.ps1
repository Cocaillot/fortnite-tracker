<#
.SYNOPSIS
  Builds the installer (Setup.exe) and update packages with Velopack, optionally publishing them
  to GitHub Releases so installed copies update themselves.

.EXAMPLE
  # Local build only: output in .\releases
  .\scripts\release.ps1 -Version 0.1.0

.EXAMPLE
  # Build and publish a GitHub release (needs a token with "contents: write" on the repo)
  $env:GITHUB_TOKEN = "<token>"
  .\scripts\release.ps1 -Version 0.2.0 -RepoUrl https://github.com/<you>/fortnite-tracker -DiscordClientId 123456789 -Upload
#>
param(
    [Parameter(Mandatory)] [string] $Version,
    # GitHub repository installed copies check for updates. Also used to fetch the previous
    # release so Velopack can build small delta updates.
    [string] $RepoUrl,
    # Discord application ID for Rich Presence (Discord Developer Portal → your app → Application ID).
    [string] $DiscordClientId,
    [string] $Token = $env:GITHUB_TOKEN,
    [switch] $Upload
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$publishDir = Join-Path $root 'publish'
$releasesDir = Join-Path $root 'releases'
$project = Join-Path $root 'src\FortniteTracker.Desktop'

function Invoke-Step([string] $name, [scriptblock] $command) {
    Write-Host "==> $name" -ForegroundColor Cyan
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$name failed (exit code $LASTEXITCODE)" }
}

Push-Location $root
try {
    Invoke-Step 'Restore tools' { dotnet tool restore }

    if ($RepoUrl) {
        # Previous release, for delta packages. Fails harmlessly on the very first release.
        Write-Host '==> Download previous release' -ForegroundColor Cyan
        dotnet vpk download github --repoUrl $RepoUrl -o $releasesDir --token $Token
        if ($LASTEXITCODE -ne 0) { Write-Host '    (none found, building a full release only)' }
    }

    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    # Self-contained: friends don't need to install .NET first.
    Invoke-Step 'Publish' {
        dotnet publish $project -c Release -r win-x64 --self-contained true -p:Version=$Version -o $publishDir
    }

    # Release-specific settings go into the published copy only; the repo's appsettings.json stays empty.
    $settingsPath = Join-Path $publishDir 'appsettings.json'
    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
    if ($RepoUrl) { $settings.Updates.Url = $RepoUrl }
    if ($DiscordClientId) { $settings.Discord.ClientId = $DiscordClientId }
    $settings | ConvertTo-Json -Depth 5 | Set-Content $settingsPath -Encoding utf8

    Invoke-Step 'Pack' {
        dotnet vpk pack `
            --packId FortniteTracker `
            --packVersion $Version `
            --packDir $publishDir `
            --mainExe FortniteTracker.exe `
            --packTitle 'Fortnite Tracker' `
            --icon (Join-Path $project 'Assets\app.ico') `
            --framework webview2 `
            --outputDir $releasesDir
    }

    if ($Upload) {
        if (-not $RepoUrl -or -not $Token) { throw '-Upload needs -RepoUrl and a token (-Token or $env:GITHUB_TOKEN).' }
        Invoke-Step 'Upload to GitHub Releases' {
            dotnet vpk upload github --repoUrl $RepoUrl --token $Token -o $releasesDir `
                --publish --releaseName "Fortnite Tracker $Version" --tag "v$Version"
        }
        Write-Host "`nShare this link: $RepoUrl/releases/latest/download/FortniteTracker-win-Setup.exe" -ForegroundColor Green
    }
    else {
        Write-Host "`nInstaller: $(Join-Path $releasesDir 'FortniteTracker-win-Setup.exe')" -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
