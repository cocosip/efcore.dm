#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Packs the provider and verifies the package the way a consumer restores it.

.DESCRIPTION
    Packs into artifacts/packages, restores tests/DM.EntityFrameworkCore.PackageConsumer into an
    isolated NuGet package cache, compares the SHA-256 of the package the consumer resolved with the
    packed one, then runs the consumer. A cache shared with the build output can silently validate an
    older package, so the cache and the feed are both private to this run.

    The consumer needs no database, which is why this script runs on a build machine.
#>

[CmdletBinding()]
param(
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$feed = Join-Path $root 'artifacts' 'packages'
$consumer = Join-Path $root 'tests' 'DM.EntityFrameworkCore.PackageConsumer'
$providerProject = Join-Path $root 'src' 'DM.EntityFrameworkCore' 'DM.EntityFrameworkCore.csproj'
$cache = Join-Path ([System.IO.Path]::GetTempPath()) ('dm-consumer-' + [guid]::NewGuid().ToString('N'))
$previousCache = $env:NUGET_PACKAGES
$shared = if ($previousCache) { $previousCache } else { Join-Path $HOME '.nuget' 'packages' }

function Invoke-DotNet {
    param([string[]] $Arguments)

    & dotnet @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

try {
    Remove-Item $feed -Recurse -Force -ErrorAction Ignore

    Write-Host "Packing into $feed"
    Invoke-DotNet @('pack', $providerProject, '-c', $Configuration, '-o', $feed, '-v', 'quiet')

    $nupkg = Get-ChildItem $feed -Filter '*.nupkg' | Select-Object -First 1
    if (-not $nupkg) {
        throw "No package was produced in $feed"
    }

    $identity = Invoke-DotNet @('msbuild', $providerProject, '-getProperty:PackageId', '-getProperty:Version') |
        Out-String |
        ConvertFrom-Json |
        Select-Object -ExpandProperty Properties

    $packedHash = (Get-FileHash $nupkg.FullName -Algorithm SHA256).Hash
    Write-Host "Packed $($identity.PackageId) $($identity.Version) ($packedHash)"

    Write-Host "Restoring the consumer into $cache"

    New-Item -ItemType Directory -Path $cache -Force | Out-Null

    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <fallbackPackageFolders>
    <add key="shared" value="$shared" />
  </fallbackPackageFolders>
</configuration>
"@ | Set-Content (Join-Path $cache 'NuGet.config') -Encoding utf8

    $env:NUGET_PACKAGES = $cache

    Invoke-DotNet @(
        'restore', $consumer,
        '--configfile', (Join-Path $cache 'NuGet.config'),
        '-v', 'quiet')

    $resolved = Join-Path $cache $identity.PackageId.ToLowerInvariant() |
        Join-Path -ChildPath $identity.Version |
        Join-Path -ChildPath "$($identity.PackageId).$($identity.Version).nupkg"

    if (-not (Test-Path $resolved)) {
        throw "The consumer did not resolve $($identity.PackageId) $($identity.Version): $resolved is missing"
    }

    $resolvedHash = (Get-FileHash $resolved -Algorithm SHA256).Hash

    if ($resolvedHash -ne $packedHash) {
        throw "The consumer resolved $resolvedHash but $packedHash was packed"
    }

    Write-Host 'Running the consumer'

    Invoke-DotNet @('run', '--project', $consumer, '-c', $Configuration, '--no-restore', '-v', 'quiet')

    Write-Host 'Package smoke test passed'
}
finally {
    if ($previousCache) {
        $env:NUGET_PACKAGES = $previousCache
    }
    else {
        Remove-Item Env:\NUGET_PACKAGES -ErrorAction Ignore
    }

    Remove-Item $cache -Recurse -Force -ErrorAction Ignore
}
