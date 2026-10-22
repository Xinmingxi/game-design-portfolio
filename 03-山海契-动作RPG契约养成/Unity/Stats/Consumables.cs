using System;

namespace ShanHaiQi.Stats
{
    /// <summary>
    /// 符箓 / 阵法 / 丹药 的规则层（I13；主方案 §8.1–§8.4）。
    /// 只放「可复算的常数与判定」，具体条目数值在 Data/Items.json。
    /// </summary>
    public static class Talisman
    {
        public const int SlotsDefault = 3;          // §8.2：3 格
        public const int PerSlotDefault = 3;        // §8.2：每格 3 张
        public const int CarriedMaxDefault = 9;     // §8.2：9 张
        public const int CarriedMaxUpgraded = 12;   // §8.2：符囊升级到 12
        public const double CastSec = 0.8;          // §8.2：使用前摇 0.8 s，被打断则报废

        public const double F1MaxDurationSec = 6.0; // §8.1 F1：一切超自然效果 ≤ 6 s
        public const double F2MaxRadiusM = 4.0;     // §8.1 F2：作用半径 ≤ 4 m
        public const double RainFailChance = 0.50;  // §8.1 F4：雨天 50% 失效
        public const int WindFailLevel = 5;         // §8.1 F4：风力 > 5 级失效
        public const double CaveFireDamageMultiplier = 1.3; // §8.1 F4：洞窟内火符 ×1.3

        /// <summary>F4 天气否决：roll01 由调用方注入以保证可复算。</summary>
        public static bool FailsInRain(double roll01)
        {
            return roll01 < RainFailChance;
        }

        /// <summary>F4 风力否决。</summary>
        public static bool FailsInWind(int windLevel)
        {
            return windLevel > WindFailLevel;
        }

        /// <summary>F1/F2 自检：条目是否符合两条硬约束。</summary>
        public static bool RespectsHardLimits(double durationSec, double radiusM)
        {
            bool d = durationSec < 0.0 || durationSec <= F1MaxDurationSec;
            bool r = radiusM < 0.0 || radiusM <= F2MaxRadiusM;
            return d && r;
        }
    }

    public static class Formation
    {
        public const double BuildSec = 2.5;         // §8.3：布设 2.5 s
        public const double DurabilityMin = 60.0;   // §8.3：耐久 60–120
        public const double DurabilityMax = 120.0;
        public const double MaxRadiusM = 4.0;       // §8.1 F2
    }

    public static class Pill
    {
        public const int CarriedMax = 6;            // §8.4：携带上限 6 枚
        public const double EatSec = 1.2;           // §8.4：服食 1.2 s，会被打断
        public const double ToleranceBase = 0.6;    // §8.4：第 n 枚效力 ×0.6^(n−1)
        public const double PoisonToxicityThreshold = 3.0; // §8.4：毒性累计 ≥3 触发中毒
        public const double PoisonedStatMultiplier = 0.7;  // §8.4：全属性 ×0.7

        /// <summary>同种连续服用的耐受衰减：第 1 枚 1.0、第 2 枚 0.6、第 3 枚 0.36…</summary>
        public static double EffectScaleAt(int nthDose)
        {
            if (nthDose <= 1) return 1.0;
            return Math.Pow(ToleranceBase, nthDose - 1);
        }

        /// <summary>毒性累计是否已触发中毒（持续到营地休息）。</summary>
        public static bool IsPoisoned(double toxicityAccum)
        {
            return toxicityAccum >= PoisonToxicityThreshold;
        }

        /// <summary>中毒期间的全属性倍率。</summary>
        public static double StatMultiplier(double toxicityAccum)
        {
            return IsPoisoned(toxicityAccum) ? PoisonedStatMultiplier : 1.0;
        }
    }
}
