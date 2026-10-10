param([Parameter(Mandatory=$true)][string]$UnityEditor,[string]$Reference='3d19038a16e033d222692acccfac0b2c238f2e0e')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$folder=Join-Path $repo 'unity-prototype/Assets/LocalReference'
New-Item -ItemType Directory -Force $folder | Out-Null
if($Reference -notmatch '^[a-fA-F0-9]{7,40}$'){throw 'Reference must be a commit SHA'}
$source=(& git -C $repo show "$($Reference):unity-prototype/Assets/Scripts/StressEffects.cs") -join "`n"
if($LASTEXITCODE -ne 0){throw 'Cannot read reference effects implementation'}
$source=$source.Replace('public sealed class StressEffects','public sealed class EffectsReference').Replace('Time.unscaledDeltaTime','deltaTime').Replace('void LateUpdate() {',"void LateUpdate() { AdvanceAndRender(Time.unscaledDeltaTime); }`n    public void AdvanceAndRender(float deltaTime) {")
[IO.File]::WriteAllText((Join-Path $folder 'EffectsReference.cs'),$source,[Text.UTF8Encoding]::new($false))
$destination=Join-Path $repo '.cache/unity-effects-cpu'
New-Item -ItemType Directory -Force $destination | Out-Null
$env:STRONGHOLD_FX_CPU_RESULT=Join-Path $destination 'result.json'
$launchArgs=@('-batchmode','-quit','-projectPath',('"'+(Join-Path $repo 'unity-prototype')+'"'),'-executeMethod','EffectsCpuBenchmark.Run','-logFile',('"'+(Join-Path $destination 'benchmark.log')+'"'))
$process=Start-Process -FilePath $UnityEditor -ArgumentList $launchArgs -WindowStyle Hidden -PassThru
try{
    $deadline=(Get-Date).AddMinutes(5)
    while(!$process.HasExited -and (Get-Date) -lt $deadline){Start-Sleep -Seconds 1}
    if(!$process.HasExited){throw 'Effects CPU benchmark timed out'}
    $process.WaitForExit()
    if($process.ExitCode -ne 0){throw 'Effects CPU benchmark failed; inspect .cache/unity-effects-cpu/benchmark.log'}
    Get-Content -LiteralPath $env:STRONGHOLD_FX_CPU_RESULT
}finally{
    if(!$process.HasExited){Stop-Process -Id $process.Id}
    Remove-Item -LiteralPath (Join-Path $folder 'EffectsReference.cs'),(Join-Path $folder 'EffectsReference.cs.meta') -ErrorAction SilentlyContinue
}
