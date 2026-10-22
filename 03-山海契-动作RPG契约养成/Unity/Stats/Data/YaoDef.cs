using System;

namespace ShanHaiQi.Stats.Data
{
    /// <summary>
    /// 一只妖的图鉴数值（主方案 §9 表的字段，一一对应）。
    /// </summary>
    [Serializable]
    public class YaoDef
    {
        public string Name;              // 名称
        public string SourceChapter;     // 《山海经》出处篇目（[待核] 原样保留）
        public string Form;              // 形态与攻击方式
        public string ContactForm;       // 接触形态（幼崽/卵/成体）
        public double B;                 // 战力指数基础值（§4.4 用）
        public double DBreed;            // 野性系数 D（§4.2 用，0 温和 1 野）
        public string SignDifficulty;    // 签订难度：易/中/难/极难
        public string BacklashRiskLevel; // 强行反噬风险等级枚举：Low / Medium / High / Extreme（§9 明令「只出等级、不出概率」）
        public string BacklashRiskLabel; // 同一等级的中文写法：低 / 中 / 高 / 极高（照抄 §9 表）
        public double InitialAgitation;  // 初始惊扰度（§9「初始惊扰度」表，14 种全部给定 [估算]）
        public string SizeClass;         // 体型：小 / 中 / 大（§9 同表；影响预制体缩放与初始惊扰）
        public string Note;
    }

    [Serializable]
    public class YaoTableFile
    {
        public YaoDef[] Yao = new YaoDef[0];
    }
}
