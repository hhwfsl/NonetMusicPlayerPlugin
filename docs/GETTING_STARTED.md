# 从零开发 Nonet 插件

本教程使用模板自带的 Hello World，完成开发、打包、导入、调试与同 ID 更新。接口细节见 [REFERENCE.md](REFERENCE.md)。

## 1. 准备开发环境

安装 Git、.NET 10 SDK 和 PowerShell 7。在终端检查：

```powershell
git --version
dotnet --version
pwsh --version
```

建议 SDK 10.0.401。不要将 .NET Runtime 当作 SDK；仅运行播放器不等于具备构建工具。

克隆两个项目，保留为相邻目录：

```powershell
git clone https://github.com/hhwfsl/NonetMusicPlayer.git
git clone https://github.com/hhwfsl/NonetMusicPlayerPlugin.git
cd NonetMusicPlayerPlugin
```

模板引用播放器的 `src/NonetMusicPlayer.PluginSdk`。这是正常的源码依赖，不需要把整个播放器复制到插件仓库。其他目录布局使用 `NonetPlayerRoot` 参数。

## 2. 运行原始示例

```powershell
dotnet build NonetMusicPlayerPlugin.slnx -c Release
dotnet run --project tests/NonetMusicPlayerPlugin.Checks -c Release -- .
pwsh ./build.ps1 -Player ../NonetMusicPlayer -Rids win-x64
```

macOS 用 `osx-x64`，Linux 用 `linux-x64` 或 `linux-arm64`，Windows 可用 `win-x64`、`win-x86` 或 `win-arm64`。省略 `-Rids` 时打包全部六种目标。本示例是共享宿主运行时的托管 DLL，无需下载独立运行时或安装 Native AOT 工具链。

导入 `publish/developer.hello-world-1.1.0-win-x64.impp` 到 Nonet 插件中心。启用托管代码时确认信任提示，打开侧边栏插件页，点击问候按钮，确认计数增加。此时已验证源码、SDK、打包器与真实宿主整条链路。

## 3. 为自己的插件建立身份

为每个插件建立独立目录和 Git 仓库。若直接从模板仓库开始，可移除模板 remote 并设置自己的 remote；不应向模板仓库提交个人插件。不要使用已存在的插件 ID。

修改 `plugin/manifest.json`：

```json
{
  "id": "yourname.hello-nonet",
  "name": "Hello Nonet",
  "author": "Your Name",
  "repositoryOwner": "YourGitHubAccount",
  "repositoryName": "nonet_plugin_hello_nonet",
  "version": "1.0.0"
}
```

保留其余运行形态、Contract、入口与页面字段，直到需要改变它们。代码 namespace、DLL 名称和 `extensionClass` 若更改，应同时改清单入口和构建脚本；仅改显示名称无需改 DLL。

版本只能递增。用户配置与数据以 ID 关联；发布后改变 ID 会被当作另一插件。

## 4. 添加状态与按钮动作

编辑 `plugin/PluginDefinition.cs`：

- `InitializeAsync` 从 Configuration 读取问候配置，写入 `_frame.State["greeting"]`。
- `InvokeAsync` 接收页面的 `greet` 动作，改变 count 并递增 Revision。
- `SyncAsync` 返回当前 Frame，不能每次都重新初始化计数。
- `CompleteAsync` 是宿主服务回调，不是网络请求入口。

编辑 `page.json`，在 root.children 增加：

```json
{ "type": "text", "bind": "count", "fontSize": 18 }
```

将按钮的 `text` 改为自己的用户可见名称，`action` 仍须与后端处理的字符串一致。需要可复制文字时用 `selectable-text`，不能通过可编辑 input 假装只读输出。

## 5. 增加用户配置

在 `plugin_config_schema.json` 添加字符串、数字、开关或枚举字段，设置默认值和合法范围。清单不包含用户真实 API Key；发布包只分发字段定义。

用户打开插件配置、填写并保存后，宿主验证全部值、持久化配置、重新初始化插件。空 Schema 表示无配置。敏感项标记为 `sensitive`；不要输出其值到日志、Frame 或菜单。

如果配置会降低安全确认，应使用审批能力并由宿主确认升级，不能仅靠插件自行读取布尔值跳过宿主确认。

## 6. 调用播放器服务

例如暂停音乐：

1. 在 permissions 增加 `player-control`。
2. 给页面添加 `action: pause` 的按钮。
3. 后端在 pause 分支生成唯一请求 ID，将 commands 服务请求写入 Frame.Requests。
4. 将 Busy 设为 true。
5. 宿主验证权限和参数，执行事务，调用 CompleteAsync。
6. 检查返回的 success，移除该 ID 请求，再将 Busy 设为 false。

```csharp
public ValueTask<ExtensionFrame> CompleteAsync(
    string requestId, System.Text.Json.Nodes.JsonObject result,
    CancellationToken cancellationToken)
{
    _frame.Requests.RemoveAll(r => r.Id == requestId);
    _frame.State["result"] = result.DeepClone();
    _frame.Busy = false;
    _frame.Revision++;
    return ValueTask.FromResult(_frame);
}
```

先请求 catalog 获取当前允许的操作和参数，不编造歌曲 ID，不把路径当作歌曲 ID，也不使用 `--yes` 伪造确认。被拒绝后不得换一种表达自动重试同一危险事务。

## 7. UI 扩展与长任务

用 contributions 向指定插槽提供菜单、卡片、页面或覆盖层，申请 ui-extend。用 ui 服务读取当前布局后再修改；保留全部必要播放部件。

网络或计算不能阻塞 InvokeAsync。启动可取消的后台任务，设置 Busy，逐次更新 State 和 Revision。任务结束或失败时关闭 Busy，释放资源。流式文本应更新已有状态，不重复追加同一消息；只在完整轮次结束时写持久历史。

动态数据应分页、有界，不把 API Key、文件路径、整个音乐库或无限对话全部塞到每个 Frame。

## 8. 数据与生命周期

初始化的 StorageDirectory 是插件独立数据目录。不要把用户数据写在 DLL 旁边；同 ID 更新会替换包文件，但保留独立存储。

注册需要的事件及生命周期通知，停用时取消 CancellationTokenSource，释放 HttpClient、计时器与流。DisposeAsync 不能永久等待。卸载若删除文件，数据可能不可恢复；保留文件则只断开连接。

## 9. 调试与测试

```powershell
dotnet build NonetMusicPlayerPlugin.slnx -c Release
dotnet run --project tests/NonetMusicPlayerPlugin.Checks -c Release -- .
pwsh ./build.ps1 -Player ../NonetMusicPlayer -Rids win-x64
```

若路径不同，对 build 和 run 均传 `-p:NonetPlayerRoot=...`。测试输出位于 artifacts，不能提交这些夹具。

真实宿主验证至少包括：

- 空配置、合法配置、非法值、重启后的配置与数据。
- 按钮、键盘、窗口窄尺寸、语言与主题变化。
- 用户拒绝危险操作、取消网络、断网、服务失败。
- 停用/启用、同 ID 更新、卸载保留文件后重新导入。
- 当前平台包正确，其余平台在对应系统实机验证。

开发者应先阅读源码再信任托管或独立进程插件。它们不是沙箱，宿主只约束通过服务接口发出的操作。

## 10. 发布与更新

例如将 manifest.version 从 `1.1.0` 增加到 `1.1.1`，重新构建。用户拖入新包时宿主识别同 ID，确认后升级；低版本拒绝覆盖，高版本不改变原身份。

GitHub Release 按支持的 RID 分别上传 impp，每包仅包含一个平台入口。不要上传 bin/obj、凭据、缓存或个人使用数据，也不要将源码 ZIP 作为插件安装包。包名称可以带版本，身份和版本仍以清单为准。

公开发布前检查许可证、第三方许可文本、仓库名称、版本、最小能力及实际平台测试结果。Hello World 基础接口可在公开宿主上构建；新增可选能力是否可用，以安装目标的能力列表为准。
