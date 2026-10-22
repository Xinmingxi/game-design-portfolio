using System;
using System.Collections.Generic;
using ShanHaiQi.Stats.Data;

namespace ShanHaiQi.Stats
{
    /// <summary>一对支配关系（或一对「险过」关系）。四轴逐轴留档，便于在 F1 面板上直接对照。</summary>
    public struct DominancePair
    {
        public string Dominator;
        public string Dominated;
        public string TriggerAxis;   // 触发 1.15 倍判定的那一轴：输出 / 触及 / 覆盖 / 持续
        public double Margin;        // 四轴上最大的 A/B 比值

        public double DpsA, DpsB;      // 输出轴 = 伤害 × 攻速
        public double LenA, LenB;      // 触及轴 = 长度（m）
        public double SweepA, SweepB;  // 覆盖轴 = 横扫角（度）
        public double DurA, DurB;      // 持续轴 = 耐久（命中次数）
    }

    /// <summary>I12 兵器预算自检的完整结果。</summary>
    public struct BudgetReport
    {
        public int WeaponCount;
        public int EvaluatedCount;      // 参与 K / 支配自检的件数（排除控制类）
        public int ControlClassCount;
        public int PairCount;           // 两两组合数 = C(EvaluatedCount, 2)
        public double KMin;
        public double KMax;
        public double KRatio;
        public List<DominancePair> DominancePairs;
        public List<DominancePair> NearMisses; // 四轴均 ≥ 但未过 1.15 倍（设计上「最险」的对）
        public List<string> Warnings;
        public bool Pass;
    }

    /// <summary>
    /// 近战预算 K 与四轴反支配自检（I12；主方案 §6.3）。
    /// K = 伤害 × 攻速 × √长度。
    /// 四轴相互独立：输出 DPS(伤害×攻速) / 触及(长度) / 覆盖(横扫角) / 持续(耐久)。
    /// 支配判定：A 在四轴上均 ≥ B，且至少一轴 &gt; B 的 1.15 倍 → A 支配 B。
    /// 旧口径「长度 × 横扫角」相乘已被主方案否决（它会把 2.8 m 长槊判给 0.9 m 短刀），
    /// 这里只在 <see cref="LegacyProductPairs"/> 里保留一份对照实现，不参与任何验收。
    /// </summary>
    public static class WeaponBudget
    {
        public const double KTargetMin = 49.5;   // §6.2 区间口径（按首轮实测回填，不是先定后凑）
        public const double KTargetMax = 68.5;
        public const double KTargetTolerance = 0.05; // 表内 K 值只给到 0.1 位，留半个刻度
        public const double MaxKRatio = 1.5;     // §6.3 / §12 K4：极差 ≤ 1.5 倍
        public const double DominanceMargin = 1.15;

        /// <summary>预算 K = 伤害 × 攻速 × √长度（§6.3）。</summary>
        public static double K(double dmg, double atkPerSec, double lengthM)
        {
            if (lengthM < 0.0) lengthM = 0.0;
            return dmg * atkPerSec * Math.Sqrt(lengthM);
        }

        /// <summary>输出轴 = 伤害 × 攻速（DPS）。</summary>
        public static double OutputAxis(double dmg, double atkPerSec)
        {
            return dmg * atkPerSec;
        }

        /// <summary>触及轴 = 长度（m）。</summary>
        public static double ReachAxis(double lengthM)
        {
            return lengthM;
        }

        /// <summary>覆盖轴 = 横扫角（度）。</summary>
        public static double CoverageAxis(double sweepDeg)
        {
            return sweepDeg;
        }

        /// <summary>持续轴 = 耐久（命中次数）。</summary>
        public static double SustainAxis(int durabilityHits)
        {
            return durabilityHits;
        }

        /// <summary>四轴支配判定：A 支配 B 时返回 true，并给出触发轴与最大比值。</summary>
        public static bool Dominates(double dpsA, double lenA, double sweepA, double durA,
                                     double dpsB, double lenB, double sweepB, double durB,
                                     out string triggerAxis, out double margin)
        {
            triggerAxis = string.Empty;
            margin = 0.0;
            if (dpsA < dpsB || lenA < lenB || sweepA < sweepB || durA < durB) return false;

            double rd = dpsB > 0.0 ? dpsA / dpsB : double.PositiveInfinity;
            double rl = lenB > 0.0 ? lenA / lenB : double.PositiveInfinity;
            double rc = sweepB > 0.0 ? sweepA / sweepB : double.PositiveInfinity;
            double rs = durB > 0.0 ? durA / durB : double.PositiveInfinity;
            margin = Math.Max(Math.Max(rd, rl), Math.Max(rc, rs));

            if (rd > DominanceMargin) triggerAxis = "输出";
            else if (rl > DominanceMargin) triggerAxis = "触及";
            else if (rc > DominanceMargin) triggerAxis = "覆盖";
            else if (rs > DominanceMargin) triggerAxis = "持续";

            return triggerAxis.Length > 0;
        }

        /// <summary>按四轴对一整张兵器表跑自检。</summary>
        public static BudgetReport Check(IList<WeaponDef> weapons)
        {
            BudgetReport rep = new BudgetReport();
            rep.DominancePairs = new List<DominancePair>();
            rep.NearMisses = new List<DominancePair>();
            rep.Warnings = new List<string>();
            rep.WeaponCount = weapons == null ? 0 : weapons.Count;

            List<WeaponDef> wt = new List<WeaponDef>();
            for (int i = 0; i < rep.WeaponCount; i++)
            {
                WeaponDef w = weapons[i];
                if (w == null) continue;
                if (w.IsControlClass) rep.ControlClassCount++;
                else wt.Add(w);
            }
            rep.EvaluatedCount = wt.Count;
            rep.PairCount = wt.Count * (wt.Count - 1) / 2;

            rep.KMin = double.PositiveInfinity;
            rep.KMax = 0.0;
            for (int i = 0; i < wt.Count; i++)
            {
                double k = K(wt[i].Dmg, wt[i].AtkPerSec, wt[i].LengthM);
                if (k < rep.KMin) rep.KMin = k;
                if (k > rep.KMax) rep.KMax = k;
                if (k < KTargetMin - KTargetTolerance || k > KTargetMax + KTargetTolerance)
                {
                    rep.Warnings.Add(wt[i].Name + " K=" + k.ToString("F2")
                        + " 越出 §6.2 区间 " + KTargetMin.ToString("F1") + "–" + KTargetMax.ToString("F1"));
                }
            }
            if (wt.Count == 0)
            {
                rep.KMin = 0.0;
                rep.KRatio = 0.0;
            }
            else
            {
                rep.KRatio = rep.KMin > 0.0 ? rep.KMax / rep.KMin : double.PositiveInfinity;
            }

            for (int i = 0; i < wt.Count; i++)
            {
                for (int j = 0; j < wt.Count; j++)
                {
                    if (i == j) continue;
                    WeaponDef a = wt[i];
                    WeaponDef b = wt[j];
                    DominancePair p = MakePair(a, b);

                    string axis;
                    double margin;
                    if (Dominates(p.DpsA, p.LenA, p.SweepA, p.DurA,
                                  p.DpsB, p.LenB, p.SweepB, p.DurB, out axis, out margin))
                    {
                        p.TriggerAxis = axis;
                        p.Margin = margin;
                        rep.DominancePairs.Add(p);
                    }
                    else if (p.DpsA >= p.DpsB && p.LenA >= p.LenB && p.SweepA >= p.SweepB && p.DurA >= p.DurB)
                    {
                        p.TriggerAxis = "—";
                        p.Margin = margin;
                        rep.NearMisses.Add(p);
                    }
                }
            }
            rep.NearMisses.Sort(delegate (DominancePair x, DominancePair y)
            {
                return y.Margin.CompareTo(x.Margin);
            });

            rep.Pass = rep.DominancePairs.Count == 0 && rep.KRatio <= MaxKRatio;
            return rep;
        }

        /// <summary>取出一对兵器的四轴数值（也给 F1 面板/单对复核用）。</summary>
        public static DominancePair MakePair(WeaponDef a, WeaponDef b)
        {
            DominancePair p = new DominancePair();
            p.Dominator = a.Name;
            p.Dominated = b.Name;
            p.DpsA = OutputAxis(a.Dmg, a.AtkPerSec);
            p.DpsB = OutputAxis(b.Dmg, b.AtkPerSec);
            p.LenA = ReachAxis(a.LengthM);
            p.LenB = ReachAxis(b.LengthM);
            p.SweepA = CoverageAxis(a.SweepDeg);
            p.SweepB = CoverageAxis(b.SweepDeg);
            p.DurA = SustainAxis(a.DurabilityHits);
            p.DurB = SustainAxis(b.DurabilityHits);
            return p;
        }

        // ------------------------------------------------------------------
        // 旧口径对照（已作废，只留证据，不参与验收）
        // ------------------------------------------------------------------

        /// <summary>作废口径的控制轴：长度 × 横扫角。</summary>
        public static double LegacyControlAxis(double lengthM, double sweepDeg)
        {
            return lengthM * sweepDeg;
        }

        /// <summary>旧口径（长度 × 横扫角的乘积）下的支配对，供「尺子换掉前后」的对照打印。</summary>
        public static List<string> LegacyProductPairs(IList<WeaponDef> weapons)
        {
            List<string> outPairs = new List<string>();
            for (int i = 0; i < weapons.Count; i++)
            {
                for (int j = 0; j < weapons.Count; j++)
                {
                    if (i == j) continue;
                    WeaponDef a = weapons[i];
                    WeaponDef b = weapons[j];
                    if (a.IsControlClass || b.IsControlClass) continue;

                    double dpsA = OutputAxis(a.Dmg, a.AtkPerSec);
                    double ctlA = LegacyControlAxis(a.LengthM, a.SweepDeg);
                    double durA = SustainAxis(a.DurabilityHits);
                    double dpsB = OutputAxis(b.Dmg, b.AtkPerSec);
                    double ctlB = LegacyControlAxis(b.LengthM, b.SweepDeg);
                    double durB = SustainAxis(b.DurabilityHits);

                    if (dpsA < dpsB || ctlA < ctlB || durA < durB) continue;
                    bool over = dpsA > dpsB * DominanceMargin
                             || ctlA > ctlB * DominanceMargin
                             || durA > durB * DominanceMargin;
                    if (over) outPairs.Add(a.Name + "→" + b.Name);
                }
            }
            return outPairs;
        }

        /// <summary>主方案 §6.2 末列的 K 值与本实现 K 公式的回归误差上限（表内给到 0.1 位小数）。</summary>
        public const double KTableTolerance = 0.11;

        /// <summary>核对 §6.2 的 K 列能否由公式复算出来（I12 的一部分）。</summary>
        public static List<string> KTableMismatches(IList<WeaponDef> weapons)
        {
            List<string> bad = new List<string>();
            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponDef w = weapons[i];
                if (w == null || w.IsControlClass || w.KTable <= 0.0) continue;
                double k = K(w.Dmg, w.AtkPerSec, w.LengthM);
                if (Math.Abs(k - w.KTable) > KTableTolerance)
                {
                    bad.Add(w.Name + " 公式 K=" + k.ToString("F2") + " 表值 " + w.KTable.ToString("F2"));
                }
            }
            return bad;
        }
    }
}
