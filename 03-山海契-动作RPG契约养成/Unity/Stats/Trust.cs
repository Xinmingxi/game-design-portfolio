using System;

namespace ShanHaiQi.Stats
{
    /// <summary>信赖事件（§5.1 表逐行）。</summary>
    public enum TrustEventType
    {
        FeedRight = 0,        // +6，每日上限 +12
        FeedWrong = 1,        // +1，每日上限 +2
        Groom = 2,            // +5，每日上限 +10
        FightTogether = 3,    // +2，每日上限 +6
        CoopKill = 4,         // +5，每日上限 +15
        LetItChoose = 5,      // +4，每日上限 +8
        ProtectHero = 6,      // +8，无上限（需信赖 ≥60）
        FriendlyFire = 7,     // −6
        Hungry = 8,           // −2（每日结算）
        ForcedAgainstNature = 9, // −8
        KillSameSpecies = 10, // −25（另触发 §4.6 T2）
        IdleInCamp = 11       // −1/日
    }

    /// <summary>信赖五段（§5.3 表）。</summary>
    public enum TrustBand
    {
        Hostile = 0,     // 敌意 0–19
        Tolerated = 1,   // 容忍 20–39
        Coordinated = 2, // 配合 40–59
        Close = 3,       // 亲近 60–79
        Bound = 4        // 契友 80–100
    }

    /// <summary>段位的全部可程序检属性（§5.3 表）。</summary>
    public struct TrustBandInfo
    {
        public TrustBand Band;
        public string Name;
        public double Min;
        public double Max;
        public string Behavior;
        public double CommandDelaySec;
        public double DefianceChance;
        public double BacklashPer10s;      // 战斗中每 10 秒的反噬判定概率
        public double DailyT2Chance;       // 「敌意」段的每日 T2 判定
        public string Unlock;
    }

    /// <summary>事件结算结果。</summary>
    public struct TrustEventResult
    {
        public double Applied;      // 实际生效的增量（已按每日上限截断）
        public double NewTrust;     // 结算后的信赖值
        public bool CappedByDayLimit;
    }

    /// <summary>
    /// 信赖值系统（I7 / I8）。信赖不参与战斗伤害加成，只做行为开关（§5 开头）。
    /// </summary>
    public static class Trust
    {
        public const double Min = 0.0;
        public const double Max = 100.0;

        public const double BaseDailyDecay = 0.8;         // §5.2
        public const double RainOvernightMultiplier = 1.5;
        public const double CampCrowdedMultiplier = 1.3;
        public const double GroomingHabitMultiplier = 0.5;
        public const int CampCrowdedThreshold = 2;        // 「营地妖 > 2 只」

        public const double GrudgeDays = 7.0;             // §4.1 记仇期
        public const double GrudgeDailyDelta = -1.0;

        public const double ForcedStartTrust = 5.0;       // §4.1 强行签约起手
        public const double ForcedInstantPenalty = -15.0; // §4.1 立契瞬间 −15
        public const double VoluntaryStartMin = 20.0;     // §4.1 起手区间
        public const double VoluntaryStartMax = 45.0;

        public static double Clamp(double v, double min, double max)
        {
            return CombatPower.Clamp(v, min, max);
        }

        /// <summary>I8 Trust → 段位。边界左闭右开（20 / 40 / 60 / 80）。</summary>
        public static TrustBand BandOf(double trust)
        {
            if (trust < 20.0) return TrustBand.Hostile;
            if (trust < 40.0) return TrustBand.Tolerated;
            if (trust < 60.0) return TrustBand.Coordinated;
            if (trust < 80.0) return TrustBand.Close;
            return TrustBand.Bound;
        }

        /// <summary>I8 段位静态数据（§5.3 表逐行照抄）。</summary>
        public static TrustBandInfo BandInfoOf(double trust)
        {
            TrustBand band = BandOf(trust);
            TrustBandInfo i = new TrustBandInfo();
            i.Band = band;
            if (band == TrustBand.Hostile)
            {
                i.Name = "敌意"; i.Min = 0; i.Max = 19;
                i.Behavior = "拒绝出战，被强召则攻击你";
                i.CommandDelaySec = double.NaN;      // 表内无值：拒绝出战
                i.DefianceChance = 1.0;              // 拒绝出战即 100% 不听令
                i.BacklashPer10s = double.NaN;
                i.DailyT2Chance = 0.15;
                i.Unlock = "无";
            }
            else if (band == TrustBand.Tolerated)
            {
                i.Name = "容忍"; i.Min = 20; i.Max = 39;
                i.Behavior = "可出战；命令延迟 +0.4 s；15% 抗命（原地不动）";
                i.CommandDelaySec = 0.4;
                i.DefianceChance = 0.15;
                i.BacklashPer10s = 0.12;             // 血量 <30% 时
                i.DailyT2Chance = 0.0;
                i.Unlock = "基础指令：跟随/攻击";
            }
            else if (band == TrustBand.Coordinated)
            {
                i.Name = "配合"; i.Min = 40; i.Max = 59;
                i.Behavior = "命令延迟 0；不再抗命";
                i.CommandDelaySec = 0.0;
                i.DefianceChance = 0.0;
                i.BacklashPer10s = 0.05;
                i.DailyT2Chance = 0.0;
                i.Unlock = "协同技 1（如「扑咬牵制」）";
            }
            else if (band == TrustBand.Close)
            {
                i.Name = "亲近"; i.Min = 60; i.Max = 79;
                i.Behavior = "主动绕后、自动拾取掉落";
                i.CommandDelaySec = 0.0;
                i.DefianceChance = 0.0;
                i.BacklashPer10s = 0.01;
                i.DailyT2Chance = 0.0;
                i.Unlock = "护主（每场 1 次替你挡致命伤）；协同技 2";
            }
            else
            {
                i.Name = "契友"; i.Min = 80; i.Max = 100;
                i.Behavior = "可脱离指令自主作战 20 秒；进门先嗅、遇同类先看你";
                i.CommandDelaySec = 0.0;
                i.DefianceChance = 0.0;
                i.BacklashPer10s = 0.0;
                i.DailyT2Chance = 0.0;
                i.Unlock = "第二形态（外观 + 1 个新动作）；可「托付」：单独看守一个节点";
            }
            return i;
        }

        /// <summary>§5.1 事件规则：增量与每日上限（上限为 0 表示「无上限」或「无正值」）。</summary>
        public static double EventDelta(TrustEventType type)
        {
            switch (type)
            {
                case TrustEventType.FeedRight: return 6.0;
                case TrustEventType.FeedWrong: return 1.0;
                case TrustEventType.Groom: return 5.0;
                case TrustEventType.FightTogether: return 2.0;
                case TrustEventType.CoopKill: return 5.0;
                case TrustEventType.LetItChoose: return 4.0;
                case TrustEventType.ProtectHero: return 8.0;
                case TrustEventType.FriendlyFire: return -6.0;
                case TrustEventType.Hungry: return -2.0;
                case TrustEventType.ForcedAgainstNature: return -8.0;
                case TrustEventType.KillSameSpecies: return -25.0;
                case TrustEventType.IdleInCamp: return -1.0;
                default: return 0.0;
            }
        }

        /// <summary>§5.1 每日上限（表内「—」按 0 = 不设正向上限处理）。</summary>
        public static double DailyCap(TrustEventType type)
        {
            switch (type)
            {
                case TrustEventType.FeedRight: return 12.0;
                case TrustEventType.FeedWrong: return 2.0;
                case TrustEventType.Groom: return 10.0;
                case TrustEventType.FightTogether: return 6.0;
                case TrustEventType.CoopKill: return 15.0;
                case TrustEventType.LetItChoose: return 8.0;
                default: return 0.0;
            }
        }

        /// <summary>
        /// I7 单事件结算：先按每日上限截断，再夹到 0–100。
        /// alreadyAppliedToday 为该事件今天已生效的累计量（调用方保存）。
        /// </summary>
        public static TrustEventResult ApplyEvent(TrustEventType type, double current,
                                                 double alreadyAppliedToday)
        {
            double delta = EventDelta(type);
            double cap = DailyCap(type);
            bool capped = false;
            if (cap > 0.0 && delta > 0.0 && alreadyAppliedToday + delta > cap)
            {
                delta = cap - alreadyAppliedToday;
                if (delta < 0.0) delta = 0.0;
                capped = true;
            }
            TrustEventResult r = new TrustEventResult();
            r.Applied = delta;
            r.NewTrust = Clamp(current + delta, Min, Max);
            r.CappedByDayLimit = capped;
            return r;
        }

        /// <summary>§5.2 每日衰减量（正数）：0.8 × 天气修正 × 营地拥挤修正 × 梳毛日常修正。</summary>
        public static double DailyDecayAmount(bool rainOvernight, int campYaoCount, bool groomingHabit)
        {
            double amount = BaseDailyDecay;
            if (rainOvernight) amount *= RainOvernightMultiplier;
            if (campYaoCount > CampCrowdedThreshold) amount *= CampCrowdedMultiplier;
            if (groomingHabit) amount *= GroomingHabitMultiplier;
            return amount;
        }

        /// <summary>§5.2 营地日结算：事件总和 − 衰减（一次性扣，不连续掉）。</summary>
        public static double SettleDay(double current, double eventDeltaSum, bool rainOvernight,
                                      int campYaoCount, bool groomingHabit)
        {
            double decay = DailyDecayAmount(rainOvernight, campYaoCount, groomingHabit);
            return Clamp(current + eventDeltaSum - decay, Min, Max);
        }

        /// <summary>复利式日衰减（只用于「N 天后剩多少」的推演/测试，营地实际走 SettleDay）。</summary>
        public static double AfterDaysOfDecayOnly(double start, int days, bool rainOvernight,
                                                 int campYaoCount, bool groomingHabit)
        {
            double v = start;
            double decay = DailyDecayAmount(rainOvernight, campYaoCount, groomingHabit);
            for (int i = 0; i < days; i++) v = Clamp(v - decay, Min, Max);
            return v;
        }

        /// <summary>§4.1 强行签约起手：5，并进入 7 日记仇期（每日 −1）。</summary>
        public static double ForcedStartAfterInstant(double baseTrust)
        {
            return Clamp(baseTrust + ForcedInstantPenalty, Min, Max);
        }

        /// <summary>§5.3 抗命概率（K9：Trust&lt;40 时 15%）。</summary>
        public static double DefianceChance(double trust)
        {
            return BandInfoOf(trust).DefianceChance;
        }

        /// <summary>§5.3 命令延迟（秒）。</summary>
        public static double CommandDelay(double trust)
        {
            return BandInfoOf(trust).CommandDelaySec;
        }

        /// <summary>§5.3 「亲近」及以上才有护主（每场 1 次替你挡致命伤）。</summary>
        public static bool HasGuardian(double trust)
        {
            return trust >= 60.0;
        }

        /// <summary>§5.3 「契友」才可托付看守节点。</summary>
        public static bool CanEntrust(double trust)
        {
            return trust >= 80.0;
        }
    }
}
