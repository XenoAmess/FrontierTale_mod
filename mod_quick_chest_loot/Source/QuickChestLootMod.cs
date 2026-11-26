using System;
using System.Collections.Generic;
using System.Reflection;
using FrontierTale.Modding;
using UnityEngine;
using V;
using V.Framework;
using V.UI;

namespace QuickChestLoot
{
    public sealed class QuickChestLootMod : GameMod
    {
        private const double InitialPickupDelay = 0.7;
        private const double PickupInterval = 0.35;
        private const int CoinType = 7;
        private const string AutomaticPickupPreference = "frontiertale.quick-chest-loot.automatic-pickup";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private FieldInfo searchCoroutine;
        private FieldInfo pickSlots;
        private FieldInfo unlockReady;
        private FieldInfo unlockSuccessAction;
        private FieldInfo unlockAnimation;
        private Action<WildBagUI> completeSearch;
        private Action<WildBagUI> closeChest;
        private Action<UnlockingUI> completeUnlock;
        private Action<UnlockingUI> resetUnlockVisuals;
        private Func<WildBagUI, BagItem, bool> takeItem;
        private Func<WildBagUI, bool> itemInteractionBlocked;
        private Action showBagFullWarning;
        private Func<UserDataManager, int, ulong, int> maxStackCount;
        private Func<UserDataManager, List<CommonData>, ulong, int, bool> hasReceiveSpace;

        private List<BagItem> activePickSlots;
        private ModContext context;
        private UICanvas settingsHost;
        private double nextSettingsScan;
        private bool automaticPickup;
        private bool finished;
        private double nextPickupTime;

        public override void Load(ModContext context)
        {
            this.context = context;
            automaticPickup = PlayerPrefs.GetInt(AutomaticPickupPreference, 1) != 0;
            searchCoroutine = GetField(typeof(WildBagUI), "pickSearchCoroutine", typeof(Coroutine));
            pickSlots = GetField(typeof(WildBagUI), "totalPickItems", typeof(List<BagItem>));
            unlockReady = GetField(typeof(UICanvas), "clickMask", typeof(Action));
            unlockSuccessAction = GetField(typeof(UnlockingUI), "successAction", typeof(Action));
            unlockAnimation = GetField(typeof(UnlockingUI), "actionSequence");
            completeSearch = Bind<Action<WildBagUI>>(typeof(WildBagUI), "CompletePickSearchImmediately");
            closeChest = Bind<Action<WildBagUI>>(typeof(WildBagUI), "ClosePanel");
            completeUnlock = Bind<Action<UnlockingUI>>(typeof(UnlockingUI), "CompleteUnlockSuccess");
            resetUnlockVisuals = Bind<Action<UnlockingUI>>(typeof(UnlockingUI), "ResetVisualState");
            takeItem = Bind<Func<WildBagUI, BagItem, bool>>(typeof(WildBagUI), "TakeCurrentPickItem", typeof(BagItem));
            itemInteractionBlocked = Bind<Func<WildBagUI, bool>>(typeof(WildBagUI), "get_IsItemInfoBlocked");
            MethodInfo warning = typeof(WildBagUI).GetMethod("ShowBagFullWarning", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(WildBagUI).FullName, "ShowBagFullWarning");
            showBagFullWarning = (Action)warning.CreateDelegate(typeof(Action));
            maxStackCount = Bind<Func<UserDataManager, int, ulong, int>>(typeof(UserDataManager),
                "GetItemMaxStackCount", typeof(int), typeof(ulong));
            hasReceiveSpace = Bind<Func<UserDataManager, List<CommonData>, ulong, int, bool>>(typeof(UserDataManager),
                "HasItemReceiveSpace", typeof(List<CommonData>), typeof(ulong), typeof(int));

            Canvas.preWillRenderCanvases += RevealBeforeRender;
            Debug.Log("[QuickChestLoot] Loaded");
        }

        public override void Unload()
        {
            Canvas.preWillRenderCanvases -= RevealBeforeRender;
            activePickSlots = null;
            settingsHost = null;
        }

        private void RevealBeforeRender()
        {
            if (!UIManager.IsActive())
                return;

            var ui = UIManager.Instance;
            TryAutoUnlock(ui);
            if (ui.GetTopUICanvas() is WildBagUI chest)
                SkipSearch(chest);
        }

        private void TryAutoUnlock(UIManager ui)
        {
            if (!(ui.GetTopUICanvas() is UnlockingUI unlocking) || unlocking == null ||
                !unlocking.isActiveAndEnabled || ui.GetActiveInputTopCanvas() != null ||
                unlockReady.GetValue(unlocking) == null ||
                !(unlockSuccessAction.GetValue(unlocking) is Action success) ||
                !(success.Target is MapWildBag chest) || chest == null)
                return;

            // CreateUI assigns clickMask after initialization. Stop an existing tween before
            // the native success method clears its reference and closes the lock UI.
            if (unlockAnimation.GetValue(unlocking) != null)
                resetUnlockVisuals(unlocking);
            completeUnlock(unlocking);
        }

        private void SkipSearch(WildBagUI chest)
        {
            // The stock coroutine yields before playing its first effect. Its handle also
            // guards against a Canvas callback while the chest is still initializing.
            if (chest != null && chest.isActiveAndEnabled && searchCoroutine.GetValue(chest) != null)
                completeSearch(chest);
        }

        public override void OnFrame()
        {
            if (!UIManager.IsActive())
                return;

            var ui = UIManager.Instance;
            var top = ui.GetTopUICanvas();
            if (top is StartUI || top is GeneralUI)
                UpdateSettings(top);
            TryAutoUnlock(ui);
            if (!(ui.GetTopUICanvas() is WildBagUI chest) || chest == null || !chest.isActiveAndEnabled)
                return;

            SkipSearch(chest);
            if (!automaticPickup)
                return;

            var slots = (List<BagItem>)pickSlots.GetValue(chest);
            if (slots == null)
                return;

            // The canvas is pooled. WildBagUI creates a fresh slot list for each opening.
            if (activePickSlots != slots)
            {
                activePickSlots = slots;
                finished = false;
                nextPickupTime = Time.unscaledTimeAsDouble + InitialPickupDelay;
            }

            if (!Application.isFocused || ui.GetActiveInputTopCanvas() != null || !UserDataManager.IsActive())
                return;

            InputManager input = InputManager.Instance;
            if (input == null || !input.interactable || input.IsRebinding || itemInteractionBlocked(chest))
                return;

            if (!slots.Exists(slot => HasLoot(slot)))
            {
                closeChest(chest);
                return;
            }

            if (finished || Time.unscaledTimeAsDouble < nextPickupTime)
                return;

            var inventory = UserDataManager.Instance;
            bool hasUnfittingItem = false;
            foreach (BagItem slot in slots)
            {
                if (!HasLoot(slot) || slot.data.isLock)
                    continue;

                CommonData item = slot.data;
                // Match the chest's GainItem path, including partial stacks and wallet coins.
                if (item.type != CoinType && !hasReceiveSpace(inventory, inventory.userData.takeBag,
                    item.configID, maxStackCount(inventory, item.type, item.configID)))
                {
                    hasUnfittingItem = true;
                    continue;
                }

                finished = !takeItem(chest, slot);
                nextPickupTime = Time.unscaledTimeAsDouble + PickupInterval;
                return;
            }

            finished = true;
            if (hasUnfittingItem)
                showBagFullWarning();
        }

        private void UpdateSettings(UICanvas host)
        {
            double now = Time.unscaledTimeAsDouble;
            if (host == settingsHost && now < nextSettingsScan)
                return;

            settingsHost = host;
            nextSettingsScan = now + 0.25;
            var settings = host.GetComponentInChildren<SettingNode>();
            if (settings != null && settings.GetComponentInChildren<ChestPickupSettingsRow>(true) == null)
                ChestPickupSettingsRow.Mount(context, settings, () => automaticPickup, SetAutomaticPickup);
        }

        private void SetAutomaticPickup(bool enabled)
        {
            if (automaticPickup == enabled)
                return;

            automaticPickup = enabled;
            activePickSlots = null;
            PlayerPrefs.SetInt(AutomaticPickupPreference, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        private static bool HasLoot(BagItem slot)
        {
            return slot != null && slot.data is CommonData item &&
                item.dataID != 0 && item.configID != 0 && item.num > 0;
        }

        private static FieldInfo GetField(Type owner, string name, Type fieldType = null)
        {
            FieldInfo field = owner.GetField(name, PrivateInstance);
            if (field == null || (fieldType != null && field.FieldType != fieldType))
                throw new MissingFieldException(owner.FullName, name);
            return field;
        }

        private static T Bind<T>(Type owner, string name, params Type[] parameters) where T : Delegate
        {
            MethodInfo method = owner.GetMethod(name, PrivateInstance, null, parameters, null)
                ?? throw new MissingMethodException(owner.FullName, name);
            return (T)method.CreateDelegate(typeof(T));
        }
    }
}
