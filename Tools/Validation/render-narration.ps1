$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Speech
$narrationDirectory = Join-Path $PSScriptRoot '../../Assets/StormSim/Sibucao/Art/Narration'
$narrationManifest = Get-Content -LiteralPath (Join-Path $narrationDirectory 'manifest.json') -Raw | ConvertFrom-Json
$oldManifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../Backups/Batches4-7_20260930/Narration/manifest.json') -Raw | ConvertFrom-Json
$narrationVoice = New-Object System.Speech.Synthesis.SpeechSynthesizer
$narrationVoice.SelectVoice('Microsoft David Desktop')
$narrationVoice.Rate = 0
$audioReport = @()
try {
    foreach ($cue in $narrationManifest.cues) {
        $oldCue = $oldManifest.cues | Where-Object { $_.key -eq $cue.key }
        if ($oldCue.text -eq $cue.text) { continue }
        $wavePath = Join-Path $narrationDirectory ($cue.key + '.wav')
        $narrationVoice.SetOutputToWaveFile($wavePath)
        $narrationVoice.Speak($cue.text)
        $narrationVoice.SetOutputToNull()
        $audioReport += [pscustomobject]@{ key=$cue.key; voice='Microsoft David Desktop (local synthesized speech)'; bytes=(Get-Item -LiteralPath $wavePath).Length; text=$cue.text }
    }
} finally { $narrationVoice.Dispose() }
$audioReport | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $PSScriptRoot '../../Captures/Batches4-7_20260930/narration-generation.json')
Write-Output ('Generated ' + $audioReport.Count + ' control-neutral narration clips; existing GUIDs retained.')
