[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$compilerCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw 'The .NET Framework C# compiler was not found. Install/enable .NET Framework 4.8.'
}

$outputDirectory = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$outputFile = Join-Path $outputDirectory 'XInputRumbleTester.exe'
$mainSource = Join-Path $PSScriptRoot 'src\RumbleTester.cs'
$assemblyInfo = Join-Path $PSScriptRoot 'src\AssemblyInfo.cs'
$manifest = Join-Path $PSScriptRoot 'src\app.manifest'

& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /warn:4 /warnaserror+ `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
    "/win32manifest:$manifest" "/out:$outputFile" $mainSource $assemblyInfo
if ($LASTEXITCODE -ne 0) { throw "Compilation failed (exit $LASTEXITCODE)." }

$hash = (Get-FileHash -LiteralPath $outputFile -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $outputDirectory 'SHA256SUMS.txt'), "$hash  XInputRumbleTester.exe`n", [Text.Encoding]::ASCII)
Write-Output "Built: $outputFile"
Write-Output "SHA256: $hash"
