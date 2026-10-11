function Resolve-RoslynCompiler([string]$CompilerPath) {
    if ($CompilerPath) {
        if (-not (Test-Path -LiteralPath $CompilerPath -PathType Leaf)) { throw 'CompilerPath must point to Roslyn csc.dll.' }
        return [IO.Path]::GetFullPath($CompilerPath)
    }
    $sdkLines = & dotnet --list-sdks
    if ($LASTEXITCODE -ne 0) { throw 'Install a .NET SDK or provide -CompilerPath.' }
    $sdks = @($sdkLines | ForEach-Object {
        if ($_ -match '^([0-9]+\.[0-9]+\.[0-9]+)[^ ]* \[(.+)\]$') {
            [PSCustomObject]@{ Version = [Version]$Matches[1]; Root = $Matches[2]; Folder = ($_ -split ' ')[0] }
        }
    })
    # Prefer the tested SDK family, without requiring its original installation path.
    $sdk = $sdks | Where-Object { $_.Version.Major -eq 7 } | Sort-Object Version -Descending | Select-Object -First 1
    if (-not $sdk) { $sdk = $sdks | Sort-Object Version -Descending | Select-Object -First 1 }
    if (-not $sdk) { throw 'No .NET SDK found. Tested compiler: SDK 7.0.410.' }
    $candidate = Join-Path $sdk.Root (Join-Path $sdk.Folder 'Roslyn\bincore\csc.dll')
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw 'Roslyn compiler not found in selected SDK.' }
    return $candidate
}
