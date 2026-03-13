# FEFractionatorWindow 设计文档

> 最后更新：2026-03-13
> 相关文件：`FractionateEverything/src/UI/Patches/FEFractionatorWindow.cs`

---

## 一、目标：模组分馏塔详情窗口

原版 DSP 的分馏塔详情窗口（`UIFractionatorWindow`）只能显示**单一产物**。
本模组新增的五种分馏塔（交互塔、矿物复制塔、点数聚集塔、转化塔、回收塔）支持**多产物**，需要专用窗口。

---

## 二、窗口布局（目标样式）

窗口尺寸与原版**完全一致，不随配方切换而改变**。

```
┌─────────────────────────────────────────────────────┐
│  [建筑名称] +N          [电力]  [状态]              │  ← 标题栏（原版不改）
│  [左：流体输入区，原版不改]                          │
├─────────────────────────────────────────────────────┤
│  主产物                  [图标1][图标2][图标3][图标4] │  ← Area 1
│  >>>                                                │
├─────────────────────────────────────────────────────┤  ← 分割线（原版 sepLine0/1）
│  副产物                  [图标1][图标2][图标3][图标4] │  ← Area 2
│  >>>                                                │
├─────────────────────────────────────────────────────┤  ← 克隆分割线
│  流体输出       [流体图标]  配方强化 +N              │  ← Area 3
│  >>>                       流动率%  [损毁率%]        │
└─────────────────────────────────────────────────────┘
```

### 各区域说明

| 区域 | 内容 | 位置来源 |
|---|---|---|
| **Area 1 主产物** | 标签"主产物" + 最多4个图标（含数量、概率） + 3个流动箭头 | 原版 productBox 位置（y 轴不变） |
| **Area 2 副产物** | 标签"副产物" + 最多4个图标（含数量、概率） + 克隆箭头 | 原版 oriProductBox 位置（y 轴不变） |
| **Area 3 流体输出** | 标签"流体输出" + 原版 oriProductBox（移至此处） + fluidRightText + 克隆箭头 | y = oriProductBox.y - areaHeight |

### 图标位置规则

- **固定不居中**：`slot[0].localPosition = 原版对应图标的 localPosition`
- `slot[i].x = slot[0].x + i × 55f`（SlotSpacing = 55f）
- 配方切换时图标个数变化，**只控制显隐，不移动位置**

### 颜色规则

- 概率文字：金色 `(1.0, 0.9, 0.3, 1.0)`
- 损毁率文字：红色 `(1.0, 0.35, 0.35, 1.0)`（通过 RichText `<color=...>` 内嵌）
- 流动率文字：正常白色

### fluidRightText 内容格式

```
配方强化 +N
流动率%  [损毁率%]
```

例：`配方强化 +3\n62.5%  <color=#FF5959FF>2.0%</color>`

---

## 三、实现架构

### 3.1 窗口生命周期

```
游戏初始化
  → UIFractionatorWindow._OnInit Postfix
  → Object.Instantiate 复制原版窗口 → modWindowGo
  → ApplyModLayoutOnce（一次性修改布局，此后不再移动元素）
  → modWindowGo.SetActive(false)

用户点击模组建筑
  → originalWindow._Open() → active=true
  → 原版 _OnOpen 执行（设置 factory/factorySystem/powerSystem/player）
  → [_OnOpen Postfix]
      originalWindow.gameObject.SetActive(false)   // 隐藏原版窗口
      originalWindow.unsafeGameObjectState = true  // 让游戏继续驱动其 _Update
      modWindowGo.SetActive(true)                  // 显示 modWindow

每帧
  → 游戏调用 originalWindow._Update()（因 active=true 且 unsafeGameObjectState=true）
  → 原版 _OnUpdate 被触发
  → [_OnUpdate Prefix] 拦截，执行 DoModWindowUpdate(originalWindow)，return false

用户关闭窗口
  → originalWindow._Close() → active=false
  → 原版 _OnClose 被触发
  → [_OnClose Prefix] 注销事件、隐藏 modWindow、复原 unsafeGameObjectState，return true
  → 原版 _OnClose 正常清理 factory/player/button 等字段
```

### 3.2 关键设计决策

| 决策 | 原因 |
|---|---|
| 用 `_OnOpen` **Postfix**（不 return false） | 让原版初始化 factory/player 等字段，避免手动重复初始化 |
| 保持 `originalWindow.active=true` + `unsafeGameObjectState=true` | 让游戏继续驱动 originalWindow._Update，进而触发我们的 Prefix 更新 modWindow |
| 用 `_OnUpdate` **Prefix**（return false） | 跳过原版 _OnUpdate 的显示逻辑（否则原版会重新显示 productBox 等） |
| `modWindow` 只用 `SetActive` 管理，不用 `_Open/_Close` | modWindow 未经 `_Init()` 初始化，`inited=false`，调用 `_Open()` 无效；且游戏不知道 modWindow 存在，无需接入游戏的 ManualBehaviour 生命周期 |
| `_OnClose` Prefix return true | 让原版 _OnClose 正常清理所有字段，不需要手动重复清理 |

### 3.3 避免的 Anti-Patterns

- ❌ 在 `_OnOpen` Prefix 里 return false 跳过原版 → 导致 factory/player 未初始化，关闭时 NPE
- ❌ 在 Prefix 里调用 `__instance._Close()` 关闭原版窗口 → player==null 时 NPE
- ❌ 用 `modWindowGo.SetActive(true)` 代替 `modWindow._Open()` → 但这是必须的（modWindow.inited=false）；只是更新驱动不能依赖 modWindow.active
- ❌ 在 `_OnUpdate` Postfix 里更新 modWindow → Postfix 需要原版 _OnUpdate 先被执行，但被 Prefix return false 之后 Postfix 不会运行

---

## 四、文件结构

### 修改的文件

| 文件 | 变更内容 |
|---|---|
| `UI/Patches/FEFractionatorWindow.cs` | **新增**：模组分馏塔独立窗口全部逻辑 |
| `Logic/Manager/ProcessManager.cs` | 新增翻译注册："主产物"、"副产物"、"流体输出"、"流动"、"损毁"、"配方强化" |

### 未修改的文件

- `Logic/Manager/ProcessManager.cs`（除翻译外）：原有分馏逻辑不变
- 原版窗口：不做任何永久性修改

---

## 五、Harmony Patch 一览

| Patch | 类型 | 目标方法 | 作用 |
|---|---|---|---|
| `CreateModWindowInstance` | Postfix | `UIFractionatorWindow._OnInit` | 复制窗口，一次性修改布局 |
| `OnWindowOpen` | Postfix | `UIFractionatorWindow._OnOpen` | 模组建筑时隐藏原版窗口，显示 modWindow |
| `OnWindowClose` | Prefix | `UIFractionatorWindow._OnClose` | 清理自定义事件，隐藏 modWindow，复原 unsafeGameObjectState |
| `OnWindowUpdate` | Prefix | `UIFractionatorWindow._OnUpdate` | modWindow 显示时拦截，执行 DoModWindowUpdate，return false |
| `OnProductUIButtonClick_Postfix` | Postfix | `UIFractionatorWindow.OnProductUIButtonClick` | 模组建筑的多产物点击取出逻辑 |

---

## 六、待确认/待完善项

- [ ] 窗口右侧 fluidRightText 的 x 偏移（当前 +80f）是否视觉上合适，需游戏内验证
- [ ] 克隆箭头（_sideArrows / _fluidArrows）的图片数量是否与 modWindow.speedArrows 一致（GetComponentsInChildren 的顺序依赖 Unity 层级）
- [ ] 当分馏塔没有配方时（recipe==null），Area1/Area2 全空，Area3 仍显示流体输出——是否符合预期
- [ ] `unsafeGameObjectState` 在切换星球或加载存档时是否会残留（正常 _OnClose 会复原，应无问题）
