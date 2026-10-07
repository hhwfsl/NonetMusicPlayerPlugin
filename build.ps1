param([string]$Player = (Join-Path $PSScriptRoot '../NonetMusicPlayer'), [string[]]$Rids = @('win-x64','osx-x64','linux-x64'))
$ErrorActionPreference = 'Stop'
$taskPlayer = [IO.Path]::GetFullPath($Player)
$taskPackager = Join-Path $taskPlayer 'tools/NonetMusicPlayer.PluginPackager/NonetMusicPlayer.PluginPackager.csproj'
if (-not (Test-Path -LiteralPath $taskPackager)) { throw 'Clone NonetMusicPlayer or specify -Player first.' }
# 模板引用宿主 API，运行包只包含声明的 JSON/入口；不复制 SDK 或生成源码 ZIP。
dotnet build (Join-Path $PSScriptRoot 'NonetMusicPlayerPlugin.slnx') -c Release "-p:NonetPlayerRoot=$taskPlayer"
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
$taskManifest = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'plugin/manifest.json') | ConvertFrom-Json
$taskStage = Join-Path $PSScriptRoot ('artifacts/package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskStage -Force | Out-Null
foreach ($name in @('manifest.json','page.json','plugin_config_schema.json','LICENSE')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot "plugin/$name") -Destination (Join-Path $taskStage $name) }
foreach ($rid in @('win-x64','linux-x64','osx-x64')) {
    $target = Join-Path $taskStage "bin/$rid"; New-Item -ItemType Directory -Path $target -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'plugin/bin/Release/net10.0/NonetMusicPlayerPlugin.dll') -Destination $target
}
foreach ($taskRid in $Rids) {
if ($taskRid -notin @("win-x64","osx-x64","linux-x64")) { throw "Unsupported platform." }
$taskOutput = Join-Path $PSScriptRoot "publish/$($taskManifest.id)-$($taskManifest.version)-$taskRid.impp"
dotnet run --project $taskPackager -c Release -- pack --source $taskStage --output $taskOutput --rid $taskRid --force
if ($LASTEXITCODE -ne 0) { throw 'Plugin validation failed.' }

}
