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
> to [xxdlm](https://github.com/xxldm) for his tremendous support.

## 反馈问题与建议 Issue or Idea Feedback

如果有任何bug或建议，点击[这里](https://github.com/MengLeiFudge/MLJ_DSPmods/issues/new)反馈，万分感谢。

If there are any bugs or suggestions, click [here](https://github.com/MengLeiFudge/MLJ_DSPmods/issues/new) to give
feedback. I would be appreciated.

## 模组列表 Mods List

### [Get Dsp Data 获取游戏数据](https://github.com/MengLeiFudge/MLJ_DSPmods/tree/master/GetDspData)

仅供MOD开发者使用，可以将游戏内物品、配方、模型、科技等信息导出至指定文件。

For mod developers only, you can export in-game items, recipes, models, technology and other information to a specified
file.

### [Fractionate Everything 万物分馏](https://github.com/MengLeiFudge/MLJ_DSPmods/tree/master/FractionateEverything)

包含多个新分馏塔，大量分馏配方，还有可以直接与数据中心交互的物流交互站！尽情享受分馏的乐趣吧！

Featuring multiple new fractionators, a vast array of distillation recipes, and Interaction Stations that interact
directly with the data centre! Have fun with fractionation!

## 模组安装说明 Mod Installation Guide

你可以在[thunderstore](https://thunderstore.io/c/dyson-sphere-program/p/MengLei/)查看我发布的所有模组。

建议通过[r2modman](https://thunderstore.io/c/dyson-sphere-program/p/ebkr/r2modman/)模组管理器进行安装。

You can view all the mods I've released at [thunderstore](https://thunderstore.io/c/dyson-sphere-program/p/MengLei/).

It is recommended to install them using the [r2modman](https://thunderstore.io/c/dyson-sphere-program/p/ebkr/r2modman/)
mod manager.

## oh-my-opencode 相关

项目目前使用[oh-my-opencode](https://github.com/code-yeongyu/oh-my-opencode)辅助开发。
这是一个开源ai智能体框架，可以统筹各种ai。下面是 Windows系统 下的安装说明。

### 1.安装[wsl](https://learn.microsoft.com/zh-cn/windows/wsl/install)

wsl相当于Linux框架，可以模拟Linux环境，方便在Windows上运行Linux命令和软件。

```
# 安装wsl
wsl --install
# 重启电脑以使wsl生效
# 通过wsl安装Ubuntu
wsl.exe --install Ubuntu
```

安装完成后，在命令行输入`wsl`即可进入安装好的Linux系统。现在先不着急进去。

### 2.解决wsl无法通过代理访问的问题

按下 Win + R，输入 %UserProfile% 并回车。在该目录下寻找或新建一个名为 .wslconfig 的文件。

用记事本打开，添加以下配置：

```
[wsl2]
networkingMode=mirrored
```

保存文件后，在 PowerShell 中执行 `wsl --shutdown` 重启 WSL。

接下来验证网络是否正常：

```
# 进入wsl
wsl
# 等待一会...
# 测试连接，ctrl+c终止
ping www.baidu.com
ping www.google.com
```

注意，即使能正常ping通，apt相关指令也可能卡住。
尤其是在开了clash的TUN模式的情况下，必须让apt强制走代理。例子：

❌️`sudo apt-get update`

✅️`sudo apt-get -o Acquire::http::Proxy="http://127.0.0.1:7890" update`

或者也可以考虑关闭TUN模式（我没尝试过）。

### 3.安装oh-my-opencode前置工具（下面都是在wsl里面了）

通过apt-get安装dotnet-sdk-8.0、npm、npx、LSP、unzip、OmniSharp、opencode等等

```
# 更新并安装基础工具
sudo apt-get -o Acquire::http::Proxy="http://127.0.0.1:7890" update
sudo apt-get -o Acquire::http::Proxy="http://127.0.0.1:7890" install -y \
    dotnet-sdk-8.0 \
    nodejs npm \
    unzip curl git build-essential ca-certificates
curl -fsSL https://opencode.ai/install | bash
```

将opencode加入PATH中：

```
# 确认路径：
~/.opencode/bin/opencode --version
# 加入path
echo 'export PATH="$HOME/.opencode/bin:$PATH"' >> ~/.bashrc
# 刷新配置
source ~/.bashrc
# ctrl+d退出wsl
# 检查能不能找到opencode
wsl bash -ic "opencode --version"
# 此时应该显示opencode版本号
```

### 4.让ai自动安装oh-my-opencode

打开ai智能体，粘贴以下内容：

```
请按照此处的说明安装并配置oh-my-opencode：https://raw.githubusercontent.com/code-yeongyu/oh-my-opencode/refs/heads/dev/docs/guide/installation.md。
注意，我是Windows系统，并且已经通过wsl安装了opencode。
运行wsl bash指令时，你必须使用 `wsl bash -ic` 而非 `wsl bash -lc`。
需要通过apt-get安装内容时，由于我的代理开启了TUN模式，你必须强制指定使用指定代理，例如`sudo apt-get -o Acquire::http::Proxy="http://127.0.0.1:7890" update`。
```

等待一会，根据实际情况回复，例如我是Claude Pro账号+Z.ai Pro订阅+DeepSeek，此时先忽略DeepSeek。

1.有 Claude Pro
2.没有 OpenAI/ChatGPT Plus
3.不集成 Gemini
4.没有 GitHub Copilot
5.没有 OpenCode Zen 访问权限
6.有 Z.ai Coding Plan 订阅

继续等待安装完成。

### 5.修改oh-my-opencode配置

在安装的时候，它只问了六个。如果想用别的ai怎么办？

注：不知道指令的话，就运行`opencode --help`来查找。

查找后发现，模型指令是`opencode models [provider]`。那么：

```
# 查看所有支持的model字段
opencode models
# 或者只查看某一家厂商支持的model字段
opencode models 厂商名字
```

此时会显示格式为“厂商/模型名”的“model字符串”，我们就知道模型对应的字符串是什么了。例如：

```
deepseek/deepseek-chat -> deepseek日常对话模式，按量付费
deepseek/deepseek-reasoner -> deepseek深度推理模式，按量付费
zai/glm-5 -> glm5，按量付费
zai-coding-plan/glm-5 -> glm5，按订阅付费
```

记下对应的字符串后，修改配置：

```
# 打开oh-my-opencode.json
nano ~/.config/opencode/oh-my-opencode.json
# ctrl+s保存，ctrl+x退出
```

只需在此处修改各个"model"的内容，oh-my-opencode就会在执行对应任务时自动调用相应ai。

下面是我的一个例子：

```
{
  "system_prompt": "You are a helpful assistant. Please always respond in Chinese.",
  "$schema": "https://raw.githubusercontent.com/code-yeongyu/oh-my-opencode/dev/assets/oh-my-opencode.schema.json",
  "agents": {
    "sisyphus": {
      "model": "anthropic/claude-opus-4-6",
      "variant": "max"
    },
    "oracle": {
      "model": "anthropic/claude-opus-4-6",
      "variant": "max"
    },
    "librarian": {
      "model": "zai-coding-plan/glm-4.7"
    },
    "explore": {
      "model": "anthropic/claude-haiku-4-5"
    },
    "multimodal-looker": {
      "model": "zai-coding-plan/glm-4.6v"
    },
    "prometheus": {
      "model": "anthropic/claude-opus-4-6",
      "variant": "max"
    },
    "metis": {
      "model": "anthropic/claude-opus-4-6",
      "variant": "max"
    },
    "momus": {
      "model": "anthropic/claude-opus-4-6",
      "variant": "max"
    },
    "atlas": {
      "model": "anthropic/claude-sonnet-4-5"
    }
  },
  "categories": {
    "visual-engineering": {
      "model": "zai-coding-plan/glm-5"
    },
    "ultrabrain": {
      "model": "anthropic/claude-opus-4-6",
      "variant": "max"
    },
    "quick": {
      "model": "anthropic/claude-haiku-4-5"
    },
    "unspecified-low": {
      "model": "anthropic/claude-sonnet-4-5"
    },
    "unspecified-high": {
      "model": "anthropic/claude-sonnet-4-5"
    },
    "writing": {
      "model": "anthropic/claude-sonnet-4-5"
    }
  }
}
```

### 6.登录对应ai的账户或者输入api-key

```
# 打开统一设置页面
opencode auth login
```

在这个页面中，可以直接搜索对应ai（最上面有search），也可以上下切换慢慢找。

选中某一个ai之后，回车，之后有可能是登录账号（比如claude），有可能是输入api-key。

一次只能录入一个ai的信息。当你有多个ai，就多执行几次`opencode auth login`，全配置好就行。
