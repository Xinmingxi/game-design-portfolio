using System;

namespace ShanHaiQi.Stats.Data
{
    /// <summary>
    /// 符箓 / 阵法 / 丹药 三种消耗品的统一数据类（主方案 §8.2 / §8.3 / §8.4 表）。
    /// 不适用字段填 -1。
    /// </summary>
    [Serializable]
    public class ItemDef
    {
        public string Name;
        public string Category;        // "符箓" / "阵法" / "丹药"
        public string Materials;       // 材料（每张/每套/每枚）
        public string Effect;          // 效果原文
        public double DurationSec;     // 持续时间（-1 = 不适用）
        public double RadiusM;         // 作用半径（-1 = 不适用）
        public double DurabilityMin;   // 阵法耐久下限（§8.3：60–120；单阵给定值时为该值）
        public double DurabilityMax;
        public double CraftSuccessChance; // 丹药火候成功率（-1 = 不适用）
        public double Toxicity;        // 丹药毒性（-1 = 不适用）
        public string Veto;            // 环境否决 / 破坏条件
        public string Note;
    }

    [Serializable]
    public class ItemTableFile
    {
        public ItemDef[] Items = new ItemDef[0];
    }
}
