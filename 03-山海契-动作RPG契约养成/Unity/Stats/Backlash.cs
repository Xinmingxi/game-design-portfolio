using System;

namespace ShanHaiQi.Stats
{
    /// <summary>三类反噬触发（§4.6）。</summary>
    public enum BacklashTrigger
    {
        T1Signing = 0,   // 立契瞬间
        T2Breach = 1,    // 违契（长期）
        T3Combat = 2     // 战斗中
    }

    /// <summary>三种互斥后果（§4.6）。</summary>
    public enum BacklashConsequence
    {
        None = 0,
        C1Attack = 1,   // 反噬袭击
        C2BreakContract = 2, // 脱契逃离
        C3Scar = 3      // 留创
    }

    /// <summary>结构化输出：一次反噬判定的全部可结算字段。</summary>
    public struct BacklashResult
    {
        public bool Triggered;
        public BacklashTrigger Trigger;
        public double TriggerChance;          // 本次判定的触发概率
        public BacklashConsequence Consequence;

        // C1
        public double Damage;                 // = CPT_yao × 3.5，必定破防
        public bool GuardBroken;              // 必定破防
        // C1 + C2 共用
        public double TrustAfter;
        // C2
        public int ConsumablesLost;           // 带走 1 件随机消耗品
        public bool ContractSlotFreed;        // 契约位空出
        public bool SpeciesDespawned;         // 该巢穴不再刷新此个体
        // C3
        public int BodyCapDelta;              // 体魄上限 −1
        public double SpeciesAgitationDelta;  // 该妖种对你的初始惊扰 +20
        public double VoluntaryMultiplier;    // 自愿概率 ×0.8（持续到伤愈）
        public string RecoveryPath;

        public string Narrative;
    }

    /// <summary>
    /// 反噬判定（I9）。T1 概率取 Contract.BacklashOnSigning（§4.6 权威口径）；
    /// T2/T3 概率取自 §4.6 表；后果按 §4.6 的权重抽取（T1 时 C1 提到 70%）。
    /// 随机数由调用方以 roll01 ∈ [0,1) 注入，保证可复算。
    /// </summary>
    public static class Backlash
    {
        public const double C1DamageCoef = 3.5;      // §12 K13：CPT_yao × 3.5

        public const double WeightC1 = 0.50;
        public const double WeightC2 = 0.30;
        public const double WeightC3 = 0.20;
        public const double WeightC1OnT1 = 0.70;

        // §4.6 T2
        public const double T2TrustThreshold = 20.0;
        public const int T2IdleDays = 3;
        public const double T2KillSameSpeciesChance = 0.40;
        public const int T2ForcedConflictEvery = 5;
        public const double T2ForcedConflictChance = 0.25;
        public const double T2KillSameSpeciesTrustDelta = -25.0;

        // §4.6 T3
        public const double T3YaoHpThreshold = 0.30;
        public const double T3TrustThreshold = 40.0;
        public const double T3Per10sChance = 0.12;
        public const double T3PlayerDownChance = 0.30;

        // §4.6 C3
        public const int C3BodyCapDelta = -1;
        public const double C3SpeciesAgitationDelta = 20.0;
        public const double C3VoluntaryMultiplier = 0.8;

        /// <summary>T1 立契瞬间是否反噬，以及后果（概率走 §4.6 T1 的 K3 档内曲线）。</summary>
        public static BacklashResult JudgeSigning(double r, double trust, double cptYao, double roll01)
        {
            double chance = Contract.BacklashOnSigning(r);
            BacklashResult res = NewResult(BacklashTrigger.T1Signing, chance, trust);
            if (roll01 >= chance)
            {
                res.Narrative = "立契失败，妖离开（R ≥ 1.25 时不会反噬）";
                return res;
            }
            res.Triggered = true;
            return ResolveConsequence(res, cptYao, roll01 / chance);
        }

        /// <summary>T2 违契（长期）：① 信赖 &lt; 20 且连续 3 日未互动；② 在妖面前杀死同种妖；③ 强令冲突每 5 次。</summary>
        public static BacklashResult JudgeBreachKeepTrust(double trust, int idleDays,
                                                         bool killedSameSpecies, int forcedConflicts,
                                                         double cptYao, double roll01)
        {
            bool condA = trust < T2TrustThreshold && idleDays >= T2IdleDays;
            double chance = 0.0;
            if (condA) chance = Math.Max(chance, Trust.BandInfoOf(trust).DailyT2Chance);
            if (killedSameSpecies) chance = Math.Max(chance, T2KillSameSpeciesChance);
            if (forcedConflicts > 0 && forcedConflicts % T2ForcedConflictEvery == 0)
            {
                chance = Math.Max(chance, T2ForcedConflictChance * (forcedConflicts / T2ForcedConflictEvery));
            }
            chance = CombatPower.Clamp(chance, 0.0, 1.0);

            double t = trust;
            if (killedSameSpecies) t = Trust.Clamp(t + T2KillSameSpeciesTrustDelta, Trust.Min, Trust.Max);

            BacklashResult res = NewResult(BacklashTrigger.T2Breach, chance, t);
            if (roll01 >= chance)
            {
                res.Narrative = "没有触发反噬";
                return res;
            }
            res.Triggered = true;
            return ResolveConsequence(res, cptYao, roll01 / chance);
        }

        /// <summary>T3 战斗中：① 血量 &lt; 30% 且信赖 &lt; 40 → 每 10 秒 12%；② 玩家倒地且信赖 &lt; 40 → 30%。</summary>
        public static BacklashResult JudgeCombat(double yaoHpPct, double trust, bool playerDown,
                                                double cptYao, double roll01)
        {
            double chance = 0.0;
            if (playerDown && trust < T3TrustThreshold) chance = T3PlayerDownChance;
            else if (yaoHpPct < T3YaoHpThreshold && trust < T3TrustThreshold)
            {
                chance = Trust.BandInfoOf(trust).BacklashPer10s;
                if (double.IsNaN(chance)) chance = T3Per10sChance;
            }
            BacklashResult res = NewResult(BacklashTrigger.T3Combat, chance, trust);
            if (roll01 >= chance)
            {
                res.Narrative = "没有触发反噬";
                return res;
            }
            res.Triggered = true;
            return ResolveConsequence(res, cptYao, roll01 / chance);
        }

        /// <summary>§4.6 后果权重：C1 50 / C2 30 / C3 20；T1 触发时 C1 提到 70（其余按 30:20 重分配）。</summary>
        public static BacklashConsequence DrawConsequence(BacklashTrigger trigger, double roll01)
        {
            double w1 = (trigger == BacklashTrigger.T1Signing) ? WeightC1OnT1 : WeightC1;
            double rest = 1.0 - w1;
            double scale = (WeightC2 + WeightC3) > 0.0 ? rest / (WeightC2 + WeightC3) : 0.0;
            double c2End = w1 + WeightC2 * scale;

            if (roll01 < w1) return BacklashConsequence.C1Attack;
            if (roll01 < c2End) return BacklashConsequence.C2BreakContract;
            return BacklashConsequence.C3Scar;
        }

        private static BacklashResult NewResult(BacklashTrigger trigger, double chance, double trust)
        {
            BacklashResult r = new BacklashResult();
            r.Triggered = false;
            r.Trigger = trigger;
            r.TriggerChance = chance;
            r.Consequence = BacklashConsequence.None;
            r.TrustAfter = trust;
            r.ConsumablesLost = 0;
            r.BodyCapDelta = 0;
            r.SpeciesAgitationDelta = 0.0;
            r.VoluntaryMultiplier = 1.0;
            r.Damage = 0.0;
            r.GuardBroken = false;
            r.ContractSlotFreed = false;
            r.SpeciesDespawned = false;
            r.RecoveryPath = string.Empty;
            r.Narrative = string.Empty;
            return r;
        }

        private static BacklashResult ResolveConsequence(BacklashResult res, double cptYao, double roll01)
        {
            res.Consequence = DrawConsequence(res.Trigger, roll01);
            if (res.Consequence == BacklashConsequence.C1Attack)
            {
                res.Damage = cptYao * C1DamageCoef;
                res.GuardBroken = true;         // 「必定破防」
                res.TrustAfter = 0.0;           // 「信赖清零」
                res.SpeciesDespawned = true;    // 「该个体离开该区域」
                res.RecoveryPath = "无，该个体离开该区域";
                res.Narrative = "反噬袭击：妖转身攻击你一次，你被击倒，它逃走";
            }
            else if (res.Consequence == BacklashConsequence.C2BreakContract)
            {
                res.ConsumablesLost = 1;
                res.ContractSlotFreed = true;
                res.SpeciesDespawned = true;
                res.RecoveryPath = "需另找同种个体，无因果惩罚";
                res.Narrative = "脱契逃离：妖撕掉契约物，带走 1 件随机消耗品";
            }
            else
            {
                res.BodyCapDelta = C3BodyCapDelta;
                res.SpeciesAgitationDelta = C3SpeciesAgitationDelta;
                res.VoluntaryMultiplier = C3VoluntaryMultiplier;
                res.RecoveryPath = "静养 7 日 或 服「续断丹」";
                res.Narrative = "留创：体魄上限 −1，该妖种对你的初始惊扰 +20";
            }
            return res;
        }
    }
}
