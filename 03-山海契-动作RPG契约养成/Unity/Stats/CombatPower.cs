using System;

namespace ShanHaiQi.Stats
{
    /// <summary>妖的当前状态系数（主方案 §4.4：怒 1.15 / 常 1.0 / 疲 0.85）。</summary>
    public enum YaoMood
    {
        Calm = 0,
        Angry = 1,
        Tired = 2
    }

    /// <summary>R 的四档（主方案 §4.4 表）。</summary>
    public enum PowerBand
    {
        Forbidden = 0,     // R < 1.00      禁止
        Dangerous = 1,     // 1.00 ≤ R < 1.25  危险
        Controllable = 2,  // 1.25 ≤ R < 1.60  可控
        Stable = 3         // R ≥ 1.60      稳
    }

    /// <summary>档位的静态数据：档名、P_force 区间、§4.4 表里的反噬参考值。</summary>
    public struct BandInfo
    {
        public PowerBand Band;
        public string Name;
        public double PForceMin;
        public double PForceMax;
        // §4.4 表「立契瞬间反噬概率」一列（已按 §4.6 T1 统一：稳 0% / 可控 0% / 危险 40%（曲线 45%→40%）/ 禁止 100%）。
        // 判定仍然只走 Contract.BacklashOnSigning 一处，这里只作 UI 展示。
        public double BacklashRefMin;
        public double BacklashRefMax;
    }

    /// <summary>
    /// 战力指数 CPT 与战力比 R（主方案 §4.4、附录 B、I1、I2）。
    /// 纯函数，无状态，不引用任何 Unity 引擎类型（该约束由 ShanHaiQi.Stats.asmdef 的 noEngineReferences 编译期强制）。
    /// </summary>
    public static class CombatPower
    {
        // 主方案 §4.4：CPT_hero = (体魄×0.40 + 技艺×0.35 + 器×0.25) × S
        public const double WeightBody = 0.40;
        public const double WeightSkill = 0.35;
        public const double WeightGear = 0.25;

        // 主方案 §4.4：S = HP% × (1 − 疲劳×0.3) × (1 − 伤病×0.2)
        public const double FatigueCoef = 0.30;
        public const double InjuryCoef = 0.20;

        // 主方案 §4.4 / §12 K1：两条跳档线
        public const double ThresholdDanger = 1.25;
        public const double ThresholdStable = 1.60;

        public static double Clamp(double v, double min, double max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        /// <summary>状态系数：怒 1.15 / 常 1.0 / 疲 0.85（§4.4）。</summary>
        public static double StateCoef(YaoMood mood)
        {
            if (mood == YaoMood.Angry) return 1.15;
            if (mood == YaoMood.Tired) return 0.85;
            return 1.00;
        }

        /// <summary>三项手工加点（各 0–20）的加权和，不含状态。</summary>
        public static double HeroStatWeighted(double body, double skill, double gear)
        {
            return body * WeightBody + skill * WeightSkill + gear * WeightGear;
        }

        /// <summary>S = HP% × (1 − 疲劳×0.3) × (1 − 伤病×0.2)，取值约 0.3–1.0。</summary>
        public static double HeroConditionS(double hpPct, double fatigue, double injury)
        {
            return hpPct * (1.0 - fatigue * FatigueCoef) * (1.0 - injury * InjuryCoef);
        }

        /// <summary>I1 主角战力指数。hpPct/fatigue/injury 均为 0–1。</summary>
        public static double HeroCpt(double body, double skill, double gear,
                                    double hpPct, double fatigue, double injury)
        {
            return HeroStatWeighted(body, skill, gear) * HeroConditionS(hpPct, fatigue, injury);
        }

        /// <summary>I1 妖侧战力指数：CPT_yao = B × 当前血量% × 状态系数。</summary>
        public static double YaoCpt(double b, double hpPct, YaoMood mood)
        {
            return b * hpPct * StateCoef(mood);
        }

        /// <summary>I2 战力比 R = CPT_hero / CPT_yao。CPT_yao 为 0 时返回 double.PositiveInfinity（不抛异常，交给上层处理）。</summary>
        public static double R(double cptHero, double cptYao)
        {
            if (cptYao <= 0.0)
            {
                return cptHero > 0.0 ? double.PositiveInfinity : 1.0;
            }
            return cptHero / cptYao;
        }

        /// <summary>R → 四档。边界取「左闭右开」：1.25 归可控档，1.60 归稳档（§4.4 表）。</summary>
        public static PowerBand BandOf(double r)
        {
            if (r < 1.00) return PowerBand.Forbidden;
            if (r < ThresholdDanger) return PowerBand.Dangerous;
            if (r < ThresholdStable) return PowerBand.Controllable;
            return PowerBand.Stable;
        }

        /// <summary>档位静态数据（§4.4 表逐行照抄）。</summary>
        public static BandInfo BandInfoOf(PowerBand band)
        {
            BandInfo info = new BandInfo();
            info.Band = band;
            if (band == PowerBand.Forbidden)
            {
                info.Name = "禁止";
                info.PForceMin = double.NaN;   // 表内为「—」：R<1.0 不给强行成功率
                info.PForceMax = double.NaN;
                info.BacklashRefMin = 1.00;
                info.BacklashRefMax = 1.00;
            }
            else if (band == PowerBand.Dangerous)
            {
                info.Name = "危险";
                info.PForceMin = 0.22;
                info.PForceMax = 0.45;
                info.BacklashRefMin = 0.40;   // §4.4 表取 R=1.24 处的 40%；档内曲线自 K3(45%) 递减到此
                info.BacklashRefMax = 0.45;
            }
            else if (band == PowerBand.Controllable)
            {
                info.Name = "可控";
                info.PForceMin = 0.45;
                info.PForceMax = 0.68;
                info.BacklashRefMin = 0.00;   // §4.4 已按 §4.6 统一：R ≥ 1.25 立契失败不反噬
                info.BacklashRefMax = 0.00;
            }
            else
            {
                info.Name = "稳";
                info.PForceMin = 0.68;
                info.PForceMax = 0.85;
                info.BacklashRefMin = 0.00;   // 同上：只有 R < 1.25 才可能反噬
                info.BacklashRefMax = 0.00;
            }
            return info;
        }
    }
}
