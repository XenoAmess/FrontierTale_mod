using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;
using V;
using V.Framework;

namespace Vivhite
{
    internal static class FollowerPatches
    {
        private const BindingFlags Methods = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static;
        private const string HarmonyId = "xenoamess.frontiertale.vivhite";
        private static Harmony harmony;
        private static VillagerObject profile;
        private static FollowerArt art;
        private static Action<MapVillager> sacrifice;

        internal static void Install(VillagerObject data, FollowerArt artwork)
        {
            profile = data;
            art = artwork;
            sacrifice = (Action<MapVillager>)Method(typeof(MapVillager), "Die").CreateDelegate(typeof(Action<MapVillager>));
            harmony = new Harmony(HarmonyId);

            Prefix(typeof(MapVillager), nameof(MapVillager.CreateAccompanyAsync), nameof(UseVivhiteData),
                typeof(VillagerData), typeof(bool), typeof(bool), typeof(GameObject), typeof(VillagerObject),
                typeof(System.Threading.CancellationToken));
            Prefix(typeof(MapVillager), "GetAttackBlockReason", nameof(RequireEquippedWeapon));
            Prefix(typeof(MapVillager), "ShouldIgnoreArrowAmmo", nameof(UseInfiniteAmmunition), typeof(WeaponConfig));
            Prefix(typeof(CharacterVitals), "HandleHealthStateChange", nameof(InterceptPlayerDeath), typeof(EnumConfig.HealthState));
            harmony.Patch(Method(typeof(CharacterSkin), "ApplyVillagerSkin",
                    typeof(VillagerData), typeof(VillagerObject), typeof(bool), typeof(bool), typeof(string)),
                prefix: PatchMethod(nameof(HideFallbackWeapon)), postfix: PatchMethod(nameof(AnimateHeadDirection)));
            Prefix(typeof(CharacterSkin), nameof(CharacterSkin.ApplyCombinedSkin), nameof(RegisterHeadSkin),
                typeof(Skeleton), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string));
            Prefix(typeof(LocalizationManager), nameof(LocalizationManager.GetText), nameof(Localize), typeof(string));
            Prefix(typeof(LoadUtils), nameof(LoadUtils.LoadSprite), nameof(LoadAvatar), typeof(string), typeof(string));
            Prefix(typeof(LoadUtils), nameof(LoadUtils.LoadSpriteHaveCache), nameof(LoadAvatar), typeof(string), typeof(string));
            Prefix(typeof(DialogueUI), "SwitchSpriteSmoothly", nameof(ShowPortrait), typeof(Image), typeof(string), typeof(float));

            foreach (Type bag in new[] { typeof(TakeBagUI), typeof(WarehouseUI), typeof(WildBagUI) })
            {
                Prefix(bag, "ShowAccompanyDefaultWeapon", nameof(HideDefaultWeaponPanel));
                Prefix(bag, "ShowDefaultWeaponInfo", nameof(SkipDefaultWeaponInfo));
                Type[] parameters = bag == typeof(WarehouseUI) ? new[] { typeof(int), typeof(bool) } : new[] { typeof(int) };
                harmony.Patch(Method(bag, "ShowEquipNode", parameters), postfix: PatchMethod(nameof(HidePanelAfterTabChange)));
            }
            Prefix(typeof(WarehouseUI), "ShowAccompanyDefaultWeaponOnce", nameof(HideDefaultWeaponPanel));
        }

        internal static void Remove()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            art = null;
            profile = null;
            sacrifice = null;
        }

        private static bool IsVivhite(VillagerData data) => data?.npcID == VivhiteMod.NpcId;

        private static void UseVivhiteData(VillagerData data, ref VillagerObject actorData)
        {
            // The stock spawn path otherwise loads the asset by addressable key.
            if (IsVivhite(data))
                actorData = profile;
        }

        private static bool RequireEquippedWeapon(MapVillager __instance, ref string __result, bool ___usingDefaultWeapon)
        {
            if (!IsVivhite(__instance.data) || !___usingDefaultWeapon)
                return true;
            __result = "vivhiteNoEquippedWeapon";
            return false;
        }

        private static bool UseInfiniteAmmunition(MapVillager __instance, WeaponConfig config, ref bool __result)
        {
            if (!IsVivhite(__instance.data) || config == null || !config.UsesMagazine)
                return true;
            __result = true;
            return false;
        }

        private static bool InterceptPlayerDeath(CharacterVitals __instance, EnumConfig.HealthState state, CharacterCtrl ___ctrl)
        {
            if (state != EnumConfig.HealthState.Dead || ___ctrl == null || __instance.HP.CurrentValue > 0)
                return true;

            MapVillager follower = ___ctrl.accompany;
            if (follower == null || !IsVivhite(follower.data) || !follower.isAccompany ||
                !follower.isActiveAndEnabled || !follower.IsAlive || follower.hp <= 0 ||
                float.IsNaN(follower.hp) || float.IsInfinity(follower.hp) || __instance.HP.Value <= 0)
                return true;

            float health = follower.hp;
            follower.hp = 0;
            sacrifice(follower);
            // Assign the exact transfer through the native cap and broadcasts, bypassing treatment modifiers.
            __instance.HP.CurrentValue = health;
            return false;
        }

        private static void HideFallbackWeapon(VillagerData villagerData, ref bool showWeapon, bool usingDefaultWeapon)
        {
            if (IsVivhite(villagerData) && usingDefaultWeapon)
                showWeapon = false;
        }

        private static void AnimateHeadDirection(VillagerData villagerData, SkeletonAnimation ___skeletonAnimation)
        {
            if (IsVivhite(villagerData))
                art.BindDirection(___skeletonAnimation);
        }

        private static void RegisterHeadSkin(Skeleton skeleton, ref string headSkin, ref string handSkin, ref string legSkin)
        {
            if (headSkin != "Head/" + VivhiteMod.HeadSkin)
                return;

            headSkin = art.GetHeadSkin(skeleton);
            // These native rig skins dress the empty slots. Equipment continues to supply its own skin.
            if (handSkin == "Hand/Hand_00")
                handSkin = "Hand/Hand_MaBuChanShou";
            if (legSkin == "Leg/Leg_00")
                legSkin = "Leg/Leg_LvZheChangXue";
        }

        private static bool Localize(LocalizationManager __instance, string key, ref string __result)
        {
            bool english = __instance.CurrentLanguage == GameLanguage.English;
            switch (key)
            {
                case VivhiteMod.NameKey:
                    __result = english ? "Vivhite" : "白绮";
                    break;
                case VivhiteMod.DescriptionKey:
                    __result = english ? "Genius magical girl" : "超天才魔法少女";
                    break;
                case VivhiteMod.AbilityKey:
                    __result = english
                        ? "Genius magical girl. Infinite ammunition for equipped ranged weapons. While following and alive, sacrifices herself on lethal damage and transfers her remaining HP to the player."
                        : "超天才魔法少女。装备的远程武器弹药无限。跟随且存活时，主角受到致命伤害，白绮牺牲自己，将全部剩余生命转给主角。";
                    break;
                case VivhiteMod.QuoteKey:
                    __result = "……麻烦死了";
                    break;
                default:
                    return true;
            }
            return false;
        }

        private static bool LoadAvatar(string spriteName, ref Sprite __result)
        {
            if (spriteName != VivhiteMod.AvatarAsset)
                return true;
            __result = art.Avatar;
            return false;
        }

        private static bool ShowPortrait(Image targetImage, string assetPath)
        {
            if (assetPath != VivhiteMod.PortraitAsset)
                return true;
            if (targetImage != null)
            {
                targetImage.sprite = art.Portrait;
                targetImage.color = Color.white;
                targetImage.gameObject.SetActive(true);
            }
            return false;
        }

        private static bool CurrentFollowerIsVivhite() =>
            UserDataManager.IsActive() && IsVivhite(UserDataManager.Instance.GetAccompanyData());

        private static bool SkipDefaultWeaponInfo() => !CurrentFollowerIsVivhite();

        private static bool HideDefaultWeaponPanel(Image ___defaultNode)
        {
            if (!CurrentFollowerIsVivhite())
                return true;
            ___defaultNode?.gameObject.SetActive(false);
            return false;
        }

        private static void HidePanelAfterTabChange(Image ___defaultNode)
        {
            if (CurrentFollowerIsVivhite())
                ___defaultNode?.gameObject.SetActive(false);
        }

        private static void Prefix(Type owner, string original, string patch, params Type[] parameters) =>
            harmony.Patch(Method(owner, original, parameters), prefix: PatchMethod(patch));

        private static HarmonyMethod PatchMethod(string name) =>
            new HarmonyMethod(Method(typeof(FollowerPatches), name, null));

        private static MethodInfo Method(Type owner, string name, params Type[] parameters)
        {
            MethodInfo method = parameters == null ? owner.GetMethod(name, Methods) :
                owner.GetMethod(name, Methods, null, parameters, null);
            return method ?? throw new MissingMethodException(owner.FullName, name);
        }
    }
}
