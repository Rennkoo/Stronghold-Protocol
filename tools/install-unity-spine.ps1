$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$cache = Join-Path $repo '.cache/unity-spine38'
$project = Join-Path $repo 'unity-prototype'
$runtime = Join-Path $project 'Assets/Spine/Runtime'
if (Test-Path $runtime) { throw 'Runtime already exists. Preserve existing installation; this installer only handles a fresh project.' }
New-Item -ItemType Directory -Force $cache | Out-Null
$package = Join-Path $repo '.cache/unity-spine38.unitypackage'
Invoke-WebRequest 'https://en.esotericsoftware.com/files/runtimes/unity/spine-unity-3.8-2021-11-10.unitypackage' -OutFile $package
tar -xzf $package -C $cache
if ($LASTEXITCODE -ne 0) { throw 'Package extraction failed' }
Get-ChildItem $cache -Directory | ForEach-Object {
    $pathname = Join-Path $_.FullName 'pathname'
    if (Test-Path $pathname) {
        $rel = (Get-Content $pathname -Raw).Trim()
        # Only runtime assets, shaders and license: old editor integration is incompatible with Unity 6.
        if ($rel.StartsWith('Assets/Spine/Runtime/') -or $rel -match '^Assets/Spine/[^/]+$') {
            $dest = [IO.Path]::GetFullPath((Join-Path $project $rel))
            if (!$dest.StartsWith([IO.Path]::GetFullPath($project) + [IO.Path]::DirectorySeparatorChar)) { throw 'Invalid package path' }
            $asset = Join-Path $_.FullName 'asset'
            if (Test-Path $asset) {
                New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
                Copy-Item -LiteralPath $asset -Destination $dest
                $meta = Join-Path $_.FullName 'asset.meta'
                if (Test-Path $meta) { Copy-Item -LiteralPath $meta -Destination ($dest + '.meta') }
            }
        }
    }
}
$mecanim = Join-Path $runtime 'spine-unity/Components/SkeletonMecanim.cs'
$code = Get-Content -LiteralPath $mecanim -Raw
$code = $code.Replace('return x.GetInstanceID() == y.GetInstanceID();', 'return x == y;').Replace('return o.GetInstanceID();', 'return o.GetHashCode();')
Set-Content -LiteralPath $mecanim -Value $code -NoNewline
Write-Output 'Installed official Spine 3.8 runtime with Unity 6 comparator compatibility patch.'
