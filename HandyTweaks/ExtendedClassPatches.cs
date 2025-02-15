using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace HandyTweaks
{
    [HarmonyPatch(typeof(UiDragonCustomization))]
    static class Patch_DragonCustomization
    {
        [HarmonyPatch("SetColorSelector")]
        [HarmonyPostfix]
        static void SetColorSelector(UiDragonCustomization __instance)
        {
            if (Main.CustomColorPickerMode == ColorPickerMode.Disabled)
                return;
            if (__instance.mIsUsedInJournal && !ExtendedDragonCustomization.FreeCustomization(__instance.mFreeCustomization, __instance) && CommonInventoryData.pInstance.GetQuantity(__instance.mUiJournalCustomization._DragonTicketItemID) <= 0)
                return;
            var ui = ColorPicker.OpenUI((x) =>
            {
                if (__instance.mSelectedColorBtn == __instance.mPrimaryColorBtn)
                    __instance.mPrimaryColor = x;
                else if (__instance.mSelectedColorBtn == __instance.mSecondaryColorBtn)
                    __instance.mSecondaryColor = x;
                else if (__instance.mSelectedColorBtn == __instance.mTertiaryColorBtn)
                    __instance.mTertiaryColor = x;
                else
                {
                    var e = ExtendedDragonCustomization.Get(__instance);
                    if (__instance.mSelectedColorBtn == e.emissionColorBtn)
                        e.emissionColor = x;
                    else if (__instance.mSelectedColorBtn == e.fireballColorBtn)
                        e.fireballColor = x;
                }
                __instance.mSelectedColorBtn.pBackground.color = x;
                __instance.mRebuildTexture = true;
                __instance.RemoveDragonSkin();
                __instance.mIsResetAvailable = true;
                __instance.RefreshResetBtn();
                __instance.mMenu.mModified = true;
            });
            ui.Requires = () => __instance && __instance.isActiveAndEnabled && __instance.GetVisibility();
            ui.current = __instance.mSelectedColorBtn.pBackground.color;
        }

        [HarmonyPatch("SetColorSelector")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> SetColorSelector(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            code.InsertRange(code.FindIndex(x => x.operand is MethodInfo m && m.Name == "get_white")+ 1, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ExtendedDragonCustomization),nameof(ExtendedDragonCustomization.GetSelectedColor)))
            });
            return code;
        }

        [HarmonyPatch("UpdateCustomizationUI")]
        [HarmonyPrefix]
        static void UpdateCustomizationUI(UiDragonCustomization __instance) => ExtendedDragonCustomization.Get(__instance);

        [HarmonyPatch("OnClick")]
        [HarmonyPostfix]
        static void OnClick(UiDragonCustomization __instance, KAWidget inItem)
        {
            var e = ExtendedDragonCustomization.Get(__instance);
            if (inItem == e.emissionColorBtn || inItem == e.fireballColorBtn)
            {
                __instance.mSelectedColorBtn = inItem;
                __instance.SetColorSelector();
            }
            if (e.ToggleBtnRepaired && inItem == e.ToggleBtnRepaired)
            {
                __instance.mMenu.mModified = true;
                ExtendedPetData.Get(__instance.pPetData).isIntact = e.ToggleBtnRepaired.IsChecked();
                MeshConversion.EnforceModel(__instance.pPetData, __instance.mPet.mRendererMap.Values);
            }
        }

        [HarmonyPatch("RefreshUI")]
        [HarmonyPostfix]
        static void RefreshUI(UiDragonCustomization __instance)
        {
            bool flag = SanctuaryData.GetPetCustomizationType(__instance.pPetData) == PetCustomizationType.Default;
            __instance.mToggleBtnMale.SetVisibility(flag);
            __instance.mToggleBtnFemale.SetVisibility(flag);
        }

        [HarmonyPatch("OnPressRepeated")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> OnPressRepeated(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            code.InsertRange(code.FindIndex(x => x.operand is FieldInfo f && f.Name == "mFreeCustomization") + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ExtendedDragonCustomization),nameof(ExtendedDragonCustomization.FreeCustomization)))
            });
            code.InsertRange(code.FindIndex(x => x.opcode == OpCodes.Ldfld && x.operand is FieldInfo f && f.Name == "mRebuildTexture"), new[]
            {
                new CodeInstruction(OpCodes.Ldloc_S,10),
                new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ExtendedDragonCustomization),nameof(ExtendedDragonCustomization.OnPaletteClick)))
            });
            return code;
        }

        [HarmonyPatch("OnCloseCustomization")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> OnCloseCustomization(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].operand is MethodInfo m && m.Name == "SetColors" && m.DeclaringType == typeof(SanctuaryPet))
                {
                    code.RemoveAt(i);
                    code.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ExtendedDragonCustomization),nameof(ExtendedDragonCustomization.StoreValues)))
                    });
                }
            return code;
        }
        [HarmonyPatch("Update")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Update(IEnumerable<CodeInstruction> instructions) => OnCloseCustomization(instructions);

        [HarmonyPatch("Update")]
        [HarmonyPostfix] 
        static void Update(UiDragonCustomization __instance)
        {
            if (Input.GetMouseButtonDown(1))
            {
                var e = ExtendedDragonCustomization.Get(__instance);
                var flag = false;
                if (KAUI.GetGlobalMouseOverItem() == e.emissionColorBtn)
                {
                    e.emissionColor = null;
                    e.emissionColorBtn.pBackground.color = ExtendedDragonCustomization.NullColorFallback;
                    flag = true;
                }
                else if (KAUI.GetGlobalMouseOverItem() == e.fireballColorBtn)
                {
                    e.fireballColor = null;
                    e.fireballColorBtn.pBackground.color = ExtendedDragonCustomization.NullColorFallback;
                    flag = true;
                }
                if (flag)
                {
                    __instance.mRebuildTexture = true;
                    __instance.RemoveDragonSkin();
                    __instance.mMenu.mModified = true;
                }
            }
        }
    }

    [HarmonyPatch(typeof(SanctuaryPet), "UpdateShaders")]
    static class Patch_UpdatePetShaders
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            code.InsertRange(code.FindIndex(x => x.opcode == OpCodes.Ldloc_S && ((x.operand is LocalBuilder l && l.LocalIndex == 6) || (x.operand is IConvertible i && i.ToInt32(CultureInfo.InvariantCulture) == 6))) + 1,
                new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_UpdatePetShaders), nameof(EditMat)))
                });
            return code;
        }
        static Material EditMat(Material material, SanctuaryPet pet)
        {
            if (material.HasProperty("_EmissiveColor"))
            {
                var pe = ExtendedPetData.Get(pet.pData);
                if (pe.EmissionColor != null)
                {
                    var e = MaterialEdit.Get(material).OriginalEmissive;
                    material.SetColor("_EmissiveColor", new Color(pe.EmissionColor.Value.r * e.strength, pe.EmissionColor.Value.g * e.strength, pe.EmissionColor.Value.b * e.strength, pe.EmissionColor.Value.a * e.alpha));
                }
                else
                    material.SetColor("_EmissiveColor", MaterialEdit.Get(material).OriginalEmissive.original);
            }
            return material;
        }
    }

    [HarmonyPatch(typeof(RaisedPetData))]
    static class Patch_PetData
    {
        [HarmonyPatch("ParseResStringEx")]
        static void Postfix(string s, RaisedPetData __instance)
        {
            ExtendedPetData.Get(__instance).isIntact = false;
            foreach (var i in s.Split('*'))
                if (i == ExtendedPetData.ISINTACT_KEY)
                    ExtendedPetData.Get(__instance).isIntact = true;
                else
                {
                    var values = i.Split('$');
                    if (values.Length >= 2 && values[0] == ExtendedPetData.FIREBALLCOLOR_KEY)
                    {
                        if (values.TryParseColor(out var c, 1))
                            ExtendedPetData.Get(__instance).FireballColor = c;
                        else if (values[1].TryParseColor(out c))
                            ExtendedPetData.Get(__instance).FireballColor = c;
                    }
                    else if (values.TryParseColor(out var c, 1))
                        ExtendedPetData.Get(__instance).EmissionColor = c;
                }
        }
        [HarmonyPatch("SaveToResStringEx")]
        static void Postfix(RaisedPetData __instance, ref string __result)
        {
            var d = ExtendedPetData.Get(__instance);
            if (d.FireballColor != null)
                __result += ExtendedPetData.FIREBALLCOLOR_KEY + "$" + d.FireballColor.Value.JoinValues() + "*";
            if (d.EmissionColor != null)
                __result += ExtendedPetData.EMISSIONCOLOR_KEY + "$" + d.EmissionColor.Value.JoinValues() + "*";
            if (d.isIntact)
                __result += ExtendedPetData.ISINTACT_KEY + "*";
        }
        [HarmonyPatch("SaveDataReal")]
        static void Prefix(RaisedPetData __instance)
        {
            var d = ExtendedPetData.Get(__instance);
            if (d.EmissionColor == null)
                __instance.SetAttrData(ExtendedPetData.EMISSIONCOLOR_KEY, "false", DataType.BOOL);
            else
                __instance.SetAttrData(ExtendedPetData.EMISSIONCOLOR_KEY, d.EmissionColor.Value.JoinValues(), DataType.STRING);

            if (d.FireballColor == null)
                __instance.SetAttrData(ExtendedPetData.FIREBALLCOLOR_KEY, "false", DataType.BOOL);
            else
                __instance.SetAttrData(ExtendedPetData.FIREBALLCOLOR_KEY, d.FireballColor.Value.JoinValues(), DataType.STRING);

            if (d.isIntact)
                __instance.SetAttrData(ExtendedPetData.ISINTACT_KEY, "true", DataType.BOOL);
            else
                __instance.SetAttrData(ExtendedPetData.ISINTACT_KEY, "false", DataType.BOOL);
        }
        [HarmonyPatch("ResolveLoadedData")]
        static void Postfix(RaisedPetData __instance)
        {
            var d = ExtendedPetData.Get(__instance);
            var a = __instance.FindAttrData(ExtendedPetData.FIREBALLCOLOR_KEY);
            if (a?.Value != null && a.Type == DataType.STRING)
            {
                var values = a.Value.Split('$');
                if (values.TryParseColor(out var c))
                    d.FireballColor = c;
            }
            a = __instance.FindAttrData(ExtendedPetData.EMISSIONCOLOR_KEY);
            if (a?.Value != null && a.Type == DataType.STRING)
            {
                var values = a.Value.Split('$');
                if (values.TryParseColor(out var c))
                    d.EmissionColor = c;
            }
            a = __instance.FindAttrData(ExtendedPetData.ISINTACT_KEY);
            d.isIntact = a?.Value == "true" && a.Type == DataType.BOOL;
        }
    }

    [HarmonyPatch]
    static class Patch_ColorDragonShot
    {
        [HarmonyPatch(typeof(ObAmmo), "Activate")]
        [HarmonyPrefix]
        static void ActivateAmmo_Pre(ObAmmo __instance, WeaponManager inManager)
        {
            var a = ExtendedAmmo.Get(__instance);
            a.manager = inManager;
            ExtendedAmmo.EditColors(__instance.gameObject, __instance);
        }

        [HarmonyPatch(typeof(ObAmmo), "PlayHitParticle")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ObAmmo_PlayHitParticle(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var ind = code.FindIndex(x => x.opcode == OpCodes.Stloc_0);
            var lbl = code[ind].labels;
            code[ind].labels = new List<Label>();
            code.InsertRange(ind, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0) { labels = lbl },
                new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ExtendedAmmo),nameof(ExtendedAmmo.EditColors)))
            });
            return code;
        }

        [HarmonyPatch(typeof(ObBlastAmmo), "PlayHitParticle")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ObBlastAmmo_PlayHitParticle(IEnumerable<CodeInstruction> instructions) => ObAmmo_PlayHitParticle(instructions);

        [HarmonyPatch(typeof(ObCatapultAmmo), "PlayHitParticle")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ObCatapultAmmo_PlayHitParticle(IEnumerable<CodeInstruction> instructions) => ObAmmo_PlayHitParticle(instructions);
    }

    [HarmonyPatch]
    static class Patch_ApplyPetCustomization
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ApplyPetCustomization), "SetSkinData");
            yield return AccessTools.Method(typeof(ApplyPetCustomization), "OnMeshLoaded");
            yield break;
        }
        static void Postfix(ApplyPetCustomization __instance) => MeshConversion.EnforceModel(__instance.mRaisedPetData, __instance.mRendererMap.Values);
    }

    [HarmonyPatch]
    static class Patch_SanctuaryPet
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(SanctuaryPet), "SetSkinData");
            yield return AccessTools.Method(typeof(SanctuaryPet), "ResetSkinData");
            yield return AccessTools.Method(typeof(SanctuaryPet), "UpdateData");
            yield return AccessTools.Method(typeof(SanctuaryPet), "Init");
            yield break;
        }
        static void Postfix(SanctuaryPet __instance) => MeshConversion.EnforceModel(__instance.pData, __instance.mRendererMap.Values);
    }

    [HarmonyPatch(typeof(UiDragonsInfoCardItem))]
    static class Patch_DragonInfoCard
    {
        [HarmonyPatch("RefreshUI")]
        [HarmonyPostfix]
        static void RefreshUI(UiDragonsInfoCardItem __instance) => ExtendedInfoCard.Get(__instance).Refresh();

        [HarmonyPatch("OnClick", typeof(KAWidget))]
        [HarmonyPostfix]
        static void OnClick(UiDragonsInfoCardItem __instance, KAWidget inWidget) => ExtendedInfoCard.Get(__instance).OnClick(inWidget);
    }
}