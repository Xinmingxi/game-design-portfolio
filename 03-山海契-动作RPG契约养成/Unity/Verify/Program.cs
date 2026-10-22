using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using ShanHaiQi.Stats;
using ShanHaiQi.Stats.Data;

namespace ShanHaiQi.Verify
{
    /// <summary>
    /// 《山海契》数值层命令行验证程序。
    /// 直接编译 ../Stats 下的源码（同一份实现），读取 ../Stats/Data/*.json 的真实数值。
    /// 用法：cd Verify &amp;&amp; dotnet run
    /// </summary>
    internal static class Program
    {
        private static int _pass;
        private static int _fail;
        private static readonly List<string> _failures = new List<string>();

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            WeaponTableFile weaponFile = Load<WeaponTableFile>("Weapons.json");
            YaoTableFile yaoFile = Load<YaoTableFile>("Yao.json");
            ItemTableFile itemFile = Load<ItemTableFile>("Items.json");

            if (weaponFile == null || yaoFile == null || itemFile == null)
            {
                Console.WriteLine();
                Console.WriteLine("FAIL");
                return 1;
            }

            Console.WriteLine("=== 《山海契》数值层验证（直接链接 Stats 源码） ===");
            Console.WriteLine("数据：Weapons.json " + weaponFile.Weapons.Length + " 件 / Yao.json "
                + yaoFile.Yao.Length + " 种 / Items.json " + itemFile.Items.Length + " 条");
            Console.WriteLine();

            Section("§4.2 自愿立契 P_vol");
            CheckVol();

            Section("§4.4 战力指数 CPT 与战力比 R（算例复算）");
            CheckCombatPower();

            Section("§4.4 四档跳变 + §4.6 T1 立契瞬间反噬");
            CheckBands();

            Section("§5.2 信赖衰减与 §5.3 五段阈值");
            CheckTrust();

            Section("§7.3 下坠表与提前量");
            CheckBallistics();

            Section("§7.2 蓄力曲线");
            CheckCharge();

            Section("§7.4 远程四条判据（两个原文算例）");
            CheckRangedCriteria();

            Section("§6.3 十八件兵器：K 与三轴支配自检（I12）");
            CheckWeapons(weaponFile.Weapons);

            Section("§8 符箓 / 阵法 / 丹药 规则常数");
            CheckConsumables();

            Section("数据文件完整性（18 / 14 / 6+4+6）");
            CheckData(weaponFile, yaoFile, itemFile);

            Console.WriteLine();
            Console.WriteLine("=== 汇总：" + _pass + " 通过 / " + _fail + " 失败 ===");
            if (_fail > 0)
            {
                Console.WriteLine();
                Console.WriteLine("失败明细：");
                for (int i = 0; i < _failures.Count; i++) Console.WriteLine("  - " + _failures[i]);
                Console.WriteLine();
                Console.WriteLine("FAIL");
                return 1;
            }
            Console.WriteLine();
            Console.WriteLine("PASS");
            return 0;
        }

        // ---------------------------------------------------------------- §4.2

        private static void CheckVol()
        {
            // 原文算例：狌狌 D=0.30，第三次见面(0.7)，野果(M=1)，惊扰 20(0.8)，收刀未点火(1.0)，无铃(0)
            VolInputs v = new VolInputs();
            v.MOffer = 1.0;
            v.QCalm = Contract.QCalm(20.0);
            v.QGear = Contract.QGear(true, false);
            v.QBond = Contract.QBond(3, false);
            v.QTool = 0.0;
            v.DBreed = 0.30;
            double p = Contract.PVol(v);
            Check("P_vol 原文算例（狌狌，§4.2 算例 = 0.696）", p, 0.696, 1e-9);

            double byAgitation = Contract.PVol(1.0, 20.0, 1.0, 0.7, 0.0, 0.30);
            Check("P_vol 便捷重载（惊扰直传 20）一致", byAgitation, p, 1e-12);

            // 权重合计与夹取
            VolInputs zero = new VolInputs();
            Check("P_vol 全零 → 基础项 0.10", Contract.PVol(zero), 0.10, 1e-12);

            VolInputs low = new VolInputs();
            low.DBreed = 1.0;
            Check("P_vol 下界夹取 0.03（0.10 − 0.25 = −0.15）", Contract.PVol(low), 0.03, 1e-12);

            VolInputs high = new VolInputs();
            high.MOffer = 1.0; high.QCalm = 1.0; high.QGear = 1.0; high.QBond = 1.0; high.QTool = 1.0;
            Check("P_vol 上界夹取 0.90（0.10 + 0.85 = 0.95）", Contract.PVol(high), 0.90, 1e-12);

            Check("Q_gear 收刀未点火", Contract.QGear(true, false), 1.0, 0.0);
            Check("Q_gear 收刀点火", Contract.QGear(true, true), 0.5, 0.0);
            Check("Q_gear 持械", Contract.QGear(false, false), 0.0, 0.0);
            Check("Q_bond 第二次见面", Contract.QBond(2, false), 0.4, 0.0);
            Check("Q_bond 第三次起", Contract.QBond(3, false), 0.7, 0.0);
            Check("Q_bond 有过献物史", Contract.QBond(1, true), 1.0, 0.0);

            VolGates g = new VolGates();
            g.G1NotAgitated = true; g.G2WeaponSheathed = true; g.G3HpAtLeast60 = true;
            g.G4NotForcedBefore = false; g.G5AgitationOk = true;
            Check("I5 前置门槛首个失败项 = G4", Contract.FirstFailingGate(g), 4, 0);
            CheckText("I5 失败提示语", Contract.GateHint(4), "山里传开了");

            Check("I6 惊扰：攻击一次 (+40)", Contract.AgitationDelta(AgitationEvent.Attacked), 40.0, 0.0);
            Check("I6 惊扰回落至初始值（80→20，tick 100 s）",
                Contract.UpdateAgitation(80.0, 20.0, 100.0, 0.0), 20.0, 1e-12);
        }

        // ---------------------------------------------------------------- §4.4

        private static void CheckCombatPower()
        {
            // 原文算例：体魄 11 / 技艺 13 / 器 15，HP 70%、疲劳 0.2、无伤
            double weighted = CombatPower.HeroStatWeighted(11, 13, 15);
            Check("CPT 加权和 = 4.4 + 4.55 + 3.75", weighted, 12.7, 1e-12);

            double s = CombatPower.HeroConditionS(0.70, 0.20, 0.0);
            Check("S = 0.7 × 0.94", s, 0.658, 1e-12);

            double cptHero = CombatPower.HeroCpt(11, 13, 15, 0.70, 0.20, 0.0);
            Check("CPT_hero（原文 8.36）", cptHero, 8.36, 0.005);

            double cptYao = CombatPower.YaoCpt(9.5, 0.35, YaoMood.Calm);
            Check("CPT_yao（狰 B=9.5，血量 35%）", cptYao, 3.325, 1e-12);

            double r = CombatPower.R(cptHero, cptYao);
            Check("R（原文 2.51）", r, 2.51, 0.005);

            double pForce = Contract.PForce(r, 0.60, true, true, false);
            Check("P_force（原文 0.70：0.15+0.45+0.15+0.10−0.15）", pForce, 0.70, 1e-9);

            // 玩家血量 45% 的第二个算例
            double cptHero2 = CombatPower.HeroCpt(11, 13, 15, 0.45, 0.20, 0.0);
            Check("CPT_hero（HP 45%，原文 5.4）", cptHero2, 5.4, 0.05);
            double r2 = CombatPower.R(cptHero2, cptYao);
            Check("R（HP 45%，原文 1.62）", r2, 1.62, 0.005);

            // §4.4 算例第二步（主方案已修正：早前误写 0.55，是漏了「R 同时下降」这一步）
            // HP 45% → CPT_hero 5.37 → R 1.62 → g(R) 0.77 → P_force = 0.15+0.45×0.77+0.15+0.10−0.15−0.15 = 0.45
            double gAt162 = Contract.GOfR(r2);
            Check("g(R) 在 R=1.62 处 = 0.77（已不再封顶）", gAt162, 0.77, 0.005);
            Check("P_force（HP 45%，§4.4 修正后原文 0.45）", Contract.PForce(r2, 0.60, true, true, true), 0.45, 0.005);
            double pForceFull = Contract.PForce(r, 0.60, true, true, false);
            CheckTrue("掉血对强签是双重惩罚：0.70 → 0.45",
                pForceFull - Contract.PForce(r2, 0.60, true, true, true) > 0.20,
                "掉血 25 个点，成功率掉 " + Fmt(pForceFull - Contract.PForce(r2, 0.60, true, true, true)));
            CheckText("R=1.62 仍在「稳」区", CombatPower.BandInfoOf(CombatPower.BandOf(r2)).Name, "稳");

            Check("状态系数 怒", CombatPower.StateCoef(YaoMood.Angry), 1.15, 0.0);
            Check("状态系数 常", CombatPower.StateCoef(YaoMood.Calm), 1.00, 0.0);
            Check("状态系数 疲", CombatPower.StateCoef(YaoMood.Tired), 0.85, 0.0);
        }

        private static void CheckBands()
        {
            CheckText("R=0.999 → 禁止", CombatPower.BandInfoOf(CombatPower.BandOf(0.999)).Name, "禁止");
            CheckText("R=1.000 → 危险", CombatPower.BandInfoOf(CombatPower.BandOf(1.000)).Name, "危险");
            CheckText("R=1.249 → 危险", CombatPower.BandInfoOf(CombatPower.BandOf(1.249)).Name, "危险");
            CheckText("R=1.250 → 可控", CombatPower.BandInfoOf(CombatPower.BandOf(1.250)).Name, "可控");
            CheckText("R=1.599 → 可控", CombatPower.BandInfoOf(CombatPower.BandOf(1.599)).Name, "可控");
            CheckText("R=1.600 → 稳", CombatPower.BandInfoOf(CombatPower.BandOf(1.600)).Name, "稳");

            // §4.6 T1 曲线（K3 默认 45%）：禁止档 100% / 危险档自 45% 递减 5 个点到 40% / 强档 0%
            Check("T1 反噬概率 R=0.99（禁止档 → 100%）", Contract.BacklashOnSigning(0.99), 1.00, 0.0);
            Check("T1 反噬概率 R=1.00（危险档起点 = K3 45%）", Contract.BacklashOnSigning(1.00), 0.45, 1e-12);
            double near = Contract.BacklashOnSigning(1.24);
            Check("T1 反噬概率 R=1.24（档内递减 → 40.2%）", near, 0.402, 1e-3);
            double far = Contract.BacklashOnSigning(1.26);
            Check("T1 反噬概率 R=1.25（强档 → 0）", Contract.BacklashOnSigning(1.25), 0.0, 0.0);
            Check("T1 反噬概率 R=1.26（强档 → 0）", far, 0.0, 0.0);
            CheckTrue("T1 在 1.25 处是跳变而非线性滑过（40.2% → 0，落差 ≥ 0.35）",
                (near - far) >= 0.35,
                "R=1.24 得 " + Fmt(near) + "，R=1.26 得 " + Fmt(far) + "，落差 " + Fmt(near - far));
            Check("T1 档内曲线中点 R=1.125（45% 与 40% 的中值）", Contract.BacklashOnSigning(1.125), 0.425, 1e-12);
            Check("T1 K3 可调：K3=65% 时 R=1.00 → 65%", Contract.BacklashOnSigning(1.00, 0.65), 0.65, 1e-12);
            Check("T1 K3 可调：K3=25% 时 R=1.24 → 20.2%（仍递减 5 个点）", Contract.BacklashOnSigning(1.24, 0.25), 0.202, 1e-3);
            CheckTrue("T1 概率随 R 在危险档内单调递减",
                Contract.BacklashOnSigning(1.00) > Contract.BacklashOnSigning(1.125)
                    && Contract.BacklashOnSigning(1.125) > Contract.BacklashOnSigning(1.24),
                "45% > 42.5% > 40.2%");

            // §4.4 表的档位参考值（仅展示，判定口径在 §4.6）
            BandInfo danger = CombatPower.BandInfoOf(PowerBand.Dangerous);
            Check("危险档 §4.4 反噬参考下界 40%（R=1.24 处）", danger.BacklashRefMin, 0.40, 0.0);
            Check("危险档 §4.4 反噬参考上界 45%（K3 档内起点）", danger.BacklashRefMax, 0.45, 0.0);
            Check("稳档 §4.4 反噬参考 0%（已按 §4.6 统一）",
                CombatPower.BandInfoOf(PowerBand.Stable).BacklashRefMax, 0.00, 0.0);
            Check("可控档 §4.4 反噬参考 0%（已按 §4.6 统一）",
                CombatPower.BandInfoOf(PowerBand.Controllable).BacklashRefMax, 0.00, 0.0);
            CheckTrue("§4.4 表与 §4.6 T1 现已同口径（R≥1.25 两边都是 0）",
                CombatPower.BandInfoOf(PowerBand.Controllable).BacklashRefMax == 0.0
                    && Contract.BacklashOnSigning(1.30) == 0.0,
                "R=1.30：§4.4 参考 0%，§4.6 判定 0%");

            // 反噬后果权重：默认 50/30/20，T1 时 C1 提到 70%
            Console.WriteLine(Indent + "反噬后果抽取边界：默认 C1 [0,0.5) / C2 [0.5,0.8) / C3 [0.8,1.0]；"
                + "T1 时 C1 [0,0.7) / C2 [0.7,0.88) / C3 [0.88,1.0]");
            CheckText("后果 T1 roll=0.65 → C1", Backlash.DrawConsequence(BacklashTrigger.T1Signing, 0.65).ToString(), "C1Attack");
            CheckText("后果 T1 roll=0.75 → C2", Backlash.DrawConsequence(BacklashTrigger.T1Signing, 0.75).ToString(), "C2BreakContract");
            CheckText("后果 T1 roll=0.95 → C3", Backlash.DrawConsequence(BacklashTrigger.T1Signing, 0.95).ToString(), "C3Scar");
            CheckText("后果 T3 roll=0.55 → C2", Backlash.DrawConsequence(BacklashTrigger.T3Combat, 0.55).ToString(), "C2BreakContract");

            // C1 伤害 = CPT_yao × 3.5
            BacklashResult c1 = Backlash.JudgeSigning(0.90, 40.0, 3.325, 0.10);
            Check("C1 伤害 = CPT_yao × 3.5", c1.Damage, 3.325 * 3.5, 1e-12);
            CheckTrue("C1 必定破防且信赖清零", c1.GuardBroken && c1.TrustAfter == 0.0, "GuardBroken=" + c1.GuardBroken + " TrustAfter=" + Fmt(c1.TrustAfter));
        }

        // ---------------------------------------------------------------- §5

        private static void CheckTrust()
        {
            Check("§5.2 每日衰减 0.8", Trust.DailyDecayAmount(false, 0, false), 0.8, 1e-12);
            Check("§5.2 雨天在外过夜 ×1.5", Trust.DailyDecayAmount(true, 0, false), 1.2, 1e-12);
            Check("§5.2 营地拥挤（>2 只）×1.3", Trust.DailyDecayAmount(false, 3, false), 1.04, 1e-12);
            Check("§5.2 梳毛日常 ×0.5", Trust.DailyDecayAmount(false, 0, true), 0.4, 1e-12);

            Check("§5.2 原文读法：昨天 62 → 今天 61.2", Trust.SettleDay(62.0, 0.0, false, 0, false), 61.2, 1e-12);

            double d90 = Trust.AfterDaysOfDecayOnly(100.0, 90, false, 0, false);
            Check("§5.2 90 天后（100 − 90×0.8 = 28）", d90, 28.0, 1e-9);
            CheckText("90 天后段位 = 容忍", Trust.BandInfoOf(d90).Name, "容忍");

            CheckText("Trust 19 → 敌意", Trust.BandInfoOf(19.0).Name, "敌意");
            CheckText("Trust 20 → 容忍", Trust.BandInfoOf(20.0).Name, "容忍");
            CheckText("Trust 39 → 容忍", Trust.BandInfoOf(39.0).Name, "容忍");
            CheckText("Trust 40 → 配合", Trust.BandInfoOf(40.0).Name, "配合");
            CheckText("Trust 59 → 配合", Trust.BandInfoOf(59.0).Name, "配合");
            CheckText("Trust 60 → 亲近", Trust.BandInfoOf(60.0).Name, "亲近");
            CheckText("Trust 79 → 亲近", Trust.BandInfoOf(79.0).Name, "亲近");
            CheckText("Trust 80 → 契友", Trust.BandInfoOf(80.0).Name, "契友");

            Check("容忍段命令延迟 +0.4 s", Trust.CommandDelay(30.0), 0.4, 0.0);
            Check("配合段命令延迟 0", Trust.CommandDelay(50.0), 0.0, 0.0);
            Check("容忍段抗命 15%", Trust.DefianceChance(30.0), 0.15, 0.0);
            Check("信任段每 10 秒反噬 5%", Trust.BandInfoOf(50.0).BacklashPer10s, 0.05, 0.0);
            Check("敌意段每日 T2 15%", Trust.BandInfoOf(10.0).DailyT2Chance, 0.15, 0.0);
            CheckTrue("亲近段才有护主", Trust.HasGuardian(60.0) && !Trust.HasGuardian(59.9), "60 → true, 59.9 → false");

            // 每日上限：喂对食物 +6/次，上限 +12
            TrustEventResult r1 = Trust.ApplyEvent(TrustEventType.FeedRight, 50.0, 0.0);
            TrustEventResult r2 = Trust.ApplyEvent(TrustEventType.FeedRight, r1.NewTrust, r1.Applied);
            TrustEventResult r3 = Trust.ApplyEvent(TrustEventType.FeedRight, r2.NewTrust, r1.Applied + r2.Applied);
            Check("喂对食物一次 +6", r1.Applied, 6.0, 0.0);
            Check("喂对食物两次累计 +12", r1.Applied + r2.Applied, 12.0, 0.0);
            Check("喂对食物第三次被每日上限截断为 0", r3.Applied, 0.0, 0.0);
            Check("负向事件不受每日上限影响（误伤 −6）", Trust.ApplyEvent(TrustEventType.FriendlyFire, 50.0, 0.0).Applied, -6.0, 0.0);

            Check("§4.1 强行签约起手 5 − 15 = 0（夹取）", Trust.ForcedStartAfterInstant(Trust.ForcedStartTrust), 0.0, 0.0);
        }

        // ---------------------------------------------------------------- §7.3

        private static void CheckBallistics()
        {
            RangedSpec bow = Ballistics.SpecByName("角弓");
            RangedSpec crossbow = Ballistics.SpecByName("蹶张弩");
            RangedSpec repeater = Ballistics.SpecByName("连弩");

            // §7.3 表：20 / 40 / 60 m
            double[] dist = new double[] { 20.0, 40.0, 60.0 };
            double[] bowTable = new double[] { 0.65, 2.59, 5.83 };
            double[] cbTable = new double[] { 0.46, 1.86, 4.18 };
            double[] repTable = new double[] { 0.97, 3.87, 8.71 };

            for (int i = 0; i < dist.Length; i++)
            {
                Check("下坠 角弓 " + dist[i] + " m（§7.3 表 " + bowTable[i] + "）",
                    Ballistics.Drop(dist[i], bow.MuzzleSpeed), bowTable[i], 0.01);
            }
            for (int i = 0; i < dist.Length; i++)
            {
                Check("下坠 蹶张弩 " + dist[i] + " m（§7.3 表 " + cbTable[i] + "）",
                    Ballistics.Drop(dist[i], crossbow.MuzzleSpeed), cbTable[i], 0.01);
            }
            for (int i = 0; i < dist.Length; i++)
            {
                Check("下坠 连弩 " + dist[i] + " m（§7.3 表 " + repTable[i] + "）",
                    Ballistics.Drop(dist[i], repeater.MuzzleSpeed), repTable[i], 0.01);
            }

            // 公式本身：drop = 0.5 × 9.8 × t²
            double t = Ballistics.FlightTime(40.0, 55.0);
            Check("飞行时间 t = 距离 / 初速（40/55）", t, 40.0 / 55.0, 1e-12);
            Check("下坠解析式 0.5·g·t²（40 m 角弓）", 0.5 * 9.8 * t * t, Ballistics.Drop(40.0, 55.0), 1e-12);

            // §7.3 提前量算例：狰 6 m/s、25 m、飞行 0.45 s → 0.81 m
            double lead = Ballistics.Lead(6.0, 25.0, bow.MuzzleSpeed);
            Check("提前量（狰 6 m/s，25 m 角弓，原文 0.81 m）", lead, 0.81, 0.01);
            Check("提前量 = 速度 × 飞行 × 0.3（flight=0.45）",
                Ballistics.LeadFromFlight(6.0, 0.45), 0.81, 1e-12);
        }

        private static void CheckCharge()
        {
            Check("§7.2 角弓蓄力 0 s → 0.35", Ballistics.BowDamageCoef(0.0), 0.35, 1e-12);
            Check("§7.2 角弓蓄满 1.2 s → 1.00", Ballistics.BowDamageCoef(1.2), 1.00, 1e-12);
            Check("§7.2 角弓 0.6 s → 0.675", Ballistics.BowDamageCoef(0.6), 0.675, 1e-12);
            Check("§7.2 弹弓蓄满 0.6 s → 0.55", Ballistics.SlingDamageCoef(0.6), 0.55, 1e-12);
            Check("§7.2 弹弓 0 s → 0.20", Ballistics.SlingDamageCoef(0.0), 0.20, 1e-12);
            Check("§7.2 蹶张弩固定 1.00",
                Ballistics.ChargeDamageCoef(Ballistics.SpecByName("蹶张弩"), 0.0), 1.00, 1e-12);
            Check("§7.2 稳定窗（1.1 s）散布 −25%", Ballistics.SpreadMultiplier(1.1), 0.75, 1e-12);
            Check("§7.2 过拉 1.5 s 散布 +40%", Ballistics.SpreadMultiplier(1.5), 1.40, 1e-12);
            CheckTrue("§7.2 过拉 2.0 s 强制脱手", Ballistics.ForcesRelease(2.0), "2.0 s → 体力 −15");
        }

        private static void CheckRangedCriteria()
        {
            RangedSpec crossbow = Ballistics.SpecByName("蹶张弩");
            // 判据 2 原文：蹶张弩 = 3.2 + 0.6 + 0.2 = 4.0 s
            RangedCriteriaResult r25 = Ballistics.RangedWins(25.0, 6.0, crossbow, ArmorType.Soft, 90.0, 60.0, 0.9);
            Check("判据 2 出手窗口 3.2+0.6+0.2（原文 4.0 s）", r25.RequiredWindowSec, 4.0, 1e-12);
            Check("判据 2 狰 6 m/s @25 m 突进时间（原文 4.17 s）", r25.TimeToContactSec, 4.1667, 0.001);
            CheckTrue("判据 2 @25 m 成立（4.17 ≥ 4.0）", r25.TimeOk, "Tc=" + Fmt(r25.TimeToContactSec));

            RangedCriteriaResult r18 = Ballistics.RangedWins(18.0, 6.0, crossbow, ArmorType.Soft, 90.0, 60.0, 0.9);
            Check("判据 2 狰 @18 m 突进时间（原文 3.0 s）", r18.TimeToContactSec, 3.0, 0.001);
            CheckTrue("判据 2 @18 m 不成立（3.0 < 4.0）", !r18.TimeOk, "Tc=" + Fmt(r18.TimeToContactSec));

            // 判据 3 原文：刀 0.9 m 对 6 m/s 狰，接近率 6/(6+1.8) = 0.77，近战有效 DPS = 60×0.77
            Check("判据 3 接近率（原文 0.77）", r25.ApproachRate, 0.7692, 0.001);
            Check("判据 3 近战有效 DPS（原文 46.2）", r25.MeleeEffectiveDps, 46.2, 0.05);
            Check("判据 3 蹶张弩 25 m 期望 DPS（修正后原文 25.1，飞行 25/65≈0.385 s）",
                r25.RangedExpectedDps, 25.1, 0.1);
            CheckTrue("判据 3 对快目标远程不划算（这正是设计意图）",
                r25.RangedExpectedDps < r25.MeleeEffectiveDps,
                "远程 " + Fmt(r25.RangedExpectedDps) + " < 近战 " + Fmt(r25.MeleeEffectiveDps));

            // 角弓起手算例：抽箭 0.5 + 蓄力 1.2 + 飞行 0.45 = 2.15 s → 60 / 2.15 = 27.9
            RangedSpec bow = Ballistics.SpecByName("角弓");
            RangedCriteriaResult bow25 = Ballistics.RangedWins(25.0, 6.0, bow, ArmorType.Soft, 60.0, 60.0, 0.9);
            Check("起手窗口 DPS（角弓 25 m，原文 27.9）", bow25.RangedExpectedDps, 27.9, 0.1);

            // 判据 4：软甲/悬空 ×1.25，硬壳 ×0.6
            Check("判据 4 硬壳 ×0.6", Ballistics.TypeMultiplier(ArmorType.Hard), 0.6, 0.0);
            Check("判据 4 悬空 ×1.25", Ballistics.TypeMultiplier(ArmorType.Airborne), 1.25, 0.0);
            CheckTrue("判据 4 硬壳目标直接否决远程", !Ballistics.RangedWins(25.0, 6.0, crossbow, ArmorType.Hard, 90.0, 60.0, 0.9).TypeOk, "硬壳 → TypeOk=false");
            CheckTrue("判据 1 距离 <12 m 否决远程", !Ballistics.RangedWins(11.0, 6.0, crossbow, ArmorType.Soft, 90.0, 60.0, 0.9).DistanceOk, "11 m → DistanceOk=false");
        }

        // ---------------------------------------------------------------- §6.3

        private static void CheckWeapons(WeaponDef[] weapons)
        {
            Check("兵器条目数 = 18（§6.2）", weapons.Length, 18, 0);

            BudgetReport rep = WeaponBudget.Check(weapons);
            Check("参与 K / 支配自检件数 = 17（排除控制类「抓」）", rep.EvaluatedCount, 17, 0);
            Check("控制类件数 = 1（「抓」）", rep.ControlClassCount, 1, 0);
            Check("两两组合数 = C(17,2) = 136（§6.3 组合数口径）", rep.PairCount, 136, 0);

            Check("K 最小值（§6.3 实测 49.5）", rep.KMin, 49.49, 0.01);
            Check("K 最大值（§6.3 实测 68.2）", rep.KMax, 68.17, 0.02);
            Check("K 极差 ≈ 1.378（上限 1.5）", rep.KRatio, 1.378, 0.003);
            CheckTrue("全表 K 落在 §6.2 区间 49.5–68.5 内（0 条越界警告）", rep.Warnings.Count == 0,
                rep.Warnings.Count == 0 ? "49.5–68.5 全部命中" : string.Join("; ", rep.Warnings.ToArray()));

            List<string> mismatches = WeaponBudget.KTableMismatches(weapons);
            CheckTrue("§6.2 末列 K 值可由 K = 伤害×攻速×√长度 复算（0 处不一致）",
                mismatches.Count == 0, "不一致：" + mismatches.Count + " 处 " + string.Join("; ", mismatches.ToArray()));

            Check("四轴支配对 = 0（§6.3 第 3 步 / V8）", rep.DominancePairs.Count, 0, 0);
            if (rep.DominancePairs.Count > 0)
            {
                for (int i = 0; i < rep.DominancePairs.Count; i++)
                {
                    DominancePair p = rep.DominancePairs[i];
                    _failures.Add("支配对 " + p.Dominator + " → " + p.Dominated + "（触发轴 " + p.TriggerAxis
                        + "，最大比值 " + Fmt(p.Margin) + "）");
                }
            }

            PrintClosestPairs(rep, 3);
            CheckRivalPair(weapons, "槊", "刀",
                "长槊只在触及轴赢，短刀在另外三轴赢 → 互不支配（旧乘积口径会把长槊判死）");
            CheckRivalPair(weapons, "棍", "叉",
                "§6.3 的示范对：叉靠 DPS 赢一轴，棍靠覆盖与耐久赢两轴");
            PrintLegacyCaliberComparison(weapons);
        }

        /// <summary>打印最险的几对（四轴均 ≥ 但未过 1.15 倍）。</summary>
        private static void PrintClosestPairs(BudgetReport rep, int top)
        {
            Console.WriteLine(Indent + "最险的 " + top + " 对（四轴均 ≥ 但未过 1.15 倍，比值越接近 1.15 越危险）：");
            if (rep.NearMisses.Count == 0)
            {
                Console.WriteLine(Indent + "  （无：四轴口径下没有一对满足「全部不劣」，每对都在至少一轴上互有胜负）");
            }
            for (int i = 0; i < rep.NearMisses.Count && i < top; i++)
            {
                DominancePair p = rep.NearMisses[i];
                Console.WriteLine(Indent + "  " + p.Dominator + " → " + p.Dominated
                    + "  最大比值 " + Fmt(p.Margin)
                    + "（输出 " + Fmt(p.DpsA) + "/" + Fmt(p.DpsB)
                    + "，触及 " + Fmt(p.LenA) + "/" + Fmt(p.LenB)
                    + "，覆盖 " + Fmt(p.SweepA) + "/" + Fmt(p.SweepB)
                    + "，持续 " + Fmt(p.DurA) + "/" + Fmt(p.DurB) + "）");
            }
            if (rep.Warnings.Count > 0)
            {
                Console.WriteLine(Indent + "K 区间警告 " + rep.Warnings.Count + " 条：");
                for (int i = 0; i < rep.Warnings.Count; i++) Console.WriteLine(Indent + "  " + rep.Warnings[i]);
            }
        }

        /// <summary>逐轴打印一对兵器的对照，并断言两者互不支配。</summary>
        private static void CheckRivalPair(WeaponDef[] weapons, string nameA, string nameB, string why)
        {
            WeaponDef a = FindWeapon(weapons, nameA);
            WeaponDef b = FindWeapon(weapons, nameB);
            DominancePair ab = WeaponBudget.MakePair(a, b);
            DominancePair ba = WeaponBudget.MakePair(b, a);

            Console.WriteLine(Indent + nameA + " vs " + nameB + " 四轴逐轴对照（横扫角 "
                + Fmt(ab.SweepA) + "° vs " + Fmt(ab.SweepB) + "°）：");
            PrintAxisLine("输出 DPS", ab.DpsA, ab.DpsB, nameA, nameB);
            PrintAxisLine("触及长度", ab.LenA, ab.LenB, nameA, nameB);
            PrintAxisLine("横扫角  ", ab.SweepA, ab.SweepB, nameA, nameB);
            PrintAxisLine("耐久    ", ab.DurA, ab.DurB, nameA, nameB);
            Console.WriteLine(Indent + "  → " + why);

            CheckTrue(nameA + " 与 " + nameB + " 四轴上互不支配", !PairDominates(ab) && !PairDominates(ba),
                "两向支配判定均为 false");
        }

        private static void PrintAxisLine(string label, double va, double vb, string nameA, string nameB)
        {
            string who;
            double ratio;
            if (va > vb) { who = nameA + " +" + Fmt((va / vb - 1.0) * 100.0) + "%"; ratio = va / vb; }
            else if (vb > va) { who = nameB + " +" + Fmt((vb / va - 1.0) * 100.0) + "%"; ratio = vb / va; }
            else { who = "平"; ratio = 1.0; }
            Console.WriteLine(Indent + "  " + label + "  " + Fmt(va) + " / " + Fmt(vb)
                + "   谁赢：" + who + "（比值 " + Fmt(ratio) + "）");
        }

        private static bool PairDominates(DominancePair p)
        {
            string axis;
            double margin;
            return WeaponBudget.Dominates(p.DpsA, p.LenA, p.SweepA, p.DurA,
                                          p.DpsB, p.LenB, p.SweepB, p.DurB, out axis, out margin);
        }

        /// <summary>
        /// §6.3「归零三步工序」的对照打印：同一张表在两种尺子下的支配对数。
        /// 旧口径 = 长度 × 横扫角的乘积（已作废）；现行口径 = 四轴独立。
        /// </summary>
        private static void PrintLegacyCaliberComparison(WeaponDef[] current)
        {
            WeaponDef[] preRevision = LegacyTable();
            WeaponDef[] preFlat = LegacyTable();
            for (int i = 0; i < preFlat.Length; i++) preFlat[i].SweepDeg = 90.0;   // 一律 90°
            WeaponDef[] midRevision = MidRevisionTable();                           // 第一轮 5 处改动之后

            int c1 = WeaponBudget.LegacyProductPairs(preFlat).Count;
            int c2 = WeaponBudget.LegacyProductPairs(preRevision).Count;
            int c3 = WeaponBudget.LegacyProductPairs(current).Count;
            int c4 = WeaponBudget.Check(preRevision).DominancePairs.Count;
            int c5 = WeaponBudget.Check(midRevision).DominancePairs.Count;
            int c6 = WeaponBudget.Check(current).DominancePairs.Count;

            Console.WriteLine(Indent + "§6.3 归零工序对照（旧口径 = 长度×横扫角 的乘积；现行口径 = 四轴独立）：");
            Console.WriteLine(Indent + "  ① 旧口径 · 原数值 · 扫角一律 90°       → " + c1 + " 对");
            Console.WriteLine(Indent + "  ② 旧口径 · 原数值 · §6.2b 分档扫角     → " + c2 + " 对"
                + "（§6.3 定稿 32，实测一致）");
            Console.WriteLine(Indent + "  ③ 旧口径 · 现行数值 · 分档扫角         → " + c3 + " 对（§6.3 定稿 27，随现行数据走）");
            Console.WriteLine(Indent + "  ④ 四轴   · 原数值 · 分档扫角           → " + c4 + " 对（§6.3 第 1 步，主方案列 8 对）");
            Console.WriteLine(Indent + "  ⑤ 四轴   · 第一轮 5 处改动后           → " + c5 + " 对（§6.3 第 2 步冒出的新对）");
            Console.WriteLine(Indent + "  ⑥ 四轴   · 第二轮 6 处（现行数值）      → " + c6 + " 对（§6.3 第 3 步 = 归零）");

            Check("① 旧口径·原数值·一律 90° 的支配对数（主方案 §6.3 写 7）", c1, 7, 0);
            Check("④ 四轴·原数值 = 8 对（§6.3 第 1 步）", c4, 8, 0);
            Check("⑤ 四轴·第一轮 5 处后 = 1 对（§6.3 第 2 步）", c5, 1, 0);
            Check("⑥ 四轴·现行数值 = 0 对（§6.3 第 3 步 = 归零）", c6, 0, 0);

            // §6.3 定稿已把「33 对」改成「32 对」（作者确认为转写错误：脚本一直打印 32）。
            // 六行对照自此全部是回归基线：7 / 32 / 27 / 8 / 1 / 0。
            Check("② 旧口径·原数值·分档扫角 = 32 对（§6.3 定稿）", c2, 32, 0);
            Check("③ 旧口径·现行数值·分档扫角 = 27 对（§6.3 定稿；此数随现行数据走，改表须重校）", c3, 27, 0);
            CheckTrue("换尺子后支配对数一律下降（旧口径 " + c2 + "→四轴 " + c4 + "；现行 " + c3 + "→" + c6 + "）",
                c2 > c4 && c3 > c6 && c3 <= c2, "度量修对了，不是数字凑齐了");

            List<string> step1 = PairNames(WeaponBudget.Check(preRevision).DominancePairs);
            string[] expected1 = new string[] { "刀→斧", "剑→斧", "钩→斧", "刀→钺", "剑→钺", "钩→钺", "棍→叉", "棍→棒" };
            Console.WriteLine(Indent + "  第 1 步 8 对实测：" + string.Join("、", step1.ToArray()));
            Check("§6.3 第 1 步的 8 对与主方案列表逐对一致", CountSame(step1, expected1), 8, 0);

            List<string> step2 = PairNames(WeaponBudget.Check(midRevision).DominancePairs);
            Console.WriteLine(Indent + "  第 2 步新对实测：" + string.Join("、", step2.ToArray())
                + "（主方案：第一轮把棒升到 DPS 49.4 后冒出「棒 → 锏」；第二轮补锏耐久 265 才消掉）");
            Check("§6.3 第 2 步冒出的新对 = 棒→锏（与主方案一致）", CountSame(step2, new string[] { "棒→锏" }), 1, 0);
        }

        private static List<string> PairNames(List<DominancePair> pairs)
        {
            List<string> names = new List<string>();
            for (int i = 0; i < pairs.Count; i++) names.Add(pairs[i].Dominator + "→" + pairs[i].Dominated);
            return names;
        }

        private static int CountSame(List<string> actual, string[] expected)
        {
            int n = 0;
            for (int i = 0; i < expected.Length; i++) if (actual.Contains(expected[i])) n++;
            return n;
        }

        /// <summary>§6.2b 六档横扫角（与数值一起冻结，保证历史对照不受日后调参影响）。</summary>
        private static double[] FrozenSweeps()
        {
            return new double[] { 90, 25, 70, 50, 35, 55, 100, 70, 60, 130, 20, 110, 90, 80, 30, 120, 180 };
        }

        private static string[] FrozenNames()
        {
            return new string[] { "刀", "枪", "剑", "戟", "斧", "钺", "钩", "叉", "镋", "棍", "槊", "棒", "鞭", "锏", "锤", "拐", "流星" };
        }

        /// <summary>
        /// §6.3 的历史快照：返工前的 §6.2 数值（剑 240 / 斧 200 / 钺 0.85 / 叉 30 / 棒 34+280 / 锏 250）。
        /// 只用于复现主方案 §6.3 的 7 / 33 / 27 / 8 对照，不参与现行数值的任何验收。
        /// </summary>
        private static WeaponDef[] LegacyTable()
        {
            double[,] nums =
            {
                { 30, 2.00, 0.90, 220 }, { 40, 0.95, 2.60, 260 }, { 28, 2.10, 0.95, 240 },
                { 46, 0.85, 2.40, 230 }, { 64, 0.90, 0.80, 200 }, { 68, 0.80, 0.85, 190 },
                { 32, 1.80, 1.00, 210 }, { 30, 1.40, 1.70, 240 }, { 48, 0.90, 2.00, 220 },
                { 24, 1.90, 1.90, 300 }, { 50, 0.75, 2.80, 200 }, { 34, 1.30, 1.30, 280 },
                { 44, 1.10, 1.10, 260 }, { 42, 1.15, 1.05, 250 }, { 78, 0.80, 0.65, 180 },
                { 38, 1.80, 0.60, 220 }, { 44, 1.00, 2.40, 160 }
            };
            return BuildFrozen(nums);
        }

        /// <summary>
        /// §6.3 第 2 步之后的快照：第一轮 5 处改动（剑 210 / 斧 230 / 钺 1.05+55° / 叉 34 / 棒 38+250）已生效，
        /// 但还没有第二轮那次「锏 耐久 250 → 265」。用它复现「棒 → 锏」这对新支配。
        /// </summary>
        private static WeaponDef[] MidRevisionTable()
        {
            double[,] nums =
            {
                { 30, 2.00, 0.90, 220 }, { 40, 0.95, 2.60, 260 }, { 28, 2.10, 0.95, 210 },
                { 46, 0.85, 2.40, 230 }, { 64, 0.90, 0.80, 230 }, { 68, 0.80, 1.05, 190 },
                { 32, 1.80, 1.00, 210 }, { 34, 1.40, 1.70, 240 }, { 48, 0.90, 2.00, 220 },
                { 24, 1.90, 1.90, 300 }, { 50, 0.75, 2.80, 200 }, { 38, 1.30, 1.30, 250 },
                { 44, 1.10, 1.10, 260 }, { 42, 1.15, 1.05, 250 }, { 78, 0.80, 0.65, 180 },
                { 38, 1.80, 0.60, 220 }, { 44, 1.00, 2.40, 160 }
            };
            return BuildFrozen(nums);
        }

        private static WeaponDef[] BuildFrozen(double[,] nums)
        {
            string[] names = FrozenNames();
            double[] sweeps = FrozenSweeps();
            WeaponDef[] list = new WeaponDef[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                WeaponDef w = new WeaponDef();
                w.Name = names[i];
                w.Dmg = nums[i, 0];
                w.AtkPerSec = nums[i, 1];
                w.LengthM = nums[i, 2];
                w.DurabilityHits = (int)nums[i, 3];
                w.SweepDeg = sweeps[i];
                w.IsControlClass = false;
                list[i] = w;
            }
            return list;
        }

        // ---------------------------------------------------------------- §8

        private static void CheckConsumables()
        {
            Check("§8.2 符箓携带上限 9 张", Talisman.CarriedMaxDefault, 9, 0);
            Check("§8.2 符箓升级上限 12 张", Talisman.CarriedMaxUpgraded, 12, 0);
            Check("§8.2 使用前摇 0.8 s", Talisman.CastSec, 0.8, 0.0);
            Check("§8.1 F1 效果 ≤ 6 s", Talisman.F1MaxDurationSec, 6.0, 0.0);
            Check("§8.1 F4 雨天 50% 失效", Talisman.RainFailChance, 0.50, 0.0);
            CheckTrue("§8.1 F4 风力 5 级不失效、6 级失效",
                !Talisman.FailsInWind(5) && Talisman.FailsInWind(6), "5 → false, 6 → true");

            Check("§8.3 布阵 2.5 s", Formation.BuildSec, 2.5, 0.0);
            Check("§8.3 阵法耐久区间上限 120", Formation.DurabilityMax, 120.0, 0.0);

            Check("§8.4 丹药携带上限 6 枚", Pill.CarriedMax, 6, 0);
            Check("§8.4 服食 1.2 s", Pill.EatSec, 1.2, 0.0);
            Check("§8.4 耐受：第 1 枚 ×1.0", Pill.EffectScaleAt(1), 1.0, 1e-12);
            Check("§8.4 耐受：第 2 枚 ×0.6", Pill.EffectScaleAt(2), 0.6, 1e-12);
            Check("§8.4 耐受：第 3 枚 ×0.36", Pill.EffectScaleAt(3), 0.36, 1e-12);
            CheckTrue("§8.4 毒性 ≥3 触发中毒", Pill.IsPoisoned(3.0) && !Pill.IsPoisoned(2.9), "3.0 → true, 2.9 → false");
            Check("§8.4 中毒全属性 ×0.7", Pill.StatMultiplier(3.0), 0.7, 1e-12);
        }

        // ---------------------------------------------------------------- 数据

        private static void CheckData(WeaponTableFile weapons, YaoTableFile yao, ItemTableFile items)
        {
            Check("妖条目数 = 14（§9）", yao.Yao.Length, 14, 0);
            Check("符箓 = 6（§8.2）", CountCategory(items.Items, "符箓"), 6, 0);
            Check("阵法 = 4（§8.3）", CountCategory(items.Items, "阵法"), 4, 0);
            Check("丹药 = 6（§8.4）", CountCategory(items.Items, "丹药"), 6, 0);

            YaoDef xing = FindYao(yao.Yao, "狌狌");
            Check("狌狌 B = 4.2", xing.B, 4.2, 1e-9);
            Check("狌狌 D_breed = 0.30", xing.DBreed, 0.30, 1e-9);
            Check("狌狌 初始惊扰 = 20（§4.5）", xing.InitialAgitation, 20.0, 0.0);

            YaoDef zheng = FindYao(yao.Yao, "狰");
            Check("狰 B = 9.5", zheng.B, 9.5, 1e-9);
            Check("狰 D_breed = 0.60", zheng.DBreed, 0.60, 1e-9);

            YaoDef fox = FindYao(yao.Yao, "九尾狐");
            Check("九尾狐 B = 13.0", fox.B, 13.0, 1e-9);
            Check("九尾狐 初始惊扰 = 70（§4.5）", fox.InitialAgitation, 70.0, 0.0);

            WeaponDef spear = FindWeapon(weapons.Weapons, "枪");
            Check("枪 器值 = 15（参与 CPT）", spear.Qi, 15, 0);
            Check("枪 长度 2.60 m", spear.LengthM, 2.60, 1e-9);

            int missingAgitation = 0;
            for (int i = 0; i < yao.Yao.Length; i++) if (yao.Yao[i].InitialAgitation < 0.0) missingAgitation++;
            Check("§9 初始惊扰度 14 种全部给定（0 个未给）", missingAgitation, 0, 0);
            Check("狰 初始惊扰 = 45（§9 表）", zheng.InitialAgitation, 45.0, 0.0);
            Check("穷奇 初始惊扰 = 85（§9 表，全场最高）", FindYao(yao.Yao, "穷奇").InitialAgitation, 85.0, 0.0);
            Check("鹿蜀 初始惊扰 = 15（§9 表，全场最低）", FindYao(yao.Yao, "鹿蜀").InitialAgitation, 15.0, 0.0);
            CheckTrue("体型标注齐备（小/中/大）", FindYao(yao.Yao, "穷奇").SizeClass == "大"
                && FindYao(yao.Yao, "狌狌").SizeClass == "小", "穷奇=大、狌狌=小");

            // §9 明令「强行反噬风险」只出等级、不出概率
            CheckText("狌狌 反噬风险等级 = Low", FindYao(yao.Yao, "狌狌").BacklashRiskLevel, "Low");
            CheckText("狰 反噬风险等级 = High", FindYao(yao.Yao, "狰").BacklashRiskLevel, "High");
            CheckText("九尾狐 反噬风险等级 = Extreme", FindYao(yao.Yao, "九尾狐").BacklashRiskLevel, "Extreme");
            int percentFields = 0;
            for (int i = 0; i < yao.Yao.Length; i++)
            {
                if ((yao.Yao[i].BacklashRiskLabel ?? string.Empty).IndexOf('%') >= 0) percentFields++;
                if ((yao.Yao[i].BacklashRiskLevel ?? string.Empty).IndexOf('%') >= 0) percentFields++;
            }
            Check("图鉴里不得出现百分比（§9：本表不出概率）", percentFields, 0, 0);
            int levelOk = 0;
            for (int i = 0; i < yao.Yao.Length; i++)
            {
                string lv = yao.Yao[i].BacklashRiskLevel;
                if (lv == "Low" || lv == "Medium" || lv == "High" || lv == "Extreme") levelOk++;
            }
            Check("14 种妖的反噬风险等级全部落在 Low/Medium/High/Extreme", levelOk, 14, 0);
        }

        // ---------------------------------------------------------------- 工具

        private static int CountCategory(ItemDef[] items, string category)
        {
            int n = 0;
            for (int i = 0; i < items.Length; i++) if (items[i].Category == category) n++;
            return n;
        }

        private static YaoDef FindYao(YaoDef[] all, string name)
        {
            for (int i = 0; i < all.Length; i++) if (all[i].Name == name) return all[i];
            throw new InvalidOperationException("数据缺条目：" + name);
        }

        private static WeaponDef FindWeapon(WeaponDef[] all, string name)
        {
            for (int i = 0; i < all.Length; i++) if (all[i].Name == name) return all[i];
            throw new InvalidOperationException("数据缺条目：" + name);
        }

        private const string Indent = "       ";

        private static T Load<T>(string fileName) where T : class
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "Data");
            string path = Path.Combine(dir, fileName);
            if (!File.Exists(path))
            {
                Console.WriteLine("[FAIL] 数据文件缺失：" + path);
                _fail++;
                _failures.Add("数据文件缺失：" + path);
                return null;
            }
            JsonSerializerOptions opts = new JsonSerializerOptions();
            opts.IncludeFields = true;
            opts.PropertyNameCaseInsensitive = true;
            opts.AllowTrailingCommas = true;
            opts.ReadCommentHandling = JsonCommentHandling.Skip;
            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8), opts);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FAIL] 解析 " + fileName + " 失败：" + ex.Message);
                _fail++;
                _failures.Add("解析 " + fileName + " 失败：" + ex.Message);
                return null;
            }
        }

        private static string Fmt(double v)
        {
            if (double.IsNaN(v)) return "NaN";
            if (double.IsInfinity(v)) return "Infinity";
            return v.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static void Section(string title)
        {
            Console.WriteLine("--- " + title + " ---");
        }

        private static void Check(string name, double actual, double expected, double tolerance)
        {
            double diff = Math.Abs(actual - expected);
            bool ok = diff <= tolerance + 1e-12;
            if (ok)
            {
                _pass++;
                Console.WriteLine("[PASS] " + name + "：实测=" + Fmt(actual) + " 期望=" + Fmt(expected)
                    + " 差=" + diff.ToString("0.######", CultureInfo.InvariantCulture));
            }
            else
            {
                _fail++;
                string msg = name + "：实测=" + Fmt(actual) + " 期望=" + Fmt(expected)
                    + " 差=" + Fmt(diff) + " 容差=" + Fmt(tolerance);
                Console.WriteLine("[FAIL] " + msg);
                _failures.Add(msg);
            }
        }

        private static void CheckTrue(string name, bool condition, string detail)
        {
            if (condition)
            {
                _pass++;
                Console.WriteLine("[PASS] " + name + "：" + detail);
            }
            else
            {
                _fail++;
                string msg = name + "：" + detail;
                Console.WriteLine("[FAIL] " + msg);
                _failures.Add(msg);
            }
        }

        private static void CheckText(string name, string actual, string expected)
        {
            bool ok = actual == expected;
            if (ok)
            {
                _pass++;
                Console.WriteLine("[PASS] " + name + "：实测=" + actual);
            }
            else
            {
                _fail++;
                string msg = name + "：实测=" + actual + " 期望=" + expected;
                Console.WriteLine("[FAIL] " + msg);
                _failures.Add(msg);
            }
        }
    }
}
