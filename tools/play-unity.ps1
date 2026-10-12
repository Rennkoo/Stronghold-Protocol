param([switch]$Hidden)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $repo 'unity-prototype/Builds/Windows/StrongholdBenchmark.exe'
$bridge=Join-Path $repo 'tools/unity-play-server.mjs'
if(!(Test-Path -LiteralPath $exe)){throw 'Build the Unity Windows player first'}
$log=Join-Path $repo '.cache/unity-play'
New-Item -ItemType Directory -Force $log | Out-Null
$server=Start-Process -FilePath (Get-Command node).Source -ArgumentList ('"'+$bridge+'"') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $log 'server.log') -RedirectStandardError (Join-Path $log 'server-error.log')
try{
    Start-Sleep -Seconds 2
    if($server.HasExited){throw 'Local bridge failed to start; inspect .cache/unity-play/server-error.log'}
    $launchArgs=@('-play','-screen-width','1280','-screen-height','720','-screen-fullscreen','0','-logFile',('"'+(Join-Path $log 'player.log')+'"'))
    if($Hidden){$player=Start-Process -FilePath $exe -ArgumentList $launchArgs -WindowStyle Hidden -PassThru}
    else{$player=Start-Process -FilePath $exe -ArgumentList $launchArgs -PassThru}
    while(!$player.HasExited){Start-Sleep -Seconds 1}
}finally{if(!$server.HasExited){Stop-Process -Id $server.Id}}
