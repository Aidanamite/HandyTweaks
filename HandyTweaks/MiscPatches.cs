using BepInEx;
using ConfigTweaks;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using System.Runtime.CompilerServices;
using UnityEngine.Assertions.Must;

namespace HandyTweaks
{
    [HarmonyPatch(typeof(UiMyRoomBuilder), "Update")]
    static class Patch_RoomBuilder
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].opcode == OpCodes.Stfld && code[i].operand is FieldInfo f && f.Name == "mCanPlace")
                    code.Insert(i, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_RoomBuilder), nameof(EditCanPlace))));
            return code;
        }
        static bool EditCanPlace(bool original) => original || Main.CanPlaceAnywhere;
    }

    [HarmonyPatch(typeof(UiQuizPopupDB))]
    static class Patch_InstantAnswer
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        static void Start(UiQuizPopupDB __instance, ref bool ___mIsQuestionAttemped, ref bool ___mCheckForTaskCompletion)
        {
            if (Main.SkipTrivia)
            {
                ___mCheckForTaskCompletion = true;
                ___mIsQuestionAttemped = true;
                __instance._MessageObject.SendMessage(__instance._QuizAnsweredMessage, true, SendMessageOptions.DontRequireReceiver);
                __instance.SaveAndExitQuiz();
            }
        }
        [HarmonyPatch("IsQuizAnsweredCorrect")]
        [HarmonyPostfix]
        static void IsQuizAnsweredCorrect(ref bool __result)
        {
            if (Main.SkipTrivia)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch]
    static class Patch_ApplyTexture
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(AvatarData), "SetStyleTexture", new[] { typeof(AvatarData.InstanceInfo), typeof(string), typeof(string), typeof(int) });
            yield return AccessTools.Method(typeof(CustomAvatarState), "SetPartTexture");
            yield return AccessTools.Method(typeof(CustomAvatarState), "SetTextureData");
            yield return AccessTools.Method(typeof(UiAvatarCustomizationMenu), "SetPartTextureByIndex");
            yield return AccessTools.Method(typeof(UiAvatarCustomizationMenu), "UpdatePartTexture");
        }
        static bool Prefix() => !Input.GetKey(Main.DontApplyTextures);
    }

    [HarmonyPatch(typeof(AvatarData), "SetGeometry", typeof(AvatarData.InstanceInfo), typeof(string), typeof(string), typeof(int))]
    static class Patch_ApplyGeometry
    {
        static bool Prefix() => !Input.GetKey(Main.DontApplyGeometry);
    }

    [HarmonyPatch(typeof(UiStableQuestDragonsMenu), "LoadDragonsList")]
    static class Patch_LoadStableQuestDragonsList
    {
        static bool Prefix(UiStableQuestDragonsMenu __instance)
        {
            if (!Main.SortStableQuestDragonsByValue)
                return true;
            __instance.ClearItems();

            var l = new SortedSet<(float, RaisedPetData)>(new ComparePetValue());
            if (RaisedPetData.pActivePets != null)
                foreach (RaisedPetData[] array in RaisedPetData.pActivePets.Values)
                    if (array != null)
                        foreach (RaisedPetData pet in array)
                            if (StableData.GetByPetID(pet.RaisedPetID) != null && pet.pStage >= RaisedPetStage.BABY && pet.IsPetCustomized())
                                l.Add((TimedMissionManager.pInstance.GetWinProbabilityForPet(UiStableQuestMain.pInstance._StableQuestDetailsUI.pCurrentMissionData, pet.RaisedPetID), pet));
            foreach (var p in l)
                __instance.CreateDragonWiget(p.Item2);
            __instance.pMenuGrid.repositionNow = true;
            return false;
        }
    }

    [HarmonyPatch]
    static class Patch_ShowFlightCompare
    {
        static UiStatCompareDB.ItemCompareDetails equipped;
        static UiStatCompareDB.ItemCompareDetails unequipped;
        [HarmonyPatch(typeof(UiStatCompareDB), "Initialize")]
        [HarmonyPrefix]
        static void UiStatCompareDB_Initialize(UiStatCompareDB.ItemCompareDetails inLeftItem, UiStatCompareDB.ItemCompareDetails inRightItem)
        {
            equipped = inLeftItem;
            unequipped = inRightItem;
        }
        [HarmonyPatch(typeof(UiStatsCompareMenu), "Populate")]
        [HarmonyPostfix]
        static void UiStatsCompareMenu_Populate(UiStatsCompareMenu __instance, bool showCompare, ItemStat[] equippedStats, ItemStat[] unequippedStats)
        {
            if (!Main.ShowRacingEquipmentStats)
                return;
            bool shouldClear = !(equippedStats?.Length > 0 || unequippedStats?.Length > 0);
            void Show(string name, string stat1, string stat2)
            {
                if (stat1 == null && stat2 == null)
                    return;
                if (shouldClear)
                {
                    __instance.ClearItems();
                    shouldClear = false;
                }
                KAWidget kawidget2 = __instance.DuplicateWidget(__instance._Template, UIAnchor.Side.Center);
                __instance.AddWidget(kawidget2);
                kawidget2.SetVisibility(true);
                string text = null;
                string text2 = null;
                string diffVal = null;
                var num = 0f;
                var num2 = 0f;
                if (stat1 != null)
                {
                    float.TryParse(stat1, out num);
                    text = Math.Round(num * 100) + "%";
                }
                if (stat2 != null)
                {
                    float.TryParse(stat2, out num2);
                    text2 = Math.Round(num2 * 100) + "%";
                }
                
                var statCompareResult = (num == num2) ? UiStatsCompareMenu.StatCompareResult.Equal : (num2 > num) ? UiStatsCompareMenu.StatCompareResult.Greater : UiStatsCompareMenu.StatCompareResult.Lesser;
                if (statCompareResult != UiStatsCompareMenu.StatCompareResult.Equal)
                    diffVal = Math.Round(Math.Abs(num - num2) * 100) + "%";
                __instance.ShowStatInfo(kawidget2, text, name, text2, diffVal, statCompareResult, showCompare);
            }
            var s = new SortedSet<string>();
            foreach (var att in new[] { equipped?._ItemData?.Attribute, unequipped?._ItemData?.Attribute })
                if (att != null)
                    foreach (var a in att)
                    {
                        if (a == null || s.Contains(a.Key))
                            continue;
                        var n = Main.GetCustomStatInfo(a.Key);
                        if (n != null && n.Valid)
                            s.Add(a.Key);
                    }
            foreach (var f in s)
                Show(
                    Main.GetCustomStatInfo(f).DisplayName,
                    equipped?._ItemData?.GetAttribute<string>(f, null),
                    unequipped?._ItemData?.GetAttribute<string>(f, null));
        }
        [HarmonyPatch(typeof(UiStoreStatCompare), "UpdateStatsCompareData")]
        [HarmonyPostfix]
        static void UiStoreStatCompare_UpdateStatsCompareData(UiStoreStatCompare __instance, List<UiStoreStatCompare.StatDataContainer> ___mStatDataList, KAUIMenu ___mContentMenu, int previewIndex, List<PreviewItemData> previewList)
        {
            if (!Main.ShowRacingEquipmentStats)
                return;
            ___mStatDataList.RemoveAll(x => x._EquippedStat == x._ModifiedStat);
            void Show(string name, string abv, float equipped, float unequipped)
            {
                var statDataContainer = new UiStoreStatCompare.StatDataContainer();
                statDataContainer._StatName = name;
                statDataContainer._AbvStatName = abv;
                statDataContainer._EquippedStat = equipped;
                statDataContainer._ModifiedStat = unequipped;
                statDataContainer._DiffStat = statDataContainer._ModifiedStat - statDataContainer._EquippedStat;
                ___mStatDataList.Add(statDataContainer);
                if (equipped != unequipped)
                {
                    var kawidget = ___mContentMenu.AddWidget(___mContentMenu._Template.name);
                    kawidget.FindChildItem("AbvStatWidget", true).SetText(statDataContainer._AbvStatName);
                    kawidget.FindChildItem("StatDiffWidget", true).SetText(Math.Round(Math.Abs(equipped - unequipped)) + "%");
                    var arrowWidget = kawidget.FindChildItem("ArrowWidget", true);
                    arrowWidget.SetVisibility(true);
                    arrowWidget.SetRotation(Quaternion.Euler(0f, 0f, 0f));
                    if (statDataContainer._DiffStat == 0f)
                    {
                        arrowWidget.SetVisibility(false);
                    }
                    else if (statDataContainer._DiffStat < 0f)
                    {
                        arrowWidget.pBackground.color = Color.red;
                        arrowWidget.SetRotation(Quaternion.Euler(0f, 0f, 180f));
                    }
                    else
                    {
                        arrowWidget.pBackground.color = Color.green;
                    }
                    kawidget.SetVisibility(true);
                }
            }
            var s = new SortedSet<string>();
            var d = new Dictionary<string, (float, float)>();
            var e = new Dictionary<string, (ItemData, ItemData)>();
            foreach (var part in AvatarData.pInstance.Part)
                if (part != null)
                {
                    var equipped = part.UserInventoryId > 0 ? CommonInventoryData.pInstance.FindItemByUserInventoryID(part.UserInventoryId.Value)?.Item : null;
                    if (equipped != null)
                    {
                        var key = part.PartType;
                        if (key.StartsWith("DEFAULT_"))
                            key = key.Remove(0, 8);
                        var t = e.GetOrCreate(key);
                        e[key] = (equipped, t.Item2);
                    }
                }
            foreach (var preview in previewIndex == -1 ? previewList as IEnumerable<PreviewItemData> : new[] { previewList[previewIndex] })
                if (preview.pItemData != null)
                {
                    var key = AvatarData.GetPartName(preview.pItemData);
                    if (key.StartsWith("DEFAULT_"))
                        key = key.Remove(0, 8);
                    var t = e.GetOrCreate(key);
                    if (t.Item2 == null)
                        e[key] = (t.Item1, preview.pItemData);
                }
            foreach (var p in e)
            {
                var item2 = p.Value.Item2 ?? p.Value.Item1;
                //Debug.Log($"\n{p.Key}\n - [{p.Value.Item1?.Attribute?.Join(x => x.Key + "=" + x.Value)}]\n - [{item2?.Attribute?.Join(x => x.Key + "=" + x.Value)}]");
                if (p.Value.Item1?.Attribute != null)
                    foreach (var a in p.Value.Item1.Attribute)
                    {
                        if (a == null)
                            continue;
                        var cs = Main.GetCustomStatInfo(a.Key);
                        if (cs == null || !cs.Valid)
                            continue;
                        if (!float.TryParse(a.Value, out var value))
                            continue;
                        s.Add(a.Key);
                        var t = d.GetOrCreate(a.Key);
                        d[a.Key] = (t.Item1 + value, t.Item2);
                    }
                if (item2?.Attribute != null)
                    foreach (var a in item2.Attribute)
                    {
                        if (a == null)
                            continue;
                        var cs = Main.GetCustomStatInfo(a.Key);
                        if (cs == null || !cs.Valid)
                            continue;
                        if (!float.TryParse(a.Value, out var value))
                            continue;
                        s.Add(a.Key);
                        var t = d.GetOrCreate(a.Key);
                        d[a.Key] = (t.Item1, t.Item2 + value);
                    }
            }
            foreach (var i in s)
            {
                var t = d[i];
                var c = Main.GetCustomStatInfo(i);
                if (t.Item1 != t.Item2)
                    Show(c.DisplayName, c.Abreviation, t.Item1 * 100, t.Item2 * 100);
            }
        }

        [HarmonyPatch(typeof(UiAvatarCustomization), "ShowAvatarStats")]
        [HarmonyPostfix]
        static void UiAvatarCustomization_ShowAvatarStats(UiAvatarCustomization __instance, UiStatPopUp ___mUiStats)
        {
            if (!Main.ShowRacingEquipmentStats)
                return;
            void Show(string name, string value)
            {
                KAWidget kawidget = ___mUiStats.mContentMenuCombat.AddWidget(___mUiStats.mContentMenuCombat._Template.name);
                kawidget.FindChildItem("CombatStatWidget", true).SetText(name);
                kawidget.FindChildItem("CombatStatValueWidget", true).SetText(value);
            }
            var custom = __instance.pCustomAvatar;
            var e = new HashSet<string>();
            var s = new SortedSet<string>();
            var d = new Dictionary<string, float>();
            foreach (var part in AvatarData.pInstance.Part)
                if (part != null)
                {
                    var equipped = custom == null
                        ? part.UserInventoryId > 0
                            ? CommonInventoryData.pInstance.FindItemByUserInventoryID(part.UserInventoryId.Value)?.Item
                            : null
                        : CommonInventoryData.pInstance.FindItemByUserInventoryID(custom.GetInventoryId(part.PartType))?.Item;
                    if (equipped != null)
                    {
                        var key = part.PartType;
                        if (key.StartsWith("DEFAULT_"))
                            key = key.Remove(0, 8);
                        if (!e.Add(key))
                            continue;
                        if (equipped.Attribute != null)
                            foreach (var a in equipped.Attribute)
                            {
                                if (a == null)
                                    continue;
                                var cs = Main.GetCustomStatInfo(a.Key);
                                if (cs == null || !cs.Valid)
                                    continue;
                                if (!float.TryParse(a.Value, out var value))
                                    continue;
                                s.Add(a.Key);
                                d[a.Key] = d.GetOrCreate(a.Key) + value;
                            }
                    }
                }
            foreach (var k in s)
                Show(Main.GetCustomStatInfo(k).DisplayName, Math.Round(d[k] * 100) + "%");
        }
    }

    [HarmonyPatch(typeof(BaseUnityPlugin), MethodType.Constructor, new Type[0])]
    static class Patch_CreatePluginObj
    {
        static void Postfix(BaseUnityPlugin __instance)
        {
            if (Main.CheckForModUpdates)
                Main.instance.CheckModVersion(__instance);
        }
    }

    [HarmonyPatch(typeof(KAUIStore))]
    static class Patch_Store
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        static void Start(KAUIStore __instance, KAWidget ___mBtnPreviewBuy)
        {
            var n = __instance.DuplicateWidget(___mBtnPreviewBuy, UIAnchor.Side.BottomLeft);
            n.name = "btnBuyAll";
            n.SetText("Buy All");
            n.SetVisibility(true);
            n.SetInteractive(true);
            var p = ___mBtnPreviewBuy.transform.position;
            p.x = -p.x * 0.7f;
            n.transform.position = p;
        }

        [HarmonyPatch("OnClick")]
        [HarmonyPostfix]
        static void OnClick(KAUIStore __instance, KAWidget item)
        {
            if (item.name == "btnBuyAll")
            {
                var byCatergory = CommonInventoryData.pInstance.mInventory;

                var all = new List<ItemData>();
                var check = new HashSet<int>();
                var gems = 0;
                var coins = 0;
                var cache = KAUIStore.pInstance.pChooseMenu.mCachedItemData;
                foreach (var ite in cache.Keys)
                    if (ite != null
                        && !ite.IsBundleItem()
                        && !ite.HasCategory(Category.MysteryBox)
                        && !ite.HasCategory(Category.DragonTickets)
                        && !ite.HasCategory(Category.DragonAgeUp)
                        && (!ite.Locked || SubscriptionInfo.pIsMember)
                        && (__instance.pCategoryMenu.pDisableRankCheck || !ite.IsRankLocked(out _, __instance.pStoreInfo._RankTypeID))
                        && ite.HasPrereqItem()
                        && CommonInventoryData.pInstance.GetQuantity(ite.ItemID) <= 0
                        && check.Add(ite.ItemID))
                    {
                        all.Add(ite);
                        if (ite.GetPurchaseType() == 1)
                            coins += ite.GetFinalCost();
                        else
                            gems += ite.GetFinalCost();
                    }
                if (all.Count == 0)
                    GameUtilities.DisplayOKMessage("PfKAUIGenericDB", "No items left to buy", null, "");
                else
                {
                    Main.CoinCost = coins;
                    Main.GemCost = gems;
                    Main.Buying = all;
                    GameUtilities.DisplayGenericDB("PfKAUIGenericDB", $"Buying these {Main.Buying.Count} items will cost {(gems > 0 ? coins > 0 ? $"{coins} coins and {gems} gems" : $"{gems} gems" : coins > 0 ? $"{coins} coins" : "nothing")}. Are you sure you want to buy these?", "Buy All", Main.instance.gameObject, nameof(Main.ConfirmBuyAll), nameof(Main.DoNothing), null, null, true);
                }
            }
        }
    }

    [HarmonyPatch(typeof(CaAvatarCam), "LateUpdate")]
    static class Patch_AvatarCam
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].opcode == OpCodes.Ldfld && code[i].operand is FieldInfo f && f.Name == "mMaxCameraDistance")
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_AvatarCam), nameof(EditMaxZoom))));
                else if (code[i].opcode == OpCodes.Ldc_R4 && (float)code[i].operand == 0.25f)
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_AvatarCam), nameof(EditZoomSpeed))));
            return code;
        }
        static float EditMaxZoom(float original) => Main.InfiniteZoom ? float.PositiveInfinity : original;
        static float EditZoomSpeed(float original) => original * Main.ZoomSpeed;
    }

    [HarmonyPatch(typeof(UiDragonCustomization), "RemoveDragonSkin")]
    static class Patch_ChangeDragonColor
    {
        static bool Prefix() => !Main.DisableDragonAutomaticSkinUnequip;
    }

    [HarmonyPatch(typeof(SanctuaryData), "GetPetCustomizationType", typeof(int))]
    static class Patch_PetCustomization
    {
        static bool Prefix(ref PetCustomizationType __result)
        {
            if (Main.AllowCustomizingSpecialDragons)
            {
                __result = PetCustomizationType.Default;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UserNotifyDragonTicket))]
    static class Patch_OpenCloseCustomization
    {
        public static (string, string)? closed;
        [HarmonyPatch("ActivateDragonCreationUIObj")]
        [HarmonyPrefix]
        static void ActivateDragonCreationUIObj()
        {
            if (KAUIStore.pInstance)
            {
                closed = (KAUIStore.pInstance.pCategory, KAUIStore.pInstance.pStoreInfo._Name);
                KAUIStore.pInstance.ExitStore();
            }
        }
        [HarmonyPatch("OnStableUIClosed")]
        [HarmonyPostfix]
        static void OnStableUIClosed()
        {
            if (closed != null)
            {
                var t = closed.Value;
                closed = null;
                StoreLoader.Load(true, t.Item1, t.Item2, null, UILoadOptions.AUTO, "", null);
            }
        }
    }

    [HarmonyPatch(typeof(SanctuaryData), "GetLocalizedPetName")]
    static class Patch_GetPetName
    {
        static void Postfix(RaisedPetData raisedPetData, ref string __result)
        {
            if (Main.DisableNameCustomizationPatch)
                return;
            if (__result.Length == 15 && __result.StartsWith("Dragon-") && uint.TryParse(__result.Remove(0, 7), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
                __result = SanctuaryData.GetPetDefaultName(raisedPetData.PetTypeID);
        }
    }

    [HarmonyPatch]
    static class Patch_GetStableQuestDuration
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(StableQuestSlotWidget), "HandleAdButtons");
            yield return AccessTools.Method(typeof(StableQuestSlotWidget), "StateChangeInit");
            yield return AccessTools.Method(typeof(StableQuestSlotWidget), "Update");
            yield return AccessTools.Method(typeof(TimedMissionManager), "CheckMissionCompleted", new[] { typeof(TimedMissionSlotData) });
            yield return AccessTools.Method(typeof(TimedMissionManager), "CheckMissionSuccess");
            yield return AccessTools.Method(typeof(TimedMissionManager), "GetCompletionTime");
            yield return AccessTools.Method(typeof(TimedMissionManager), "GetPetEngageTime");
            yield return AccessTools.Method(typeof(TimedMissionManager), "StartMission");
            yield return AccessTools.Method(typeof(UiStableQuestDetail), "HandleAdButton");
            yield return AccessTools.Method(typeof(UiStableQuestDetail), "MissionLogIndex");
            yield return AccessTools.Method(typeof(UiStableQuestDetail), "SetSlotData");
            yield return AccessTools.Method(typeof(UiStableQuestMissionStart), "RefreshUi");
            yield return AccessTools.Method(typeof(UiStableQuestSlotsMenu), "OnAdWatched");
            yield break;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iL)
        {
            var code = instructions.ToList();
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].operand is FieldInfo f && f.Name == "Duration" && f.DeclaringType == typeof(TimedMission))
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, typeof(Patch_GetStableQuestDuration).GetMethod(nameof(EditDuration), ~BindingFlags.Default)));
            return code;
        }

        static int EditDuration(int original) => (int)Math.Round(original * Main.StableQuestTimeMultiplier);
    }

    [HarmonyPatch]
    static class Patch_GetStableQuestBaseChance
    {
        static IEnumerable<MethodBase> TargetMethods() => from m in typeof(TimedMissionManager).GetMethods(~BindingFlags.Default) where m.Name == "GetWinProbability" select m;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iL)
        {
            var code = instructions.ToList();
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].operand is FieldInfo f && f.Name == "WinFactor" && f.DeclaringType == typeof(TimedMission))
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, typeof(Patch_GetStableQuestBaseChance).GetMethod(nameof(EditChance), ~BindingFlags.Default)));
            return code;
        }

        static int EditChance(int original) => original + Main.StableQuestChanceBoost;
    }

    [HarmonyPatch(typeof(TimedMissionManager), "GetWinProbabilityForPet")]
    static class Patch_GetStableQuestPetChance
    {
        static void Postfix(ref float __result) => __result *= Main.StableQuestDragonValueMultiplier;
    }

    [HarmonyPatch]
    static class Patch_GetInputLength
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(KAUIStoreBuyPopUp), "RefreshValues");
            yield return AccessTools.Method(typeof(UIInput), "Insert");
            yield return AccessTools.Method(typeof(UIInput), "Validate", new[] { typeof(string) });
            yield return AccessTools.Method(typeof(UiItemTradeGenericDB), "RefreshQuantity");
            yield return AccessTools.Method(typeof(UiPrizeCodeEnterDB), "Start");
            yield break;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iL)
        {
            var code = instructions.ToList();
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].operand is FieldInfo f && f.Name == "characterLimit" && f.DeclaringType == typeof(UIInput))
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, typeof(Patch_GetInputLength).GetMethod(nameof(EditLength), ~BindingFlags.Default)));
            return code;
        }

        static int EditLength(int original) => Main.BiggerInputBoxes ? (int)Math.Min((long)original * original, int.MaxValue) : original;
    }

    [HarmonyPatch(typeof(UIInput), "Validate", typeof(string), typeof(int), typeof(char))]
    static class Patch_CanInput
    {
        public static Dictionary<char, char> replace = new Dictionary<char, char>
        {
            {',','‚' },
            {':','꞉' },
            {'$','＄' },
            {'*','∗' },
            {'/','∕' },
            { '|','∣' }
        };
        static bool Prefix(UIInput __instance, string text, int pos, char ch, ref char __result)
        {
            if (Main.MoreNameFreedom && (__instance.validation == UIInput.Validation.Alphanumeric || __instance.validation == UIInput.Validation.Username || __instance.validation == UIInput.Validation.Name))
            {
                var cat = char.GetUnicodeCategory(ch);
                if (cat == UnicodeCategory.Control || cat == UnicodeCategory.Format || cat == UnicodeCategory.OtherNotAssigned)
                    __result = '\0';
                else if (replace.TryGetValue(ch, out var n))
                    __result = n;
                else
                    __result = ch;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(KAEditBox))]
    static class Patch_CanInput2
    {
        [HarmonyPatch("ValidateText", typeof(string), typeof(int), typeof(char))]
        static bool Prefix(KAEditBox __instance, string text, int charIndex, char addedChar, ref char __result)
        {
            if (Main.MoreNameFreedom && (__instance._CheckValidityOnInput && __instance._RegularExpression != null && __instance._RegularExpression.Contains("a-z")))
            {
                var cat = char.GetUnicodeCategory(addedChar);
                if (cat == UnicodeCategory.Control || cat == UnicodeCategory.Format || cat == UnicodeCategory.OtherNotAssigned)
                    __result = '\0';
                else if (Patch_CanInput.replace.TryGetValue(addedChar, out var n))
                    __result = n;
                else
                    __result = addedChar;
                return false;
            }
            return true;
        }
        [HarmonyPatch("IsValidText")]
        static void Postfix(KAEditBox __instance, ref bool __result)
        {
            if (!__result && Main.MoreNameFreedom && (__instance._RegularExpression != null && __instance._RegularExpression.Contains("a-z")))
                __result = true;
        }
    }

    [HarmonyPatch(typeof(WsWebService), "SetDisplayName")]
    static class Patch_SetDisplayName
    {
        static void Prefix(SetDisplayNameRequest requestObj)
        {
            var s = AvatarData.pInstance.DisplayName;
            var modified = false;
            var builder = new StringBuilder(s.Length);
            foreach (var c in s)
                builder.Append(Patch_CanInput.replace.TryGetValue(c, out var nc) && (modified = true) ? nc : c);
            var state = new RichTextState(Color.white);
            for (int i = 0; i < s.Length;)
                if (!state.ParseSymbol(s, ref i))
                    i++;
            if (state.bold && (modified = true))
                builder.Append("[/b]");
            if (state.italic && (modified = true))
                builder.Append("[/i]");
            if (state.under && (modified = true))
                builder.Append("[/u]");
            if (state.strike && (modified = true))
                builder.Append("[/s]");
            if (state.sub != 0 && (modified = true))
                builder.Append("[/sub]");
            for (int i = state.colors.size - 1; i > 1 && (modified = true); i--)
                builder.Append("[-]");
            if (state.ignore && (modified = true))
                builder.Append("[/c]");
            if (modified)
                requestObj.DisplayName = builder.ToString();
        }
    }

    [HarmonyPatch(typeof(UiSelectProfile), "InitProfile")]
    static class Patch_AvatarDataLoad
    {
        static void Postfix()
        {
            Main.TryFixUsername();
        }
    }

    [HarmonyPatch(typeof(UiAvatarControls), "Update")]
    static class Patch_ControlsUpdate
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iL)
        {
            var code = instructions.ToList();
            var flag = false;
            for (int i = 0; i < code.Count; i++)
                if (code[i].operand is MethodInfo m && flag)
                {
                    if (m.Name == "GetButtonDown")
                        code[i] = new CodeInstruction(OpCodes.Call, typeof(Patch_ControlsUpdate).GetMethod(nameof(ButtonDown), ~BindingFlags.Default));
                    else if (m.Name == "GetButtonUp")
                        code[i] = new CodeInstruction(OpCodes.Call, typeof(Patch_ControlsUpdate).GetMethod(nameof(ButtonUp), ~BindingFlags.Default));
                    flag = false;
                }
                else if (code[i].operand is string str)
                    flag = str == "DragonFire";
            return code;
        }
        static bool ButtonDown(string button) => Main.AutomaticFireballs ? KAInput.GetButton(button) : KAInput.GetButtonDown(button);
        static bool ButtonUp(string button) => Main.AutomaticFireballs ? KAInput.GetButton(button) : KAInput.GetButtonUp(button);
    }

    [HarmonyPatch(typeof(RacingManager), "AddPenalty")]
    static class Patch_AddRacingCooldown
    {
        public static bool Prefix() => RacingManager.Instance.State >= RacingManagerState.RaceCountdown;
    }

    [HarmonyPatch(typeof(UiSelectName), "OnClick")]
    static class Patch_SelectName
    {
        public static bool SkipNameChecks = false;
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iL)
        {
            var code = instructions.ToList();
            var lbl = iL.DefineLabel();
            code[code.FindLastIndex(code.FindIndex(x => x.operand is MethodInfo m && m.Name == "get_Independent"), x => x.opcode == OpCodes.Ldarg_0)].labels.Add(lbl);
            code.InsertRange(code.FindIndex(x => x.opcode == OpCodes.Stloc_0) + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldsfld,AccessTools.Field(typeof(Patch_SelectName),nameof(SkipNameChecks))),
                new CodeInstruction(OpCodes.Brtrue,lbl)
            });
            return code;
        }
    }

    [HarmonyPatch(typeof(SanctuaryPet), "PetMoodParticleAllowed")]
    static class Patch_MoodParticleAllowed
    {
        static void Postfix(SanctuaryPet __instance, ref bool __result)
        {
            var n = __instance.GetTypeInfo()._Name;
            if (Main.DisableHappyParticles.TryGetValue(n, out var v))
            {
                if (v)
                    __result = false;
            }
            else
            {
                Main.DisableHappyParticles[n] = false;
                Main.instance.Config.Save();
            }
        }
    }

    [HarmonyPatch(typeof(AvAvatarController), "ShowArmorWing")]
    static class Patch_ArmorWingsVisible
    {
        static void Prefix(ref bool show)
        {
            if (Main.AlwaysShowArmourWings)
                show = true;
        }
    }

    [HarmonyPatch(typeof(UiAvatarCustomization), "SetSkinColorPickersVisibility")]
    static class Patch_AvatarCustomization
    {
        static void Postfix(UiAvatarCustomization __instance)
        {
            if (Main.CustomColorPickerMode == ColorPickerMode.Disabled)
                return;
            var ui = ColorPicker.OpenUI((x) =>
            {
                __instance.mSelectedColorBtn.pBackground.color = x;
                __instance.SetColor(x);
            });
            ui.Requires = () =>
                __instance
                && __instance.isActiveAndEnabled
                && __instance.GetVisibility()
                && (
                    (__instance.pColorPalette && __instance.pColorPalette.GetVisibility())
                    ||
                    (__instance.mSkinColorPalette && __instance.mSkinColorPalette.GetVisibility())
                );
            ui.current = __instance.mSelectedColorBtn.pBackground.color;
        }
    }

    [HarmonyPatch(typeof(StoreData), "SetStoreData")]
    static class Patch_SetStoreItemData
    {
        public static Dictionary<int, int> originalMaxes = new Dictionary<int, int>();
        public static void Postfix(StoreData __instance)
        {
            if (Main.RemoveItemBuyLimits)
                foreach (var i in __instance._Items)
                    if (i.InventoryMax != -1)
                    {
                        originalMaxes[i.ItemID] = i.InventoryMax;
                        i.InventoryMax = -1;
                    }
        }
    }

    [HarmonyPatch(typeof(KAUIStoreBuyPopUp), "SetItemData")]
    static class Patch_SetBuyPopupItem
    {
        static (string text, int textid, Color color, int width)? originalText;
        static void Postfix(KAUIStoreBuyPopUp __instance, KAStoreItemData itemData)
        {
            var label = __instance.mBattleSlots.GetLabel();
            if (itemData._ItemData.HasCategory(Category.DragonTickets) && itemData._ItemData.InventoryMax < 0)
            {
                if (originalText == null)
                {
                    originalText = (label.text, label.textID, label.pOrgColorTint, label.width);
                    label.textID = 0;
                    label.text = "Buying many of this item is not recommended";
                    label.ResetEnglishText();
                    label.color = label.pOrgColorTint = originalText.Value.color.Shift(Color.red);
                    label.width = (int)(originalText.Value.width * 1.8);
                    __instance.mBattleSlots.transform.position += new Vector3((label.width - originalText.Value.width), 0, 0);
                    __instance.mOccupiedBattleSlots.SetText("");
                }
                __instance.mBattleSlots.SetVisibility(true);
            }
            else if (originalText != null)
            {
                label.text = originalText.Value.text;
                label.textID = originalText.Value.textid;
                label.ResetEnglishText();
                label.color = label.pOrgColorTint = originalText.Value.color;
                __instance.mBattleSlots.transform.position -= new Vector3((label.width - originalText.Value.width), 0, 0);
                label.width = originalText.Value.width;
                originalText = null;
            }

        }
    }

    [HarmonyPatch(typeof(UtWWWAsync), "InitializeAssetVersionLists")]
    static class Patch_OverrideExpiration
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            code.Insert(
                code.FindIndex(x => x.operand is MethodInfo m && m.Name == "set_expirationDelay"),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_OverrideExpiration), nameof(ModifyExpiration))));
            return code;
        }
        static int ModifyExpiration(int original) => Main.CacheExpiration;
    }

    [HarmonyPatch(typeof(UtWWWAsync), "LoadBundle")]
    static class Patch_MarkCacheUsed
    {
        static void Prefix(Hash128 hash, ref UtWWWEventHandler callback)
        {
            if (Main.FixCacheExpiration && hash != default)
                callback = ((a, b) => { if (a == UtAsyncEvent.COMPLETE) Caching.MarkAsUsed(b.pURL, hash); }) + callback;
        }
    }

    [HarmonyPatch(typeof(UILabel))]
    static class Patch_ForceEnableEncoding
    {
        [HarmonyPatch("OnEnable")]
        public static void Prefix(UILabel __instance)
        {
            if (Main.ForceTextTagRendering && (Main.ForceTagsForInputs || !Patch_MarkUILabel.IsEdit(__instance)))
            {
                __instance.supportEncoding = true;
                __instance.symbolStyle = NGUIText.SymbolStyle.Colored;
            }
        }
        [HarmonyPatch("set_supportEncoding")]
        static void Prefix(UILabel __instance, ref bool value)
        {
            if (Main.ForceTextTagRendering && (Main.ForceTagsForInputs || !Patch_MarkUILabel.IsEdit(__instance)))
                value = true;
        }
    }

    [HarmonyPatch(typeof(UIInput), "Start")]
    static class Patch_MarkUILabel
    {
        static ConditionalWeakTable<UILabel, object> marked = new();
        static void Postfix(UIInput __instance)
        {
            if (__instance.label)
            {
                if (!marked.TryGetValue(__instance.label, out _))
                    marked.Add(__instance.label, new object());
                if (!Main.ForceTagsForInputs)
                {
                    __instance.label.supportEncoding = false;
                    __instance.label.SetDirty();
                }
            }
        }
        public static bool IsEdit(UILabel label) => marked.TryGetValue(label, out _);
    }

    [HarmonyPatch(typeof(TMP_Text))]
    static class Patch_TMPText
    {
        [HarmonyPatch("set_text")]
        static void Prefix(TMP_Text __instance, ref string value)
        {
            if (Main.ForceTextTagRendering && value.SoDtoUnityRich(out var newValue, __instance.color, true, __instance.GetComponentInParent<TMP_InputField>()))
            {
                __instance.richText = true;
                value = newValue;
            }
        }
    }

    /*[HarmonyPatch] // Just some debug code
    static class Patch_GetAsmType
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var t in typeof(Assembly).Assembly.GetTypes())
                if (typeof(Assembly).IsAssignableFrom(t))
                    foreach (var m in t.GetMethods(~BindingFlags.Default))
                        if (typeof(Type).IsAssignableFrom(m.ReturnType) && m.HasMethodBody())
                            yield return m;
            yield break;
        }

        static void Postfix(object[] __args, ref Type __result)
        {
            Debug.Log("Type request: [" + __args.Join() + "] >> " + __result?.FullName ?? "null");
        }
    }*/

    [HarmonyPatch(typeof(AvAvatarController), "IsAirRefillingAllowed")]
    static class Patch_HasAirSupply
    {
        static void Postfix(ref bool __result) => __result |= Main.InfiniteOxygen;
    }

    [HarmonyPatch(typeof(Task), "CheckRange")]
    static class Patch_CheckInRange
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            code.InsertRange(
                code.FindIndex(code.FindIndex(x => x.operand is string s && s == "Proximity"), x => x.operand is MethodInfo m && m.Name == "Get") + 1,
                new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldloc_1),
                    new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(Patch_CheckInRange),nameof(ModifyRange)))
                });
            code.InsertRange(
                code.FindIndex(
                    code.FindIndex(x => x.opcode == OpCodes.Ldfld && x.operand is FieldInfo f && f.Name == "_NPC") + 1,
                    x => x.opcode == OpCodes.Ldfld && x.operand is FieldInfo f && f.Name == "_NPC") + 1,
                new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldloc_1),
                    new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(Patch_CheckInRange),nameof(CheckNPC)))
                });
            return code;
        }
        static float ModifyRange(float original, Task task, TaskObjective objective)
        {

            //Debug.Log("============================= Patch_CheckInRange >> " + task.pData.Type + " compare to " + Main.RangeMissionAlwaysInRange);
            return Main.RangeMissionAlwaysInRange != 0 && Enum.TryParse<RangeType>(task.pData.Type, out var type) && Main.RangeMissionAlwaysInRange.HasFlag(type)
                ? type == RangeType.Chase ? float.Epsilon : -1
                : original;
        }
        static GameObject CheckNPC(GameObject original, Task task, TaskObjective objective)
        {
            if (Main.ForceRangeMissionCompleteWithoutNPC != 0
                && !original
                && Enum.TryParse<RangeType>(task.pData.Type, out var type)
                && Main.ForceRangeMissionCompleteWithoutNPC.HasFlag(type))
            {
                objective._WithinProximity = type != RangeType.Chase;
                objective._ProximityTimer = 0f;
                return null;
            }
            return original;
        }
    }

    [HarmonyPatch(typeof(AIBehavior_Mission), "ProcessProximity")]
    static class Patch_CheckInRange2
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            code.InsertRange(
                code.FindIndex(x => x.operand is FieldInfo f && f.Name == "Proximity") + 1,
                new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(Patch_CheckInRange2),nameof(ModifyRange)))
                });
            return code;
        }
        static float ModifyRange(float original, AIBehavior_Mission actor)
        {
            // Note: yes, the "escort" behaviour is used for "follow" missions and the "follow" behaviour is used for "escort" missions. Don't ask me why
            var type =
                actor is AIBehavior_ChaseMission ? RangeType.Chase
                : actor is AIBehavior_EscortPlayerMission ? RangeType.Follow
                : actor is AIBehavior_NPCFollowPlayer ? RangeType.Escort
                : RangeType.None;
            return Main.RangeMissionAlwaysInRange != 0 && Main.RangeMissionAlwaysInRange.HasFlag(type)
                ? type == RangeType.Chase ? float.Epsilon : float.PositiveInfinity
                : original;
        }
    }

    [HarmonyPatch(typeof(MyRoomsIntMain), "HasCreativePointsLimitReached")]
    static class Patch_ReachedCreativeLimit
    {
        static void Postfix(ref bool __result) => __result &= !Main.InfiniteCreativity;
    }

    [HarmonyPatch(typeof(FogController), "Update")]
    static class Patch_DisableFogPlane
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].operand is FieldInfo f && f.Name == "mIsFogActive")
                    code.Insert(
                        i + 1,
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_DisableFogPlane), nameof(OverrideActive))));
            return code;
        }
        static bool OverrideActive(bool original) => original && !Main.DisableFog;
    }

    [HarmonyPatch(typeof(FogArea))]
    static class Patch_FogAreaStart
    {
        [HarmonyPatch("Start")]
        static void Postfix(FogArea __instance)
        {
            __instance.gameObject.GetOrAddComponent<AreaDisabler>();
        }
    }

    public class AreaDisabler : MonoBehaviour
    {
        public bool disabled;
        public void Update()
        {
            if (Main.DisableFog ? GetComponent<FogArea>().mFogParticleSystem.gameObject.activeSelf : disabled)
            {
                var inst = GetComponent<FogArea>().mFogParticleSystem.gameObject;
                inst.SetActive(!inst.activeSelf);
                disabled = !inst.activeSelf;
            }
        }
    }

    [HarmonyPatch]
    static class Patch_KeepName
    {
        static string prevName;

        [HarmonyPatch(typeof(UiProfile), "ChangeName")]
        static void Prefix(UiProfile __instance)
        {
            if (Main.KeepPreviousOnRename)
                prevName = __instance.mAvatarName.GetText();
        }

        [HarmonyPatch(typeof(UiProfile), "ChangeName")]
        static void Finalizer() => prevName = null;

        [HarmonyPatch(typeof(UiSelectName), "Init")]
        static void Prefix(ref string name)
        {
            if (string.IsNullOrEmpty(name) && prevName != null)
                name = prevName;
        }

        [HarmonyPatch(typeof(UiSelectName), "SetNames")]
        static void Prefix(UiSelectName __instance, ref string name)
        {
            if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(__instance.mSelectedName ?? __instance.mTxtName?.GetText()))
                name = __instance.mSelectedName ?? __instance.mTxtName.GetText();
        }

        [HarmonyPatch(typeof(UiDragonName), "Start")]
        static void Postfix(UiDragonName __instance)
        {
            if (Main.KeepPreviousOnRename && __instance.mSelectedPetData != null && __instance.mSelectedPetData.pIsNameCustomized)
                __instance.mEditLabel.SetText(__instance.mSelectedPetData.Name);
        }
    }
    
}
