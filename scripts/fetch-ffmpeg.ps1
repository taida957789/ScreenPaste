# Downloads a static win64 ffmpeg.exe for the region-recording feature and places it
# at src/ScreenPaste/ffmpeg/ffmpeg.exe, where FFmpegLocator/the csproj expect it.
# Used by CI before publish; run it locally once to enable recording in dev builds.
#
#   pwsh scripts/fetch-ffmpeg.ps1
#
# BtbN builds are fully static (a single self-contained ffmpeg.exe, GPL) and include
# libx264 (mp4) and libwebp (webp) alongside gif support.

param(
    [string]$OutDir = (Join-Path $PSScriptRoot '..\src\ScreenPaste\ffmpeg'),
    [string]$Url = 'https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip'
)

$ErrorActionPreference = 'Stop'

$target = Join-Path $OutDir 'ffmpeg.exe'
if (Test-Path $target) {
    Write-Host "ffmpeg already present at $target — skipping download."
    exit 0
}

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("ffmpeg-dl-" + [System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
try {
    $zip = Join-Path $tmp 'ffmpeg.zip'
    Write-Host "Downloading ffmpeg from $Url ..."
    Invoke-WebRequest -Uri $Url -OutFile $zip

    Write-Host 'Extracting ...'
    Expand-Archive -Path $zip -DestinationPath $tmp -Force

    $exe = Get-ChildItem -Path $tmp -Recurse -Filter 'ffmpeg.exe' | Select-Object -First 1
    if (-not $exe) { throw 'ffmpeg.exe not found in the downloaded archive.' }

    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
    Copy-Item $exe.FullName $target -Force
    Write-Host "ffmpeg ready at $target"
}
finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
