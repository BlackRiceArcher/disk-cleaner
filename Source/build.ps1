$ErrorActionPreference = "Stop"

$src = $PSScriptRoot
$root = Split-Path $src -Parent
$csc = "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\Roslyn\csc.exe"
$fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"

if (-not (Test-Path $csc)) {
    $csc = (Get-ChildItem "C:\Program Files (x86)\Microsoft Visual Studio" -Recurse -Filter csc.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like "*Roslyn*" } | Select-Object -First 1).FullName
}
if (-not $csc) { throw "未找到 Roslyn csc.exe" }

$sources = @(
    "$src\Theme.cs", "$src\Model.cs", "$src\DiskScanner.cs", "$src\TreemapLayout.cs",
    "$src\Cleaner.cs", "$src\TreemapView.cs", "$src\ItemList.cs", "$src\CleanupList.cs",
    "$src\Breadcrumb.cs", "$src\HeaderBar.cs", "$src\DriveBar.cs", "$src\MainForm.cs", "$src\Program.cs",
    "$src\AssemblyInfo.cs"
)

& $csc /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /langversion:7.3 `
    /out:"$root\磁盘清理工具.exe" `
    /win32icon:"$src\app.ico" /win32manifest:"$src\app.manifest" `
    /r:"$fw\mscorlib.dll" /r:"$fw\System.dll" /r:"$fw\System.Core.dll" `
    /r:"$fw\System.Drawing.dll" /r:"$fw\System.Windows.Forms.dll" /r:"$fw\Microsoft.VisualBasic.dll" `
    $sources

if ($LASTEXITCODE -ne 0) { throw "编译失败: $LASTEXITCODE" }
Write-Output ("编译完成: " + (Join-Path $root "磁盘清理工具.exe"))
