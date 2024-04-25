using BepInEx.Configuration;
using CommonAPI;
using System.Collections.Generic;
using UnityEngine;
using xiaoye97;

namespace FractionateEverything
{
    class AcceptableIntValue(int defVal, int min, int max) : AcceptableValueBase(typeof(int))
    {
        private readonly int defVal = defVal >= min && defVal <= max ? defVal : min;
        public override object Clamp(object value) => IsValid(value) ? (int)value : defVal;
        public override bool IsValid(object value) => value.GetType() == ValueType && (int)value >= min && (int)value <= max;
        public override string ToDescriptionString() => null;
    }

    class AcceptableBoolValue(bool defVal) : AcceptableValueBase(typeof(bool))
    {
        public override object Clamp(object value) => IsValid(value) ? (bool)value : defVal;
        public override bool IsValid(object value) => value.GetType() == ValueType;
        public override string ToDescriptionString() => null;
    }

    static class ProtoID
    {
        internal const int I铁矿 = 1001;
        internal const int I铜矿 = 1002;
        internal const int I硅石 = 1003;
        internal const int I钛石 = 1004;
        internal const int I石矿 = 1005;
        internal const int I煤矿 = 1006;
        internal const int I木材 = 1030;
        internal const int I植物燃料 = 1031;
        internal const int I可燃冰 = 1011;
        internal const int I金伯利矿石 = 1012;
        internal const int I分形硅石 = 1013;
        internal const int I光栅石 = 1014;
        internal const int I刺笋结晶 = 1015;
        internal const int I单极磁石 = 1016;
        internal const int I铁块 = 1101;
        internal const int I铜块 = 1104;
        internal const int I高纯硅块 = 1105;
        internal const int I钛块 = 1106;
        internal const int I石材 = 1108;
        internal const int I高能石墨 = 1109;
        internal const int I钢材 = 1103;
        internal const int I钛合金 = 1107;
        internal const int I玻璃 = 1110;
        internal const int I钛化玻璃 = 1119;
        internal const int I棱镜 = 1111;
        internal const int I金刚石 = 1112;
        internal const int I晶格硅 = 1113;
        internal const int I齿轮 = 1201;
        internal const int I磁铁 = 1102;
        internal const int I磁线圈 = 1202;
        internal const int I电动机 = 1203;
        internal const int I电磁涡轮 = 1204;
        internal const int I超级磁场环 = 1205;
        internal const int I粒子容器 = 1206;
        internal const int I奇异物质 = 1127;
        internal const int I电路板 = 1301;
        internal const int I处理器 = 1303;
        internal const int I量子芯片 = 1305;
        internal const int I微晶元件 = 1302;
        internal const int I位面过滤器 = 1304;
        internal const int I粒子宽带 = 1402;
        internal const int I电浆激发器 = 1401;
        internal const int I光子合并器 = 1404;
        internal const int I太阳帆 = 1501;
        internal const int I水 = 1000;
        internal const int I原油 = 1007;
        internal const int I精炼油 = 1114;
        internal const int I硫酸 = 1116;
        internal const int I氢 = 1120;
        internal const int I重氢 = 1121;
        internal const int I反物质 = 1122;
        internal const int I临界光子 = 1208;
        internal const int I液氢燃料棒 = 1801;
        internal const int I氘核燃料棒 = 1802;
        internal const int I反物质燃料棒 = 1803;
        internal const int I奇异湮灭燃料棒 = 1804;
        internal const int I塑料 = 1115;
        internal const int I石墨烯 = 1123;
        internal const int I碳纳米管 = 1124;
        internal const int I有机晶体 = 1117;
        internal const int I钛晶石 = 1118;
        internal const int I卡西米尔晶体 = 1126;
        internal const int I燃烧单元 = 1128;
        internal const int I爆破单元 = 1129;
        internal const int I晶石爆破单元 = 1130;
        internal const int I引力透镜 = 1209;
        internal const int I空间翘曲器 = 1210;
        internal const int I湮灭约束球 = 1403;
        internal const int I动力引擎 = 1407;
        internal const int I推进器 = 1405;
        internal const int I加力推进器 = 1406;
        internal const int I配送运输机 = 5003;
        internal const int I物流运输机 = 5001;
        internal const int I星际物流运输船 = 5002;
        internal const int I框架材料 = 1125;
        internal const int I戴森球组件 = 1502;
        internal const int I小型运载火箭 = 1503;
        internal const int I地基 = 1131;
        internal const int I增产剂MkI = 1141;
        internal const int I增产剂MkII = 1142;
        internal const int I增产剂MkIII = 1143;
        internal const int I机枪弹箱 = 1601;
        internal const int I钛化弹箱 = 1602;
        internal const int I超合金弹箱 = 1603;
        internal const int I炮弹组 = 1604;
        internal const int I高爆炮弹组 = 1605;
        internal const int I晶石炮弹组 = 1606;
        internal const int I等离子胶囊 = 1607;
        internal const int I反物质胶囊 = 1608;
        internal const int I导弹组 = 1609;
        internal const int I超音速导弹组 = 1610;
        internal const int I引力导弹组 = 1611;
        internal const int I干扰胶囊 = 1612;
        internal const int I压制胶囊 = 1613;
        internal const int I原型机 = 5101;
        internal const int I精准无人机 = 5102;
        internal const int I攻击无人机 = 5103;
        internal const int I护卫舰 = 5111;
        internal const int I驱逐舰 = 5112;
        internal const int I黑雾矩阵 = 5201;
        internal const int I硅基神经元 = 5202;
        internal const int I物质重组器 = 5203;
        internal const int I负熵奇点 = 5204;
        internal const int I核心素 = 5205;
        internal const int I能量碎片 = 5206;
        internal const int I传送带 = 2001;
        internal const int I高速传送带 = 2002;
        internal const int I极速传送带 = 2003;
        internal const int I分拣器 = 2011;
        internal const int I高速分拣器 = 2012;
        internal const int I极速分拣器 = 2013;
        internal const int I集装分拣器 = 2014;
        internal const int I四向分流器 = 2020;
        internal const int I自动集装机 = 2040;
        internal const int I流速监测器 = 2030;
        internal const int I喷涂机 = 2313;
        internal const int I物流配送器 = 2107;
        internal const int I小型储物仓 = 2101;
        internal const int I大型储物仓 = 2102;
        internal const int I储液罐 = 2106;
        internal const int I制造台MkI = 2303;
        internal const int I制造台MkII = 2304;
        internal const int I制造台MkIII = 2305;
        internal const int I重组式制造台 = 2318;
        internal const int I电力感应塔 = 2201;
        internal const int I无线输电塔 = 2202;
        internal const int I卫星配电站 = 2212;
        internal const int I风力涡轮机 = 2203;
        internal const int I火力发电厂 = 2204;
        internal const int I微型聚变发电站 = 2211;
        internal const int I地热发电站 = 2213;
        internal const int I采矿机 = 2301;
        internal const int I大型采矿机 = 2316;
        internal const int I抽水站 = 2306;
        internal const int I电弧熔炉 = 2302;
        internal const int I位面熔炉 = 2315;
        internal const int I负熵熔炉 = 2319;
        internal const int I原油萃取站 = 2307;
        internal const int I原油精炼厂 = 2308;
        internal const int I化工厂 = 2309;
        internal const int I量子化工厂 = 2317;
        internal const int I分馏塔 = 2314;
        internal const int I太阳能板 = 2205;
        internal const int I蓄电器 = 2206;
        internal const int I蓄电器满 = 2207;
        internal const int I电磁轨道弹射器 = 2311;
        internal const int I射线接收站 = 2208;
        internal const int I垂直发射井 = 2312;
        internal const int I能量枢纽 = 2209;
        internal const int I微型粒子对撞机 = 2310;
        internal const int I人造恒星 = 2210;
        internal const int I行星内物流运输站 = 2103;
        internal const int I星际物流运输站 = 2104;
        internal const int I轨道采集器 = 2105;
        internal const int I矩阵研究站 = 2901;
        internal const int I自演化研究站 = 2902;
        internal const int I高斯机枪塔 = 3001;
        internal const int I高频激光塔 = 3002;
        internal const int I聚爆加农炮 = 3003;
        internal const int I磁化电浆炮 = 3004;
        internal const int I导弹防御塔 = 3005;
        internal const int I干扰塔 = 3006;
        internal const int I信号塔 = 3007;
        internal const int I行星护盾发生器 = 3008;
        internal const int I战场分析基站 = 3009;
        internal const int I近程电浆塔 = 3010;
        internal const int I电磁矩阵 = 6001;
        internal const int I能量矩阵 = 6002;
        internal const int I结构矩阵 = 6003;
        internal const int I信息矩阵 = 6004;
        internal const int I引力矩阵 = 6005;
        internal const int I宇宙矩阵 = 6006;
        internal const int I沙土 = 1099;
        internal const int I低功率分馏塔 = 2320;
        internal const int I建筑极速分馏塔 = 2321;
        internal const int I增殖分馏塔 = 2322;
        internal const int I引力发生装置 = 9480;
        internal const int I位面约束环 = 9481;
        internal const int I引力钻头 = 9482;
        internal const int I隧穿激发装置 = 9483;
        internal const int I谐振盘 = 9484;
        internal const int I光子探针 = 9485;
        internal const int I量子计算机 = 9486;
        internal const int I星际组装厂组件 = 9487;
        internal const int I物质解压器运载火箭 = 9488;
        internal const int I科学枢纽运载火箭 = 9489;
        internal const int I谐振发射器运载火箭 = 9490;
        internal const int I星际组装厂运载火箭 = 9491;
        internal const int I晶体重构器运载火箭 = 9492;
        internal const int I多功能集成组件 = 9500;
        internal const int I力场发生器 = 9503;
        internal const int I复合态晶体 = 9504;
        internal const int I电磁力抑制器 = 9505;
        internal const int I胶子发生器 = 9506;
        internal const int I强力过载装置 = 9507;
        internal const int I导流框架 = 9508;
        internal const int I恒星炮组件 = 9509;
        internal const int I恒星炮运载火箭 = 9510;
        internal const int I水滴 = 9511;

        internal const int M水 = 494;
        internal const int M燃烧单元 = 489;
        internal const int M爆破单元 = 490;
        internal const int M晶石爆破单元 = 491;
        internal const int M配送运输机 = 372;
        internal const int M物流运输机 = 47;
        internal const int M星际物流运输船 = 48;
        internal const int M小型运载火箭 = 75;
        internal const int M炮弹组 = 486;
        internal const int M高爆炮弹组 = 487;
        internal const int M晶石炮弹组 = 488;
        internal const int M导弹组 = 432;
        internal const int M超音速导弹组 = 433;
        internal const int M引力导弹组 = 434;
        internal const int M干扰胶囊 = 492;
        internal const int M压制胶囊 = 493;
        internal const int M原型机 = 448;
        internal const int M精准无人机 = 449;
        internal const int M攻击无人机 = 450;
        internal const int M护卫舰 = 451;
        internal const int M驱逐舰 = 452;
        internal const int M传送带 = 35;
        internal const int M高速传送带 = 36;
        internal const int M极速传送带 = 37;
        internal const int M分拣器 = 41;
        internal const int M高速分拣器 = 42;
        internal const int M极速分拣器 = 43;
        internal const int M集装分拣器 = 483;
        internal const int M四向分流器 = 38;
        internal const int M自动集装机 = 257;
        internal const int M流速监测器 = 208;
        internal const int M喷涂机 = 120;
        internal const int M物流配送器 = 371;
        internal const int M小型储物仓 = 51;
        internal const int M大型储物仓 = 52;
        internal const int M储液罐 = 121;
        internal const int M制造台MkI = 65;
        internal const int M制造台MkII = 66;
        internal const int M制造台MkIII = 67;
        internal const int M重组式制造台 = 456;
        internal const int M电力感应塔 = 44;
        internal const int M无线输电塔 = 71;
        internal const int M卫星配电站 = 68;
        internal const int M风力涡轮机 = 53;
        internal const int M火力发电厂 = 54;
        internal const int M微型聚变发电站 = 118;
        internal const int M地热发电站 = 255;
        internal const int M采矿机 = 57;
        internal const int M大型采矿机 = 256;
        internal const int M抽水站 = 60;
        internal const int M电弧熔炉 = 62;
        internal const int M位面熔炉 = 194;
        internal const int M负熵熔炉 = 457;
        internal const int M原油萃取站 = 61;
        internal const int M原油精炼厂 = 63;
        internal const int M化工厂 = 64;
        internal const int M量子化工厂 = 376;
        internal const int M分馏塔 = 119;
        internal const int M太阳能板 = 55;
        internal const int M蓄电器 = 46;
        internal const int M蓄电器满 = 46;
        internal const int M电磁轨道弹射器 = 72;
        internal const int M射线接收站 = 73;
        internal const int M垂直发射井 = 74;
        internal const int M能量枢纽 = 45;
        internal const int M微型粒子对撞机 = 69;
        internal const int M人造恒星 = 56;
        internal const int M行星内物流运输站 = 49;
        internal const int M星际物流运输站 = 50;
        internal const int M轨道采集器 = 117;
        internal const int M矩阵研究站 = 70;
        internal const int M自演化研究站 = 455;
        internal const int M高斯机枪塔 = 374;
        internal const int M高频激光塔 = 373;
        internal const int M聚爆加农炮 = 375;
        internal const int M磁化电浆炮 = 408;
        internal const int M导弹防御塔 = 407;
        internal const int M干扰塔 = 422;
        internal const int M信号塔 = 403;
        internal const int M行星护盾发生器 = 402;
        internal const int M战场分析基站 = 453;
        internal const int M近程电浆塔 = 482;
        //modelIndex可选范围：495-554
        internal const int M低功率分馏塔 = 530;
        internal const int M建筑极速分馏塔 = 531;
        internal const int M增殖分馏塔 = 532;

        internal const int R铁块 = 1;
        internal const int R磁铁 = 2;
        internal const int R铜块 = 3;
        internal const int R石材 = 4;
        internal const int R齿轮 = 5;
        internal const int R磁线圈 = 6;
        internal const int R风力涡轮机 = 7;
        internal const int R电力感应塔 = 8;
        internal const int R电磁矩阵 = 9;
        internal const int R矩阵研究站 = 10;
        internal const int R棱镜 = 11;
        internal const int R电浆激发器 = 12;
        internal const int R无线输电塔 = 13;
        internal const int R原油萃取站 = 14;
        internal const int R原油精炼厂 = 15;
        internal const int R等离子精炼 = 16;
        internal const int R高能石墨 = 17;
        internal const int R能量矩阵 = 18;
        internal const int R液氢燃料棒 = 19;
        internal const int R推进器 = 20;
        internal const int R加力推进器 = 21;
        internal const int R化工厂 = 22;
        internal const int R塑料 = 23;
        internal const int R硫酸 = 24;
        internal const int R有机晶体 = 25;
        internal const int R钛晶石 = 26;
        internal const int R结构矩阵 = 27;
        internal const int R卡西米尔晶体 = 28;
        internal const int R卡西米尔晶体高效 = 29;
        internal const int R钛化玻璃 = 30;
        internal const int R石墨烯 = 31;
        internal const int R石墨烯高效 = 32;
        internal const int R碳纳米管 = 33;
        internal const int R硅石 = 34;
        internal const int R碳纳米管高效 = 35;
        internal const int R粒子宽带 = 36;
        internal const int R晶格硅 = 37;
        internal const int R位面过滤器 = 38;
        internal const int R微型粒子对撞机 = 39;
        internal const int R重氢 = 40;
        internal const int R氘核燃料棒 = 41;
        internal const int R湮灭约束球 = 42;
        internal const int R人造恒星 = 43;
        internal const int R反物质燃料棒 = 44;
        internal const int R制造台MkI = 45;
        internal const int R制造台MkII = 46;
        internal const int R制造台MkIII = 47;
        internal const int R采矿机 = 48;
        internal const int R抽水站 = 49;
        internal const int R电路板 = 50;
        internal const int R处理器 = 51;
        internal const int R量子芯片 = 52;
        internal const int R微晶元件 = 53;
        internal const int R有机晶体原始 = 54;
        internal const int R信息矩阵 = 55;
        internal const int R电弧熔炉 = 56;
        internal const int R玻璃 = 57;
        internal const int RX射线裂解 = 58;
        internal const int R高纯硅块 = 59;
        internal const int R金刚石 = 60;
        internal const int R金刚石高效 = 61;
        internal const int R晶格硅高效 = 62;
        internal const int R钢材 = 63;
        internal const int R火力发电厂 = 64;
        internal const int R钛块 = 65;
        internal const int R钛合金 = 66;
        internal const int R太阳能板 = 67;
        internal const int R光子合并器 = 68;
        internal const int R光子合并器高效 = 69;
        internal const int R太阳帆 = 70;
        internal const int R电磁轨道弹射器 = 71;
        internal const int R射线接收站 = 72;
        internal const int R卫星配电站 = 73;
        internal const int R质能储存 = 74;
        internal const int R宇宙矩阵 = 75;
        internal const int R蓄电器 = 76;
        internal const int R能量枢纽 = 77;
        internal const int R空间翘曲器 = 78;
        internal const int R空间翘曲器高级 = 79;
        internal const int R框架材料 = 80;
        internal const int R戴森球组件 = 81;
        internal const int R垂直发射井 = 82;
        internal const int R小型运载火箭 = 83;
        internal const int R传送带 = 84;
        internal const int R分拣器 = 85;
        internal const int R小型储物仓 = 86;
        internal const int R四向分流器 = 87;
        internal const int R高速分拣器 = 88;
        internal const int R高速传送带 = 89;
        internal const int R极速分拣器 = 90;
        internal const int R大型储物仓 = 91;
        internal const int R极速传送带 = 92;
        internal const int R行星内物流运输站 = 93;
        internal const int R物流运输机 = 94;
        internal const int R星际物流运输站 = 95;
        internal const int R星际物流运输船 = 96;
        internal const int R电动机 = 97;
        internal const int R电磁涡轮 = 98;
        internal const int R粒子容器 = 99;
        internal const int R粒子容器高效 = 100;
        internal const int R引力透镜 = 101;
        internal const int R引力矩阵 = 102;
        internal const int R超级磁场环 = 103;
        internal const int R奇异物质 = 104;
        internal const int R增产剂MkI = 106;
        internal const int R增产剂MkII = 107;
        internal const int R增产剂MkIII = 108;
        internal const int R喷涂机 = 109;
        internal const int R分馏塔 = 110;
        internal const int R轨道采集器 = 111;
        internal const int R地基 = 112;
        internal const int R微型聚变发电站 = 113;
        internal const int R储液罐 = 114;
        internal const int R重氢分馏 = 115;
        internal const int R位面熔炉 = 116;
        internal const int R流速监测器 = 117;
        internal const int R地热发电站 = 118;
        internal const int R大型采矿机 = 119;
        internal const int R自动集装机 = 120;
        internal const int R重整精炼 = 121;
        internal const int R物流配送器 = 122;
        internal const int R配送运输机 = 123;
        internal const int R量子化工厂 = 124;
        internal const int R高斯机枪塔 = 125;
        internal const int R高频激光塔 = 126;
        internal const int R聚爆加农炮 = 127;
        internal const int R磁化电浆炮 = 128;
        internal const int R导弹防御塔 = 129;
        internal const int R干扰塔 = 130;
        internal const int R信号塔 = 131;
        internal const int R行星护盾发生器 = 132;
        internal const int R燃烧单元 = 133;
        internal const int R爆破单元 = 134;
        internal const int R晶石爆破单元 = 135;
        internal const int R机枪弹箱 = 136;
        internal const int R钛化弹箱 = 137;
        internal const int R超合金弹箱 = 138;
        internal const int R炮弹组 = 139;
        internal const int R高爆炮弹组 = 140;
        internal const int R晶石炮弹组 = 141;
        internal const int R等离子胶囊 = 142;
        internal const int R反物质胶囊 = 143;
        internal const int R导弹组 = 144;
        internal const int R超音速导弹组 = 145;
        internal const int R引力导弹组 = 146;
        internal const int R原型机 = 147;
        internal const int R精准无人机 = 148;
        internal const int R攻击无人机 = 149;
        internal const int R护卫舰 = 150;
        internal const int R驱逐舰 = 151;
        internal const int R动力引擎 = 105;
        internal const int R战场分析基站 = 152;
        internal const int R自演化研究站 = 153;
        internal const int R重组式制造台 = 154;
        internal const int R负熵熔炉 = 155;
        internal const int R奇异湮灭燃料棒 = 156;
        internal const int R近程电浆塔 = 157;
        internal const int R干扰胶囊 = 158;
        internal const int R压制胶囊 = 159;
        internal const int R集装分拣器 = 160;
        internal const int R引力发生装置 = 530;
        internal const int R位面约束环 = 531;
        internal const int R引力钻头 = 532;
        internal const int R隧穿激发装置 = 533;
        internal const int R谐振盘 = 534;
        internal const int R光子探针 = 535;
        internal const int R量子计算机 = 536;
        internal const int R星际组装厂组件 = 537;
        internal const int R多功能集成组件 = 550;
        internal const int R物质解压器运载火箭 = 538;
        internal const int R科学枢纽运载火箭 = 539;
        internal const int R谐振发射器运载火箭 = 540;
        internal const int R星际组装厂运载火箭 = 541;
        internal const int R晶体重构器运载火箭 = 542;
        internal const int R力场发生器Recipe = 565;
        internal const int R复合态晶体Recipe = 566;
        internal const int R电磁力抑制器Recipe = 567;
        internal const int R胶子发生器Recipe = 568;
        internal const int R强力过载装置Recipe = 569;
        internal const int R导流框架Recipe = 570;
        internal const int R恒星炮组件Recipe = 571;
        internal const int R恒星炮运载火箭Recipe = 572;
        internal const int R水滴gmRecipe = 573;

        internal const int T戴森球计划 = 1;
        internal const int T电磁学 = 1001;
        internal const int T电磁矩阵 = 1002;
        internal const int T高效电浆控制 = 1101;
        internal const int T等离子萃取精炼 = 1102;
        internal const int TX射线裂解 = 1103;
        internal const int T重整精炼 = 1104;
        internal const int T能量矩阵 = 1111;
        internal const int T氢燃料棒 = 1112;
        internal const int T推进器 = 1113;
        internal const int T加力推进器 = 1114;
        internal const int T流体储存封装 = 1120;
        internal const int T基础化工 = 1121;
        internal const int T高分子化工 = 1122;
        internal const int T高强度晶体 = 1123;
        internal const int T结构矩阵 = 1124;
        internal const int T卡西米尔晶体 = 1125;
        internal const int T高强度玻璃 = 1126;
        internal const int T应用型超导体 = 1131;
        internal const int T高强度材料 = 1132;
        internal const int T粒子可控 = 1133;
        internal const int T重氢分馏 = 1134;
        internal const int T波函数干扰 = 1141;
        internal const int T微型粒子对撞机 = 1142;
        internal const int T奇异物质 = 1143;
        internal const int T人造恒星 = 1144;
        internal const int T可控湮灭反应 = 1145;
        internal const int T增产剂MkI = 1151;
        internal const int T增产剂MkII = 1152;
        internal const int T增产剂MkIII = 1153;
        internal const int T基础制造 = 1201;
        internal const int T高速制造 = 1202;
        internal const int T量子打印 = 1203;
        internal const int T处理器 = 1302;
        internal const int T量子芯片 = 1303;
        internal const int T光子聚束采矿 = 1304;
        internal const int T亚微观量子纠缠 = 1305;
        internal const int T半导体材料 = 1311;
        internal const int T信息矩阵 = 1312;
        internal const int T自动化冶金 = 1401;
        internal const int T冶炼提纯 = 1402;
        internal const int T晶体冶炼 = 1403;
        internal const int T钢材冶炼 = 1411;
        internal const int T火力发电 = 1412;
        internal const int T钛矿冶炼 = 1413;
        internal const int T高强度钛合金 = 1414;
        internal const int T移山填海工程 = 1415;
        internal const int T微型核聚变发电 = 1416;
        internal const int T位面冶金 = 1417;
        internal const int T太阳能收集 = 1501;
        internal const int T光子变频 = 1502;
        internal const int T太阳帆轨道系统 = 1503;
        internal const int T射线接收站 = 1504;
        internal const int T行星电离层利用 = 1505;
        internal const int T狄拉克逆变机制 = 1506;
        internal const int T宇宙矩阵 = 1507;
        internal const int T任务完成 = 1508;
        internal const int T能量储存 = 1511;
        internal const int T星际电力运输 = 1512;
        internal const int T地热开采 = 1513;
        internal const int T高强度轻质结构 = 1521;
        internal const int T垂直发射井 = 1522;
        internal const int T戴森球应力系统 = 1523;
        internal const int T基础物流系统 = 1601;
        internal const int T改良物流系统 = 1602;
        internal const int T高效物流系统 = 1603;
        internal const int T行星物流系统 = 1604;
        internal const int T星际物流系统 = 1605;
        internal const int T气态行星开采 = 1606;
        internal const int T集装物流系统 = 1607;
        internal const int T配送物流系统 = 1608;
        internal const int T电磁驱动 = 1701;
        internal const int T磁悬浮 = 1702;
        internal const int T粒子磁力阱 = 1703;
        internal const int T引力波折射 = 1704;
        internal const int T引力矩阵 = 1705;
        internal const int T超级磁场发生器 = 1711;
        internal const int T卫星配电系统 = 1712;
        internal const int T武器系统 = 1801;
        internal const int T燃烧单元 = 1802;
        internal const int T爆破单元 = 1803;
        internal const int T晶石爆破单元 = 1804;
        internal const int T动力引擎 = 1805;
        internal const int T导弹防御塔 = 1806;
        internal const int T聚爆加农炮 = 1807;
        internal const int T信号塔 = 1808;
        internal const int T行星防御系统 = 1809;
        internal const int T干扰塔 = 1810;
        internal const int T磁化电浆炮 = 1811;
        internal const int T钛化弹箱 = 1812;
        internal const int T超合金弹箱 = 1813;
        internal const int T高爆炮弹组 = 1814;
        internal const int T超音速导弹组 = 1815;
        internal const int T晶石炮弹组 = 1816;
        internal const int T引力导弹组 = 1817;
        internal const int T反物质胶囊 = 1818;
        internal const int T原型机 = 1819;
        internal const int T精准无人机 = 1820;
        internal const int T攻击无人机 = 1821;
        internal const int T护卫舰 = 1822;
        internal const int T驱逐舰 = 1823;
        internal const int T压制胶囊 = 1824;
        internal const int T战场分析基站 = 1826;
        internal const int T数字模拟计算 = 1901;
        internal const int T物质重组 = 1902;
        internal const int T负熵递归 = 1903;
        internal const int T高密度可控湮灭 = 1904;
        internal const int T机甲核心 = 2101;
        internal const int T机械骨骼 = 2201;
        internal const int T机舱容量 = 2301;
        internal const int T通讯控制 = 2401;
        internal const int T能量回路 = 2501;
        internal const int T无人机引擎 = 2601;
        internal const int T批量建造 = 2701;
        internal const int T能量护盾 = 2801;
        internal const int T驱动引擎 = 2901;
        internal const int T自动标记重建 = 2951;
        internal const int T太阳帆寿命 = 3101;
        internal const int T射线传输效率 = 3201;
        internal const int T分拣器货物叠加 = 3301;
        internal const int T分拣器货物集装 = 3306;
        internal const int T集装分拣器改良 = 3311;
        internal const int T配送范围 = 4001;
        internal const int T运输船引擎 = 3401;
        internal const int T运输机舱扩容 = 3501;
        internal const int T运输站集装物流 = 3801;
        internal const int T矿物利用 = 3601;
        internal const int T垂直建造 = 3701;
        internal const int T研究速度 = 3901;
        internal const int T宇宙探索 = 4101;
        internal const int T动能武器伤害 = 5001;
        internal const int T能量武器伤害 = 5101;
        internal const int T爆破武器伤害 = 5201;
        internal const int T战斗无人机伤害 = 5301;
        internal const int T战斗无人机射速 = 5401;
        internal const int T战斗无人机耐久 = 5601;
        internal const int T行星护盾 = 5701;
        internal const int T地面编队扩容 = 5801;
        internal const int T太空编队扩容 = 5901;
        internal const int T结构强化 = 6001;
        internal const int T电磁武器效果 = 6101;
        internal const int T尼科尔戴森光束 = 1918;
    }

    /// <summary>
    /// 创建一个分馏配方。
    /// </summary>
    /// <param name="fracNumRatioDic">分馏成功率，key为产物个数，value为成功率</param>
    class FracRecipe(RecipeProto recipe, Dictionary<int, double> fracNumRatioDic)
    {
        public int GetOutputNum(double randomVal)
        {
            double value = 0;
            foreach (var p in fracNumRatioDic)
            {
                if (value + p.Value > randomVal)
                {
                    return p.Key;
                }
                value += p.Value;
            }
            return 0;
        }
    }

    internal static class CopyModelUtils
    {
        private static ModelProto Copy(this ModelProto proto) =>
            new ModelProto
            {
                ObjectType = proto.ObjectType,
                RuinType = proto.RuinType,
                RendererType = proto.RendererType,
                HpMax = proto.HpMax,
                HpUpgrade = proto.HpUpgrade,
                HpRecover = proto.HpRecover,
                RuinId = proto.RuinId,
                RuinCount = proto.RuinCount,
                RuinLifeTime = proto.RuinLifeTime,
                PrefabPath = proto.PrefabPath,
                _colliderPath = proto._colliderPath,
                _ruinPath = proto._ruinPath,
                _wreckagePath = proto._wreckagePath,
                _ruinOriginModelIndex = proto._ruinOriginModelIndex,
            };


        internal static ModelProto CopyModelProto(int oriId, int id, int item, int buildIndex, string buildingName, Color? color = null)
        {
            ModelProto oriModel = LDB.models.Select(oriId);
            //ModelProto model = new();
            //oriModel.CopyPropsTo(ref model);
            ModelProto model = oriModel.Copy();
            model.Name = id.ToString();
            model.ID = id;

            PrefabDesc desc = oriModel.prefabDesc;
            GameObject prefab = desc.prefab ? desc.prefab : Resources.Load<GameObject>(oriModel.PrefabPath);
            GameObject colliderPrefab = desc.colliderPrefab ? desc.colliderPrefab : Resources.Load<GameObject>(oriModel._colliderPath);

            ref PrefabDesc modelPrefabDesc = ref model.prefabDesc;
            modelPrefabDesc = prefab == null ? PrefabDesc.none :
                colliderPrefab == null ? new PrefabDesc(id, prefab) : new PrefabDesc(id, prefab, colliderPrefab);

            for (var i = 0; i < modelPrefabDesc.lodMaterials.Length; i++)
            //foreach (Material[] lodMaterial in modelPrefabDesc.lodMaterials)
            {
                var lodMaterial = modelPrefabDesc.lodMaterials[i];
                if (lodMaterial == null) continue;
                for (var j = 0; j < lodMaterial.Length; j++)
                {
                    ref Material material = ref lodMaterial[j];
                    if (material == null) continue;
                    material = new Material(material);
                    if (!color.HasValue) continue;
                    material.SetColor("_Color", color.Value);

                }
            }

            modelPrefabDesc.modelIndex = id;
            modelPrefabDesc.hasBuildCollider = desc.hasBuildCollider;
            modelPrefabDesc.colliders = desc.colliders;
            modelPrefabDesc.buildCollider = desc.buildCollider;
            modelPrefabDesc.buildColliders = desc.buildColliders;
            modelPrefabDesc.colliderPrefab = desc.colliderPrefab;
            modelPrefabDesc.dragBuild = desc.dragBuild;
            modelPrefabDesc.dragBuildDist = desc.dragBuildDist;
            modelPrefabDesc.blueprintBoxSize = desc.blueprintBoxSize;
            modelPrefabDesc.roughHeight = desc.roughHeight;
            modelPrefabDesc.roughWidth = desc.roughWidth;
            modelPrefabDesc.roughRadius = desc.roughRadius;
            modelPrefabDesc.barHeight = desc.barHeight;
            modelPrefabDesc.barWidth = desc.barWidth;

            model.sid = "";
            model.SID = "";

            // ModelProto oriModel = LDB.models.Select(oriId);
            // ModelProto model = new();
            // oriModel.CopyPropsTo(ref model);
            // model.prefabDesc = new();
            // oriModel.prefabDesc.CopyPropsTo(ref model.prefabDesc);
            // model.prefabDesc.modelIndex = id;
            // model.ID = id;
            // model.Name = id.ToString();
            // model.name = id.ToString();
            // model.sid = buildingName;
            // model.SID = buildingName;

            LDBTool.PreAddProto(model);
            //ProtoRegistry.AddModelToItemProto(model, LDB.items.Select(item), [], buildIndex);
            return model;
        }
    }
}
