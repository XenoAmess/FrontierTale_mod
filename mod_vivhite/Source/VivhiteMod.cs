using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using FrontierTale.Modding;
using Newtonsoft.Json.Linq;
using UnityEngine;
using V;
using V.Framework;

namespace Vivhite
{
    public sealed class VivhiteMod : GameMod
    {
        internal const ulong NpcId = 928700010;
        private const ulong ExtraId = 928700020;
        private const ulong DialogueId = 928700030;
        private const ulong MiaId = 29000010;
        internal const string DataAsset = "VivhiteFollowerData";
        internal const string HeadSkin = "Head_Vivhite";
        internal const string AvatarAsset = "VivhiteAvatar";
        internal const string PortraitAsset = "VivhitePortrait";
        internal const string NameKey = "xenoamess.vivhite.name";
        internal const string DescriptionKey = "xenoamess.vivhite.description";
        internal const string AbilityKey = "xenoamess.vivhite.ability";
        internal const string QuoteKey = "xenoamess.vivhite.quote";

        private Dictionary<string, Dictionary<ulong, JObject>> tables;
        private Dictionary<string, VillagerObject> characterAssets;
        private readonly List<(string table, ulong id)> registeredRows = new List<(string, ulong)>();
        private VillagerObject profile;
        private FollowerArt art;
        private WindowIcon windowIcon;
        private UserData seenSave;
        private bool active;

        public override void Load(ModContext context)
        {
            LoadHarmony();
            try
            {
                art = new FollowerArt(context.Folder);
                RegisterCharacter();
                InstallPatches();
                windowIcon = WindowIcon.CreateForPlayer(context.Folder);
                windowIcon?.Update();
                active = true;
                Debug.Log("[Vivhite] Loaded");
            }
            catch
            {
                Unload();
                throw;
            }
        }

        private static void LoadHarmony()
        {
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "0Harmony"))
                return;

            Assembly assembly = typeof(VivhiteMod).Assembly;
            using (Stream resource = assembly.GetManifestResourceStream("Vivhite.0Harmony.dll")
                ?? throw new InvalidDataException("Embedded Harmony assembly is missing."))
            using (var bytes = new MemoryStream())
            {
                resource.CopyTo(bytes);
                Assembly.Load(bytes.ToArray());
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void InstallPatches() => FollowerPatches.Install(profile, art);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RemovePatches() => FollowerPatches.Remove();

        private void RegisterCharacter()
        {
            const BindingFlags fields = BindingFlags.Static | BindingFlags.NonPublic;
            Type store = typeof(ConfigDataStore);
            tables = (Dictionary<string, Dictionary<ulong, JObject>>)store.GetField("configStore", fields)?.GetValue(null)
                ?? throw new MissingFieldException(store.FullName, "configStore");
            characterAssets = (Dictionary<string, VillagerObject>)store.GetField("villiger_cfg_store", fields)?.GetValue(null)
                ?? throw new MissingFieldException(store.FullName, "villiger_cfg_store");

            JObject miaNpc = tables["NpcConfig"][MiaId];
            JObject miaExtra = tables["NPCExtraConfig"][miaNpc.Value<ulong>("connectExtraId")];
            string miaAssetName = miaExtra.Value<string>("prefab");
            VillagerObject mia = LoadUtils.LoadAssetSync<VillagerObject>(miaAssetName)
                ?? throw new InvalidDataException("Could not load Mia's base attributes.");
            try
            {
                profile = UnityEngine.Object.Instantiate(mia);
            }
            finally
            {
                LoadUtils.RecycleAsset(mia);
            }

            profile.name = DataAsset;
            profile.npcID = NpcId;
            profile.defaultWeaponID = 0;
            profile.headSkin = HeadSkin;
            profile.clothingSkin = "Cloth_NvWuDouPeng";
            profile.freeArrowAmmo = true;
            profile.leechValue = 50f;
            profile.luckyValue = 50f;
            profile.buffID = 0;
            characterAssets.Add(DataAsset, profile);

            var extra = (JObject)miaExtra.DeepClone();
            extra["configID"] = ExtraId;
            extra["nameKey"] = NameKey;
            extra["descriptionKey"] = DescriptionKey;
            extra["powerStringKey"] = AbilityKey;
            extra["prefab"] = DataAsset;
            extra["illustration"] = PortraitAsset;
            extra["personality"] = "INTJ";
            extra["isEmployee"] = true;
            extra["price"] = 0;
            extra["defaultCloth"] = 0;
            extra.Remove("raceKey");
            extra.Remove("signKey");
            extra["age"] = 0;
            AddRow("NPCExtraConfig", ExtraId, extra);

            AddRow("NpcConfig", NpcId, new JObject
            {
                ["configID"] = NpcId,
                ["nameKey"] = NameKey,
                ["connectExtraId"] = ExtraId,
                ["headIcon"] = AvatarAsset,
                ["dialogueIdentityID"] = 0
            });
            AddRow("NPCDialogConfig", DialogueId, new JObject
            {
                ["configID"] = DialogueId,
                ["speakerID"] = NpcId,
                ["npcId"] = NpcId,
                ["repeatType"] = 3,
                ["dayRange"] = "1,0",
                ["type"] = 0,
                ["contentKey"] = QuoteKey,
                ["charaPic1"] = PortraitAsset
            });
            NPCDailyDialogueManager.Instance.InitializeConfigurations(
                ConfigDataStore.GetAllDataLimit<NPCDialogConfig>("NPCDialogConfig", "configID", value => value != null));
        }

        private void AddRow(string table, ulong id, JObject row)
        {
            tables[table].Add(id, row);
            registeredRows.Add((table, id));
        }

        public override void OnFrame()
        {
            if (!active)
                return;
            windowIcon?.Update();
            if (!UserDataManager.IsActive())
                return;

            UserDataManager manager = UserDataManager.Instance;
            UserData save = manager.userData;
            if (save == null || save == seenSave || manager.IsApplyingSaveData ||
                save.customHomeData?.totalVilligers == null || RegionManager.instance == null ||
                RegionManager.instance.mainUI == null || !CharacterCore.IsActive() || CharacterCore.Instance.c_ctrl == null)
                return;

            VillagerData candidate = save.customHomeData.totalVilligers.Values.FirstOrDefault(DualWeapons.IsVivhite);
            if (candidate == null)
            {
                // The native constructor allocates independent, empty equipment slots in the save.
                candidate = manager.InitAccompanyData(DataAsset).GetAwaiter().GetResult();
                candidate.employeeType = EnumConfig.EmployeeType.Unhired;
                NPCDailyDialogueManager.Instance.RecordRecruitment(candidate);
            }
            DualWeapons.EnsureSlots(candidate);
            seenSave = save;
        }

        public override void Unload()
        {
            if (active || profile != null)
                RemovePatches();
            active = false;
            seenSave = null;
            windowIcon?.Dispose();
            windowIcon = null;

            foreach ((string table, ulong id) in registeredRows)
                tables[table].Remove(id);
            if (registeredRows.Count > 0 && NPCDailyDialogueManager.IsActive())
            {
                NPCDailyDialogueManager.Instance.InitializeConfigurations(
                    ConfigDataStore.GetAllDataLimit<NPCDialogConfig>("NPCDialogConfig", "configID", value => value != null));
            }
            registeredRows.Clear();
            if (profile != null)
            {
                if (characterAssets.TryGetValue(DataAsset, out VillagerObject cached) && cached == profile)
                    characterAssets.Remove(DataAsset);
                UnityEngine.Object.Destroy(profile);
                profile = null;
            }
            art?.Dispose();
            art = null;
        }
    }
}
