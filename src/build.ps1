param(
    [Parameter(Mandatory=$true)][string]$GameRoot,
    [string]$OutputDir = (Join-Path $PSScriptRoot '..\build'),
    [string]$ResourceRoot = (Join-Path $PSScriptRoot '..\assets'),
    [string]$CompilerPath
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\scripts\Compiler.ps1')
$compiler = Resolve-RoslynCompiler $CompilerPath
$managed = Join-Path $GameRoot 'GHPC_Data\Managed'
$source = Join-Path $PSScriptRoot 'Mod.cs'
$visualSource = Join-Path $PSScriptRoot 'VisualSwap.cs'
$assetRoot = $ResourceRoot
$mesh = Join-Path $assetRoot 'humvee-painted.meshbin'
$launcherMesh = Join-Path $assetRoot 'losat-four-launcher.meshbin'
$texture = Join-Path $assetRoot 'humvee-weathered-woodland.png'
$output = Join-Path $OutputDir 'M151LOSATTest.dll'

if (Test-Path -LiteralPath $output) { throw 'Output DLL already exists. Choose a new -OutputDir; no released DLL is overwritten.' }

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$references = @(
    'mscorlib.dll',
    'System.dll',
    'System.Core.dll',
    'netstandard.dll',
    'Assembly-CSharp.dll',
    'UnityEngine.CoreModule.dll',
    'UnityEngine.AudioModule.dll',
    'UnityEngine.ParticleSystemModule.dll',
    'UnityEngine.PhysicsModule.dll',
    'UnityEngine.TerrainPhysicsModule.dll',
    'UnityEngine.AnimationModule.dll',
    'UnityEngine.ImageConversionModule.dll',
    'UnityEngine.InputLegacyModule.dll',
    'UnityEngine.IMGUIModule.dll',
    'UnityEngine.TextRenderingModule.dll',
    'Unity.Postprocessing.Runtime.dll',
    'Unity.TextMeshPro.dll'
    'FMODUnity.dll'
) | ForEach-Object { Join-Path $managed $_ }
$references += Join-Path $GameRoot 'MelonLoader\net35\MelonLoader.dll'
$references += Join-Path $GameRoot 'MelonLoader\net35\0Harmony.dll'
$references += Join-Path $managed 'Rewired_Core.dll'
foreach ($reference in $references) {
    if (-not (Test-Path -LiteralPath $reference -PathType Leaf)) { throw "Missing local game/loader reference: $reference" }
}

$arguments = @(
    $compiler,
    '/nologo',
    '/target:library',
    '/langversion:latest',
    "/out:$output"
)
$arguments += $references | ForEach-Object { "/reference:$_" }
$arguments += $source
$arguments += Join-Path $PSScriptRoot 'VehicleDisplayName.cs'
$arguments += Join-Path $PSScriptRoot 'DiscoveryBudget.cs'
$arguments += Join-Path $PSScriptRoot 'LaunchDataRules.cs'
$arguments += Join-Path $PSScriptRoot 'IncrementalDiscovery.cs'
$arguments += $visualSource
$arguments += Join-Path $PSScriptRoot 'SurfaceFinish.cs'
$arguments += Join-Path $PSScriptRoot 'SurfaceFinishRules.cs'
$arguments += Join-Path $PSScriptRoot 'ArmorProtection.cs'
$arguments += Join-Path $PSScriptRoot 'FourTubeSalvo.cs'
$arguments += Join-Path $PSScriptRoot 'SalvoSequence.cs'
$arguments += Join-Path $PSScriptRoot 'BodyDetails.cs'
$arguments += Join-Path $PSScriptRoot 'LOSATOptics.cs'
$arguments += Join-Path $PSScriptRoot 'PerformanceCompatibility.cs'
$arguments += Join-Path $PSScriptRoot 'PerformanceRules.cs'
$arguments += Join-Path $PSScriptRoot 'TargetLock.cs'
$arguments += Join-Path $PSScriptRoot 'TargetLockRules.cs'
$arguments += Join-Path $PSScriptRoot 'TargetSalvoPlan.cs'
$arguments += Join-Path $PSScriptRoot 'TargetAttackState.cs'
$arguments += Join-Path $PSScriptRoot 'AttackConfirmation.cs'
$arguments += Join-Path $PSScriptRoot 'MissileReplay.cs'
$arguments += Join-Path $PSScriptRoot 'GuidanceChannels.cs'
$arguments += Join-Path $PSScriptRoot 'GuidanceControl.cs'
$arguments += Join-Path $PSScriptRoot 'GuidanceLamp.cs'
$arguments += Join-Path $PSScriptRoot 'LockOcclusion.cs'
$arguments += Join-Path $PSScriptRoot 'LockRecoveryRules.cs'
$arguments += Join-Path $PSScriptRoot 'LockTracking.cs'
$arguments += Join-Path $PSScriptRoot 'SmokeProvenance.cs'
$arguments += Join-Path $PSScriptRoot 'MissileTargetAssignment.cs'
$arguments += Join-Path $PSScriptRoot 'SuspensionRules.cs'
$arguments += Join-Path $PSScriptRoot 'SightHud.cs'
$arguments += Join-Path $PSScriptRoot 'SightHudRules.cs'
$arguments += Join-Path $PSScriptRoot 'LockFeedback.cs'
$arguments += Join-Path $PSScriptRoot 'LockToneRules.cs'
$arguments += Join-Path $PSScriptRoot 'StraightFlight.cs'
$arguments += Join-Path $PSScriptRoot 'NativeReticleVisibility.cs'
$arguments += Join-Path $PSScriptRoot 'AntiEra.cs'
$arguments += Join-Path $PSScriptRoot 'EraOptimizationRules.cs'
$arguments += Join-Path $PSScriptRoot 'ShallowArcRules.cs'
$arguments += Join-Path $PSScriptRoot 'ShallowArc.cs'
$arguments += Join-Path $PSScriptRoot 'ManualSteeringRules.cs'
$arguments += Join-Path $PSScriptRoot 'ManualSightGuidance.cs'
$arguments += Join-Path $PSScriptRoot 'LaunchAudio.cs'
$arguments += Join-Path $PSScriptRoot 'LaunchAudioSettings.cs'
$arguments += Join-Path $PSScriptRoot 'LaunchVolumeRules.cs'
$arguments += Join-Path $PSScriptRoot 'ReloadAudio.cs'
$arguments += Join-Path $PSScriptRoot 'ReloadSoundSequence.cs'
$arguments += Join-Path $PSScriptRoot 'LaunchEffects.cs'
$arguments += Join-Path $PSScriptRoot 'TankImpactSmoke.cs'
$arguments += Join-Path $PSScriptRoot 'ImpactSmokeRules.cs'
$arguments += Join-Path $PSScriptRoot 'TowSmokeRules.cs'
$arguments += Join-Path $PSScriptRoot 'MotorFlame.cs'
$arguments += Join-Path $PSScriptRoot 'MotorFlameRules.cs'
$arguments += Join-Path $PSScriptRoot 'LockViewRules.cs'
$arguments += Join-Path $PSScriptRoot 'LockViewContext.cs'
$arguments += Join-Path $PSScriptRoot 'CabinInterior.cs'
$arguments += Join-Path $PSScriptRoot 'CabinViews.cs'
$arguments += Join-Path $PSScriptRoot 'CrewSeats.cs'
$arguments += Join-Path $PSScriptRoot 'CabinMobilityRules.cs'
$arguments += Join-Path $PSScriptRoot 'CabinMobility.cs'
$arguments += Join-Path $PSScriptRoot 'CenterTargetLock.cs'
$arguments += Join-Path $PSScriptRoot 'TargetPickRules.cs'
$arguments += Join-Path $PSScriptRoot 'FixedLauncherRules.cs'
$arguments += Join-Path $PSScriptRoot 'LaunchSector.cs'
$arguments += Join-Path $PSScriptRoot 'SectorPresentationRules.cs'
$arguments += Join-Path $PSScriptRoot 'SectorVoice.cs'
$arguments += "/resource:$assetRoot\Turn_Left_Female.wav,M151LOSATTest.Voice.Left"
$arguments += "/resource:$assetRoot\Turn_Right_Female.wav,M151LOSATTest.Voice.Right"
$arguments += "/resource:$assetRoot\LOSAT_Launch_Restored.wav,M151LOSATTest.Launch.Wave"
$arguments += "/resource:$mesh,M151LOSATTest.Humvee.Mesh"
$arguments += "/resource:$launcherMesh,M151LOSATTest.Launcher.Mesh"
$arguments += "/resource:$texture,M151LOSATTest.Humvee.Texture"
$arguments += "/resource:$assetRoot\humvee-details.meshbin,M151LOSATTest.Humvee.Details"
$arguments += "/resource:$assetRoot\humvee-cabin.meshbin,M151LOSATTest.Humvee.Cabin"
$arguments += "/resource:$assetRoot\humvee-paint-normal.png,M151LOSATTest.Humvee.Normal"
$arguments += "/resource:$assetRoot\humvee-panel-occlusion.png,M151LOSATTest.Humvee.Occlusion"
$arguments += "/resource:$assetRoot\humvee-paint-finish.png,M151LOSATTest.Humvee.Finish"
foreach ($argument in $arguments | Where-Object { $_.StartsWith('/resource:') }) {
    $resourceFile = $argument.Substring(10).Split(',')[0]
    if (-not (Test-Path -LiteralPath $resourceFile -PathType Leaf)) { throw "Missing resource: $resourceFile. See BUILDING.md." }
}

& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE" }

$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $output).Hash
$sourceHashes=@{}
foreach($inputSource in $arguments | Where-Object {$_.EndsWith('.cs')}) {
    $sourceHashes[[IO.Path]::GetFileName($inputSource)]=(Get-FileHash -Algorithm SHA256 -LiteralPath $inputSource).Hash
}
@{dll_sha256=$hash;source_sha256=$sourceHashes} | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $OutputDir 'build-manifest.json') -Encoding UTF8
Write-Output "Built: $output"
Write-Output "SHA256: $hash"
