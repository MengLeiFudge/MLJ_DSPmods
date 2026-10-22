# 万物分馏图标资源

此项目把 `Icons/` 内的 PNG 嵌入 `FractionateEverything.Resources.dll`，随 FE 一起发布。它不包含插件、游戏逻辑或 Unity 依赖。主项目只声明构建顺序并复制产物，不编译引用此程序集。

## 图标约定

- 图片为 80×80 透明 PNG，资源名固定为 `FE.Icons.<文件名>.png`。
- 原型使用 `Assets/fe/<文件名>` 虚拟路径，不带扩展名；加载由主项目 `IconResources` 负责。
- 物品图标保留原有彩色素材。科技文件名与中文科技系列名一致，等级共用；科技图案为纯白 RGB，通过 alpha 表达透明背景与抗锯齿。
- 科技图标可用已有 Pillow 环境运行 `python tools/generate_technology_icons.py` 重绘；PNG 是构建输入，正常编译不运行绘图脚本，也不需要 Python 或 Unity。
- 新增图标应同时更新原型引用，删除资源前应核对全部引用。资源 DLL 与主 DLL 必须成套安装。

## 构建与部署

按仓库根目录规范，用 Windows MSBuild 构建 `MLJ_DSPmods.sln`。主项目构建后将资源 DLL 复制到 `FractionateEverything/bin/Debug/`，发布工具从该目录打包和同步 R2。

现有物品 PNG 源自 `DSP_FEAssets/Assets/fe` 中仍使用的素材；本目录是后续维护的源文件位置，不再从外部 Unity 项目覆盖。
