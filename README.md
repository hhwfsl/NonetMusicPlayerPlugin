# Nonet 插件开发模板

用于开发 Nonet 桌面端插件的最小 C# 工程。模板通过 `ProjectReference` 直接引用播放器源码中的 Plugin SDK，并使用播放器的打包器生成 `.impp`；不复制 SDK、播放器或打包器。

许可证：GPL-3.0-only。可用运行形态包括主进程托管、独立进程以及纯声明式扩展。当前 Hello World 使用托管形态，Contract 2，基础示例可用于支持该 Contract 的 Nonet；额外能力应先协商，不能依据应用版本假定其存在。

## 环境

- .NET SDK 10.0.401 或兼容的 .NET 10 SDK。
- Git。
- PowerShell 7（运行 `build.ps1`，Windows/macOS/Linux 均可用）。
- Nonet 源码，默认与模板位于同一父目录。
- Native AOT 仅在需要独立进程时使用，另需目标操作系统的 C/C++ 链接工具链；当前托管示例不需要。

自动化验证目前在 Windows x64 执行。其他平台的包可构建，仍需在对应系统运行测试。

## 快速开始

```powershell
git clone https://github.com/hhwfsl/NonetMusicPlayer.git
git clone https://github.com/hhwfsl/NonetMusicPlayerPlugin.git
cd NonetMusicPlayerPlugin
dotnet build NonetMusicPlayerPlugin.slnx -c Release
dotnet run --project tests/NonetMusicPlayerPlugin.Checks -c Release -- .
pwsh ./build.ps1 -Player ../NonetMusicPlayer
```

如果播放器不在相邻目录：

```powershell
dotnet build NonetMusicPlayerPlugin.slnx -c Release -p:NonetPlayerRoot=D:/Source/NonetMusicPlayer
pwsh ./build.ps1 -Player D:/Source/NonetMusicPlayer -Rids win-x64
```

三个平台的发行包分别生成在 `publish/`，每包只含本平台所需入口、页面、配置规范和许可证。不要把源码目录压缩后当作安装包。

## 修改示例

1. 修改 `plugin/manifest.json` 的 `id`、`name`、`author`、`repositoryOwner` 和 `repositoryName`。ID 必须稳定，仓库名称须与 GitHub 一致；更新只提高 `version`，不得改变 ID。
2. 在 `plugin/PluginDefinition.cs` 中实现业务逻辑；`HelloWorld` 已展示初始化、动作、状态同步、服务结果、事件和释放六个入口。
3. 用 `plugin/page.json` 定义控件树，用 `bind` 读取插件状态，用 `action` 调用后端。UI 描述不是 XAML，不执行脚本。
4. 在 `plugin/plugin_config_schema.json` 定义用户可填写的配置。开发者决定表单结构，用户只填写值。
5. 按需要声明最小权限和 `requiredCapabilities`；不需要的权限不要添加。
6. 构建、运行测试，在真实 Nonet 的插件中心导入对应平台包。托管代码首次启用须阅读并确认信任警告。

## 工程结构

```text
plugin/
  PluginDefinition.cs          后端与生命周期
  manifest.json               身份、版本、权限、运行形态
  page.json                   页面控件与绑定
  plugin_config_schema.json   配置表单规范
  LICENSE                     随安装包分发的许可
tests/                        SDK 与最小包回归
docs/                         开发教程及接口参考
build.ps1                     调用宿主打包器
Directory.Build.props         定位并引用宿主 SDK
```

`bin/`、`obj/`、`artifacts/` 和 `publish/` 是本地产物，不进入源码提交。每个插件独立一个工程目录和 Git 仓库。

## 文档

- [从零开发、构建与调试](docs/GETTING_STARTED.md)
- [运行形态、权限、控件、服务与生命周期](docs/REFERENCE.md)
- [通用扩展协议](docs/EXTENSIONS.md)
- [旧 Contract 与音源/歌词协议](docs/PLUGIN_DEVELOPMENT.md)
- [Agent 使用模板的约束与步骤](docs/AGENT_GUIDE.md)

不要将托管或独立进程插件视为操作系统沙箱。它们以当前用户权限运行，应审查来源和代码；宿主服务的权限检查不能阻止恶意进程自行访问操作系统。
