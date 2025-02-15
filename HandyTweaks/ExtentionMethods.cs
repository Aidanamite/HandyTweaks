using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Net;
using UnityEngine;

namespace HandyTweaks
{
    static class ExtentionMethods
    {
        static MethodInfo _IsCropPlaced = typeof(FarmSlot).GetMethod("IsCropPlaced", ~BindingFlags.Default);
        public static bool IsCropPlaced(this FarmSlot item) => (bool)_IsCropPlaced.Invoke(item, new object[0]);
        static MethodInfo _OnContextAction = typeof(MyRoomItem).GetMethod("OnContextAction", ~BindingFlags.Default);
        public static void OnContextAction(this MyRoomItem item, string actionName) => _OnContextAction.Invoke(item, new[] { actionName });
        static MethodInfo _IsCurrentStageFeedConsumed = typeof(AnimalFarmItem).GetMethod("IsCurrentStageFeedConsumed", ~BindingFlags.Default);
        public static bool IsCurrentStageFeedConsumed(this AnimalFarmItem item) => (bool)_IsCurrentStageFeedConsumed.Invoke(item, new object[0]);
        static MethodInfo _ConsumeFeed = typeof(AnimalFarmItem).GetMethod("ConsumeFeed", ~BindingFlags.Default);
        public static void ConsumeFeed(this AnimalFarmItem item) => _ConsumeFeed.Invoke(item, new object[0]);
        static FieldInfo _mCurrentUsedConsumableCriteria = typeof(ComposterFarmItem).GetField("mCurrentUsedConsumableCriteria", ~BindingFlags.Default);
        public static void SetCurrentUsedConsumableCriteria(this ComposterFarmItem item, ItemStateCriteriaConsumable consumable) => _mCurrentUsedConsumableCriteria.SetValue(item, consumable);
        static FieldInfo _mCurrentUsedConsumableCriteria2 = typeof(FishTrapFarmItem).GetField("mCurrentUsedConsumableCriteria", ~BindingFlags.Default);
        public static void SetCurrentUsedConsumableCriteria(this FishTrapFarmItem item, ItemStateCriteriaConsumable consumable) => _mCurrentUsedConsumableCriteria2.SetValue(item, consumable);
        static MethodInfo _GetSpeedupCost = typeof(FarmItem).GetMethod("GetSpeedupCost", ~BindingFlags.Default);
        public static int GetSpeedupCost(this FarmItem item) => (int)_GetSpeedupCost.Invoke(item, new object[0]);
        static MethodInfo _CheckGemsAvailable = typeof(FarmItem).GetMethod("CheckGemsAvailable", ~BindingFlags.Default);
        public static bool CheckGemsAvailable(this FarmItem item, int count) => (bool)_CheckGemsAvailable.Invoke(item, new object[] { count });
        static FieldInfo _mIsWaitingForWsCall = typeof(FarmItem).GetField("mIsWaitingForWsCall", ~BindingFlags.Default);
        public static bool IsWaitingForWsCall(this FarmItem item) => (bool)_mIsWaitingForWsCall.GetValue(item);
        static MethodInfo _SaveAndExitQuiz = typeof(UiQuizPopupDB).GetMethod("SaveAndExitQuiz", ~BindingFlags.Default);
        public static void SaveAndExitQuiz(this UiQuizPopupDB item) => _SaveAndExitQuiz.Invoke(item, new object[0]);
        static MethodInfo _CreateDragonWiget = typeof(UiStableQuestDragonsMenu).GetMethod("CreateDragonWiget", ~BindingFlags.Default);
        public static void CreateDragonWiget(this UiStableQuestDragonsMenu menu, RaisedPetData rpData) => _CreateDragonWiget.Invoke(menu, new object[] { rpData });
        static MethodInfo _ShowStatInfo = typeof(UiStatsCompareMenu).GetMethod("ShowStatInfo", ~BindingFlags.Default);
        public static void ShowStatInfo(this UiStatsCompareMenu instance, KAWidget widget, string baseStat, string statName, string compareStat, string diffVal, StatCompareResult compareResult = StatCompareResult.Equal, bool showCompare = false) =>
            _ShowStatInfo.Invoke(instance, new object[] { widget, baseStat, statName, compareStat, diffVal, (int)compareResult, showCompare });
        static FieldInfo _mModifierFieldMap = typeof(AvAvatarController).GetField("mModifierFieldMap", ~BindingFlags.Default);
        public static bool TryGetAttributeField(this string att, out string fieldName)
        {
            if (att != null && _mModifierFieldMap.GetValue(null) is Dictionary<string, string> d)
                return d.TryGetValue(att, out fieldName);
            fieldName = null;
            return false;
        }
        public static string GetAttributeField(this string att) => att.TryGetAttributeField(out var f) ? f : null;
        static FieldInfo _mContentMenuCombat = typeof(UiStatPopUp).GetField("mContentMenuCombat", ~BindingFlags.Default);
        public static KAUIMenu GetContentMenuCombat(this UiStatPopUp item) => (KAUIMenu)_mContentMenuCombat.GetValue(item);
        static FieldInfo _mInventory = typeof(CommonInventoryData).GetField("mInventory", ~BindingFlags.Default);
        public static Dictionary<int, List<UserItemData>> FullInventory(this CommonInventoryData inv) => (Dictionary<int, List<UserItemData>>)_mInventory.GetValue(inv);
        static FieldInfo _mCachedItemData = typeof(KAUIStoreChooseMenu).GetField("mCachedItemData", ~BindingFlags.Default);
        public static Dictionary<ItemData, int> GetCached(this KAUIStoreChooseMenu menu) => (Dictionary<ItemData, int>)_mCachedItemData.GetValue(menu);
        static MethodInfo _RemoveDragonSkin = typeof(UiDragonCustomization).GetMethod("RemoveDragonSkin", ~BindingFlags.Default);
        public static void RemoveDragonSkin(this UiDragonCustomization menu) => _RemoveDragonSkin.Invoke(menu, new object[0]);

        public static string ReadContent(this WebResponse response, Encoding encoding = null)
        {
            using (var stream = response.GetResponseStream())
            {
                var b = new byte[stream.Length];
                stream.Read(b, 0, b.Length);
                return (encoding ?? Encoding.UTF8).GetString(b);
            }
        }
        public static string GetJsonEntry(this WebResponse response, string key, Encoding encoding = null)
        {
            using (var stream = response.GetResponseStream())
            {
                var reader = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(stream, new System.Xml.XmlDictionaryReaderQuotas() {  });
                while (reader.Name != key && reader.Read())
                { }
                if (reader.Name == key && reader.Read())
                    return reader.Value;
                return null;
            }
        }

        public static bool IsRankLocked(this ItemData data, out int rid, int rankType)
        {
            rid = 0;
            if (data.RewardTypeID > 0)
                rankType = data.RewardTypeID;
            if (data.Points != null && data.Points.Value > 0)
            {
                rid = data.Points.Value;
                UserAchievementInfo userAchievementInfoByType = UserRankData.GetUserAchievementInfoByType(rankType);
                return userAchievementInfoByType == null || userAchievementInfoByType.AchievementPointTotal == null || rid > userAchievementInfoByType.AchievementPointTotal.Value;
            }
            if (data.RankId != null && data.RankId.Value > 0)
            {
                rid = data.RankId.Value;
                UserRank userRank = (rankType == 8) ? PetRankData.GetUserRank(SanctuaryManager.pCurPetData) : UserRankData.GetUserRankByType(rankType);
                return userRank == null || rid > userRank.RankID;
            }
            return false;
        }

        public static bool HasPrereqItem(this ItemData data)
        {
            if (data.Relationship == null)
                return true;
            ItemDataRelationship[] relationship = data.Relationship;
            foreach (var itemDataRelationship in data.Relationship)
                if (itemDataRelationship.Type == "Prereq")
                    return (ParentData.pIsReady && ParentData.pInstance.HasItem(itemDataRelationship.ItemId)) || CommonInventoryData.pInstance.FindItem(itemDataRelationship.ItemId) != null;
            return true;
        }
        public static Dictionary<string, Color> colorPresets = typeof(Color).GetProperties().Where(x => x.PropertyType == x.DeclaringType && x.GetGetMethod(false)?.IsStatic == true).ToDictionary(x => x.Name.ToLowerInvariant(),x => (Color)x.GetValue(null));
        public static bool TryParseColor(this string clr,out Color color)
        {
            clr = clr.ToLowerInvariant();
            color = default;
            if (colorPresets.TryGetValue(clr,out var v))
                color = v;
            else if (uint.TryParse(clr,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out var n))
                color = new Color32((byte)(n / 0x10000 & 0xFF), (byte)(n / 0x100 & 0xFF), (byte)(n & 0xFF), 255);
            else
                return false;
            return true;
        }
        public static string ToHex(this Color32 color) => color.r.ToString("X2") + color.g.ToString("X2") + color.b.ToString("X2");
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToHex(this Color color) => ((Color32)color).ToHex();
        public static Color Shift(this Color oc, Color nc)
        {
            var s = Math.Max(Math.Max(oc[0], oc[1]), oc[2]);
            return new Color(nc.r * s, nc.g * s, nc.b * s, nc.a * oc.a);
        }
        public static ParticleSystem.MinMaxGradient Shift(this ParticleSystem.MinMaxGradient o, Color newColor)
        {
            if (o.mode == ParticleSystemGradientMode.Color)
                return new ParticleSystem.MinMaxGradient(o.color.Shift(newColor));

            if (o.mode == ParticleSystemGradientMode.Gradient)
                return new ParticleSystem.MinMaxGradient(new Gradient()
                {
                    mode = o.gradient.mode,
                    alphaKeys = o.gradient.alphaKeys,
                    colorKeys = o.gradient.colorKeys.Select(x => new GradientColorKey(x.color.Shift(newColor), x.time)).ToArray()
                });

            if (o.mode == ParticleSystemGradientMode.TwoColors)
                return new ParticleSystem.MinMaxGradient(o.colorMin.Shift(newColor), o.colorMax.Shift(newColor));

            if (o.mode == ParticleSystemGradientMode.TwoGradients)
                return new ParticleSystem.MinMaxGradient(new Gradient()
                {
                    mode = o.gradientMin.mode,
                    alphaKeys = o.gradientMin.alphaKeys,
                    colorKeys = o.gradientMin.colorKeys.Select(x => new GradientColorKey(x.color.Shift(newColor), x.time)).ToArray()
                }, new Gradient()
                {
                    mode = o.gradientMax.mode,
                    alphaKeys = o.gradientMax.alphaKeys,
                    colorKeys = o.gradientMax.colorKeys.Select(x => new GradientColorKey(x.color.Shift(newColor), x.time)).ToArray()
                });

            return o;
        }
        public static List<T> GetRandom<T>(this ICollection<T> c, int count)
        {
            var r = new System.Random();
            var n = c.Count;
            if (count >= n)
                return c.ToList();
            var l = new List<T>(count);
            if (count > 0)
                foreach (var i in c)
                    if (r.Next(n--) < count)
                    {
                        count--;
                        l.Add(i);
                        if (count == 0)
                            break;
                    }
            return l;
        }
        public static Vector2 Rotate(this Vector2 v, float delta, Vector2 center = default)
        {
            if (center != default)
                v -= center;
            v = new Vector2(
                v.x * Mathf.Cos(delta) - v.y * Mathf.Sin(delta),
                v.x * Mathf.Sin(delta) + v.y * Mathf.Cos(delta)
            );
            if (center != default)
                v += center;
            return v;
        }
        public static bool TryParseColor(this string[] values, out Color result, int start = 0)
        {
            if (values != null
                && values.Length >= start + 3
                && values[start] != null && int.TryParse(values[start], out var r)
                && values[start + 1] != null && int.TryParse(values[start + 1], out var g)
                && values[start + 2] != null && int.TryParse(values[start + 2], out var b))
            {
                result = new Color(r / 255f, g / 255f, b / 255f);
                return true;
            }
            result = default;
            return false;
        }
        public static string JoinValues(this Color c, string delimeter = "$") => (int)Math.Round(c.r * 255.0) + delimeter + (int)Math.Round(c.g * 255.0) + delimeter + (int)Math.Round(c.b * 255.0);
        public static T GetOrAddComponent<T>(this GameObject g) where T : Component => g.GetComponent<T>() ?? g.AddComponent<T>();
        public static Y GetValueOrDefault<X, Y>(this IReadOnlyDictionary<X, Y> d, X key) => d.TryGetValue(key, out var value) ? value : default;

        public static T GetSafe<T>(this IList<T> l, int index, T fallback = default)
        {
            if (index < 0 || index > l.Count)
                return fallback;
            return l[index];
        }
    }
}