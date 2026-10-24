param([string]$Out = "$PSScriptRoot\..\out")
$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."
$cfg = "Release"

foreach ($p in "Boot", "Compiler", "Runtime", "Modes", "Legacy", "Shim", "Patcher") {
    dotnet build "$root\src\$p" -c $cfg | Out-Null
}

if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }
New-Item -ItemType Directory -Force $Out | Out-Null
$payload = Join-Path $Out "payload"
$homeDir = Join-Path $payload "deSwitcher"
$comp = Join-Path $payload "Compiler"
New-Item -ItemType Directory -Force $homeDir, $comp | Out-Null

Copy-Item "$root\src\Patcher\bin\$cfg\net48\*" $Out -Recurse -Force

Copy-Item "$root\src\Boot\bin\$cfg\net472\deSwitcher.Boot.dll" $payload -Force

$runtime = "$root\src\Modes\bin\$cfg\net472"
foreach ($f in "deSwitcher.Runtime.dll", "deSwitcher.Core.dll", "deSwitcher.Modes.dll", "deSwitcher.Shim.dll", "0Harmony.dll", "Mono.Cecil.dll", "Mono.Cecil.Rocks.dll", "Mono.Cecil.Mdb.dll", "Mono.Cecil.Pdb.dll") {
    if (Test-Path "$runtime\$f") { Copy-Item "$runtime\$f" $homeDir -Force }
}

Copy-Item "$root\src\Legacy\bin\$cfg\net472\deSwitcher.Legacy.dll" $homeDir -Force

Copy-Item "$root\src\Compiler\bin\$cfg\net48\*" $comp -Recurse -Force

Write-Output "packed to $Out"
