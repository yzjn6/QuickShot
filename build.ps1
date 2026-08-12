$csc = Join-Path $env:windir 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$out = Join-Path $PSScriptRoot 'QuickShot.exe'
$src = Join-Path $PSScriptRoot 'src'
$font = Join-Path $PSScriptRoot 'Res\iconfont.ttf'
$icon = Join-Path $PSScriptRoot 'Res\logo.ico'

if (-not (Test-Path $csc)) {
    Write-Error "未找到 csc.exe：$csc"
    exit 1
}

& $csc /nologo /optimize+ /target:winexe /out:$out `
       /recurse:"$src\*.cs" `
       /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.dll `
       /resource:"$font,QuickShot.iconfont.ttf" `
       /win32icon:"$icon"

if ($LASTEXITCODE -eq 0) {
    $size = [math]::Round((Get-Item $out).Length / 1KB, 1)
    Write-Host "编译成功：$out （$size KB）"
} else {
    Write-Error "编译失败，退出码 $LASTEXITCODE"
    exit $LASTEXITCODE
}
