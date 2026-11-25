using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using V;
using V.UI;

namespace Vivhite
{
    internal static class DualWeapons
    {
        internal const int MainSlot = 0;
        internal const int OffSlot = 8;

        private static readonly FieldInfo EquipData = AccessTools.Field(typeof(VillagerData), nameof(VillagerData.equipDatas));
        private static readonly ConditionalWeakTable<VillagerData, ActiveHand> Hands = new ConditionalWeakTable<VillagerData, ActiveHand>();
        private static readonly ConditionalWeakTable<CommonData, Weapon> Weapons = new ConditionalWeakTable<CommonData, Weapon>();
        private static readonly ConditionalWeakTable<MapVillager, Transform> OffhandMuzzles = new ConditionalWeakTable<MapVillager, Transform>();
        private static readonly AccessTools.FieldRef<MapVillager, Transform> Muzzle = AccessTools.FieldRefAccess<MapVillager, Transform>("midNode");
        private static readonly AccessTools.FieldRef<MapVillager, float> Windup = AccessTools.FieldRefAccess<MapVillager, float>("preAtkAni");
        private static readonly AccessTools.FieldRef<MapVillager, Coroutine> WindupRoutine = AccessTools.FieldRefAccess<MapVillager, Coroutine>("attackPreAniCoroutine");
        private static readonly AccessTools.FieldRef<MapVillager, VillagerModelCtrl> Model =
            AccessTools.FieldRefAccess<MapVillager, VillagerModelCtrl>("villagerModelCtrl");
        private static readonly AccessTools.FieldRef<MapVillager, WeaponDurabilityAttackContext> NativeDurability =
            AccessTools.FieldRefAccess<MapVillager, WeaponDurabilityAttackContext>("currentDurabilityAttackContext");
        private static readonly AccessTools.FieldRef<MapVillager, ulong> DurabilitySequence =
            AccessTools.FieldRefAccess<MapVillager, ulong>("durabilityAttackSequence");
        private static readonly Action<MapVillager> PlayAttack = Delegate<Action<MapVillager>>("PlayAtkAni");
        private static readonly Action<MapVillager> FinishAttack = Delegate<Action<MapVillager>>("OnTrackAniComplete");
        private static readonly Func<MapVillager, Vector2> MeleeArc = Delegate<Func<MapVillager, Vector2>>("GetMeleeAttackAngle");
        private static readonly Func<MapVillager, Vector3, MapMonster, CommonData, bool> ClearShot =
            Delegate<Func<MapVillager, Vector3, MapMonster, CommonData, bool>>("HasClearRangedShot", typeof(Vector3), typeof(MapMonster), typeof(CommonData));
        [ThreadStatic]
        private static CommonData damageWeapon;

        private sealed class ActiveHand
        {
            internal int Index;
            internal ulong ItemId;
        }

        private sealed class Weapon
        {
            internal CommonData Item;
            internal ulong ItemId;
            internal ulong ConfigId;
            internal string Skin;
            internal WeaponConfig Config;
            internal bool Valid => Item.dataID == ItemId && Item.configID == ConfigId && Config != null &&
                Item.extendDataInfo?.hasInitializedDurability == true && Item.extendDataInfo.currentDurability > 0;
            internal bool Ready => Valid && Item.remainCd <= 0f;
        }

        internal static bool IsVivhite(VillagerData data) => data?.npcID == VivhiteMod.NpcId;

        internal static void EnsureSlots(VillagerData data)
        {
            if (!IsVivhite(data))
                return;
            if (data.equipDatas == null || data.equipDatas.Length <= OffSlot)
            {
                var slots = new VillagerData().equipDatas;
                Array.Resize(ref slots, OffSlot + 1);
                if (data.equipDatas != null)
                    for (int i = 0; i < data.equipDatas.Length; i++)
                        slots[i] = data.equipDatas[i] ?? slots[i];
                data.equipDatas = slots;
            }
            if (data.equipDatas[OffSlot] == null)
                data.equipDatas[OffSlot] = new CommonData { type = 1, subType = 1 };
        }

        internal static void Install(Harmony harmony)
        {
            foreach (string method in new[] { "UpdateBasicStates", "CanUseGuardRangedForOutsideTarget", "GetCurrentWeaponData",
                "GetAttackBlockReason", "EnsureAttackCdCoroutineRunning", "PlayAtkAni", "BeginDurabilityAttack",
                "SwitchWeaponCD", "IsPrimaryWeaponBroken", "RefreshWeaponByDurability" })
            {
                harmony.Patch(AccessTools.Method(typeof(MapVillager), method), transpiler: Patch(nameof(ActiveSlotAccess)));
            }
            harmony.Patch(AccessTools.Method(typeof(MapVillager), "FixedUpdate"), prefix: Patch(nameof(TickWeapons)));
            harmony.Patch(AccessTools.Method(typeof(MapVillager), "SwitchWeaponCD"), prefix: Patch(nameof(SelectEquippedHand)));
            harmony.Patch(AccessTools.Method(typeof(MapVillager), "EntryAtkCD"), prefix: Patch(nameof(UseIndependentCooldowns)));
            harmony.Patch(AccessTools.Method(typeof(MapVillager), "EnsureAttackCdCoroutineRunning"), prefix: Patch(nameof(SkipStockCooldownStart)));
            harmony.Patch(AccessTools.Method(typeof(MapVillager), "EndAtkPreAni"), prefix: Patch(nameof(UseVolley)));
            harmony.Patch(AccessTools.Method(typeof(MapVillager), "IsOutOfResource", new[] { typeof(WeaponConfig), typeof(CommonData) }),
                prefix: Patch(nameof(FreeStaffUse)));
            harmony.Patch(AccessTools.Method(typeof(UserDataManager), nameof(UserDataManager.ConsumeWeaponDurability)),
                transpiler: Patch(nameof(ResolveDurabilitySlot)));
            harmony.Patch(AccessTools.Method(typeof(WeaponConfig), "GetBaseDamageRange"),
                transpiler: Patch(nameof(ResolveDamageSlot)));
            harmony.Patch(AccessTools.Method(typeof(WeaponConfig), nameof(WeaponConfig.GetNonCritDamageRange), new[] { typeof(MapVillager) }),
                transpiler: Patch(nameof(ActiveSlotAccess)));
        }

        internal static CommonData ActiveWeapon(VillagerData data)
        {
            int slot = IsVivhite(data) ? Hands.GetOrCreateValue(data).Index : MainSlot;
            return data.equipDatas[slot];
        }

        internal static string ActiveSkin(VillagerData data) => ReadWeapon(ActiveWeapon(data))?.Skin;

        private static void SelectEquippedHand(MapVillager __instance)
        {
            VillagerData data = __instance.data;
            if (!IsVivhite(data))
                return;
            EnsureSlots(data);
            var hand = Hands.GetOrCreateValue(data);
            if (ReadWeapon(data.equipDatas[hand.Index])?.Valid != true)
            {
                hand.Index = ReadWeapon(data.equipDatas[MainSlot])?.Valid == true ||
                    ReadWeapon(data.equipDatas[OffSlot])?.Valid != true ? MainSlot : OffSlot;
            }
            hand.ItemId = data.equipDatas[hand.Index].dataID;
        }

        private static void TickWeapons(MapVillager __instance, bool ___isDead, bool ___inSpecialEvent, bool ___waitingHitRecoverHurtComplete)
        {
            VillagerData data = __instance.data;
            if (!IsVivhite(data) || !__instance.isAccompany || ___isDead || !__instance.isActiveAndEnabled ||
                (UIManager.IsActive() && UIManager.Instance.IsGameplayWorldPaused))
                return;

            EnsureSlots(data);
            var main = ReadWeapon(data.equipDatas[MainSlot]);
            var off = ReadWeapon(data.equipDatas[OffSlot]);
            float elapsed = Time.fixedDeltaTime * Mathf.Max(0f,
                1f + __instance.GetAttributeSystem().GetValue(EnumConfig.AdjustAttributeType.AttackSpeedPercent) / 100f);
            TickCooldown(__instance.GetConfig(), main, elapsed);
            TickCooldown(__instance.GetConfig(), off, elapsed);
            if (__instance.inAttackAni || __instance.inTp || ___inSpecialEvent || ___waitingHitRecoverHurtComplete)
                return;

            var hand = Hands.GetOrCreateValue(data);
            var target = __instance.mapMonster;
            int slot = ChooseHand(__instance, target, main, off);
            ulong itemId = data.equipDatas[slot].dataID;
            if (slot != hand.Index || itemId != hand.ItemId)
            {
                hand.Index = slot;
                __instance.SwitchWeaponCD();
            }
        }

        private static int ChooseHand(MapVillager actor, MapMonster target, Weapon main, Weapon off)
        {
            if (off?.Valid != true)
                return MainSlot;
            if (main?.Valid != true)
                return OffSlot;
            if (target == null || !target.IsAlive)
                return OffSlotIfOnlyReady(main, off);
            bool mainCanReach = CanReach(actor, target, main);
            if (main.Ready && mainCanReach)
                return MainSlot;
            bool offCanReach = CanReach(actor, target, off);
            if (off.Ready && offCanReach)
                return OffSlot;
            if (mainCanReach)
                return MainSlot;
            if (offCanReach)
                return OffSlot;
            return Range(actor, off.Config) > Range(actor, main.Config) ? OffSlot : MainSlot;
        }

        private static int OffSlotIfOnlyReady(Weapon main, Weapon off) => !main.Ready && off.Ready ? OffSlot : MainSlot;

        private static void TickCooldown(VillagerObject actor, Weapon weapon, float elapsed)
        {
            if (weapon?.Valid != true || weapon.Item.remainCd <= 0f)
                return;
            WeaponConfig config = weapon.Config;
            float factor = config.weaponType2 switch
            {
                1 => actor.atkInt_1H,
                2 => actor.atkInt_2H,
                6 => actor.atkInt_bow,
                11 => actor.atkInt_gun,
                21 => actor.atkInt_staff,
                _ => config.IsGunLikeWeapon ? actor.atkInt_gun : 100f
            };
            weapon.Item.remainCd = Mathf.Max(0f, weapon.Item.remainCd - elapsed * factor / 100f);
        }

        private static bool SkipStockCooldownStart(MapVillager __instance) => !IsVivhite(__instance.data);

        private static bool UseIndependentCooldowns(MapVillager __instance, ref IEnumerator __result, ref bool ___inCD)
        {
            if (!IsVivhite(__instance.data))
                return true;
            ___inCD = false;
            __result = Array.Empty<object>().GetEnumerator();
            return false;
        }

        private static bool FreeStaffUse(MapVillager __instance, WeaponConfig config, ref bool __result)
        {
            if (!IsVivhite(__instance.data) || config?.IsStaffWeapon != true)
                return true;
            __result = false;
            return false;
        }

        private static bool UseVolley(MapVillager __instance, ref IEnumerator __result)
        {
            if (!IsVivhite(__instance.data))
                return true;
            __result = Volley(__instance);
            return false;
        }

        private static IEnumerator Volley(MapVillager actor)
        {
            MapMonster target = actor.mapMonster;
            VillagerData data = actor.data;
            var active = ReadWeapon(ActiveWeapon(data));
            if (active?.Ready != true)
            {
                yield return null;
                WindupRoutine(actor) = null;
                yield break;
            }
            int otherIndex = Hands.GetOrCreateValue(data).Index == MainSlot ? OffSlot : MainSlot;
            var other = ReadWeapon(data.equipDatas[otherIndex]);
            if (other?.Ready != true || !CanReach(actor, target, other))
                other = null;

            var animationState = Model(actor).GetSkeletonAnimation().AnimationState;
            var previousTrack = animationState.GetCurrent(0);
            PlayAttack(actor);
            var attackTrack = animationState.GetCurrent(0);
            bool waitsForAnimation = attackTrack != null && attackTrack != previousTrack &&
                attackTrack.Animation.Name.StartsWith("attack_WQ", StringComparison.Ordinal);
            ulong activeId = active.ItemId;
            var durability = NativeDurability(actor);
            yield return new WaitForSeconds(Windup(actor));

            if (actor.isAccompany && actor.IsAlive && actor.inAttackAni && !actor.inTp && target != null && target.IsAlive)
            {
                var secondary = other?.Ready == true && data.equipDatas[otherIndex].dataID == other.ItemId &&
                    CanReach(actor, target, other) ? other : null;
                if (ActiveWeapon(data).dataID == activeId && active.Valid && CanReach(actor, target, active))
                    Discharge(actor, target, active, durability, false);
                if (secondary != null)
                {
                    var context = new WeaponDurabilityAttackContext(++DurabilitySequence(actor),
                        WeaponDurabilityOwnerType.Accompany, data.dataID, secondary.ItemId, actor.GetAttributeSystem());
                    try
                    {
                        if (Discharge(actor, target, secondary, context, true))
                            secondary.Item.remainCd = actor.GetFinalValue(EnumConfig.FinalStatType.AttackInterval, secondary.Config.interval) / 100f;
                    }
                    finally
                    {
                        context.CompleteAction();
                    }
                }
            }
            WindupRoutine(actor) = null;
            // A weapon with no rig clip still fires, and has no animation completion callback.
            if (!waitsForAnimation && actor.inAttackAni)
                FinishAttack(actor);
        }

        private static bool Discharge(MapVillager actor, MapMonster target, Weapon hand, WeaponDurabilityAttackContext context, bool second)
        {
            // Native damage resolution reads the equipped item's upgrade multiplier synchronously.
            // Two weapons can have identical configs while carrying different instance modifiers.
            CommonData previous = damageWeapon;
            damageWeapon = hand.Item;
            try
            {
                return PerformAttack(actor, target, hand, context, second);
            }
            finally
            {
                damageWeapon = previous;
            }
        }

        private static bool PerformAttack(MapVillager actor, MapMonster target, Weapon hand, WeaponDurabilityAttackContext context, bool second)
        {
            var config = hand.Config;
            Vector3 end = target.GetMidNode();
            Vector3 origin = actor.GetMidNode();
            Vector3 direction = (end - origin).normalized;
            if (config.IsMeleeWeapon)
            {
                BattleUtils.DetectTargetsInSector(config, actor.transform, MeleeArc(actor),
                    actor.GetAttributeSystem().GetFinalArmor(), false, direction, 0f, context);
                return true;
            }

            var manager = UserDataManager.Instance;
            BulletInfo magazine = config.UsesMagazine ? manager.EnsureMagazineWeaponBulletInfo(hand.Item) : null;
            ulong projectile = magazine?.bulletId ?? manager.GetWeaponFirstConnectProjId(hand.Item);
            if (projectile == 0)
                return false;

            Transform muzzle = Muzzle(actor);
            if (second)
            {
                Vector3 offset = Vector3.Cross(Vector3.up, direction).normalized * 0.24f;
                Vector3 offhandPosition = muzzle.position + offset;
                if (!ClearShot(actor, offhandPosition, target, hand.Item))
                    return false;
                muzzle = OffhandMuzzles.GetValue(actor, CreateOffhandMuzzle);
                muzzle.position = offhandPosition;
                direction = (end - offhandPosition).normalized;
            }
            actor.equipVolume = config.volumeDict == null ? null : new Dictionary<int, int>(config.volumeDict);
            if (second && !string.IsNullOrEmpty(config.atkSound))
                AudioUtils.PlayOneShot("event:/Sound Effects/Battle/" + config.atkSound, actor.transform.position);
            var spread = new BattleUtils.DiscParam
            {
                direction = direction,
                spreadHalfAngleRad = config.GetCurrentSpreadHalfAngleRad(config.GetCrosshairInitScale(), false)
            };
            bool launched = false;
            BattleUtils.RemoteAttack(false, config, actor, projectile, false, 0f,
                Mathf.Max(1, magazine?.loadBulletNum ?? 1), muzzle, spread, end, () => launched = true, context);
            return launched;
        }

        private static Transform CreateOffhandMuzzle(MapVillager actor)
        {
            var muzzle = new GameObject("Vivhite Offhand Muzzle").transform;
            muzzle.SetParent(actor.transform, false);
            return muzzle;
        }

        private static bool CanReach(MapVillager actor, MapMonster target, Weapon hand)
        {
            if (target == null || !target.IsAlive)
                return false;
            var config = hand.Config;
            if (config.IsMeleeWeapon)
                return BattleUtils.CanTargetBeHitInSector(config, actor.transform, MeleeArc(actor), false, target);
            Vector3 distance = target.GetMidNode() - actor.GetMidNode();
            float range = Range(actor, config) + 0.35f;
            return distance.x * distance.x + distance.z * distance.z <= range * range &&
                ClearShot(actor, actor.GetMidNode(), target, hand.Item);
        }

        private static float Range(MapVillager actor, WeaponConfig config)
        {
            float value = actor.GetFinalValue(EnumConfig.FinalStatType.AttackRange, config.distanceList[0]) / 100f;
            return value > 0f ? value : 2f;
        }

        private static Weapon ReadWeapon(CommonData item)
        {
            if (item == null || item.dataID == 0 || item.configID == 0)
                return null;
            if (Weapons.TryGetValue(item, out Weapon cached) && cached.ItemId == item.dataID && cached.ConfigId == item.configID)
                return cached;
            Weapons.Remove(item);
            if (!ConfigUtility.TryGetWeaponEquipmentConfig(item, out _, out ulong id))
                return null;
            var config = ConfigDataStore.GetDataJObject<WeaponConfig>("WeaponConfig", id);
            if (config?.distanceList?.Count > 0)
            {
                var weapon = new Weapon { Item = item, ItemId = item.dataID, ConfigId = item.configID,
                    Skin = ConfigDataStore.GetDataJToken("EquipmentConfig", id, "path")?.ToString(), Config = config };
                Weapons.Add(item, weapon);
                return weapon;
            }
            return null;
        }

        private static CommonData DurabilityWeapon(VillagerData data, ulong weaponId)
        {
            if (IsVivhite(data) && data.equipDatas.Length > OffSlot && data.equipDatas[OffSlot]?.dataID == weaponId)
                return data.equipDatas[OffSlot];
            return data.equipDatas[MainSlot];
        }

        private static IEnumerable<CodeInstruction> ActiveSlotAccess(IEnumerable<CodeInstruction> instructions) =>
            RewriteSlot(instructions, nameof(ActiveWeapon), false);

        private static IEnumerable<CodeInstruction> ResolveDurabilitySlot(IEnumerable<CodeInstruction> instructions) =>
            RewriteSlot(instructions, nameof(DurabilityWeapon), true);

        private static CommonData DamageWeapon(VillagerData data) =>
            IsVivhite(data) && damageWeapon != null ? damageWeapon : ActiveWeapon(data);

        private static IEnumerable<CodeInstruction> ResolveDamageSlot(IEnumerable<CodeInstruction> instructions) =>
            RewriteSlot(instructions, nameof(DamageWeapon), false);

        private static IEnumerable<CodeInstruction> RewriteSlot(IEnumerable<CodeInstruction> instructions, string replacement, bool needsId)
        {
            var body = instructions.ToList();
            int edits = 0;
            for (int i = 0; i < body.Count; i++)
            {
                var field = body[i];
                if (i + 2 < body.Count && field.LoadsField(EquipData) &&
                    body[i + 1].opcode == OpCodes.Ldc_I4_0 && body[i + 2].opcode == OpCodes.Ldelem_Ref)
                {
                    var invoke = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DualWeapons), replacement));
                    var first = needsId ? new CodeInstruction(OpCodes.Ldarg_3) : invoke;
                    for (int j = i; j <= i + 2; j++)
                    {
                        first.MoveLabelsFrom(body[j]);
                        first.MoveBlocksFrom(body[j]);
                    }
                    yield return first;
                    if (needsId)
                        yield return invoke;
                    i += 2;
                    edits++;
                }
                else
                {
                    yield return field;
                }
            }
            if (edits == 0)
                throw new InvalidOperationException("Could not bind Vivhite's active weapon slot to the native game method.");
        }

        private static T Delegate<T>(string method, params Type[] parameters) where T : Delegate =>
            (T)AccessTools.Method(typeof(MapVillager), method, parameters).CreateDelegate(typeof(T));

        private static HarmonyMethod Patch(string name) => new HarmonyMethod(AccessTools.Method(typeof(DualWeapons), name));
    }
}
