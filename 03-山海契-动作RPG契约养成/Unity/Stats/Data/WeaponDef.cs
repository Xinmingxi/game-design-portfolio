using System;

namespace ShanHaiQi.Stats.Data
{
    /// <summary>
    /// 一件近战兵器的全部数值（主方案 §6.2 表的字段，一一对应）。
    /// 纯数据类：不引用任何 Unity 引擎类型，JsonUtility 与 System.Text.Json 都能直接反序列化。
    /// </summary>
    [Serializable]
    public class WeaponDef
    {
        public string Name;          // 兵器名
        public double Dmg;           // 单次命中基础伤害（对无甲目标）
        public double AtkPerSec;     // 攻速（次/秒）
        public double LengthM;       // 有效触及（m）
        public int DurabilityHits;   // 耐久（命中次数上限）
        public int Qi;               // 器值 1–20，参与 §4.4 的 CPT
        public double KTable;        // 主方案 §6.2 末列给出的 K 值（回归校验用）
        public double SweepDeg;      // 横扫角（度）——见下方 SweepDeg 说明
        public bool IsControlClass;  // true = 控制类（仅「抓」），不计 K、不参与支配自检
        public string Note;          // 口径备注

        // SweepDeg = §6.2 的「横扫角（度）[估算]」列（§6.2b 六档：纯刺 20–25 / 短促集中 30–45 /
        // 中幅 50–70 / 横扫 80–100 / 抡扫 110–130 / 全域 180）。它是 §6.3 四轴自检里的独立「覆盖轴」，
        // 不再与长度相乘（那个旧口径已被主方案否决）。控制类「抓」无横扫动作，记 -1 且不参与比较。
    }

    /// <summary>Weapons.json 的根对象（JsonUtility 不支持根数组，故包一层）。</summary>
    [Serializable]
    public class WeaponTableFile
    {
        public WeaponDef[] Weapons = new WeaponDef[0];
    }
}
