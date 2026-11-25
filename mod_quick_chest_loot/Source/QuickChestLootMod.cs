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
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private FieldInfo searchCoroutine;
        private FieldInfo pickSlots;
        private Action<WildBagUI> completeSearch;
        private Func<WildBagUI, BagItem, bool> takeItem;
        private Func<WildBagUI, bool> itemInteractionBlocked;
        private Action showBagFullWarning;
        private Func<UserDataManager, int, ulong, int> maxStackCount;
        private Func<UserDataManager, List<CommonData>, ulong, int, bool> hasReceiveSpace;

        private WildBagUI activeChest;
        private bool finished;
        private double nextPickupTime;

        public override void Load(ModContext context)
        {
            searchCoroutine = GetField<Coroutine>("pickSearchCoroutine");
            pickSlots = GetField<List<BagItem>>("totalPickItems");
            completeSearch = Bind<Action<WildBagUI>>(typeof(WildBagUI), "CompletePickSearchImmediately");
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
            activeChest = null;
        }

        private void RevealBeforeRender()
        {
            if (UIManager.IsActive() && UIManager.Instance.GetTopUICanvas() is WildBagUI chest)
                SkipSearch(chest);
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
            if (!UIManager.IsActive() || !(UIManager.Instance.GetTopUICanvas() is WildBagUI chest) ||
                chest == null || !chest.isActiveAndEnabled)
                return;

            if (activeChest != chest)
            {
                activeChest = chest;
                finished = false;
                nextPickupTime = Time.unscaledTimeAsDouble + InitialPickupDelay;
                SkipSearch(chest);
            }

            if (finished || Time.unscaledTimeAsDouble < nextPickupTime || !Application.isFocused ||
                UIManager.Instance.GetActiveInputTopCanvas() != null || !UserDataManager.IsActive())
                return;

            InputManager input = InputManager.Instance;
            if (input == null || !input.interactable || input.IsRebinding || itemInteractionBlocked(chest))
                return;

            var slots = (List<BagItem>)pickSlots.GetValue(chest);
            if (slots == null)
                return;

            var inventory = UserDataManager.Instance;
            bool hasUnfittingItem = false;
            foreach (BagItem slot in slots)
            {
                if (slot == null || slot.data == null || slot.data.dataID == 0 || slot.data.configID == 0 ||
                    slot.data.num <= 0 || slot.data.isLock)
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

        private static FieldInfo GetField<T>(string name)
        {
            FieldInfo field = typeof(WildBagUI).GetField(name, PrivateInstance);
            if (field?.FieldType != typeof(T))
                throw new MissingFieldException(typeof(WildBagUI).FullName, name);
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
