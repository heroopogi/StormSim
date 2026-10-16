param([string]$Action='status',[string]$CodeFile='',[int]$WaitSeconds=20)
$ErrorActionPreference='Stop'
$workspace=Resolve-Path (Join-Path $PSScriptRoot '../..')
$request=Join-Path $workspace 'Temp/stormsim-validation-request.json'
$response=Join-Path $workspace 'Temp/stormsim-validation-response.json'
if(Test-Path -LiteralPath $request){throw 'A Unity request is already pending.'}
if(Test-Path -LiteralPath $response){Remove-Item -LiteralPath $response}
$job=@{action=$Action}
if($CodeFile){$job.code=Get-Content -LiteralPath $CodeFile -Raw}
$pending=Join-Path $workspace 'Temp/stormsim-validation-request.pending'
[System.IO.File]::WriteAllText($pending,($job|ConvertTo-Json -Compress),[System.Text.UTF8Encoding]::new($false))
Move-Item -LiteralPath $pending -Destination $request
$deadline=[DateTime]::UtcNow.AddSeconds($WaitSeconds)
while([DateTime]::UtcNow -lt $deadline){if(Test-Path -LiteralPath $response){Get-Content -LiteralPath $response -Raw;exit 0};Start-Sleep -Milliseconds 200}
Write-Output 'Request queued; Unity has not returned yet.'
