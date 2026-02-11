<#
.SYNOPSIS
    Publishes and signs the GameOfLife3D.NET executable using Azure Trusted Signing.

.DESCRIPTION
    1. Runs dotnet publish in Release configuration
    2. Signs the exe using the dotnet sign CLI with Azure Trusted Signing
    Requires Azure CLI login (az login) or appropriate environment variables.

.PARAMETER ConfigPath
    Path to the signing-config.json file. Defaults to signing/signing-config.json.

.EXAMPLE
    .\signing\Publish-And-Sign.ps1
#>
param(
    [string]$ConfigPath = "$PSScriptRoot\signing-config.json"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path $PSScriptRoot -Parent
$ProjectPath = Join-Path $RepoRoot "src\GameOfLife3D.NET"
$PublishDir = Join-Path $ProjectPath "bin\Release\net10.0\win-x64\publish"

# Read signing config
if (-not (Test-Path $ConfigPath)) {
    Write-Error "Signing config not found at: $ConfigPath"
    exit 1
}

$config = Get-Content $ConfigPath -Raw | ConvertFrom-Json

if ($config.CodeSigningAccountName -eq "YOUR_ACCOUNT_NAME") {
    Write-Error "Please update signing-config.json with your Azure Trusted Signing account details."
    exit 1
}

Write-Host "Publishing Release build..." -ForegroundColor Cyan
dotnet publish $ProjectPath --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit 1
}

Write-Host ""
Write-Host "Signing executable..." -ForegroundColor Cyan

# Restore local tools (ensures sign CLI is available)
dotnet tool restore --tool-manifest (Join-Path $RepoRoot "dotnet-tools.json")

dotnet sign code trusted-signing `
    --base-directory $PublishDir `
    --trusted-signing-endpoint $config.Endpoint `
    --trusted-signing-account $config.CodeSigningAccountName `
    --trusted-signing-certificate-profile $config.CertificateProfileName `
    --verbosity Information `
    "GameOfLife3D.NET.exe"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Signing failed."
    exit 1
}

Write-Host ""
Write-Host "Signed executable at: $PublishDir\GameOfLife3D.NET.exe" -ForegroundColor Green
