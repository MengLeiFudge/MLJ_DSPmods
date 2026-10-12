# MLJ_DSPmods

![Github](https://img.shields.io/badge/Author-MengLei-blue)
![GitHub](https://img.shields.io/github/license/MengLeiFudge/MLJ_DSPmods)
![GitHub repo size](https://img.shields.io/github/repo-size/MengLeiFudge/MLJ_DSPmods)
![GitHub release (latest SemVer)](https://img.shields.io/github/v/release/MengLeiFudge/MLJ_DSPmods)
![GitHub last commit](https://img.shields.io/github/last-commit/MengLeiFudge/MLJ_DSPmods)

> 欢迎来到这里！这是由萌泪制作的戴森球计划MOD合集。
> 本项目主要由[萌泪](https://github.com/MengLeiFudge)开发，同时感谢[乡下来的喵](https://github.com/xxldm)的大力支持。
>
> Welcome to this place! This is a collection of Dyson Sphere Program mods made by MengLei.
> This project was primarily developed by [MengLei](https://github.com/MengLeiFudge), with special thanks
> to [xxldm](https://github.com/xxldm) for his tremendous support.

## 反馈问题与建议 Issue or Idea Feedback

如果有任何bug或建议，点击[这里](https://github.com/MengLeiFudge/MLJ_DSPmods/issues/new)反馈，万分感谢。

If there are any bugs or suggestions, click [here](https://github.com/MengLeiFudge/MLJ_DSPmods/issues/new) to give
feedback. I would be appreciated.

## 模组列表 Mods List

### [Fractionate Everything 万物分馏](https://github.com/MengLeiFudge/MLJ_DSPmods/tree/master/FractionateEverything)

包含培养塔、解析塔、复制塔和转化塔，以及以矩阵解析数据、协议恢复和远古科技树组织的大量分馏配方；物流交互站可以把生产线直接接入数据中心。

Featuring four specialized fractionators, protocol recovery driven by matrix analysis data, an ancient technology
tree, and Interaction Stations that connect production lines directly to the data centre.

### [Get Dsp Data 获取游戏数据](https://github.com/MengLeiFudge/MLJ_DSPmods/tree/master/GetDspData)

仅供MOD开发者使用，可以将游戏内物品、配方、模型、科技等信息导出至指定文件。

For mod developers only, you can export in-game items, recipes, models, technology and other information to a specified
file.

### [Save Data Exporter 存档数据导出](https://github.com/MengLeiFudge/MLJ_DSPmods/tree/master/SaveDataExporter)

按自定义快捷键导出当前已载入存档的星系/星球维度物品统计数据，默认快捷键为空，输出为 `.xlsx`，可在游戏设置的“杂项”页面选择按时间戳生成新文件或按存档名固定覆盖。

Exports per-star and per-planet item statistics from the currently loaded save through a custom hotkey. The hotkey is
unbound by default, the output format is `.xlsx`, and file naming can either create timestamped files or overwrite a
save-name-specific file.

### [UXAEnhance UXAssist强化](https://github.com/MengLeiFudge/MLJ_DSPmods/tree/master/UXAEnhance)

强化 UXAssist 的自动配置物流设施页面：支持通过本模组配置提高滑条上限，并为每个滑条提供单项全局应用按钮。

Enhances UXAssist's auto-config logistics UI with configurable slider limits and per-slider global apply buttons.

### [Blueprint Planet 蓝图星球](https://github.com/MengLeiFudge/MLJ_DSPmods/tree/master/BlueprintPlanet)

新建原版蓝图时自动记录当前星球名称为“蓝图星球”属性，并将它作为缩略图文字默认值。

When creating a new vanilla blueprint, records the current planet name as the “Blueprint Planet” property and uses it as the default thumbnail text.

## 模组安装说明 Mod Installation Guide

你可以在[thunderstore](https://thunderstore.io/c/dyson-sphere-program/p/MengLei/)查看我发布的所有模组。

建议通过[r2modman](https://thunderstore.io/c/dyson-sphere-program/p/ebkr/r2modman/)模组管理器进行安装。

You can view all the mods I've released at [thunderstore](https://thunderstore.io/c/dyson-sphere-program/p/MengLei/).

It is recommended to install them using the [r2modman](https://thunderstore.io/c/dyson-sphere-program/p/ebkr/r2modman/)
mod manager.

## QQ 群文件夹

模式 `1` 默认执行本地更新与群文件交付，内容未变化时去重跳过。通过本机 `DefaultPath.props` 的 `QqFolderId` 选择已有 QQ 群文件夹；省略、空白或 `/` 表示群文件最外层。这个设置只影响 QQ 上传，与本地 profile、ZIP 内容和 Thunderstore 分类无关。

```xml
<QqFolderId>实际的群文件夹ID</QqFolderId>
```

ID 从目标群的 NapCat `get_group_root_files` 响应 `folders[].folder_id` 取得，不能使用文件夹显示名称。qqbot 先确认该群中存在对应文件夹；错误或无权限会失败，不回退最外层。接口的每个文件条目携带 `folder_id`，上传、同名文件替换及回执均限定在该目录。成功缓存按接收群集合、目录和包隔离，原群根目录缓存保持兼容；换目录会重新交给服务端核对，不搬迁或删除原目录文件。

本项目与 MLJ_InFalsus_Mods 各自在自己的 DefaultPath.props 配置目标目录。只有服务端确认每个群、目录、包名和内容哈希后，客户端才保存成功缓存。DSP 自动模式仍按最新提交筛选 QQ 候选；仅更换目录、当前提交未涉及模组时，使用 `AfterBuildEvent.exe 1 <项目名>` 明确选择需要投递到新目录的包。

## Thunderstore 发布

本地交付使用 `AfterBuildEvent.exe 1`：关闭游戏、更新本地 r2modman 模组、打包、修正 Doorstop，再按变更范围向 QQ 群 319567534 推送文件。雷霆商店是独立流程，必须明确批准具体包及版本；日常本地交付和 QQ 推送不会触发商店上传。

先用 Windows MSBuild 完成 Debug 构建，并在 `AfterBuildEvent/bin/Debug` 执行模式 `1` 生成当前 ZIP。支持的商店目标为 `FractionateEverything`、`BlueprintPlanet`、`SaveDataExporter`、`UXAEnhance`、`GetDspData`，每次指定一个项目。当前 GetDspData 缺少 README.md；BlueprintPlanet、SaveDataExporter、UXAEnhance 缺少各自的 Assets/icon.png。补齐这些必需素材前，对应包会被准备阶段阻止发布。

准备阶段不读取密钥、不上传、不改本地模组：

```powershell
# 在 AfterBuildEvent/bin/Debug 中执行；将包名替换为本次目标
./AfterBuildEvent.exe 7 prepare FractionateEverything
```

工具核对 ZIP 与当前 DLL、manifest、README、图标及其他打包素材，检查项目、DLL、BepInEx 插件和 manifest 版本一致；存在 CHANGELOG 时，其首个版本条目也必须一致。随后查询远端是否已存在同一版本，已发布的版本不能覆盖。准备成功后在 `.codex/publish/thunderstore/` 生成清单及 TOML，输出包名、版本、标签、ZIP 路径与 SHA256。

标签优先沿用当前雷霆页面。没有现有分类时使用以下默认值，并向 DSP 社区分类接口核对合法性：

| 包名 | 默认分类 |
|---|---|
| FractionateEverything | Cheats、Game Breaking、Quality of Life |
| BlueprintPlanet | Blueprints、Quality of Life |
| UXAEnhance | Logistics、Quality of Life |
| GetDspData、SaveDataExporter | Info |

首次发布前，在仓库根目录安装固定版本的 CLI：

```powershell
dotnet tool install tcli --version 0.2.4 --tool-path .codex/tools/tcli
```

认证沿用 Windows User 范围的 `THUNDERSTORE_API_TOKEN`，为空时查 Machine 范围。工具只向上传子进程映射 `TCLI_AUTH_TOKEN`，不将密钥写入文件、日志或命令参数。

用户明确批准清单中的包、版本及文件后，执行准备结果打印的命令，例如：

```powershell
# 包名、版本和 SHA256 均使用本次已获批准的准备结果
./AfterBuildEvent.exe 7 publish FractionateEverything 3.0.0 --approve '<准备结果中的SHA256>'
```

`--approve` 是调用者对本次明确批准的声明，不能由日常交付自动补上。文件、版本或分类变化后必须重新准备并取得批准。上传前再次检查远端版本，持有 ZIP 只读锁直至流程结束；五分钟超时后停止 CLI 进程树，不自动重传。

完成后查询精确版本的 `is_active`、DSP 社区和分类，并单独报告 `review_status`。CLI 输出与远端结果保存在清单目录的脱敏 `receipt-*.json`。失败或超时先查看回执并查询远端，不直接重传或自动增版：

```text
https://thunderstore.io/api/experimental/package/MengLei/<包名>/<版本>/
https://thunderstore.io/api/experimental/package/MengLei/<包名>/
```
