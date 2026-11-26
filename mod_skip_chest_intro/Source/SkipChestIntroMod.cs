using System;
using System.Reflection;
using FrontierTale.Modding;
using UnityEngine;
using V.Framework;
using V.UI;

namespace SkipChestIntro
{
    public sealed class SkipChestIntroMod : GameMod
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private FieldInfo searchCoroutine;
        private FieldInfo unlockReady;
        private FieldInfo unlockSuccessAction;
        private FieldInfo unlockAnimation;
        private Action<WildBagUI> completeSearch;
        private Action<UnlockingUI> completeUnlock;
        private Action<UnlockingUI> resetUnlockVisuals;

        public override void Load(ModContext context)
        {
            searchCoroutine = GetField(typeof(WildBagUI), "pickSearchCoroutine", typeof(Coroutine));
            unlockReady = GetField(typeof(UICanvas), "clickMask", typeof(Action));
            unlockSuccessAction = GetField(typeof(UnlockingUI), "successAction", typeof(Action));
            unlockAnimation = GetField(typeof(UnlockingUI), "actionSequence");
            completeSearch = Bind<WildBagUI>("CompletePickSearchImmediately");
            completeUnlock = Bind<UnlockingUI>("CompleteUnlockSuccess");
            resetUnlockVisuals = Bind<UnlockingUI>("ResetVisualState");

            Canvas.preWillRenderCanvases += SkipChestIntro;
            Debug.Log("[SkipChestIntro] Loaded");
        }

        public override void Unload() => Canvas.preWillRenderCanvases -= SkipChestIntro;

        public override void OnFrame() => SkipChestIntro();

        private void SkipChestIntro()
        {
            if (!UIManager.IsActive())
                return;

            var ui = UIManager.Instance;
            TryAutoUnlock(ui);
            // Unlocking can open the chest in the same frame. The stock search yields before its first effect.
            if (ui.GetTopUICanvas() is WildBagUI chest && chest != null && chest.isActiveAndEnabled &&
                searchCoroutine.GetValue(chest) != null)
                completeSearch(chest);
        }

        private void TryAutoUnlock(UIManager ui)
        {
            if (!(ui.GetTopUICanvas() is UnlockingUI unlocking) || unlocking == null ||
                !unlocking.isActiveAndEnabled || ui.GetActiveInputTopCanvas() != null ||
                unlockReady.GetValue(unlocking) == null ||
                !(unlockSuccessAction.GetValue(unlocking) is Action success) ||
                !(success.Target is MapWildBag chest) || chest == null)
                return;

            // The panel assigns clickMask after initialization. Clear an existing tween before native success.
            if (unlockAnimation.GetValue(unlocking) != null)
                resetUnlockVisuals(unlocking);
            completeUnlock(unlocking);
        }

        private static FieldInfo GetField(Type owner, string name, Type fieldType = null)
        {
            FieldInfo field = owner.GetField(name, PrivateInstance);
            if (field == null || (fieldType != null && field.FieldType != fieldType))
                throw new MissingFieldException(owner.FullName, name);
            return field;
        }

        private static Action<T> Bind<T>(string name)
        {
            Type owner = typeof(T);
            MethodInfo method = owner.GetMethod(name, PrivateInstance, null, Type.EmptyTypes, null)
                ?? throw new MissingMethodException(owner.FullName, name);
            return (Action<T>)method.CreateDelegate(typeof(Action<T>));
        }
    }
}
