param(
    [Parameter(Mandatory=$true)][string]$GameRoot,
    [string]$OutputDir = (Join-Path $PSScriptRoot 'build'),
    [string]$ResourceRoot = (Join-Path $PSScriptRoot 'assets'),
    [string]$CompilerPath
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'src\build.ps1') @PSBoundParameters
