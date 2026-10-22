using System;

namespace ShanHaiQi.Stats
{
    /// <summary>目标护甲类型（§7.4 判据 4）。</summary>
    public enum ArmorType
    {
        Soft = 0,   // 软甲：远程 ×1.25
        Airborne = 1, // 悬空：远程 ×1.25
        Hard = 2    // 硬壳：远程 ×0.6
    }

    /// <summary>四种射器的静态参数（§7.1 表逐行）。</summary>
    public struct RangedSpec
    {
        public string Name;
        public double MuzzleSpeed;      // 初速 m/s，主方案标注 [估算]（附录 A10）
        public double ChargeMaxSec;     // 0 表示无蓄力
        public double DamageCoefMin;    // 蓄力起点系数
        public double DamageCoefMax;    // 蓄满系数
        public double ReloadSec;        // 装填/抽箭节奏
        public int MagazineSize;        // 0 = 单发；连弩 3
        public double MagazineReloadSec;// 连弩换盘 2.2 s
    }

    /// <summary>§7.4 四条判据的逐条结果。</summary>
    public struct RangedCriteriaResult
    {
        public bool DistanceOk;
        public bool TimeOk;
        public bool EfficiencyOk;
        public bool TypeOk;
        public bool RangedWins;          // 四条同时成立

        public double TimeToContactSec;
        public double RequiredWindowSec;
        public double ApproachRate;
        public double MeleeEffectiveDps;
        public double RangedExpectedDps;
        public double TypeMultiplier;
    }

    /// <summary>
    /// 弹道解算（I10）与远程划算判据（I11）。
    /// 公式：下坠 = 0.5 × g × t²，t = 距离 / 初速，g = 9.8；提前量 = 目标速度 × 飞行时间 × 0.3（§7.3）。
    /// </summary>
    public static class Ballistics
    {
        public const double G = 9.8;
        public const double LeadDiscount = 0.3;

        // §7.1 四种射器（角弓 / 蹶张弩 / 连弩 / 弹弓）
        public static RangedSpec[] Specs()
        {
            RangedSpec[] s = new RangedSpec[4];

            s[0].Name = "角弓";
            s[0].MuzzleSpeed = 55.0;      // [估算] 附录 A10
            s[0].ChargeMaxSec = 1.2;
            s[0].DamageCoefMin = 0.35;
            s[0].DamageCoefMax = 1.00;
            s[0].ReloadSec = 0.5;         // 抽箭 0.5 s
            s[0].MagazineSize = 0;
            s[0].MagazineReloadSec = 0.0;

            s[1].Name = "蹶张弩";
            s[1].MuzzleSpeed = 65.0;      // [估算]
            s[1].ChargeMaxSec = 0.0;
            s[1].DamageCoefMin = 1.00;
            s[1].DamageCoefMax = 1.00;
            s[1].ReloadSec = 3.2;
            s[1].MagazineSize = 0;
            s[1].MagazineReloadSec = 0.0;

            s[2].Name = "连弩";
            s[2].MuzzleSpeed = 45.0;      // [估算]
            s[2].ChargeMaxSec = 0.0;
            s[2].DamageCoefMin = 0.55;
            s[2].DamageCoefMax = 0.55;
            s[2].ReloadSec = 0.9;
            s[2].MagazineSize = 3;
            s[2].MagazineReloadSec = 2.2;

            s[3].Name = "弹弓";
            s[3].MuzzleSpeed = 40.0;      // [估算]
            s[3].ChargeMaxSec = 0.6;
            s[3].DamageCoefMin = 0.20;
            s[3].DamageCoefMax = 0.55;
            s[3].ReloadSec = 0.7;
            s[3].MagazineSize = 0;
            s[3].MagazineReloadSec = 0.0;

            return s;
        }

        public static RangedSpec SpecByName(string name)
        {
            RangedSpec[] all = Specs();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Name == name) return all[i];
            }
            return all[0];
        }

        /// <summary>飞行时间 t = 距离 / 初速。</summary>
        public static double FlightTime(double distanceM, double muzzleSpeed)
        {
            if (muzzleSpeed <= 0.0) return double.PositiveInfinity;
            return distanceM / muzzleSpeed;
        }

        /// <summary>下坠 = 0.5 × g × t²（§7.3）。</summary>
        public static double Drop(double distanceM, double muzzleSpeed)
        {
            double t = FlightTime(distanceM, muzzleSpeed);
            if (double.IsInfinity(t)) return double.PositiveInfinity;
            return 0.5 * G * t * t;
        }

        /// <summary>提前量 = 目标速度 × 飞行时间 × 0.3（0.3 为「妖会变向」的折扣系数）。</summary>
        public static double Lead(double targetSpeed, double distanceM, double muzzleSpeed)
        {
            return targetSpeed * FlightTime(distanceM, muzzleSpeed) * LeadDiscount;
        }

        /// <summary>提前量的直接形式（已知飞行时间）。</summary>
        public static double LeadFromFlight(double targetSpeed, double flightTime)
        {
            return targetSpeed * flightTime * LeadDiscount;
        }

        /// <summary>
        /// §7.2 蓄力曲线系数：伤害系数 = 0.35 + 0.65 × (t/1.2)，0–1.2 s 线性上升。
        /// 无蓄力射器返回其固定系数。
        /// </summary>
        public static double ChargeDamageCoef(RangedSpec spec, double chargeSec)
        {
            if (spec.ChargeMaxSec <= 0.0) return spec.DamageCoefMax;
            double t = chargeSec;
            if (t < 0.0) t = 0.0;
            if (t > spec.ChargeMaxSec) t = spec.ChargeMaxSec;
            double span = spec.DamageCoefMax - spec.DamageCoefMin;
            return spec.DamageCoefMin + span * (t / spec.ChargeMaxSec);
        }

        /// <summary>角弓专用便捷式（§7.2 原文系数）。</summary>
        public static double BowDamageCoef(double chargeSec)
        {
            double t = chargeSec;
            if (t < 0.0) t = 0.0;
            if (t > 1.2) t = 1.2;
            return 0.35 + 0.65 * (t / 1.2);
        }

        /// <summary>弹弓专用便捷式（0.6 s 蓄力，系数 0.20→0.55）。</summary>
        public static double SlingDamageCoef(double chargeSec)
        {
            double t = chargeSec;
            if (t < 0.0) t = 0.0;
            if (t > 0.6) t = 0.6;
            return 0.20 + 0.35 * (t / 0.6);
        }

        /// <summary>
        /// §7.2 散布倍率：拉满稳定窗（1.0–1.2 s）−25%；过拉（&gt;1.25 s）每 0.25 s +40%。
        /// 返回 1.0 表示无修正。
        /// </summary>
        public static double SpreadMultiplier(double chargeSec)
        {
            if (chargeSec >= 1.0 && chargeSec <= 1.2) return 0.75;
            if (chargeSec > 1.25)
            {
                double steps = (chargeSec - 1.25) / 0.25;
                return 1.0 + 0.40 * steps;
            }
            return 1.0;
        }

        /// <summary>§7.2 过拉 2.0 s 后强制脱手（体力 −15）。</summary>
        public static bool ForcesRelease(double chargeSec)
        {
            return chargeSec >= 2.0;
        }

        public const double ForcedReleaseStaminaCost = 15.0;

        /// <summary>§7.4 判据 4：目标类型的远程倍率。</summary>
        public static double TypeMultiplier(ArmorType type)
        {
            if (type == ArmorType.Hard) return 0.6;
            return 1.25; // 软甲 / 悬空
        }

        /// <summary>§7.4 接近率 = 速度 / (速度 + 兵器长度 × 2)。</summary>
        public static double ApproachRate(double targetSpeed, double meleeWeaponLengthM)
        {
            double denom = targetSpeed + meleeWeaponLengthM * 2.0;
            if (denom <= 0.0) return 0.0;
            return targetSpeed / denom;
        }

        public const double DistanceThresholdM = 12.0;   // 判据 1
        public const double AimSec = 0.6;                // 判据 2：瞄准
        public const double LeadCorrectionSec = 0.2;     // 判据 2：提前量修正

        /// <summary>
        /// I11 远程划算判据（§7.4）：四条同时成立才判定「远程更划算」。
        /// singleShotDamage 为该发实际伤害（已含蓄力系数与类型倍率前的裸值）。
        /// </summary>
        public static RangedCriteriaResult RangedWins(double distanceM, double targetSpeed,
                                                     RangedSpec spec, ArmorType armor,
                                                     double singleShotDamage,
                                                     double meleeDps, double meleeWeaponLengthM)
        {
            RangedCriteriaResult r = new RangedCriteriaResult();
            double flight = FlightTime(distanceM, spec.MuzzleSpeed);

            r.DistanceOk = distanceM >= DistanceThresholdM;

            r.RequiredWindowSec = spec.ReloadSec + AimSec + LeadCorrectionSec;
            r.TimeToContactSec = targetSpeed > 0.0 ? distanceM / targetSpeed : double.PositiveInfinity;
            r.TimeOk = r.TimeToContactSec >= r.RequiredWindowSec;

            r.ApproachRate = ApproachRate(targetSpeed, meleeWeaponLengthM);
            r.MeleeEffectiveDps = meleeDps * r.ApproachRate;
            // 一个完整出手周期 = 装填/抽箭 + 蓄力 + 飞行（§7.4「抽箭 0.5 + 蓄力 1.2 + 飞行 0.45 = 2.15 s」的口径）。
            // 无蓄力射器的 ChargeMaxSec = 0，于是退化为「装填 + 飞行」，与 §7.4 对蹶张弩的 3.2 + 飞行一致。
            double cycle = spec.ReloadSec + spec.ChargeMaxSec + flight;
            r.RangedExpectedDps = cycle > 0.0 ? singleShotDamage / cycle : 0.0;
            r.EfficiencyOk = r.RangedExpectedDps > r.MeleeEffectiveDps;

            r.TypeMultiplier = TypeMultiplier(armor);
            r.TypeOk = armor != ArmorType.Hard;

            r.RangedWins = r.DistanceOk && r.TimeOk && r.EfficiencyOk && r.TypeOk;
            return r;
        }
    }
}
