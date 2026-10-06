$lib = Join-Path (Split-Path $PSScriptRoot) 'Profiles\Me\XIVLauncher\addon\Hooks\dev\' # the plugin sits in the FFXIV folder
$env:DALAMUD_HOME = $lib
Set-Location $PSScriptRoot
dotnet build LevelingCompanion/LevelingCompanion.csproj -c Release -p:DalamudLibPath=$lib -nologo -v m 2>&1 |
    Select-String -Pattern 'error|Build succeeded|Build FAILED|Elapsed|LevelingCompanion ->' |
    Select-Object -Last 10
