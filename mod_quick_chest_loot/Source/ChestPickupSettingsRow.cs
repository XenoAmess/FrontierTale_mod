using System;
using System.Collections;
using System.Reflection;
using FrontierTale.Modding;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using V.Framework;
using V.UI;

namespace QuickChestLoot
{
    public sealed class ChestPickupSettingsRow : MonoBehaviour
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private SettingNode owner;
        private IDictionary pages;
        private IDictionary views;
        private IDictionary visibility;
        private object gameplayPage;
        private object itemKey;
        private UIHoverButton restoreDefaults;
        private TMP_Text caption;
        private TMP_Text value;
        private GameLanguage? displayedLanguage;
        private bool displayedPickup;
        private Func<bool> read;
        private Action<bool> save;

        internal static void Mount(ModContext context, SettingNode owner, Func<bool> read, Action<bool> save)
        {
            var page = (RectTransform)owner.transform.Find("Content/ContentArea/gameSettingNode");
            var template = (RectTransform)page.Find("settingItemAutoPickRarity");
            var row = context.Attach(page, "ChestAutoPickup");
            CopyLayout(row, template);
            row.SetSiblingIndex(template.GetSiblingIndex() + 1);

            var component = row.gameObject.AddComponent<ChestPickupSettingsRow>();
            component.owner = owner;
            component.read = read;
            component.save = save;

            var highlight = CopyImage(row, template.Find("autoPickRaritySelect1").GetComponent<Image>(), "Highlight");
            highlight.raycastTarget = false;
            component.caption = CopyText(row, template.Find("autoPickRarityTxt").GetComponent<TMP_Text>(), "Caption");
            var optionTemplate = template.Find("autoPickRaritySelectBg");
            var buttonImage = CopyImage(row, optionTemplate.GetComponent<Image>(), "PickupToggle");
            buttonImage.raycastTarget = true;
            component.value = CopyText(buttonImage.transform, optionTemplate.Find("autoPickRaritySelectTxt").GetComponent<TMP_Text>(), "Value");
            var button = buttonImage.gameObject.AddComponent<ChestPickupToggleButton>();
            button.targetGraphic = buttonImage;
            component.Register(buttonImage, highlight);
            button.onClick.AddListener(() => component.SetPickup(!read()));
            button.SetChoice = component.SetPickup;

            component.restoreDefaults = owner.transform.Find("Content/BottomBtns/restoreDefaultNode/restoreDefaultBtn").GetComponent<UIHoverButton>();
            component.restoreDefaults.onClick.AddListener(component.RestoreDefault);
            component.RefreshText();
            component.SyncNavigation();
        }

        private void Register(Image button, Image highlight)
        {
            Type type = typeof(SettingNode);
            pages = (IDictionary)type.GetField("pageItems", Private).GetValue(owner);
            views = (IDictionary)type.GetField("itemViews", Private).GetValue(owner);
            visibility = (IDictionary)type.GetField("configuredItemVisibility", Private).GetValue(owner);
            gameplayPage = Enum.Parse(type.GetNestedType("SettingPage", BindingFlags.NonPublic), "Gameplay");
            Type itemType = type.GetNestedType("SettingItem", BindingFlags.NonPublic);
            int slot = -1;
            while (views.Contains(Enum.ToObject(itemType, slot)))
                slot--;
            itemKey = Enum.ToObject(itemType, slot);
            type.GetMethod("RegisterItem", Private).Invoke(owner, new object[] { itemKey, (RectTransform)transform, button, highlight });
        }

        private void Update()
        {
            SyncNavigation();
            if (displayedLanguage != LocalizationManager.Instance.CurrentLanguage || displayedPickup != read())
                RefreshText();
        }

        private void SyncNavigation()
        {
            // The host replaces the gameplay list when the settings open or the input device changes.
            var items = (IList)pages[gameplayPage];
            if (items.Contains(itemKey))
                return;

            items.Add(itemKey);
            owner.ConfigureGamepadNavigation();
        }

        private void SetPickup(bool enabled)
        {
            if (owner.IsRebindingKey || owner.IsRuntimeLeaveConfirmShowing || read() == enabled)
                return;

            save(enabled);
            RefreshText();
        }

        private void RestoreDefault()
        {
            if (isActiveAndEnabled)
                SetPickup(true);
        }

        private void RefreshText()
        {
            displayedLanguage = LocalizationManager.Instance.CurrentLanguage;
            displayedPickup = read();
            (string label, string on, string off) = displayedLanguage switch
            {
                GameLanguage.TraditionalChinese => ("寶箱自動拾取", "開啟", "關閉"),
                GameLanguage.English => ("Auto-loot chests", "On", "Off"),
                GameLanguage.Japanese => ("宝箱の自動取得", "オン", "オフ"),
                _ => ("宝箱自动拾取", "开启", "关闭")
            };
            caption.text = label;
            value.text = displayedPickup ? on : off;
        }

        private void OnDestroy()
        {
            if (restoreDefaults != null)
                restoreDefaults.onClick.RemoveListener(RestoreDefault);
            if (owner == null || itemKey == null)
                return;

            ((IList)pages[gameplayPage]).Remove(itemKey);
            views.Remove(itemKey);
            visibility.Remove(itemKey);
            if (owner.isActiveAndEnabled)
                owner.ConfigureGamepadNavigation();
        }

        private static Image CopyImage(Transform parent, Image template, string name)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            CopyLayout(image.rectTransform, template.rectTransform);
            image.sprite = template.sprite;
            image.type = template.type;
            image.pixelsPerUnitMultiplier = template.pixelsPerUnitMultiplier;
            image.color = template.color;
            return image;
        }

        private static TMP_Text CopyText(Transform parent, TMP_Text template, string name)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(parent, false);
            CopyLayout(text.rectTransform, template.rectTransform);
            text.font = template.font;
            text.fontSharedMaterial = template.fontSharedMaterial;
            text.fontStyle = template.fontStyle;
            text.fontSize = template.fontSize;
            text.fontSizeMin = template.fontSizeMin;
            text.fontSizeMax = template.fontSizeMax;
            text.enableAutoSizing = template.enableAutoSizing;
            text.alignment = template.alignment;
            text.color = template.color;
            text.raycastTarget = false;
            return text;
        }

        private static void CopyLayout(RectTransform target, RectTransform template)
        {
            target.anchorMin = template.anchorMin;
            target.anchorMax = template.anchorMax;
            target.pivot = template.pivot;
            target.sizeDelta = template.sizeDelta;
            target.anchoredPosition = template.anchoredPosition;
        }
    }

    public sealed class ChestPickupToggleButton : UIHoverButton
    {
        internal Action<bool> SetChoice;

        public override void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right)
            {
                SetChoice(eventData.moveDir == MoveDirection.Left);
                eventData.Use();
                return;
            }

            base.OnMove(eventData);
        }
    }
}
