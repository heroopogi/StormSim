param(
    [ValidateSet('benchmark','recovery','startup')][string]$Kind='benchmark',
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-zA-Z0-9-]+$')][string]$Label
)
$ErrorActionPreference='Stop'
$workspace=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$candidate=Join-Path $workspace $(if($Kind -eq 'startup'){'output/DesktopRelease'}else{'output/DesktopCandidate'})
$executable=Join-Path $candidate 'StormSim.exe'
$buildId=Join-Path $candidate 'BUILD-ID.txt'
if(!(Test-Path -LiteralPath $executable) -or !(Test-Path -LiteralPath $buildId)){throw 'A successful desktop candidate build is required.'}
$report=Join-Path $workspace ('Captures/Batches4-7_20260930/Standalone/'+$Label)
if(Test-Path -LiteralPath $report){throw 'Choose a new label to preserve existing validation evidence.'}
New-Item -ItemType Directory -Path $report | Out-Null
$log=Join-Path $report 'Player.log'
$switch=switch($Kind){'benchmark'{'-stormsim-benchmark'} 'recovery'{'-stormsim-recovery-checks'} 'startup'{'-stormsim-startup-check'}}
$arguments=@($switch,'-stormsim-report',('"'+$report+'"'),'-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-logFile',('"'+$log+'"'))
if($Kind -ne 'startup'){$arguments+='-stormsim-dev-controls'}
$started=[DateTime]::UtcNow
$systemMemory=Get-CimInstance Win32_OperatingSystem
$editorProcesses=@(Get-Process Unity -ErrorAction SilentlyContinue)
$hostConditions=@{availablePhysicalMemoryMB=[math]::Round($systemMemory.FreePhysicalMemory/1024);unityProcessCount=$editorProcesses.Count;unityWorkingMemoryMB=[math]::Round(($editorProcesses | Measure-Object WorkingSet64 -Sum).Sum/1MB)}
$validationProcess=Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $candidate -WindowStyle Hidden -PassThru
$record=@{kind=$Kind;label=$Label;pid=$validationProcess.Id;startedUtc=$started.ToString('O');report=$report;launchVisibility='Hidden background diagnostic';hostConditions=$hostConditions;build=(Get-Content -LiteralPath $buildId -Raw)}
$record | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $report 'launch.json') -Encoding utf8
$record | Select-Object kind,label,pid,report | ConvertTo-Json -Compress
