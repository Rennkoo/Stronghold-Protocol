$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $repo 'unity-prototype/Builds/Windows/StrongholdBenchmark.exe'
$destination=Join-Path $repo '.cache/unity-replay'
New-Item -ItemType Directory -Force $destination | Out-Null
$log=Join-Path $destination 'replay.log'
if(Test-Path -LiteralPath $log){Remove-Item -LiteralPath $log}
$started=Get-Date
$process=Start-Process -FilePath $exe -ArgumentList @('-replay','-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
try{
    $deadline=(Get-Date).AddMinutes(2)
    $complete=$false
    while((Get-Date) -lt $deadline){
        if($process.HasExited){throw 'Replay exited before completion'}
        if(Test-Path -LiteralPath $log){
            $text=Get-Content -LiteralPath $log -Raw
            if($text -match 'failures=([1-9]\d*)|Exception:'){throw 'Replay initialization/runtime error; inspect log'}
            if($text -match 'REPLAY RESULT: ([^\r\n]+\.json)'){
                $path=$Matches[1].Trim()
                if((Get-Item -LiteralPath $path).LastWriteTime -lt $started){throw 'Stale replay result'}
                Copy-Item -LiteralPath $path -Destination (Join-Path $destination 'result.json')
                foreach($name in @('replay-mid.png','replay-final.png')){
                    $image=Join-Path (Split-Path $path -Parent) $name
                    Copy-Item -LiteralPath $image -Destination (Join-Path $destination $name)
                }
                & node (Join-Path $repo 'tools/verify-unity-replay.mjs') (Join-Path $destination 'result.json')
                if($LASTEXITCODE -ne 0){throw 'Native replay differs from original recording'}
                $complete=$true;break
            }
        }
        Start-Sleep -Seconds 1
    }
    if(!$complete){throw 'Replay timed out'}
}finally{if(!$process.HasExited){Stop-Process -Id $process.Id}}
