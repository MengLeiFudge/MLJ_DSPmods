# 品质系统 (Quality System)

## 概述

品质系统为游戏中的物品添加了5个品质等级，通过ID偏移来区分不同品质的物品。

## 品质等级规则

- **一级品质（普通）**: 基础物品ID（1000-10000）
- **二级品质（优良）**: 基础ID + 10000
- **三级品质（精良）**: 基础ID + 20000
- **四级品质（史诗）**: 基础ID + 30000
- **五级品质（传说）**: 基础ID + 40000

### 示例

| 物品 | 基础ID | 二级品质  | 三级品质  | 四级品质  | 五级品质  |
|----|------|-------|-------|-------|-------|
| 铁矿 | 1001 | 11001 | 21001 | 31001 | 41001 |
| 铜矿 | 1002 | 11002 | 21002 | 31002 | 41002 |
| 铁块 | 1101 | 11101 | 21101 | 31101 | 41101 |

## 使用方法

### 1. 基本ID转换

```csharp
using FE.Utils;

// 获取二级品质的铁矿ID
int ironOreQuality2 = QualitySystem.GetQualityItemId(1001, 2); // 返回 11001

// 获取五级品质的铜块ID
int copperBlockQuality5 = QualitySystem.GetQualityItemId(1104, 5); // 返回 41104
```

### 2. 提取品质等级

```csharp
// 从品质物品ID获取品质等级
int quality = QualitySystem.GetQualityLevel(21001); // 返回 3（三级品质）

// 从基础物品ID获取品质等级
int baseQuality = QualitySystem.GetQualityLevel(1001); // 返回 1（一级品质）
```

### 3. 获取基础物品ID

```csharp
// 从品质物品ID获取基础物品ID
int baseId = QualitySystem.GetBaseItemId(31001); // 返回 1001（铁矿）

// 基础物品ID本身也会返回自己
int baseId2 = QualitySystem.GetBaseItemId(1001); // 返回 1001
```

### 4. 判断物品类型

```csharp
// 判断是否为有效的基础物品ID
bool isBase = QualitySystem.IsValidBaseItemId(1001); // 返回 true
bool isBase2 = QualitySystem.IsValidBaseItemId(11001); // 返回 false

// 判断是否为品质物品（二级及以上）
bool isQuality = QualitySystem.IsQualityItem(21001); // 返回 true
bool isQuality2 = QualitySystem.IsQualityItem(1001); // 返回 false
```

### 5. 获取所有品质等级ID

```csharp
// 获取铁矿的所有品质等级ID
int[] allQualities = QualitySystem.GetAllQualityIds(1001);
// 返回: [1001, 11001, 21001, 31001, 41001]
// 索引0=一级, 索引1=二级, 索引2=三级, 索引3=四级, 索引4=五级
```

### 6. 批量生成品质物品ID

```csharp
// 为多个物品生成二级品质ID
int[] baseItems = { 1001, 1002, 1003, 1101, 1102 };
Dictionary<int, int> quality2Map = QualitySystem.BatchGenerateQualityIds(baseItems, 2);
// 返回: { 1001: 11001, 1002: 11002, 1003: 11003, 1101: 11101, 1102: 11102 }

// 为多个物品生成所有品质等级ID
Dictionary<int, int[]> allQualitiesMap = QualitySystem.BatchGenerateAllQualityIds(baseItems);
// 返回: { 1001: [1001, 11001, 21001, 31001, 41001], ... }
```

### 7. 获取所有基础物品ID

```csharp
// 通过反射从ProtoID.cs中提取所有基础物品ID
List<int> allBaseItems = QualitySystem.GetAllBaseItemIds();
// 返回所有在1000-10000范围内的物品ID列表
```

### 8. UI显示辅助方法

```csharp
// 获取品质名称
string qualityName = QualitySystem.GetQualityName(3); // 返回 "精良"

// 获取品质颜色
UnityEngine.Color qualityColor = QualitySystem.GetQualityColor(5); 
// 返回橙色 (1.0f, 0.5f, 0.0f) - 传说品质
```

## 品质颜色方案

| 品质等级 | 名称 | 颜色 | RGB值            |
|------|----|----|-----------------|
| 1    | 普通 | 白色 | (1.0, 1.0, 1.0) |
| 2    | 优良 | 绿色 | (0.0, 1.0, 0.0) |
| 3    | 精良 | 蓝色 | (0.0, 0.5, 1.0) |
| 4    | 史诗 | 紫色 | (0.6, 0.0, 1.0) |
| 5    | 传说 | 橙色 | (1.0, 0.5, 0.0) |

## 完整示例：批量创建高品质物品

```csharp
using FE.Utils;
using CommonAPI.Systems;

public class QualityItemCreator {
    public static void CreateQualityItems() {
        // 1. 获取所有基础物品ID
        List<int> baseItemIds = QualitySystem.GetAllBaseItemIds();
        
        // 2. 为每个基础物品创建2-5级品质版本
        foreach (int baseId in baseItemIds) {
            ItemProto baseItem = LDB.items.Select(baseId);
            if (baseItem == null) continue;
            
            // 为品质2-5创建物品
            for (int quality = 2; quality <= QualitySystem.MaxQuality; quality++) {
                int qualityId = QualitySystem.GetQualityItemId(baseId, quality);
                string qualityName = QualitySystem.GetQualityName(quality);
                Color qualityColor = QualitySystem.GetQualityColor(quality);
                
                // 创建品质物品名称
                string itemName = $"{baseItem.name} ({qualityName})";
                string itemDesc = $"{qualityName}品质的{baseItem.name}";
                
                // 注册新物品（示例，实际实现需要根据项目结构调整）
                // ItemProto qualityItem = ProtoRegistry.RegisterItem(
                //     qualityId, 
                //     itemName, 
                //     itemDesc,
                //     baseItem.IconPath,
                //     baseItem.GridIndex,
                //     baseItem.StackSize,
                //     baseItem.Type,
                //     ProtoRegistry.GetDefaultIconDesc(Color.white, qualityColor)
                // );
                
                // 可以在这里设置品质物品的特殊属性
                // 例如：更高的能量值、更好的效果等
            }
        }
    }
}
```

## 注意事项

1. **ID范围限制**: 品质系统只适用于ID在1000-10000范围内的物品
2. **ID冲突**: 确保生成的品质物品ID不与现有物品ID冲突
3. **性能考虑**: `GetAllBaseItemIds()`使用反射，建议在初始化时调用一次并缓存结果
4. **游戏兼容性**: 创建新物品需要使用ProtoRegistry等API，确保在正确的游戏生命周期阶段调用

## 扩展建议

后续可以添加以下功能：

1. **品质属性加成系统**: 为不同品质等级定义属性加成（如产量、速度、能耗等）
2. **品质升级系统**: 实现低品质物品升级为高品质物品的机制
3. **品质配方系统**: 为品质物品创建专门的配方
4. **品质显示系统**: 在UI中显示物品品质标识
5. **品质筛选系统**: 在物流系统中支持按品质筛选物品

## 版本历史

- **v1.0**: 初始版本，实现基础的品质ID转换和批量生成功能
