[CmdletBinding()]
param(
    [ValidateSet('All', 'FrameworkDependent', 'X86', 'SingleFile')]
    [string]$Configuration = 'All'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $repoRoot 'SpecRunner/src/SpecRunner.Console/SpecRunner.Console.csproj'
$outputDir = Join-Path $PSScriptRoot '.specrunner'

function Invoke-Publish {
    param(
        [string]$Name,
        [string[]]$PublishArgs
    )

    Write-Host "Publishing $Name..."
    & dotnet publish $csproj -c Release -o $outputDir @PublishArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Publish failed for configuration '$Name' (exit code $LASTEXITCODE)."
        exit 1
    }
}

function Publish-FrameworkDependent {
    Invoke-Publish -Name 'FrameworkDependent' -PublishArgs @()
}

function Publish-X86 {
    Invoke-Publish -Name 'X86' -PublishArgs @('-r', 'win-x86', '--self-contained', 'false')
}

function Publish-SingleFile {
    Invoke-Publish -Name 'SingleFile' -PublishArgs @('-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=true')
}

switch ($Configuration) {
    'FrameworkDependent' { Publish-FrameworkDependent }
    'X86' { Publish-X86 }
    'SingleFile' { Publish-SingleFile }
    'All' {
        Publish-FrameworkDependent
        Publish-X86
        Publish-SingleFile
    }
}
