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

## oh-my-openagent 相关

项目目前使用[oh-my-openagent](https://github.com/code-yeongyu/oh-my-openagent)辅助开发。
开源AI智能体框架，根据各个AI擅长的方向定向发布任务。

### AI简介

现在的AI付费模式就两种，即订阅付费或者按量付费。

- 订阅付费：按月/按季/按年付费。通常会有每5小时额度、以及每周额度/每月额度。
  便宜中转站的话可能只有每天额度，甚至还能当天没用完的结转到第二天。
- 按量付费：用多少付多少，通常比订阅要贵——正常来讲确实是这样，但是某些中转站可以1块钱5刀……

下面是部分AI的介绍：

- OpenCode（大杂烩）
    - 订阅：[OpenCode Go](https://opencode.ai/zh/go)
        - 10刀/月，我没用过
    - 按量：[OpenCode Zen](https://opencode.ai/zh/zen)
        - 生成API-Key后使用（可以生成一个key然后无限用免费的Minimax-M2.5-Free）
    - 支付：可用国内卡或link，20刀额度收取1.23刀手续费用于维护OpenCode
    - 说明：能用国内卡支付，而且与别的AI供应商有合作，要真说从正版考虑的话这个确实是最优解

- ~~Anthropic（Claude）~~
    - 订阅：[claude.ai](https://claude.ai/)
        - 登录使用（Claude账号直接登录，不需要API-Key；配置Key后可以在超出限制的情况下临时使用按量计费）
    - 按量：[platform.claude.com](https://platform.claude.com/)
        - 生成API-Key后使用，Key只走按量
    - 支付：没有太好的方式，直接买成品号。Claude消耗用量比较快，我的pro订阅只用sonnet都不够日常开发。建议至少买max 5x（太贵了）。
    - 说明：极度不推荐，近期有史无前例的大风控，根据使用时间和语言会自动检测封号。

- OpenAI（ChatGPT）
    - 订阅：[chatgpt.com](https://chatgpt.com)，用量在[Codex](https://chatgpt.com/codex/settings/usage)里面看
        - 登录使用
    - 按量：[platform.openai.com](https://platform.openai.com/usage)
        - 生成API-Key后使用，Key只走按量
    - 支付：可以[用自己的ios订阅](https://yingtu.ai/zh/blog/chatgpt-plus-ios-recharge)
      ，也可以在[星际放映厅](https://www.naifeistation.com/)买成品号。
    - 说明：GPT改代码很强，但是成本考虑的话建议走白嫖的free号池（下面有说），就算有需求也去linux.do看看各种拼车team席位啥的。

- ~~Google（Gemini）~~
    - 订阅：~~[Google One AI订阅](https://one.google.com/)
      虽然只能在网页用，但是可以用[opencode-antigravity-auth](https://github.com/NoeFabris/opencode-antigravity-auth)
      实现使用以及多账号使用~~经测试发现还是不好用，不推荐
    - 按量：[aistudio.google.com](https://aistudio.google.com/)
        - 生成API-Key后使用，Key只走按量
    - 支付：没看到合适的。
    - 说明：极度不推荐买任何官方的，谷歌对于账号地区、IP地址非常敏感，很容易触发封号。建议直接找个中转站。

- ~~DeepSeek（DeepSeek）~~
    - 订阅：无
    - 按量：[platform.deepseek.com](https://platform.deepseek.com/)
        - 生成API-Key后使用，Key只走按量
    - 支付：没啥说的，国产模型随便付。
    - 说明：价格虽然说确实低，但是一方面有点笨，另一方面价格不如中转站。没有选择的理由= =

- Kimi（Kimi）
    - 订阅：[Kimi Code](https://www.kimi.com/code)
        - 生成API-Key后使用
    - 按量：[Kimi Code Console](https://www.kimi.com/code/console)
        - 生成API-Key后使用，Key先走订阅再走按量
    - 支付：没啥说的，国产模型随便付。
    - 说明：支持国产的话，买就完事了。用它替代Claude的角色。目前没看到有中转站搞Kimi中转的。

- Z.AI / Z.AI Coding Plan（GLM）
    - 订阅（Z.AI Coding Plan）：[国内站](https://bigmodel.cn/glm-coding)便宜，[国际站](https://z.ai/subscribe)贵
        - 生成API-Key后使用
    - 按量（Z.AI）：一样的网址
        - 生成API-Key后使用，Key先走订阅再走按量
    - 支付：可以用国内的卡（无论是国内站还是国际站都行），毕竟也是国产模型。
    - 说明：支持国产的话，买就完事了。用它替代Claude的角色。目前有一部分中转站有提供GLM中转的。
      有一点必须说明：GLM5改代码真的不行！！！如果他不能一次改好，他就会删除各种东西，甚至把文件里面内容都删了！

实测说明：

- Claude Sonnet综合能力高，理解需求很准确。缺点就是贵，而且会各种封号。
  可以考虑用中转站，然后再搞个Kimi备用（这个不是必须的，全用GPT效果也很棒）
- Chat GPT改代码顶尖，非常好用。
  而且现在还有很多公益站，可以白嫖GPT或者低价使用GPT。
- Gemini视觉设计最好。UI方面，可以使用Gemini Pro + ui-ux-pro-max（一个视觉方面的skill），非常强。
  这个只推荐中转站。

下面是部分推荐的，可以在opencode使用的中转站：

- [QuicklyAPI](https://sub.jlypx.de/)
    - 免费GPT号池。真是救命了！YYDS！

- [RightCode](https://right.codes/)
    - 有Claude、GPT、Gemini。非常便宜，1块=5刀。

- [PackyCode](https://www.packyapi.com/)
    - 有Claude、GPT、Gemini、Kimi、GLM、豆包、千问等等很多模型。便宜，1块=1刀。
      他们也卖GPT，好像一个月30-90R，反正GPT能从QuicklyAPI白嫖，就不管了。

### omo安装说明

下面是**Windows系统**安装oh-my-openagent（简称omo）的说明。

omo是opencode的一个插件。opencode主打的是可用多家AI，而omo则是在一次会话中，自动调用不同家的AI，扬长避短。

根据opencode官网的说明，opencode最好装在Linux系统下。
所以当我们是Windows系统的时候，需要先装wsl，再在wsl中安装一个Linux系统（下面以在wsl中安装Ubuntu为例）。

### 1.安装WSL并配置一些与其相关的内容

#### 1.1 安装[wsl](https://learn.microsoft.com/zh-cn/windows/wsl/install)

wsl相当于Linux框架，可以模拟Linux环境，方便在Windows上运行Linux命令和软件。

```
# 安装wsl
wsl --install
# 安装完成后，重启电脑以使wsl生效
# 通过wsl安装Ubuntu（你也可以换成别的Linux系统）
wsl.exe --install Ubuntu

# 进入Ubuntu系统，有两种方式：
# 方式1：先打开PowerShell，然后输入wsl即可进入
# 方式2：打开PowerShell，可与看到+的右边还有一个v，点一下这个v，选择Ubuntu即可
```

#### 1.2 解决wsl中使用apt会卡住的问题

先测试一下网络连通性。

```
# 注意要在wsl里面测，不要在PowerShell里面测
# 依次执行，按ctrl+c终止
# 先看看网络连接是否正常，这两个ping一般不会出问题
ping www.baidu.com
ping www.google.com
# 再看一下apt是否可用
sudo apt update
# 如果卡死在第一个网址，试一下这个
sudo apt-get -o Acquire::http::Proxy="http://127.0.0.1:7890" update
```

根据你是否有 VPN，处理方式不一样：

- **无 VPN**：直接换 apt 源，别的不用做。
- **有 VPN**：如果 apt 仍然卡住，再继续看下面的代理/TUN通道配置。

##### 1.2.1 无 VPN：直接换 apt 源

如果你平时不用 VPN，最简单的方式就是直接换国内 apt 源，不要折腾代理。

下面给一个 Ubuntu 的常见示例（清华源）：

```
# 先备份
sudo cp /etc/apt/sources.list /etc/apt/sources.list.bak

# 用编辑器打开 sources.list
sudo nano /etc/apt/sources.list
```

将里面内容替换成你对应 Ubuntu 版本的国内源，例如：

```
deb https://mirrors.tuna.tsinghua.edu.cn/ubuntu/ noble main restricted universe multiverse
deb https://mirrors.tuna.tsinghua.edu.cn/ubuntu/ noble-updates main restricted universe multiverse
deb https://mirrors.tuna.tsinghua.edu.cn/ubuntu/ noble-backports main restricted universe multiverse
deb https://mirrors.tuna.tsinghua.edu.cn/ubuntu/ noble-security main restricted universe multiverse
```

保存之后执行：

```
sudo apt update
```

如果 `apt update` 正常，就说明无 VPN 场景已经处理完了，后面关于代理的内容都不用看。

##### 1.2.2 有 VPN：让 WSL 的 apt 正常走代理

假如用apt的时候必须要显式设置Proxy（就是上面的最后一行指令，添加-o参数），大概率是wsl没有走代理的TUN通道。

我们先不说怎么解决，先讲清楚为什么要解决。

试想一下，如果AI遇到了“需要用apt安装某个东西”的场景，它会遇到两个问题：

- sudo需要root账户密码，但是AI不知道密码是什么，它只能告诉你“我没有权限，你来执行xxx指令”
- apt必须显式指定代理，但是AI不知道需要显式指定，它只会认为你的网络有问题

这两个问题都是可以解决的。

- 新开终端不需要输入sudo（这个视情况决定要不要，毕竟给AI最高级权限很危险，而且用到sudo的场景没那么多）

```
# 使用visudo打开sudoers编辑器（visudo会在保存前检查语法错误，防止把自己锁在系统外）
sudo visudo
# 滚动到文件结尾，添加。mlj是用户名
mlj ALL=(ALL) NOPASSWD: ALL
# 按 Ctrl + O 然后回车保存，再按 Ctrl + X 退出
# 新开终端，验证效果
sudo ls
```

- apt无需显式指定代理

这个步骤比较多，一步步来。

第一步：将wsl设置为镜像模式

在镜像模式下，WSL 和 Windows 共享网络栈，直接用 127.0.0.1 就能访问宿主机的代理。

按下 Win + R，输入 %UserProfile% 并回车。在该目录下寻找或新建一个名为 .wslconfig 的文件。
用记事本打开，添加以下配置：

```
[wsl2]
networkingMode=mirrored
```

文件保存好之后，打开PowerShell，重启wsl终端。

```
# 在PowerShell中执行此操作
wsl --shutdown
```

第二步：确认代理状态

代理要开启“允许局域网”，不然wsl访问不到。
下面的教程中，端口用7890，你可以在代理中确认你用的端口（clash的第一行就是端口的设置）。

第三步：在wsl中设置apt使用的Proxy

```
# 在wsl中执行此操作
sudo tee /etc/apt/apt.conf.d/99proxy <<EOF
Acquire::http::Proxy "http://127.0.0.1:7890";
Acquire::https::Proxy "http://127.0.0.1:7890";
EOF

# 测试不显式指定代理的情况下，apt能不能用
sudo apt update
```

此时应该可以不附加任何参数使用apt了。

### 2.安装opencode、oh-my-openagent（下面简称omo）

**重点：下面的内容都是在wsl里面执行的！！！**

#### 2.1 安装一些前置工具

先安装nvm，通过nvm安装最新版Nodejs和npm。

```
# 回到用户目录
# 这一点非常关键，如果不回，比如你在/mnt/c/xxx的这种位置，就可能直接安到Windows里面而不是linux，后面就各种MCP连不上啥的
cd ~
# 安装nvm
curl -o- https://raw.githubusercontent.com | bash
# curl 用不了的话，也可以使用 wget 安装nvm
# wget -qO- https://raw.githubusercontent.com | bash
# 安装好之后刷新使命令生效
source ~/.bashrc
# 安装最新LTS版Nodejs和npm
nvm install --lts
# 查阅版本
node -v
npm -v
# 查阅路径
# 这一步一定要做，确认目录是在/home/mlj/.nvm/versions/node/v24.14.0/bin/node这样的位置，而不是在/mnt/c/xxx的这种位置
which node
which npm
```

安装一些小工具，dotnet-sdk包（不要问为什么8.0和10.0都要装，照做就是了），然后再安装opencode。

```
# 更新包列表
sudo apt-get update
# 安装一堆东西
sudo apt-get install -y \
    dotnet-sdk-8.0 dotnet-sdk-10.0 \
    unzip curl git ca-certificates
```

#### 2.2 手动安装opencode

安装opencode。

```
# 安装opencode
curl -fsSL https://opencode.ai/install | bash
```

将opencode加入PATH中，方便后续调用。

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

#### 2.3 打开opencode，让AI安装oh-my-openagent

```
# 打开opencode
opencode
```

此时应该显示为build模式，模型可以用免费的Minimax-M2.5-Free（opencode自带）。
然后发如下消息，让AI帮忙安装omo：

```
Install and configure oh-my-opencode by following the instructions here:
https://raw.githubusercontent.com/code-yeongyu/oh-my-openagent/refs/heads/dev/docs/guide/installation.md
使用中文回答。
```

等待一会，当其询问“你有哪些订阅”的时候，根据实际情况回复即可。
不要说多余的话，后面可以自行调整配置文件。

继续等待安装完成。完成后，ctrl+z退出opencode。

此时重新再打开opencode，就是omo的界面了。但是在这之前，应该先编辑配置文件。

### 3.修改opencode配置文件，配好所有模型

#### 3.1 配置opencode已经提供好的厂商

如果你是在各个官网订阅的AI，那么就通过这个方式配置。这种方式**不需要修改配置文件**。

```
# 打开opencode的内置统一配置登录大部分官方AI的页面
opencode auth login
```

在这个页面中，可以直接搜索对应AI厂商的名字（最上面有search），也可以上下切换慢慢找。

选中某一个AI之后，回车之后可能要登录账号（比如Claude），也可能要输入API Key。

一次只能录入一个AI的信息。当你有多个官方的AI，就多执行几次`opencode auth login`，全配置好就可以了。

#### 3.2 打开opencode配置文件

默认会在这里生成一个配置文件：`~/.config/opencode/opencode.json`

可以在Linux系统里面编辑，但是我更推荐直接从Windows的资源管理器打开，然后用记事本之类的工具编辑。

```
# 打开Ubuntu系统的位置
# 先打开Windows资源管理器，然后在地址栏输入以下内容并回车：
\\wsl.localhost\Ubuntu
```

根据配置文件的路径，很容易就找到它在Windows系统下的路径：`\\wsl.localhost\Ubuntu\home\用户名\.config\opencode\opencode.json`

这个文件可以配置：

- Skill，也就是AI所能使用的技能，相当于一些简短的提示词
- MCP，上下文协议，相当于教会AI如何使用Skill
- 第三方的一些中转站的接口和他们的模型

#### 3.3 配置一些第三方中转站、Skill、MCP

涉及到这些内容的配置，建议使用**手动修改配置文件**的方式。目前的一些opencode配置工具我感觉都不好用。

如果你是用了一些中转站，它们会给你以下内容，有了这些就可以配置了：

- url
- 模型类型（anthropic/google/opanai/openai-comtiable）
- 具体可用模型的名称

当然也有一些中转站会直接给你配置文件，复制粘贴就行了。

下面是一个样例，格式自己研究，添加新的第三方也是一样的道理：

``` opencode.json
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "context7": {
      "command": [
        "npx",
        "-y",
        "@upstash/context7-mcp"
      ],
      "enabled": true,
      "type": "local"
    },
    "memory": {
      "command": [
        "npx",
        "-y",
        "@modelcontextprotocol/server-memory"
      ],
      "enabled": true,
      "type": "local"
    },
    "sequential-thinking": {
      "command": [
        "npx",
        "-y",
        "@modelcontextprotocol/server-sequential-thinking"
      ],
      "enabled": true,
      "type": "local"
    }
  },
  "plugin": [
    "oh-my-opencode@latest"
  ],
  "provider": {
    "packycodegoogle": {
      "models": {
        "gemini-3-flash-preview": {
          "name": "Gemini 3 Flash Preview"
        },
        "gemini-3.1-pro-preview": {
          "name": "Gemini 3.1 Pro Preview"
        }
      },
      "name": "PackyCode",
      "npm": "@ai-sdk/google",
      "options": {
        "apiKey": "此处填写API-Key",
        "baseURL": "https://www.packyapi.com/v1"
      }
    },
    "quicklyapiopenai": {
      "models": {
        "gpt-5.3-codex": {
          "limit": {
            "context": 400000,
            "output": 128000
          },
          "name": "GPT-5.3 Codex",
          "options": {
            "store": false
          },
          "variants": {
            "high": {},
            "low": {},
            "medium": {},
            "xhigh": {}
          }
        },
        "gpt-5.4": {
          "limit": {
            "context": 1050000,
            "output": 128000
          },
          "name": "GPT-5.4",
          "options": {
            "store": false
          },
          "variants": {
            "high": {},
            "low": {},
            "medium": {},
            "xhigh": {}
          }
        },
        "gpt-5.4-mini": {
          "limit": {
            "context": 200000,
            "output": 100000
          },
          "name": "GPT-5.4 Mini",
          "options": {
            "store": false
          },
          "variants": {
            "high": {},
            "low": {},
            "medium": {}
          }
        }
      },
      "name": "QuicklyAPI_OpenAI",
      "npm": "@ai-sdk/openai",
      "options": {
        "apiKey": "此处填写API-Key",
        "baseURL": "https://sub.jlypx.de/v1"
      }
    },
    "rightcodeanthropic": {
      "models": {
        "claude-haiku-4-5": {
          "attachment": true,
          "cost": {
            "cache_read": 0.1,
            "cache_write": 1.25,
            "input": 1,
            "output": 5
          },
          "id": "claude-haiku-4-5",
          "limit": {
            "context": 200000,
            "output": 64000
          },
          "modalities": {
            "input": [
              "text",
              "image",
              "pdf"
            ],
            "output": [
              "text"
            ]
          },
          "name": "Claude Haiku 4.5",
          "options": {
            "thinking": {
              "budgetTokens": 32000,
              "type": "enabled"
            }
          },
          "reasoning": true,
          "release_date": "2025-10-15",
          "temperature": true,
          "tool_call": true
        },
        "claude-opus-4-6": {
          "attachment": true,
          "cost": {
            "cache_read": 0.5,
            "cache_write": 6.25,
            "input": 5,
            "output": 25
          },
          "id": "claude-opus-4-6",
          "limit": {
            "context": 200000,
            "output": 64000
          },
          "modalities": {
            "input": [
              "text",
              "image"
            ],
            "output": [
              "text"
            ]
          },
          "name": "Claude Opus 4.6",
          "options": {
            "thinking": {
              "budgetTokens": 32000,
              "type": "enabled"
            }
          },
          "reasoning": true,
          "release_date": "2025-11-24",
          "temperature": true,
          "tool_call": true
        },
        "claude-sonnet-4-6": {
          "attachment": true,
          "cost": {
            "cache_read": 0.3,
            "cache_write": 3.75,
            "input": 3,
            "output": 15
          },
          "id": "claude-sonnet-4-6",
          "limit": {
            "context": 200000,
            "output": 64000
          },
          "modalities": {
            "input": [
              "text",
              "image"
            ],
            "output": [
              "text"
            ]
          },
          "name": "Claude Sonnet 4.6",
          "options": {
            "thinking": {
              "budgetTokens": 32000,
              "type": "enabled"
            }
          },
          "reasoning": true,
          "release_date": "2025-09-29",
          "temperature": true,
          "tool_call": true
        }
      },
      "name": "RCode_ClaudeCode",
      "npm": "@ai-sdk/anthropic",
      "options": {
        "apiKey": "此处填写API-Key",
        "baseURL": "https://right.codes/claude-aws/v1"
      }
    },
    "rightcodegoogle": {
      "models": {
        "gemini-3-flash-preview": {
          "attachment": true,
          "cost": {
            "cache_read": 0.05,
            "context_over_200k": {
              "cache_read": 0.05,
              "input": 0.5,
              "output": 3
            },
            "input": 0.5,
            "output": 3
          },
          "family": "gemini-flash",
          "id": "gemini-3-flash-preview",
          "limit": {
            "context": 1048576,
            "output": 65536
          },
          "modalities": {
            "input": [
              "text",
              "image",
              "video",
              "audio",
              "pdf"
            ],
            "output": [
              "text"
            ]
          },
          "name": "Gemini 3 Flash Preview",
          "reasoning": true,
          "release_date": "2025-12-17",
          "temperature": true,
          "tool_call": true
        },
        "gemini-3-pro-preview": {
          "attachment": true,
          "cost": {
            "cache_read": 0.2,
            "context_over_200k": {
              "cache_read": 0.4,
              "input": 4,
              "output": 18
            },
            "input": 2,
            "output": 12
          },
          "family": "gemini-pro",
          "id": "gemini-3-pro-preview",
          "limit": {
            "context": 1048576,
            "output": 65536
          },
          "modalities": {
            "input": [
              "text",
              "image",
              "video",
              "audio",
              "pdf"
            ],
            "output": [
              "text"
            ]
          },
          "name": "Gemini 3 Pro Preview",
          "reasoning": true,
          "release_date": "2025-11-18",
          "temperature": true,
          "tool_call": true
        },
        "gemini-3.1-pro-preview": {
          "attachment": true,
          "cost": {
            "cache_read": 0.2,
            "context_over_200k": {
              "cache_read": 0.4,
              "input": 4,
              "output": 18
            },
            "input": 2,
            "output": 12
          },
          "family": "gemini-pro",
          "id": "gemini-3.1-pro-preview",
          "limit": {
            "context": 1048576,
            "output": 65536
          },
          "modalities": {
            "input": [
              "text",
              "image",
              "video",
              "audio",
              "pdf"
            ],
            "output": [
              "text"
            ]
          },
          "name": "Gemini 3.1 Pro Preview",
          "reasoning": true,
          "release_date": "2025-11-18",
          "temperature": true,
          "tool_call": true
        }
      },
      "name": "RCode_Google",
      "npm": "@ai-sdk/google",
      "options": {
        "apiKey": "此处填写API-Key",
        "baseURL": "https://right.codes/gemini/v1"
      }
    },
    "rightcodeopenai": {
      "models": {
        "gpt-5.3-codex": {
          "attachment": true,
          "cost": {
            "cache_read": 0.175,
            "input": 1.75,
            "output": 14
          },
          "headers": {
            "conversation_id": "opencode-stable-user",
            "session_id": "opencode-stable-user",
            "x-session-id": "opencode-stable-user"
          },
          "limit": {
            "context": 272000,
            "output": 128000
          },
          "name": "GPT-5.3 Codex",
          "options": {
            "include": [
              "reasoning.encrypted_content"
            ],
            "promptCacheKey": "opencode-stable-user",
            "reasoningEffort": "high",
            "reasoningSummary": "auto",
            "store": false,
            "textVerbosity": "medium"
          },
          "reasoning": true,
          "temperature": false,
          "tool_call": true,
          "variants": {
            "high": {
              "reasoningEffort": "high",
              "reasoningSummary": "auto",
              "textVerbosity": "medium"
            },
            "low": {
              "reasoningEffort": "low",
              "reasoningSummary": "auto",
              "textVerbosity": "medium"
            },
            "medium": {
              "reasoningEffort": "medium",
              "reasoningSummary": "auto",
              "textVerbosity": "medium"
            },
            "xhigh": {
              "reasoningEffort": "xhigh",
              "reasoningSummary": "auto",
              "textVerbosity": "medium"
            }
          }
        },
        "gpt-5.4": {
          "attachment": true,
          "cost": {
            "cache_read": 0.175,
            "input": 1.75,
            "output": 14
          },
          "headers": {
            "conversation_id": "opencode-stable-user",
            "session_id": "opencode-stable-user",
            "x-session-id": "opencode-stable-user"
          },
          "limit": {
            "context": 272000,
            "output": 128000
          },
          "name": "GPT-5.4",
          "options": {
            "include": [
              "reasoning.encrypted_content"
            ],
            "promptCacheKey": "opencode-stable-user",
            "reasoningEffort": "high",
            "reasoningSummary": "auto",
            "store": false,
            "textVerbosity": "medium"
          },
          "reasoning": true,
          "temperature": false,
          "tool_call": true,
          "variants": {
            "high": {
              "reasoningEffort": "high",
              "reasoningSummary": "auto",
              "textVerbosity": "medium"
            },
            "low": {
              "reasoningEffort": "low",
              "reasoningSummary": "auto",
              "textVerbosity": "medium"
            },
            "medium": {
              "reasoningEffort": "medium",
              "reasoningSummary": "auto",
              "textVerbosity": "medium"
            },
            "xhigh": {
              "reasoningEffort": "xhigh",
              "reasoningSummary": "auto",
              "textVerbosity": "medium"
            }
          }
        },
        "gpt-5.4-mini": {
          "attachment": true,
          "cost": {
            "cache_read": 0.175,
            "input": 1.75,
            "output": 14
          },
          "headers": {
            "conversation_id": "opencode-stable-user",
            "session_id": "opencode-stable-user",
            "x-session-id": "opencode-stable-user"
          },
          "limit": {
            "context": 272000,
            "output": 128000
          },
          "name": "GPT-5.4 Mini",
          "options": {
            "include": [
              "reasoning.encrypted_content"
            ],
            "promptCacheKey": "opencode-stable-user",
            "reasoningEffort": "high",
            "reasoningSummary": "auto",
            "store": false,
            "textVerbosity": "medium"
          },
          "reasoning": true,
          "temperature": false,
          "tool_call": true,
          "variants": {
            "high": {
              "reasoningEffort": "high",
              "reasoningSummary": "auto",
              "textVerbosity": "medium"
            },
            "low": {
              "reasoningEffort": "low",
              "reasoningSummary": "auto",
              "textVerbosity": "medium"
            },
            "medium": {
              "reasoningEffort": "medium",
              "reasoningSummary": "auto",
              "textVerbosity": "medium"
            },
            "xhigh": {
              "reasoningEffort": "xhigh",
              "reasoningSummary": "auto",
              "textVerbosity": "medium"
            }
          }
        }
      },
      "name": "RCode_OpenAI",
      "npm": "@ai-sdk/openai",
      "options": {
        "apiKey": "此处填写API-Key",
        "baseURL": "https://right.codes/codex/v1",
        "setCacheKey": true
      }
    }
  }
}
```

### 4.修改oh-my-openagent配置文件

#### 4.1 查看所有能用的模型

先看看现在你能用什么模型：

```
# 查看当前能使用的所有model
opencode models
```

此时会显示格式为“厂商/模型名”的“model字符串”，我们就知道模型对应的字符串是什么了。例如：

```
deepseek/deepseek-chat -> deepseek日常对话模式，按量付费
deepseek/deepseek-reasoner -> deepseek深度推理模式，按量付费
zai/glm-5 -> glm5，按量付费
zai-coding-plan/glm-5 -> glm5，按订阅付费
```

注意：如果没有找到对应的ai字符串，说明你在第5步中没有配置好。只有配好的才会在这里显示。

记下来这些字符串，这就是接下来我们给omo配置时会用到的东西。

#### 4.2 打开并修改omo配置文件

默认会在这里生成一个配置文件：`~/.config/opencode/oh-my-opencode.json`

根据配置文件的路径，很容易就找到它在Windows系统下的路径：
`\\wsl.localhost\Ubuntu\home\用户名\.config\opencode\oh-my-opencode.json`

这个文件可以配置：

- 每个角色应该使用什么模型
- 当模型不可用时，根据回退链的配置依次调用其他模型
- 一些omo的实验性功能开关

注意，这个文件允许使用jsonc格式。根据omo的文档，`.jsonc`文件后缀优于`.json`文件后缀。
所以，可以**直接将这个文件后缀名修改为`.jsonc`**。

jsonc格式允许添加注释，并且结尾可以使用逗号。

对于每个角色所使用的模型如何选择，以及实验性功能有哪些，可以参考[configuration.md](https://github.com/code-yeongyu/oh-my-openagent/blob/dev/docs/reference/configuration.md)

下面是一个样例：

``` oh-my-opencode.jsonc
{
  "$schema": "https://raw.githubusercontent.com/code-yeongyu/oh-my-opencode/master/assets/oh-my-opencode.schema.json",
  "agents": {
    "atlas": {
      "fallback_models": [
        "rightcodeopenai/gpt-5.4"
      ],
      "model": "quicklyapiopenai/gpt-5.4",
      "prompt_append": "使用中文思考并回答。",
      "variant": "xhigh"
    },
    "explore": {
      "model": "opencode/minimax-m2.5-free",
      "prompt_append": "使用中文思考并回答。"
    },
    "hephaestus": {
      "fallback_models": [
        "rightcodeopenai/gpt-5.3-codex"
      ],
      "model": "quicklyapiopenai/gpt-5.3-codex",
      "prompt_append": "使用中文思考并回答。",
      "variant": "medium"
    },
    "librarian": {
      "model": "opencode/minimax-m2.5-free",
      "prompt_append": "使用中文思考并回答。"
    },
    "metis": {
      "fallback_models": [
        "rightcodeopenai/gpt-5.4"
      ],
      "model": "quicklyapiopenai/gpt-5.4",
      "prompt_append": "使用中文思考并回答。",
      "variant": "medium"
    },
    "momus": {
      "fallback_models": [
        "rightcodeopenai/gpt-5.4"
      ],
      "model": "quicklyapiopenai/gpt-5.4",
      "prompt_append": "使用中文思考并回答。",
      "variant": "xhigh"
    },
    "multimodal-looker": {
      "fallback_models": [
        "rightcodeopenai/gpt-5.3-codex"
      ],
      "model": "quicklyapiopenai/gpt-5.3-codex",
      "prompt_append": "使用中文思考并回答。",
      "variant": "medium"
    },
    "oracle": {
      "fallback_models": [
        "rightcodeopenai/gpt-5.4"
      ],
      "model": "quicklyapiopenai/gpt-5.4",
      "prompt_append": "使用中文思考并回答。",
      "variant": "high"
    },
    "prometheus": {
      "fallback_models": [
        "rightcodeopenai/gpt-5.4"
      ],
      "model": "quicklyapiopenai/gpt-5.4",
      "prompt_append": "Leverage deep & quick agents heavily, always in parallel. 使用中文思考并回答。",
      "variant": "xhigh"
    },
    "sisyphus": {
      "model": "rightcodeanthropic/claude-sonnet-4-6",
      "prompt_append": "使用中文思考并回答。",
      "variant": "medium"
    }
  },
  "categories": {
    "artistry": {
      "fallback_models": [
        "rightcodegoogle/gemini-3.1-pro-preview",
        "packycodegoogle/gemini-3.1-pro-preview"
      ],
      "model": "rightcodegoogle/gemini-3-pro-preview",
      "prompt_append": "使用中文思考并回答。"
    },
    "deep": {
      "fallback_models": [
        "rightcodeopenai/gpt-5.3-codex"
      ],
      "model": "quicklyapiopenai/gpt-5.3-codex",
      "prompt_append": "使用中文思考并回答。",
      "variant": "medium"
    },
    "quick": {
      "fallback_models": [
        "rightcodeopenai/gpt-5.4-mini"
      ],
      "model": "quicklyapiopenai/gpt-5.4-mini",
      "prompt_append": "使用中文思考并回答。"
    },
    "ultrabrain": {
      "fallback_models": [
        "rightcodeopenai/gpt-5.4"
      ],
      "model": "quicklyapiopenai/gpt-5.4",
      "prompt_append": "使用中文思考并回答。",
      "variant": "xhigh"
    },
    "unspecified-high": {
      "fallback_models": [
        "rightcodeopenai/gpt-5.4"
      ],
      "model": "quicklyapiopenai/gpt-5.4",
      "prompt_append": "使用中文思考并回答。",
      "variant": "high"
    },
    "unspecified-low": {
      "fallback_models": [
        "rightcodeopenai/gpt-5.3-codex"
      ],
      "model": "quicklyapiopenai/gpt-5.3-codex",
      "prompt_append": "使用中文思考并回答。",
      "variant": "medium"
    },
    "visual-engineering": {
      "fallback_models": [
        "rightcodegoogle/gemini-3.1-pro-preview",
        "packycodegoogle/gemini-3.1-pro-preview"
      ],
      "model": "rightcodegoogle/gemini-3-pro-preview",
      "prompt_append": "使用中文思考并回答。"
    },
    "writing": {
      "fallback_models": [
        "packycodegoogle/gemini-3-flash-preview"
      ],
      "model": "rightcodegoogle/gemini-3-flash-preview",
      "prompt_append": "使用中文思考并回答。"
    }
  },
  "experimental": {
    "aggressive_truncation": true,
    "auto_resume": true,
    "disable_omo_env": true,
    "dynamic_context_pruning": {
      "enabled": true,
      "notification": "detailed",
      "protected_tools": [
        "task",
        "todowrite",
        "todoread",
        "lsp_rename",
        "session_read",
        "session_write",
        "session_search"
      ],
      "strategies": {
        "deduplication": {
          "enabled": true
        },
        "purge_errors": {
          "enabled": true,
          "turns": 5
        },
        "supersede_writes": {
          "aggressive": false,
          "enabled": true
        }
      },
      "turn_protection": {
        "enabled": true,
        "turns": 3
      }
    },
    "task_system": true,
    "truncate_all_tool_outputs": false
  },
  "google_auth": false,
  "runtime_fallback": true,
  "sisyphus": {
    "tasks": {
      "claude_code_compat": false,
      "storage_path": ".sisyphus/tasks"
    }
  }
}
```

### omo使用说明

#### 如何使用web界面开发

opencode支持很多终端，例如命令行（感觉还可以）、Web端（非常好用，推荐）、用户端（跟Web一样但是功能好像有点少，不推荐）。

```
# 使用命令行
opencode

# 使用Web端（前台）
opencode web

# 使用Web端（后台）
nohup opencode web > /dev/null 2>&1 &
# 查找后台运行的opencode的PID
pgrep -a opencode
# 终止后台运行的opencode
kill -9 <PID>
```

除此之外，opencode还支持远程连接（只要你有自己的服务器）。配置好之后，就可以在手机上打开opencode的Web端界面了。

```
# 在你的 WSL Linux 环境里启动 OpenCode 的 Web 服务
opencode web --hostname 0.0.0.0 --port 4096
# --hostname 0.0.0.0：让服务监听所有网络接口。这很重要，因为 Windows 宿主机需要通过虚拟网卡访问 WSL 里的这个服务。
# --port 4096：服务运行在本地的 4096 端口。

# 在PowerShell中执行以下指令，从而完成核心的“穿透”步骤，达到远程转发的目的
ssh -R 4096:127.0.0.1:4096 root@你的服务器的ip
# 4096 (第一个)：告诉公网服务器（你的服务器的ip）监听它自己的 4096 端口。
# 127.0.0.1:4096：告诉 SSH，一旦公网服务器的 4096 端口收到流量，就把它发回到你本地电脑的 127.0.0.1:4096。
# root@你的服务器的ip：登录到你的云服务器。
# 服务器别忘了开放4096端口的限制，别拦截了
```

#### 如何用omo处理一个项目

先执行 /init-deep 来生成一些 Agents.md，这样后续AI协作更好。

.sisyphus文件夹建议加入到.gitignore中。

- 如果你比较懒，不知道一个东西是什么样子，那么可以用 ulw 模式。
    - 切换到 Sisyphus，在输入的内容中添加 ulw 这三个字母（必须显式添加），即可触发ulw进行全自动编排。
    - 例子：向 Sisyphus 发送“ulw 帮我设计一个成就系统”
    - 如果不添加ulw，那就是正常处理，但是这样的话建议用GPT，也就是 Hephaestus。
- 日常使用就是 Hephaestus，这个角色只允许使用GPT——刚好GPT可以白嫖。
- 如果遇到了很复杂的内容，而且你想先规划一下，确认各种细节再执行，那么：
    - 切换到 Prometheus，这个角色只会指定计划，不会执行。
    - 等到计划执行好了，切换到 Atlas，这个角色只能执行 Prometheus 定好的计划。
    - 特别注意：同一时间只能运行一个计划！！！并且计划的执行步骤比较繁琐，需要耗时很久。

#### 额外附加内容1：wsl安装中文字体

```
# 默认下载的是没中文字体的，中文都是方块
sudo apt-get install -y fonts-noto-cjk fonts-wqy-microhei
# 安装完之后在Powershell里面执行重启命令
wsl --shutdown
```

#### git push不需要登录账户，直接使用Windows的git登录状态

```
# 没配置之前，AI只能本地Commit；跟远程仓库交互的时候总是需要验证身份，很麻烦
# 配置之后，AI就能跟远程仓库交互了
# 假设你安装了Git（不是GithubDesktop），位置是C:\Program Files\Git\mingw64\bin\git-credential-manager.exe
# 那么在wsl里面执行：
git config --global credential.helper "/mnt/c/Program\ Files/Git/mingw64/bin/git-credential-manager.exe"
# 原理是Git身份认证直接使用Windows的Git的身份认证，所以只要Windows的Git登陆了，wsl里面就可以直接用了
# 这个时候让AI测试一下，应该就不需要密码了
```
