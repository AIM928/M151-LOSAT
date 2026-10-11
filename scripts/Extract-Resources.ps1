param(
    [Parameter(Mandatory=$true)][string]$ReleaseDll,
    [string]$Destination = (Join-Path $PSScriptRoot '..\assets')
)
$ErrorActionPreference = 'Stop'
# Only accept the known published audio-update DLL. Never inspect/load an arbitrary download.
$expected = '9537824F443272A9879E2F8A27D0D3C22E8B52E1C749D46CDDCBB12BBC5AB3C7'
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $ReleaseDll).Hash -ne $expected) {
    throw 'Not the verified v0.7.58 DLL. Use the standalone DLL linked in BUILDING.md, not the old ZIP.'
}
$mapping = [ordered]@{
    'M151LOSATTest.Voice.Left' = 'Turn_Left_Female.wav'
    'M151LOSATTest.Voice.Right' = 'Turn_Right_Female.wav'
    'M151LOSATTest.Launch.Wave' = 'LOSAT_Launch_Restored.wav'
    'M151LOSATTest.Humvee.Mesh' = 'humvee-painted.meshbin'
    'M151LOSATTest.Launcher.Mesh' = 'losat-four-launcher.meshbin'
    'M151LOSATTest.Humvee.Texture' = 'humvee-weathered-woodland.png'
    'M151LOSATTest.Humvee.Details' = 'humvee-details.meshbin'
    'M151LOSATTest.Humvee.Cabin' = 'humvee-cabin.meshbin'
    'M151LOSATTest.Humvee.Normal' = 'humvee-paint-normal.png'
    'M151LOSATTest.Humvee.Occlusion' = 'humvee-panel-occlusion.png'
    'M151LOSATTest.Humvee.Finish' = 'humvee-paint-finish.png'
}
$assembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes([IO.Path]::GetFullPath($ReleaseDll)))
$names = $assembly.GetManifestResourceNames()
foreach ($entry in $mapping.GetEnumerator()) {
    if ($names -notcontains $entry.Key) { throw "Missing embedded resource: $($entry.Key)" }
    if (Test-Path -LiteralPath (Join-Path $Destination $entry.Value)) { throw 'Destination contains a resource. Choose a new directory; existing assets are not overwritten.' }
}
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
foreach ($entry in $mapping.GetEnumerator()) {
    $inputStream = $assembly.GetManifestResourceStream($entry.Key)
    $outputStream = $null
    try {
        $outputStream = [IO.File]::Open((Join-Path $Destination $entry.Value), [IO.FileMode]::CreateNew)
        $inputStream.CopyTo($outputStream)
    } finally {
        if ($outputStream) { $outputStream.Dispose() }
        $inputStream.Dispose()
    }
}
Write-Output 'Extracted 11 resources locally for rebuilding. This does not grant redistribution rights to third-party assets.'
