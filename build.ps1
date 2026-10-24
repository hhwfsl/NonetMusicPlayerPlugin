param([string]$Player = (Join-Path $PSScriptRoot '../NonetMusicPlayer'), [string[]]$Rids = @('win-x64','win-x86','win-arm64','linux-x64','linux-arm64','osx-x64'))
$ErrorActionPreference = 'Stop'
$taskPlayer = [IO.Path]::GetFullPath($Player)
$env:NUGET_PACKAGES = Join-Path $taskPlayer '.packages'
$taskPackager = Join-Path $taskPlayer 'tools/NonetMusicPlayer.PluginPackager/NonetMusicPlayer.PluginPackager.csproj'
if (-not (Test-Path -LiteralPath $taskPackager)) { throw 'Clone NonetMusicPlayer or specify -Player first.' }
# 模板引用宿主 API，运行包只包含声明的 JSON/入口；不复制 SDK 或生成源码 ZIP。
dotnet build (Join-Path $PSScriptRoot 'NonetMusicPlayerPlugin.slnx') -c Release "-p:NonetPlayerRoot=$taskPlayer"
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
$taskManifest = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'plugin/manifest.json') | ConvertFrom-Json
$taskStage = Join-Path $PSScriptRoot ('artifacts/package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskStage -Force | Out-Null
foreach ($name in @('manifest.json','page.json','plugin_config_schema.json','LICENSE')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot "plugin/$name") -Destination (Join-Path $taskStage $name) }
foreach ($rid in $Rids) {
    if ($rid -notin @('win-x64','win-x86','win-arm64','linux-x64','linux-arm64','osx-x64')) { throw "Unsupported platform: $rid" }
    $target = Join-Path $taskStage "bin/$rid"; New-Item -ItemType Directory -Path $target -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'plugin/bin/Release/net10.0/NonetMusicPlayerPlugin.dll') -Destination $target
}
foreach ($taskRid in $Rids) {
    $taskOutput = Join-Path $PSScriptRoot "publish/$($taskManifest.id)-$($taskManifest.version)-$taskRid.impp"
    dotnet run --project $taskPackager -c Release -- pack --source $taskStage --output $taskOutput --rid $taskRid --force
    if ($LASTEXITCODE -ne 0) { throw 'Plugin validation failed.' }
}
# 仅在全部新包成功后移除本模板的旧版产物；不删除源码、其他插件或当前版本。
$taskPublish = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'publish'))
$taskPrefix = [regex]::Escape($taskManifest.id)
foreach ($taskOld in Get-ChildItem -LiteralPath $taskPublish -File -Filter '*.impp') {
    if ($taskOld.Name -notmatch ('^' + $taskPrefix + '-(\d+\.\d+\.\d+)-(win-x64|win-x86|win-arm64|linux-x64|linux-arm64|osx-x64)\.impp$')) { continue }
    if ($Matches[2] -notin $Rids -or [version]$Matches[1] -ge [version]$taskManifest.version) { continue }
    if ([IO.Path]::GetDirectoryName($taskOld.FullName) -ne $taskPublish -or ($taskOld.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unexpected package cleanup target.' }
    Remove-Item -LiteralPath $taskOld.FullName
}
