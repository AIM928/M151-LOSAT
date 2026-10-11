param(
    [Parameter(Mandatory=$true)][string]$GameRoot,
    [string]$OutputDir = (Join-Path $PSScriptRoot '..\test-output'),
    [string]$CompilerPath
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Compiler.ps1')
$compiler = Resolve-RoslynCompiler $CompilerPath
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$managed = Join-Path $GameRoot 'GHPC_Data\Managed'
$references = @('mscorlib.dll', 'System.dll', 'System.Core.dll') | ForEach-Object { Join-Path $managed $_ }
foreach ($reference in $references) {
    if (-not (Test-Path -LiteralPath $reference)) { throw "Missing local reference: $reference" }
}
$cases = @(
    @{Name='guidance-channels'; Test='test-guidance-channels.cs'; Sources=@('GuidanceChannels.cs','TargetLockRules.cs','SectorPresentationRules.cs','FixedLauncherRules.cs')},
    @{Name='vehicle-display-name'; Test='test-vehicle-display-name.cs'; Sources=@('VehicleDisplayName.cs')},
    @{Name='vehicle-display-name-missing'; Test='test-vehicle-display-name.cs'; Sources=@('VehicleDisplayName.cs'); Define='MISSING_NAME'},
    @{Name='launch-volume'; Test='test-launch-volume.cs'; Sources=@('LaunchAudioSettings.cs','LaunchVolumeRules.cs')}
)
foreach ($case in $cases) {
    if (Test-Path -LiteralPath (Join-Path $OutputDir ($case.Name+'.exe'))) { throw 'Choose a fresh test output directory.' }
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
foreach ($case in $cases) {
    $exe = Join-Path $OutputDir ($case.Name+'.exe')
    $arguments = @($compiler, '/nologo', '/target:exe', '/langversion:latest', "/out:$exe")
    $arguments += $references | ForEach-Object { "/reference:$_" }
    if ($case.Define) { $arguments += "/define:$($case.Define)" }
    $arguments += Join-Path $root ('tests\'+$case.Test)
    $arguments += $case.Sources | ForEach-Object { Join-Path $root ('src\'+$_) }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Test compilation failed: $($case.Name)" }
    & $exe
    if ($LASTEXITCODE -ne 0) { throw "Test failed: $($case.Name)" }
}
Write-Output 'Four offline rule tests passed. These are not gameplay, FPS, smoke, replay or audio-listening tests.'
