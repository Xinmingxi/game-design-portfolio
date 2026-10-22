using System;

namespace ShanHaiQi.Stats
{
    /// <summary>自愿立契的五个因子 + 野性系数（§4.2 表）。取值均为 0–1。</summary>
    public struct VolInputs
    {
        public double MOffer;  // 献物匹配度 0 / 0.5 / 1
        public double QCalm;   // 1 − 惊扰度/100
        public double QGear;   // 收刀未点火 1 / 收刀点火 0.5 / 持械 0
        public double QBond;   // 初见 0 / 第二次 0.4 / 第三次起 0.7 / 有过献物史 1
        public double QTool;   // 器物辅助 0 / 1
        public double DBreed;  // 野性系数 0–1
    }

    /// <summary>自愿立契的硬性前置 G1–G5（§4.2）。任一条不满足则立契选项不出现。</summary>
    public struct VolGates
    {
        public bool G1NotAgitated;    // 最近 8 s 内未攻击你
        public bool G2WeaponSheathed; // 兵器在鞘内且未点火把
        public bool G3HpAtLeast60;    // 妖当前血量 ≥ 60%
        public bool G4NotForcedBefore;// 该妖种本周目未被强行签过
        public bool G5AgitationOk;    // 惊扰度 ≤ 40
    }

    /// <summary>惊扰度事件（§4.5 行为表）。</summary>
    public enum AgitationEvent
    {
        RaisedWeapon = 0,   // +25
        LitTorch = 1,       // +20
        RanWithin6m = 2,    // +10 / 次
        Attacked = 3,       // +40
        Fed = 4,            // −15
        Crouched3s = 5,     // −10
        Retreated10m = 6    // −5 / 秒
    }

    /// <summary>
    /// 签订系统：P_vol、P_force、前置门槛、惊扰度、立契瞬间反噬（I3/I4/I5/I6）。
    /// 公式逐字取自主方案 §4.2 / §4.3 / §4.6，接口口径见 §11.3。
    /// </summary>
    public static class Contract
    {
        // §4.2：P_vol 权重（合计 +0.95 / −0.25）
        public const double VolBase = 0.10;
        public const double VolWeightOffer = 0.30;
        public const double VolWeightCalm = 0.20;
        public const double VolWeightGear = 0.12;
        public const double VolWeightBond = 0.13;
        public const double VolWeightTool = 0.10;
        public const double VolWeightBreed = -0.25;
        public const double VolMin = 0.03;
        public const double VolMax = 0.90;

        // §4.3：P_force 权重
        public const double ForceBase = 0.15;
        public const double ForceWeightG = 0.45;
        public const double ForceWeightWeak = 0.15;
        public const double ForceWeightTool = 0.10;
        public const double ForceWeightBreed = -0.25;
        public const double ForceWeightHurt = -0.15;
        public const double ForceMin = 0.05;
        public const double ForceMax = 0.95;
        public const double ForceGDivisor = 0.8;

        // §4.6 T1 / §12 K3：危险档（1.00 ≤ R < 1.25）档内自 K3 递减 5 个百分点
        public const double K3DangerDefault = 0.45;
        public const double DangerBandDecayPoints = 0.05;

        // §4.5：惊扰度
        public const double AgitationMin = 0.0;
        public const double AgitationMax = 100.0;
        public const double AgitationDecayPerSecond = -1.0;

        /// <summary>I4 自愿立契成功率 P_vol（§4.2 公式，逐字实现）。</summary>
        public static double PVol(VolInputs v)
        {
            double p = VolBase
                     + VolWeightOffer * v.MOffer
                     + VolWeightCalm * v.QCalm
                     + VolWeightGear * v.QGear
                     + VolWeightBond * v.QBond
                     + VolWeightTool * v.QTool
                     + VolWeightBreed * v.DBreed;
            return CombatPower.Clamp(p, VolMin, VolMax);
        }

        /// <summary>I4 便捷重载：惊扰度直接传入（内部换算 Q_calm = 1 − 惊扰/100）。</summary>
        public static double PVol(double mOffer, double agitation, double qGear,
                                 double qBond, double qTool, double dBreed)
        {
            VolInputs v = new VolInputs();
            v.MOffer = mOffer;
            v.QCalm = 1.0 - agitation / AgitationMax;
            v.QGear = qGear;
            v.QBond = qBond;
            v.QTool = qTool;
            v.DBreed = dBreed;
            return PVol(v);
        }

        /// <summary>§4.2 Q_bond 取值：初见 0 / 第二次 0.4 / 第三次起 0.7 / 有过献物史 1（取大者，上限 1）。</summary>
        public static double QBond(int meetCount, bool hasOfferHistory)
        {
            double byMeet = 0.0;
            if (meetCount >= 3) byMeet = 0.7;
            else if (meetCount == 2) byMeet = 0.4;
            else byMeet = 0.0;
            double byOffer = hasOfferHistory ? 1.0 : 0.0;
            return byMeet > byOffer ? byMeet : byOffer;
        }

        /// <summary>§4.2 Q_gear 取值：收刀未点火 1 / 收刀点火 0.5 / 持械 0。</summary>
        public static double QGear(bool weaponSheathed, bool torchLit)
        {
            if (!weaponSheathed) return 0.0;
            return torchLit ? 0.5 : 1.0;
        }

        /// <summary>§4.2 Q_calm = 1 − 惊扰度/100。</summary>
        public static double QCalm(double agitation)
        {
            return 1.0 - CombatPower.Clamp(agitation, AgitationMin, AgitationMax) / AgitationMax;
        }

        /// <summary>§4.3 g(R) = clamp((R − 1) / 0.8, 0, 1)。</summary>
        public static double GOfR(double r)
        {
            return CombatPower.Clamp((r - 1.0) / ForceGDivisor, 0.0, 1.0);
        }

        /// <summary>I3 强行签约成功率 P_force（§4.3）。</summary>
        public static double PForce(double r, double dBreed, bool yaoWeakened, bool hasBinder, bool heroHurt)
        {
            double p = ForceBase
                     + ForceWeightG * GOfR(r)
                     + ForceWeightWeak * (yaoWeakened ? 1.0 : 0.0)
                     + ForceWeightTool * (hasBinder ? 1.0 : 0.0)
                     + ForceWeightBreed * dBreed
                     + ForceWeightHurt * (heroHurt ? 1.0 : 0.0);
            return CombatPower.Clamp(p, ForceMin, ForceMax);
        }

        /// <summary>§4.3 W_weak：妖血量 &lt; 40% 时为 1。</summary>
        public static bool IsYaoWeakened(double yaoHpPct)
        {
            return yaoHpPct < 0.40;
        }

        /// <summary>§4.3 H_hurt：玩家血量 &lt; 60% 时为 1。</summary>
        public static bool IsHeroHurt(double heroHpPct)
        {
            return heroHpPct < 0.60;
        }

        /// <summary>
        /// §4.6 T1 立契瞬间的反噬概率——唯一的判定口径（早前版本写的「1 − P_force」已由主方案作废）：
        /// R &lt; 1.00        → 1.00（禁止档，即时反噬）
        /// 1.00 ≤ R &lt; 1.25  → 危险档档内曲线：自 K3（默认 45%）线性递减 5 个百分点到 R→1.25 处的 40%
        /// R ≥ 1.25         → 0（强档，立契失败只是妖逃走）
        /// </summary>
        public static double BacklashOnSigning(double r, double k3Danger)
        {
            if (r < 1.00) return 1.00;
            if (r < CombatPower.ThresholdDanger)
            {
                double span = CombatPower.ThresholdDanger - 1.00;
                double t = CombatPower.Clamp((r - 1.00) / span, 0.0, 1.0);
                return CombatPower.Clamp(k3Danger - DangerBandDecayPoints * t, 0.0, 1.0);
            }
            return 0.0;
        }

        /// <summary>K3 取默认 45% 时的立契瞬间反噬概率。</summary>
        public static double BacklashOnSigning(double r)
        {
            return BacklashOnSigning(r, K3DangerDefault);
        }

        /// <summary>I5 前置门槛：返回 0 = 全部通过；1–5 = 首个不满足的 G 编号。</summary>
        public static int FirstFailingGate(VolGates g)
        {
            if (!g.G1NotAgitated) return 1;
            if (!g.G2WeaponSheathed) return 2;
            if (!g.G3HpAtLeast60) return 3;
            if (!g.G4NotForcedBefore) return 4;
            if (!g.G5AgitationOk) return 5;
            return 0;
        }

        /// <summary>I5 不满足时的玩家提示（§4.2 表「不满足时的表现」逐字照抄）。</summary>
        public static string GateHint(int failingGate)
        {
            switch (failingGate)
            {
                case 1: return "它还在扑";
                case 2: return "刀在手，它不敢过来";
                case 3: return "伤成这样，它只会拼命";
                case 4: return "山里传开了";
                case 5: return "它在退";
                default: return string.Empty;
            }
        }

        /// <summary>§4.2 G5 的判定阈：惊扰度 ≤ 40。</summary>
        public static bool Gate5AgitationOk(double agitation)
        {
            return agitation <= 40.0;
        }

        /// <summary>§4.5 行为表：单次事件对惊扰度的增量。</summary>
        public static double AgitationDelta(AgitationEvent e)
        {
            switch (e)
            {
                case AgitationEvent.RaisedWeapon: return 25.0;
                case AgitationEvent.LitTorch: return 20.0;
                case AgitationEvent.RanWithin6m: return 10.0;
                case AgitationEvent.Attacked: return 40.0;
                case AgitationEvent.Fed: return -15.0;
                case AgitationEvent.Crouched3s: return -10.0;
                case AgitationEvent.Retreated10m: return -5.0;
                default: return 0.0;
            }
        }

        /// <summary>
        /// I6 惊扰更新：先累加事件，再按「每 1 秒未受刺激 −1」回落到 initial 为止（§4.5）。
        /// 惊扰度不会低于该妖种的初始值。
        /// </summary>
        public static double UpdateAgitation(double current, double initial, double tickSeconds,
                                            double eventDeltaSum)
        {
            double v = current + eventDeltaSum;
            if (tickSeconds > 0.0)
            {
                double floor = initial;
                v += AgitationDecayPerSecond * tickSeconds;
                if (v < floor) v = floor;
            }
            return CombatPower.Clamp(v, AgitationMin, AgitationMax);
        }
    }
}
