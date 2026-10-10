param([int]$Runs=3,[int]$Samples=3600)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $repo 'unity-prototype/Builds/Windows/StrongholdBenchmark.exe'
$destination=Join-Path $repo '.cache/unity-measurements'
New-Item -ItemType Directory -Force $destination | Out-Null
$results=@()
for($run=1;$run -le $Runs;$run++) {
    $log=Join-Path $destination ("run-$run.log")
    # A prior run's Result line must never be accepted as this process's measurement.
    if(Test-Path -LiteralPath $log){Remove-Item -LiteralPath $log}
    $launchArgs=@('-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-benchmark-samples',"$Samples",'-logFile',('"'+$log+'"'))
    $process=Start-Process -FilePath $exe -ArgumentList $launchArgs -WindowStyle Hidden -PassThru
    $deadline=(Get-Date).AddMinutes(5)
    try {
        while((Get-Date) -lt $deadline) {
            if($process.HasExited) {throw 'Benchmark exited before completing'}
            if(Test-Path $log) {
                $text=Get-Content -LiteralPath $log -Raw
                if($text -match 'Models initialized: units=\d+ models=\d+ failures=([1-9]\d*)'){throw 'Invalid benchmark: models failed to load'}
                if($text -match 'Result: ([^\r\n]+\.json)') {
                    $path=$Matches[1].Trim()
                    $image=[IO.Path]::ChangeExtension($path,'.png')
                    if(!(Test-Path $image)){Start-Sleep -Milliseconds 200;continue}
                    $result=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
                    if($result.units -ne 120 -or $result.uniqueModels -ne 59 -or $result.effectsDropped -ne 0){throw 'Invalid benchmark: workload mismatch or FX pool overflow'}
                    Copy-Item -LiteralPath $path -Destination (Join-Path $destination "run-$run.json")
                    Copy-Item -LiteralPath $image -Destination (Join-Path $destination "run-$run.png")
                    $results+=$result
                    Write-Output ("Run {0}: {1:N2} FPS; p95 {2:N2}ms; peak FX {3}; dropped {4}" -f $run,$result.fps,$result.p95Ms,$result.peakEffects,$result.effectsDropped)
                    break
                }
            }
            Start-Sleep -Seconds 1
        }
        if($results.Count -lt $run){throw 'Benchmark timed out'}
    }finally{if(!$process.HasExited){Stop-Process -Id $process.Id}}
}
$results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $destination 'summary.json')
Write-Output (Join-Path $destination 'summary.json')
