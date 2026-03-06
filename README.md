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
这是一个开源ai智能体框架，可以统筹各种ai，完美完成任务。

### AI调用说明

现在的AI付费模式大致分为以下几种：

- 订阅付费：按月/按季/按年付费，可能有每5小时、每周额度。
- 按量付费：用多少付多少。
- 中转站：顾名思义，就是自己不是AI，但是可以调用其他AI的接口。

由于最终要在opencode上面使用，所以一定要先确认相关登录方式！（最下面有说）

典型的例子是“Googol AI订阅付费”，这个只能在网页用！如果要用Gemini，应该选择“Googol AI Studio按量付费”。

### AI选购说明

如果只用ulw处理一些不需要太大逻辑的东西，我感觉只有Claude就够用了。（那还要oh-my-opencode干嘛？）

如果用plan-builder和plan-executor处理复杂的东西，就涉及到多AI协作了。
这个时候，我的推荐是Claude+GPT+Googol AI Studio。注意最后一个，Google One AI订阅没用！只能是Googol AI Studio的按量计费！

当然你也可以看完最后的配置文件，自行决定用哪些。

我的意见就是，Claude好用，还快。别的AI真的比不上。就是太贵了= =

调整配置文件，尽量少用Claude之后（下面有说怎么改），感觉是完全够用的。

以下只说一部分，具体哪些AI怎么登录，安装完opencode后输入 `oencode auth login` 查看。下面有说。

- OpenCode Zen（推荐）：中转站，20刀额度收取1.23刀手续费用于维护OpenCode。
    - opencode如何登录
        - 登录账号，OpenCode账号可以通过github或者googol登录。
    - 可选方式
        - 订阅或按量付费：[opencode.ai](https://opencode.ai/)
    - 购买相关
        - 虽然有手续费但是很方便！不像是其他的订阅用国内的visa卡都付不了，这个可以直接用link去付。
        - 这个就是纯中转，唯一的溢价已经在充值的时候扣除了。

- Anthropic（Claude Pro/Max 或者 API Key）
    - opencode如何登录
        - 浏览器登录Pro/Max订阅的账号
        - 生成API Key并输入
        - 手动输入API Key
    - 可选方式
        - 订阅：[claude.ai](https://claude.ai/)
        - 按量付费：[platform.claude.com](https://platform.claude.com/)
    - 购买相关
        - 我是淘宝买Claude Pro成品号，130-150/月。淘宝有很多“中转站”，应该是没法在opencode用的吧？

- OpenAI（ChatGPT Plus/Pro 或者 API Key）
    - opencode如何登录
        - 浏览器登录Pro/Plus订阅的账号
        - 无头登录Pro/Plus订阅的账号
        - 手动输入API Key
    - 可选登录方式
        - 订阅：[chatgpt.com](https://chatgpt.com)
    - 购买相关
        - 我是看的[这个文章](https://yingtu.ai/zh/blog/chatgpt-plus-ios-recharge)，这里提到了用自己的ios订阅。
        - 不过最后还是选了[星际放映厅](https://www.naifeistation.com/)买的Chat GPT Plus成品号，426.8/3月。

- Google（Gemini API Key）
    - opencode如何登录
        - 手动输入API Key（注意，这个只能用Googol AI Studio的按量付费，Google One AI订阅没用）
    - 可选登录方式
        - 订阅：[aistudio.google.com](https://aistudio.google.com/)
    - 购买相关
        - [星际放映厅](https://www.naifeistation.com/)只能是充值，买Pro号没用。但是200块20刀也太离谱了，为何不用OpenCode
          Zen？

- DeepSeek（DeepSeek API Key）
    - opencode如何登录
        - 手动输入API Key
    - 可选登录方式
        - 按量付费：[platform.deepseek.com](https://platform.deepseek.com/)
    - 购买相关
        - 国产的，直接买。但是缺点就是深度思考比较久，而且思考的方向可能还不对。等新版本再观望一下。

- Z.AI / Z.AI Coding Plan（GLM API Key）
    - opencode如何登录
        - 手动输入API Key
    - 可选登录方式
        - Z.AI按量付费，Z.AI Coding Plan订阅：[z.ai](https://z.ai/)
    - 购买相关
        - 支持银联卡付款，直接买。GLM主要用于视觉、界面设计。

下面是 Windows系统 下的安装说明。

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

通过apt-get安装dotnet-sdk-10.0、npm、npx、LSP、unzip、OmniSharp、opencode等等

```
# 更新并安装基础工具
sudo apt-get -o Acquire::http::Proxy="http://127.0.0.1:7890" update
sudo apt-get -o Acquire::http::Proxy="http://127.0.0.1:7890" install -y \
    dotnet-sdk-10.0 \
    nodejs \
    unzip curl git ca-certificates
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

打开ai智能体，粘贴以下内容（如果没开TUN就不用复制最后一行了）：

```
请按照此处的说明安装并配置oh-my-opencode：https://raw.githubusercontent.com/code-yeongyu/oh-my-opencode/refs/heads/dev/docs/guide/installation.md。
注意，我是Windows系统，并且已经通过wsl安装了opencode。
运行wsl bash指令时，你必须使用 `wsl.exe bash -ic` 而非 `wsl bash -lc`。同样的，curl也应该使用 `curl.exe -s xxx` 而非 `curl -s xxx`。
需要通过apt安装内容时，由于我的代理开启了TUN模式，你必须强制指定使用指定代理，例如`sudo apt-get -o Acquire::http::Proxy="http://127.0.0.1:7890" update`。
```

等待一会，当其询问“你有哪些订阅”的时候，根据实际情况回复即可。
不要说多余的话！不要说多余的话！不要说多余的话！后面我们会自行调整配置文件的。

继续等待安装完成。

### 5.登录ai账户或者输入API Key

注：有关opencode的指令，可以运行`opencode --help`来查看。

```
# 打开统一设置页面
opencode auth login
```

在这个页面中，可以直接搜索对应ai（最上面有search），也可以上下切换慢慢找。

选中某一个ai之后，回车，之后有可能是登录账号（比如claude），有可能是输入API Key。

一次只能录入一个ai的信息。当你有多个ai，就多执行几次`opencode auth login`，全配置好就行。

### 6.修改oh-my-opencode配置

在安装的时候，它只问了六个ai。如果想用别的ai怎么办？

模型指令是`opencode models [provider]`。那么：

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

注意：如果没有找到对应的ai字符串，很可能是你在第5步中没有配置好对应的ai。

记下对应的字符串后，修改配置：

```
# 打开oh-my-opencode.json
nano ~/.config/opencode/oh-my-opencode.json
# ctrl+s保存，ctrl+x退出
```

只需在此处修改各个"model"的内容，oh-my-opencode就会在执行对应任务时自动调用相应ai。

注意，nano没法全选删除，但是可以设置标记来剪切。

- 移动到文件头：按下 Alt + \ (或者 Ctrl + Home)。
- 设置标记起点：按下 Alt + A (此时下方会显示 Mark Set)。
- 移动到文件尾：按下 Alt + / (或者 Ctrl + End)，此时所有文字会被高亮选中。
- 删除/剪切选中内容：按下 Ctrl + K。

或者也可以直接`rm ~/.config/opencode/oh-my-opencode.json`，然后再粘贴内容。

下面是我使用的示例，综合考虑了各ai的额度与能力：

```
{
  "$schema": "https://raw.githubusercontent.com/code-yeongyu/oh-my-opencode/dev/assets/oh-my-opencode.schema.json",
  "agents": {
    "sisyphus": {
      "model": "anthropic/claude-sonnet-4-6",
      "prompt_append": "Always respond in Simplified Chinese.",
      "description": "主要协调人。推荐Claude Opus → Kimi K2.5 → GLM 5"
    },
    "metis": {
      "model": "anthropic/claude-sonnet-4-6",
      "prompt_append": "Always respond in Simplified Chinese.",
      "description": "计划差距分析器。推荐Claude Opus → Kimi K2.5 → GPT-5.2 → Gemini 3 Pro"
    },
    "prometheus": {
      "model": "openai/gpt-5.2",
      "prompt_append": "Leverage deep & quick agents heavily, always in parallel. Always respond in Simplified Chinese.",
      "description": "战略规划师。推荐Claude Opus → GPT-5.2 → Kimi K2.5 → Gemini 3 Pro"
    },
    "atlas": {
      "model": "opencode/kimi-k2.5",
      "prompt_append": "Always respond in Simplified Chinese.",
      "description": "待办事项协调器。推荐Kimi K2.5 → Claude Sonnet → GPT-5.2"
    },
    "hephaestus": {
      "model": "openai/gpt-5.3-codex",
      "variant": "medium",
      "prompt_append": "Always respond in Simplified Chinese.",
      "description": "自主深度工作者。只能是GPT-5.3 Codex"
    },
    "oracle": {
      "model": "openai/gpt-5.2",
      "variant": "high",
      "prompt_append": "Always respond in Simplified Chinese.",
      "description": "架构顾问。推荐GPT-5.2 → Gemini 3 Pro → Claude Opus"
    },
    "momus": {
      "model": "openai/gpt-5.2",
      "variant": "medium",
      "prompt_append": "Always respond in Simplified Chinese.",
      "description": "无情评审员。推荐GPT-5.2 → Claude Opus → Gemini 3 Pro"
    },
    "explore": {
      "model": "opencode/minimax-m2.5-free",
      "prompt_append": "Always respond in Simplified Chinese.",
      "description": "快速代码库扫描。推荐Grok Code Fast → MiniMax → Haiku → GPT-5-Nano"
    },
    "librarian": {
      "model": "zai-coding-plan/glm-4.7-flashx",
      "prompt_append": "Always respond in Simplified Chinese.",
      "description": "文档/代码搜索。推荐Gemini Flash → MiniMax → GLM"
    },
    "multimodal-looker": {
      "model": "google/gemini-3-flash-preview",
      "prompt_append": "Always respond in Simplified Chinese.",
      "description": "视觉/屏幕截图。推荐Kimi K2.5 → Gemini Flash → GPT-5.2 → GLM-4.6v"
    }
  },
  "categories": {
    "visual-engineering": {
      "model": "google/gemini-3.1-pro-preview",
      "variant": "high",
      "prompt_append": "Always respond in Simplified Chinese.",
      "description": "前端、用户界面、CSS、设计。推荐Gemini 3 Pro → GLM 5 → Claude Opus"
    },
    "ultrabrain": {
      "model": "openai/gpt-5.3-codex",
      "variant": "xhigh",
      "prompt_append": "Always respond in Simplified Chinese.",
      "description": "需要最大限度的推理。推荐GPT-5.3 Codex → Gemini 3 Pro → Claude Opus"
    },
    "deep": {
      "model": "openai/gpt-5.3-codex",
      "variant": "medium",
      "prompt_append": "Always respond in Simplified Chinese.",
      "description": "深度编码，复杂逻辑。推荐GPT-5.3 Codex → Claude Opus → Gemini 3 Pro"
    },
    "artistry": {
      "model": "google/gemini-3.1-pro-preview",
      "variant": "high",
      "prompt_append": "Always respond in Simplified Chinese.",
      "description": "富有创意、新颖的方法。推荐Gemini 3 Pro → Claude Opus → GPT-5.2"
    },
    "quick": {
      "model": "anthropic/claude-haiku-4-5",
      "prompt_append": "Always respond in Simplified Chinese.",
      "description": "简单、快速的任务。推荐Claude Haiku → Gemini Flash → GPT-5-Nano"
    },
    "unspecified-low": {
      "model": "openai/gpt-5.3-codex",
      "variant": "medium",
      "prompt_append": "Always respond in Simplified Chinese.",
      "description": "一般标准工作。推荐Claude Sonnet → GPT-5.3 Codex → Gemini Flash"
    },
    "unspecified-high": {
      "model": "openai/gpt-5.2",
      "variant": "high",
      "prompt_append": "Always respond in Simplified Chinese.",
      "description": "一般复杂工作。推荐Claude Opus → GPT-5.2 → Gemini 3 Pro"
    },
    "writing": {
      "model": "google/gemini-3-flash-preview",
      "prompt_append": "Always respond in Simplified Chinese.",
      "description": "文本、文档、散文。推荐Gemini Flash → Claude Sonnet"
    }
  }
}
```
