param([string]$Label='match-smoke')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$folder=Join-Path $repo '.cache/unity-match-tests'
New-Item -ItemType Directory -Force $folder | Out-Null
$taskLog=Join-Path $folder ($Label+'.log')
if(Test-Path -LiteralPath $taskLog){Remove-Item -LiteralPath $taskLog}
$bridge=Join-Path $repo 'tools/unity-play-server.mjs'
$taskBridge=Start-Process -FilePath (Get-Command node).Source -ArgumentList ('"'+$bridge+'" --match') -WorkingDirectory $repo -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $folder 'server.log') -RedirectStandardError (Join-Path $folder 'server-error.log')
try {
    Start-Sleep -Seconds 1
    if($taskBridge.HasExited){throw 'Bridge failed to start'}
    $exe=Join-Path $repo 'unity-prototype/Builds/Windows/StrongholdBenchmark.exe'
    $taskPlayer=Start-Process -FilePath $exe -ArgumentList @('-match','-match-smoke','-screen-width','1280','-screen-height','720','-screen-fullscreen','0','-logFile',('"'+$taskLog+'"')) -WindowStyle Hidden -PassThru
    try {
        $taskDeadline=(Get-Date).AddSeconds(40)
        do {
            Start-Sleep -Seconds 1
            $taskPassed=(Test-Path -LiteralPath $taskLog) -and (Select-String -LiteralPath $taskLog -Pattern 'UNITY MATCH SMOKE PASS' -Quiet)
        }while(!$taskPassed -and (Get-Date) -lt $taskDeadline -and !$taskPlayer.HasExited)
        if(!$taskPassed){throw 'Native Match smoke failed'}
        if(Select-String -LiteralPath $taskLog -Pattern 'Serialization depth|Exception|Error:' -Quiet){throw 'Native log contains an error'}
        Write-Output "Native Match smoke PASS: $taskLog"
    }finally{if(!$taskPlayer.HasExited){Stop-Process -Id $taskPlayer.Id}}
}finally{if(!$taskBridge.HasExited){Stop-Process -Id $taskBridge.Id}}
