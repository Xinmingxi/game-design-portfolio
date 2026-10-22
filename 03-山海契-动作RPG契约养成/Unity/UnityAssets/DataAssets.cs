using UnityEngine;
using ShanHaiQi.Stats.Data;

namespace ShanHaiQi.UnityAssets
{
    /// <summary>
    /// ScriptableObject 外壳：把 Stats/Data 里的纯数据类包成可以在 Unity 里点选、拖引用的资产。
    /// 这是整个工程里唯一必须 using UnityEngine 的数据文件——
    /// ScriptableObject 本身就在 UnityEngine 里，所以它与零依赖的 Stats/ 分目录存放。
    /// </summary>
    [CreateAssetMenu(menuName = "山海契/兵器表", fileName = "WeaponTable")]
    public class WeaponTableAsset : ScriptableObject
    {
        public WeaponDef[] Weapons = new WeaponDef[0];

        /// <summary>从 Stats/Data/Weapons.json 导入（把 JSON 当 TextAsset 拖进来，右键执行本方法）。</summary>
        [ContextMenu("从 JSON 导入（需先在 Inspector 指定 json）")]
        public void ImportFromJson(TextAsset json)
        {
            if (json == null) { Debug.LogError("没有指定 JSON"); return; }
            WeaponTableFile file = JsonUtility.FromJson<WeaponTableFile>(json.text);
            Weapons = file.Weapons;
            Debug.Log("导入兵器 " + Weapons.Length + " 件");
        }
    }

    [CreateAssetMenu(menuName = "山海契/妖图鉴", fileName = "YaoTable")]
    public class YaoTableAsset : ScriptableObject
    {
        public YaoDef[] Yao = new YaoDef[0];

        public void ImportFromJson(TextAsset json)
        {
            if (json == null) { Debug.LogError("没有指定 JSON"); return; }
            YaoTableFile file = JsonUtility.FromJson<YaoTableFile>(json.text);
            Yao = file.Yao;
            Debug.Log("导入妖 " + Yao.Length + " 种");
        }
    }

    [CreateAssetMenu(menuName = "山海契/消耗品表", fileName = "ItemTable")]
    public class ItemTableAsset : ScriptableObject
    {
        public ItemDef[] Items = new ItemDef[0];

        public void ImportFromJson(TextAsset json)
        {
            if (json == null) { Debug.LogError("没有指定 JSON"); return; }
            ItemTableFile file = JsonUtility.FromJson<ItemTableFile>(json.text);
            Items = file.Items;
            Debug.Log("导入消耗品 " + Items.Length + " 条");
        }
    }
}
