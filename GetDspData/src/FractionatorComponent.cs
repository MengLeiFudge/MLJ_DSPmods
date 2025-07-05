using System.IO;
using UnityEngine;

namespace GetDspData;

/// <summary>
/// 此类为原版分馏代码，主要用于分析原版的分馏逻辑
/// </summary>
public struct FractionatorComponent {
    public int id;
    public int entityId;
    public int pcId;
    public int belt0;
    public int belt1;
    public int belt2;
    public bool isOutput0;
    public bool isOutput1;
    public bool isOutput2;
    public bool isWorking;
    public float produceProb;
    public int fluidId;
    public int productId;
    public int fluidInputCount;
    public float fluidInputCargoCount;
    public int fluidInputInc;
    public int productOutputCount;
    public int fluidOutputCount;
    public int fluidOutputInc;
    public int progress;
    public bool fractionSuccess;
    public bool incUsed;
    public int fluidInputMax;
    public int fluidOutputMax;
    public int productOutputMax;
    public int fluidOutputTotal;
    public int productOutputTotal;
    public uint seed;

    public int incLevel {
        get {
            if (fluidInputCount <= 0 || fluidInputInc <= 0)
                return 0;
            int num = fluidInputInc / fluidInputCount;
            return num >= 10 ? 10 : num;
        }
    }

    public float extraIncProduceProb {
        get {
            if (fluidInputCount <= 0 || fluidInputInc <= 0)
                return 0.0f;
            int num = fluidInputInc / fluidInputCount;
            int index = num < 10 ? num : 10;
            return (float)Cargo.accTableMilli[index];
        }
    }

    public void SetEmpty() {
        id = 0;
        entityId = 0;
        pcId = 0;
        belt0 = 0;
        belt1 = 0;
        belt2 = 0;
        isOutput0 = false;
        isOutput1 = false;
        isOutput2 = false;
        isWorking = false;
        produceProb = 0.0f;
        fluidId = 0;
        productId = 0;
        fluidInputCount = 0;
        fluidInputCargoCount = 0.0f;
        fluidInputInc = 0;
        productOutputCount = 0;
        fluidOutputCount = 0;
        fluidOutputInc = 0;
        progress = 0;
        fractionSuccess = false;
        incUsed = false;
        fluidInputMax = 0;
        productOutputMax = 0;
        fluidOutputMax = 0;
        fluidOutputTotal = 0;
        productOutputTotal = 0;
        seed = 0U;
    }

    public void Export(BinaryWriter w) {
        w.Write(5);
        w.Write(id);
        w.Write(entityId);
        w.Write(pcId);
        w.Write(belt0);
        w.Write(belt1);
        w.Write(belt2);
        w.Write(isOutput0);
        w.Write(isOutput1);
        w.Write(isOutput2);
        w.Write(isWorking);
        w.Write(produceProb);
        w.Write(fluidId);
        w.Write(productId);
        w.Write(fluidInputCount);
        w.Write(fluidInputInc);
        w.Write(fluidInputCargoCount);
        w.Write(productOutputCount);
        w.Write(fluidOutputCount);
        w.Write(fluidOutputInc);
        w.Write(fluidOutputCount);
        w.Write(progress);
        w.Write(false);
        w.Write(fractionSuccess);
        w.Write(incUsed);
        w.Write(fluidInputMax);
        w.Write(productOutputMax);
        w.Write(fluidOutputMax);
        w.Write(fluidOutputTotal);
        w.Write(productOutputTotal);
        w.Write(seed);
    }

    public void Import(BinaryReader r) {
        int num = r.ReadInt32();
        id = r.ReadInt32();
        entityId = r.ReadInt32();
        pcId = r.ReadInt32();
        belt0 = r.ReadInt32();
        belt1 = r.ReadInt32();
        belt2 = r.ReadInt32();
        isOutput0 = r.ReadBoolean();
        isOutput1 = r.ReadBoolean();
        isOutput2 = r.ReadBoolean();
        isWorking = r.ReadBoolean();
        produceProb = r.ReadSingle();
        fluidId = r.ReadInt32();
        productId = r.ReadInt32();
        fluidInputCount = r.ReadInt32();
        if (num >= 2) {
            fluidInputInc = r.ReadInt32();
            fluidInputCargoCount = num < 3 ? r.ReadInt32() : r.ReadSingle();
        } else {
            fluidInputInc = 0;
            fluidInputCargoCount = fluidInputCount;
        }
        productOutputCount = r.ReadInt32();
        fluidOutputCount = r.ReadInt32();
        if (num >= 2) {
            fluidOutputInc = r.ReadInt32();
            r.ReadInt32();
        } else
            fluidOutputInc = 0;
        progress = r.ReadInt32();
        r.ReadBoolean();
        fractionSuccess = r.ReadBoolean();
        incUsed = num < 4 ? fluidInputCount > 0 && fluidInputInc > 0 : r.ReadBoolean();
        fluidInputMax = r.ReadInt32();
        productOutputMax = r.ReadInt32();
        fluidOutputMax = r.ReadInt32();
        if (num >= 1) {
            fluidOutputTotal = r.ReadInt32();
            productOutputTotal = r.ReadInt32();
        }
        seed = r.ReadUInt32();
    }

    public void SetPCState(PowerConsumerComponent[] pcPool) {
        double num1 = fluidInputCargoCount > 0.0001
            ? fluidInputCount / (double)fluidInputCargoCount
            : 4.0;
        double num2 = (fluidInputCargoCount < 30.0 ? fluidInputCargoCount : 30.0) * num1
                      - 30.0;
        if (num2 < 0.0)
            num2 = 0.0;
        int permillage = (int)((num2 * 50.0 + 1000.0) * Cargo.powerTableRatio[incLevel] + 0.5);
        pcPool[pcId].SetRequiredEnergy(isWorking, permillage);
    }

    public void SetRecipe(int needId, SignData[] signPool) {
        incUsed = false;
        RecipeProto[] fractionatorRecipes = RecipeProto.fractionatorRecipes;
        for (int index = 0; index < fractionatorRecipes.Length; ++index) {
            if (needId == fractionatorRecipes[index].Items[0]) {
                fluidId = needId;
                productId = fractionatorRecipes[index].Results[0];
                produceProb = fractionatorRecipes[index].ResultCounts[0]
                              / (float)fractionatorRecipes[index].ItemCounts[0];
                signPool[entityId].iconId0 = (uint)productId;
                signPool[entityId].iconType = productId == 0 ? 0U : 1U;
                break;
            }
        }
    }

    public uint InternalUpdate(
        PlanetFactory factory,
        float power,
        SignData[] signPool,
        int[] productRegister,
        int[] consumeRegister) {
        //没电就不工作
        if (power < 0.1)
            return 0;
        //计算输入缓存区物品的平均堆叠，上限4。平均堆叠越高，处理次数越多。
        //fluidInputCount表示总数，fluidInputCargoCount表示多少组。除一下就是每组多少，也就是平均堆叠。
        double fluidInputCountPerCargo = 1.0;
        if (fluidInputCount == 0)
            fluidInputCargoCount = 0.0f;
        else
            fluidInputCountPerCargo = fluidInputCargoCount > 0.0001
                ? fluidInputCount / (double)fluidInputCargoCount
                : 4.0;
        //只有输入缓存大于0且流动输出、产物输出都不堵的情况下，才进行内部分馏处理
        if (fluidInputCount > 0
            && productOutputCount < productOutputMax
            && fluidOutputCount < fluidOutputMax) {
            //计算进度
            //最后的0.75可能是向上取整的简化写法？暂且抛开。
            //500.0/3.0（也就是166.666）是一个系数，也不知道怎么算出来的，也先不管
            //效率与电力（power）成正比，可以适配电力不足的情况
            //效率与输入缓存物品数目（fluidInputCount）成正比，可以适配低级传送带、传送带非满带的情况
            //fluidInputCargoCount上限30是因为原版游戏最大就是30/s的带子
            progress +=
                (int)(power
                      * (500.0 / 3.0)
                      * (fluidInputCargoCount < 30.0 ? fluidInputCargoCount : 30.0)
                      * fluidInputCountPerCargo
                      + 0.75);
            //任何一个输出堵着的时候，显然不能无限加进度，不然流通的瞬间会全部处理。限定最高瞬间处理10次。
            if (progress > 100000)
                progress = 100000;
            //10000进度处理一次
            for (; progress >= 10000; progress -= 10000) {
                //计算平均每个物品携带的增产点数（因为是int，所以实际上是向下取整了）
                int fluidInputIncAvg = fluidInputInc <= 0 || fluidInputCount <= 0
                    ? 0
                    : fluidInputInc / fluidInputCount;
                //这个是新增监视面板要的东西，不管
                if (!incUsed)
                    incUsed = fluidInputIncAvg > 0;
                //基于线性同余生成器（LCG，seed = (a * seed + c) % m，其中a = 48271，m = int.MaxValue，c = 0）
                //+1U是为了避免0的情况（在这个LCG实现中，显然为0的情况下会一直卡死为0），最后再-1U确保后续计算
                //理论上48271U可以达到最大周期2147483646，且均匀分布，十分理想
                //总之，这是一个uint且均匀分布的随机数
                seed = (uint)((seed % 2147483646U + 1U) * 48271UL % int.MaxValue) - 1U;
                //seed / 2147483646.0显然就是一个0-1之间的随机数
                //原版分馏塔被设定为增产剂以加速的比例来提升分馏概率，而对产物数目无影响，所以此处乘accTableMilli
                fractionSuccess = seed / 2147483646.0
                                  < produceProb
                                  * (1.0 + Cargo.accTableMilli[fluidInputIncAvg < 10 ? fluidInputIncAvg : 10]);
                //成功或失败的处理
                //需要注意的是，此处仅有“数目”的变动，没有“可能生成了哪种物品”的判断。
                //这是因为流动输入物品id确定的情况下，产物输出物品id、流动输出物品id也就跟随着确定了（原版分馏塔数据结构就是这样设计）
                if (fractionSuccess) {
                    //分馏成功
                    //productOutput指正面输出缓存，原版分馏塔设定为输出不带增产点数
                    //产物+1
                    ++productOutputCount;
                    //产物总数统计+1
                    ++productOutputTotal;
                    lock (productRegister)
                        //全局这个产物的生成数+1
                        ++productRegister[productId];
                    lock (consumeRegister)
                        //全局这个原料的消耗数+1
                        ++consumeRegister[fluidId];
                } else {
                    //分馏失败
                    //fluidOutput指侧面流动输出缓存，原版分馏塔设定为失败对物品和增产点数都无影响，搬过去就行
                    //流动输出+1
                    ++fluidOutputCount;
                    //流动输出总数统计+1
                    ++fluidOutputTotal;
                    //增加增产点数（流动输入->流动输出）
                    fluidOutputInc += fluidInputIncAvg;
                }
                //无论分馏是否成功，原料都被处理了
                //原料-1
                --fluidInputCount;
                //减少增产点数（流动输入->流动输出（不变）或产物输出（变为0））
                fluidInputInc -= fluidInputIncAvg;
                //重新计算输入缓存区物品的平均堆叠（因为循环的上面用到了这个值）
                fluidInputCargoCount -= (float)(1.0 / fluidInputCountPerCargo);
                if (fluidInputCargoCount < 0.0)
                    fluidInputCargoCount = 0.0f;
            }
        } else
            fractionSuccess = false;
        CargoTraffic cargoTraffic = factory.cargoTraffic;
        byte stack;
        byte inc1;
        //belt1是左侧接口
        if (belt1 > 0) {
            if (isOutput1) {
                //作为输出处理
                if (fluidOutputCount > 0) {
                    int inc2 = fluidOutputInc / fluidOutputCount;
                    CargoPath cargoPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[belt1].segPathId);
                    //尝试放1个物品（如果是4堆叠那就要执行4次才能放完）
                    if (cargoPath != null
                        && cargoPath.TryUpdateItemAtHeadAndFillBlank(fluidId,
                            Mathf.CeilToInt((float)(fluidInputCountPerCargo - 0.1)),
                            1, (byte)inc2)) {
                        --fluidOutputCount;
                        fluidOutputInc -= inc2;
                        //这里重复写而不是用循环的原因是，原版游戏传送带最大速率为30/s，游戏为60帧/s，堆叠最大为4
                        //在这样的情况下，每帧尝试放2次，每次放1个物品，一秒最多可以放2*60/4=30叠物品，刚好是传送带速率
                        if (fluidOutputCount > 0) {
                            int inc3 = fluidOutputInc / fluidOutputCount;
                            if (cargoPath.TryUpdateItemAtHeadAndFillBlank(fluidId,
                                    Mathf.CeilToInt((float)(fluidInputCountPerCargo - 0.1)), 1, (byte)inc3)) {
                                --fluidOutputCount;
                                fluidOutputInc -= inc3;
                            }
                        }
                    }
                }
            } else if (!isOutput1 && fluidInputCargoCount < (double)fluidInputMax) {
                //作为输入处理
                if (fluidId > 0) {
                    //fluidId > 0表示分馏塔内部已有物品，此时只能接收相同id的物品
                    if (cargoTraffic.TryPickItemAtRear(belt1, fluidId, null, out stack, out inc1)
                        > 0) {
                        fluidInputCount += stack;
                        fluidInputInc += inc1;
                        ++fluidInputCargoCount;
                    }
                } else {
                    //没有物品则查看输入的物品是不是fractionatorNeeds的物品之一，只有在这里面的才会取
                    int needId = cargoTraffic.TryPickItemAtRear(belt1, 0, null, out stack,
                        out inc1);
                    if (needId > 0) {
                        fluidInputCount += stack;
                        fluidInputInc += inc1;
                        ++fluidInputCargoCount;
                        //设置分馏塔的输入物品id、输出物品id、图标等
                        SetRecipe(needId, signPool);
                    }
                }
            }
        }
        //belt2是右侧接口
        if (belt2 > 0) {
            if (isOutput2) {
                if (fluidOutputCount > 0) {
                    int inc4 = fluidOutputInc / fluidOutputCount;
                    CargoPath cargoPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[belt2].segPathId);
                    if (cargoPath != null
                        && cargoPath.TryUpdateItemAtHeadAndFillBlank(fluidId,
                            Mathf.CeilToInt((float)(fluidInputCountPerCargo - 0.1)),
                            1, (byte)inc4)) {
                        --fluidOutputCount;
                        fluidOutputInc -= inc4;
                        if (fluidOutputCount > 0) {
                            int inc5 = fluidOutputInc / fluidOutputCount;
                            if (cargoPath.TryUpdateItemAtHeadAndFillBlank(fluidId,
                                    Mathf.CeilToInt((float)(fluidInputCountPerCargo - 0.1)), 1, (byte)inc5)) {
                                --fluidOutputCount;
                                fluidOutputInc -= inc5;
                            }
                        }
                    }
                }
            } else if (!isOutput2 && fluidInputCargoCount < (double)fluidInputMax) {
                if (fluidId > 0) {
                    if (cargoTraffic.TryPickItemAtRear(belt2, fluidId, null, out stack, out inc1)
                        > 0) {
                        fluidInputCount += stack;
                        fluidInputInc += inc1;
                        ++fluidInputCargoCount;
                    }
                } else {
                    int needId = cargoTraffic.TryPickItemAtRear(belt2, 0, null, out stack,
                        out inc1);
                    if (needId > 0) {
                        fluidInputCount += stack;
                        fluidInputInc += inc1;
                        ++fluidInputCargoCount;
                        SetRecipe(needId, signPool);
                    }
                }
            }
        }
        //belt0是正面输出口
        //原版游戏成功率最高为2%，也就是产物最快为30*4*0.02=2.4/s。
        //这个值远小于一次判定且无堆叠输出的速率（30/s），所以只需要判定一次
        if (belt0 > 0
            && isOutput0
            && productOutputCount > 0
            && cargoTraffic.TryInsertItemAtHead(belt0, productId, 1, 0))
            --productOutputCount;
        //如果缓存区全部清空，才重置输入id，此时才能输入id在RecipeProto.fractionatorNeeds内的任意物品
        if (fluidInputCount == 0 && fluidOutputCount == 0 && productOutputCount == 0)
            fluidId = 0;
        //工作状态设定
        isWorking = fluidInputCount > 0
                    && productOutputCount < productOutputMax
                    && fluidOutputCount < fluidOutputMax;
        return !isWorking ? 0U : 1U;
    }

    public void AlterBelt(int slot, int belt, bool isOutput) {
        switch (slot) {
            case 0:
                belt0 = belt;
                isOutput0 = isOutput;
                break;
            case 1:
                belt1 = belt;
                isOutput1 = isOutput;
                break;
            case 2:
                belt2 = belt;
                isOutput2 = isOutput;
                break;
        }
    }

    public int split_inc(ref int n, ref int m, int p) {
        if (n == 0)
            return 0;
        int num1 = m / n;
        int num2 = m - num1 * n;
        n -= p;
        int num3 = num2 - n;
        int num4 = num3 > 0 ? num1 * p + num3 : num1 * p;
        m -= num4;
        return num4;
    }

    public int split_inc_level(ref int n, ref int m, int p) {
        if (n == 0)
            return 0;
        int num1 = m / n;
        int num2 = m - num1 * n;
        n -= p;
        int num3 = num2 - n;
        m -= num3 > 0 ? num1 * p + num3 : num1 * p;
        return num1;
    }
}
