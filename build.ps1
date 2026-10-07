#!/usr/bin/env pwsh
# Jira WorkLog Assistant — Build Script

param(
    [ValidateSet("Debug","Release")]
    [string]$Configuration = "Debug",
    [switch]$Publish,
    [switch]$SelfContained,
    [switch]$Run
)

$ErrorActionPreference = "Stop"
$proj = "src\JiraWorkLogAssistant\JiraWorkLogAssistant.csproj"

Write-Host "=== Jira WorkLog Assistant Build ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray

# Restore
Write-Host "`nRestoring packages..." -ForegroundColor Yellow
dotnet restore $proj
if ($LASTEXITCODE -ne 0) { Write-Error "Restore failed"; exit 1 }

if ($Publish) {
    $outDir = if ($SelfContained) { "publish-sc" } else { "publish" }
    Write-Host "`nPublishing to .\$outDir ..." -ForegroundColor Yellow
    $args = @(
        "publish", $proj,
        "-c", "Release",
        "-r", "win-x64",
        "--self-contained", ($SelfContained ? "true" : "false"),
        "-p:PublishSingleFile=true",
        "-o", $outDir
    )
    dotnet @args
    if ($LASTEXITCODE -ne 0) { Write-Error "Publish failed"; exit 1 }
    Write-Host "`nPublished to .\$outDir\JiraWorkLogAssistant.exe" -ForegroundColor Green
} else {
    Write-Host "`nBuilding..." -ForegroundColor Yellow
    dotnet build $proj -c $Configuration
    if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }
}

if ($Run) {
    Write-Host "`nStarting application..." -ForegroundColor Green
    dotnet run --project $proj -c $Configuration
}

Write-Host "`nDone!" -ForegroundColor Green
