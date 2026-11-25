using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using V;
using V.Framework;
using Object = UnityEngine.Object;

namespace Vivhite
{
    internal static class WeaponSlotsUI
    {
        private sealed class Pair
        {
            internal RectTransform Primary;
            internal RectTransform Secondary;
            internal RectTransform Placeholder;
            internal LayoutElement PrimaryLayout;
            internal LayoutElement SecondaryLayout;
            internal bool PrimaryIgnoredLayout;
            internal bool SecondaryIgnoredLayout;
            internal Vector3 PrimaryScale;
            internal Vector3 SecondaryScale;
            internal TMP_Text PrimaryLabel;
            internal TMP_Text SecondaryLabel;
            internal Vector2 Position;
        }

        private static readonly ConditionalWeakTable<Component, Pair> Pairs = new ConditionalWeakTable<Component, Pair>();
        private static readonly Dictionary<Type, string> NativeLists = new Dictionary<Type, string>
        {
            [typeof(TakeBagUI)] = "ShowAccompanyEquipments",
            [typeof(WarehouseUI)] = "ShowAccompanyEquipments",
            [typeof(WildBagUI)] = "ShowAccompanyEquipItems",
            [typeof(SaleBoxUI)] = "ShowAccompanyEquipments"
        };

        internal static void Install(Harmony harmony)
        {
            foreach (var entry in NativeLists)
            {
                Type type = entry.Key;
                harmony.Patch(AccessTools.Method(type, entry.Value), prefix: Patch(nameof(ClearOldPair)),
                    postfix: Patch(nameof(PlaceWeaponPair)),
                    transpiler: type == typeof(WildBagUI) ? null : Patch(nameof(IncludeExtraSlot)));
                harmony.Patch(AccessTools.Method(type, "ShowEquipNode"), prefix: Patch(nameof(ResetBeforeTab)),
                    postfix: Patch(nameof(CaptionWeaponPair)));
                harmony.Patch(AccessTools.Method(type, "OnClose"), prefix: Patch(nameof(ClearOldPair)));
                var quickEquip = AccessTools.Method(type, "GetAccompanyEquipIndex", new[] { typeof(CommonData) });
                if (quickEquip != null)
                    harmony.Patch(quickEquip, prefix: Patch(nameof(PickEmptyWeaponSlot)));
            }
        }

        private static VillagerData Current() => UserDataManager.IsActive() ? UserDataManager.Instance.GetAccompanyData() : null;

        private static bool PickEmptyWeaponSlot(CommonData data, ref int __result)
        {
            VillagerData follower = Current();
            if (!DualWeapons.IsVivhite(follower) || data?.type != 1 || data.subType != 1)
                return true;
            DualWeapons.EnsureSlots(follower);
            __result = follower.equipDatas[DualWeapons.MainSlot].dataID != 0 && follower.equipDatas[DualWeapons.OffSlot].dataID == 0
                ? DualWeapons.OffSlot : DualWeapons.MainSlot;
            return false;
        }

        private static int SlotCount()
        {
            VillagerData follower = Current();
            if (!DualWeapons.IsVivhite(follower))
                return 8;
            DualWeapons.EnsureSlots(follower);
            return DualWeapons.OffSlot + 1;
        }

        private static IEnumerable<CodeInstruction> IncludeExtraSlot(IEnumerable<CodeInstruction> body)
        {
            int edits = 0;
            foreach (var instruction in body)
            {
                if (instruction.opcode == OpCodes.Ldc_I4_8)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(WeaponSlotsUI), nameof(SlotCount));
                    edits++;
                }
                yield return instruction;
            }
            if (edits != 1)
                throw new InvalidOperationException("Could not bind the native follower equipment slot count.");
        }

        private static void PlaceWeaponPair(Component __instance, List<BagItem> ___totalAccEquipItems, TMP_Text ___text_type1)
        {
            if (!DualWeapons.IsVivhite(Current()) || ___totalAccEquipItems.Count <= DualWeapons.OffSlot)
                return;

            BagItem main = ___totalAccEquipItems[DualWeapons.MainSlot];
            BagItem off = ___totalAccEquipItems[DualWeapons.OffSlot];
            main.index = DualWeapons.MainSlot;
            off.index = DualWeapons.OffSlot;
            var primary = (RectTransform)main.transform;
            var secondary = (RectTransform)off.transform;
            var parent = (RectTransform)primary.parent;
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            var primaryLayout = primary.GetComponent<LayoutElement>() ?? primary.gameObject.AddComponent<LayoutElement>();
            var secondaryLayout = secondary.GetComponent<LayoutElement>() ?? secondary.gameObject.AddComponent<LayoutElement>();
            var placeholder = new GameObject("Vivhite Weapon Space", typeof(RectTransform)).GetComponent<RectTransform>();
            placeholder.SetParent(parent, false);
            placeholder.SetSiblingIndex(primary.GetSiblingIndex());
            placeholder.sizeDelta = primary.sizeDelta;
            var placeholderLayout = placeholder.gameObject.AddComponent<LayoutElement>();
            placeholderLayout.preferredWidth = primary.rect.width;
            placeholderLayout.preferredHeight = primary.rect.height;
            var placement = new Pair
            {
                Primary = primary,
                Secondary = secondary,
                Placeholder = placeholder,
                PrimaryScale = primary.localScale,
                SecondaryScale = secondary.localScale,
                PrimaryLayout = primaryLayout,
                SecondaryLayout = secondaryLayout,
                PrimaryIgnoredLayout = primaryLayout.ignoreLayout,
                SecondaryIgnoredLayout = secondaryLayout.ignoreLayout,
                Position = primary.anchoredPosition
            };
            primaryLayout.ignoreLayout = true;
            secondaryLayout.ignoreLayout = true;
            secondary.anchorMin = primary.anchorMin;
            secondary.anchorMax = primary.anchorMax;
            secondary.pivot = primary.pivot;
            secondary.sizeDelta = primary.sizeDelta;
            primary.localScale = placement.PrimaryScale * 0.46f;
            secondary.localScale = placement.SecondaryScale * 0.46f;
            float spacing = Mathf.Max(26f, primary.rect.width * 0.245f);
            primary.anchoredPosition = placement.Position + Vector2.left * spacing;
            secondary.anchoredPosition = placement.Position + Vector2.right * spacing;
            bool english = LocalizationManager.Instance.CurrentLanguage == GameLanguage.English;
            placement.PrimaryLabel = CreateLabel(___text_type1, primary, english ? "Main" : "主");
            placement.SecondaryLabel = CreateLabel(___text_type1, secondary, english ? "Off" : "副");
            Pairs.Add(__instance, placement);
        }

        private static TMP_Text CreateLabel(TMP_Text source, RectTransform slot, string label)
        {
            var copy = Object.Instantiate(source, slot, false);
            var rect = copy.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, -23f);
            rect.sizeDelta = new Vector2(0f, 21f);
            copy.alignment = TextAlignmentOptions.Center;
            copy.fontSize = Mathf.Max(28f, copy.fontSize);
            copy.raycastTarget = false;
            copy.text = label;
            copy.gameObject.SetActive(true);
            return copy;
        }

        private static void ResetBeforeTab(Component __instance, int type)
        {
            if (type != 1)
                ClearOldPair(__instance);
        }

        private static void CaptionWeaponPair(Component __instance, int type, TMP_Text ___text_type1)
        {
            if (type == 1 && Pairs.TryGetValue(__instance, out _))
                ___text_type1.text = LocalizationManager.Instance.CurrentLanguage == GameLanguage.English ? "Weapons" : "武器";
        }

        private static void ClearOldPair(Component __instance)
        {
            if (!Pairs.TryGetValue(__instance, out Pair previous))
                return;
            Pairs.Remove(__instance);
            if (previous.Primary != null)
            {
                previous.Primary.localScale = previous.PrimaryScale;
                previous.Primary.anchoredPosition = previous.Position;
                previous.PrimaryLayout.ignoreLayout = previous.PrimaryIgnoredLayout;
            }
            if (previous.Secondary != null)
            {
                previous.Secondary.localScale = previous.SecondaryScale;
                previous.SecondaryLayout.ignoreLayout = previous.SecondaryIgnoredLayout;
            }
            if (previous.Placeholder != null)
            {
                previous.Placeholder.gameObject.SetActive(false);
                Object.Destroy(previous.Placeholder.gameObject);
            }
            RemoveLabel(previous.PrimaryLabel);
            RemoveLabel(previous.SecondaryLabel);
        }

        private static void RemoveLabel(TMP_Text label)
        {
            if (label != null)
            {
                label.gameObject.SetActive(false);
                Object.Destroy(label.gameObject);
            }
        }

        private static HarmonyMethod Patch(string name) => new HarmonyMethod(AccessTools.Method(typeof(WeaponSlotsUI), name));
    }
}
