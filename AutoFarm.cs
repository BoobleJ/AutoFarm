using System.Collections.Generic;
using Oxide.Core.Configuration;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using System;
using VLB;
using Rust;

namespace Oxide.Plugins
{
    [Info("Auto Farm", "Razor", "1.2.0", ResourceId = 34)]
    [Description("Auto Farm The PlanterBoxes")]
    public class AutoFarm : RustPlugin
    {
        #region Init
        FarmEntity pcdData;
        private DynamicConfigFile PCDDATA;
        static System.Random random = new System.Random();
        private static AutoFarm _;
        public Dictionary<ulong, int> playerTotals = new Dictionary<ulong, int>();
        public Dictionary<ulong, bool> disabledFarmPlayer = new Dictionary<ulong, bool>();
        public List<ulong> GametipShown = new List<ulong>();

        void Init()
        {
            _ = this;
            if (configData.settings.Permission == null || configData.settings.Permission.Count < 0)
            {
                configData.settings.Permission = new Dictionary<string, int>() { { "autofarm.allow", 200 }, { "autofarm.vip", 4 } };
                Config.WriteObject(configData, true);
            }

            RegisterPermissions();

            PCDDATA = Interface.Oxide.DataFileSystem.GetFile(Name + "/FarmData");
            LoadData();

            foreach (var i in pcdData.pEntity.ToList())
            {
                var networkable = BaseNetworkable.serverEntities.Find(i.Key);
                if (networkable != null)
                {
                    planterBoxBehavior mono = networkable.GetOrAddComponent<planterBoxBehavior>();
                    mono.autoFill();
                }
            }
        }

        private void OnServerInitialized()
        {
            if (configData.settings.seedsAllowedAndMultiplier.Count <= 0)
            {
                configData.settings.seedsAllowedAndMultiplier.Add(803954639, 1);
                configData.settings.seedsAllowedAndMultiplier.Add(998894949, 1);
                configData.settings.seedsAllowedAndMultiplier.Add(1911552868, 1);
                configData.settings.seedsAllowedAndMultiplier.Add(-1776128552, 1);
                configData.settings.seedsAllowedAndMultiplier.Add(-237809779, 1);
                configData.settings.seedsAllowedAndMultiplier.Add(-2084071424, 1);
                configData.settings.seedsAllowedAndMultiplier.Add(-1511285251, 1);
                configData.settings.seedsAllowedAndMultiplier.Add(830839496, 1);
                configData.settings.seedsAllowedAndMultiplier.Add(-992286106, 1);
                configData.settings.seedsAllowedAndMultiplier.Add(-520133715, 1);
                configData.settings.seedsAllowedAndMultiplier.Add(838831151, 1);
                configData.settings.seedsAllowedAndMultiplier.Add(-778875547, 1);
                configData.settings.seedsAllowedAndMultiplier.Add(-1305326964, 1);
                configData.settings.seedsAllowedAndMultiplier.Add(-886280491, 1);
                configData.settings.seedsAllowedAndMultiplier.Add(1512054436, 1);
                configData.settings.seedsAllowedAndMultiplier.Add(1898094925, 1);
                configData.settings.seedsAllowedAndMultiplier.Add(2133269020, 1);
                configData.settings.seedsAllowedAndMultiplier.Add(1533551194, 1);
                configData.settings.seedsAllowedAndMultiplier.Add(390728933, 1);
                SaveConfig();
            }
            NextTick(() =>
            {
                int removeCount = 0;
                if (pcdData.pEntity.Count <= 0) return;
                foreach (var i in pcdData.pEntity.ToList())
                {
                    var networkable = BaseNetworkable.serverEntities.Find(i.Key);
                    if (networkable == null) { pcdData.pEntity.Remove(i.Key); removeCount++; }
                    else
                    {
                        planterBoxBehavior mono = networkable.GetOrAddComponent<planterBoxBehavior>();
                        _.timer.Once(3, () => { if (mono != null) mono.autoFill(); });
                    }
                }
                SaveData();
                if (removeCount > 0) { PrintWarning($"Removed {removeCount} planters not found from datafile."); }
            });
        }

        private void Unload()
        {
            foreach (var Controler in UnityEngine.Object.FindObjectsOfType<planterBoxBehavior>())
            {
                UnityEngine.Object.Destroy(Controler);
            }
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                player.SendConsoleCommand("gametip.hidegametip");
            }
        }

        private void RegisterPermissions()
        {
            if (configData.settings.Permission != null && configData.settings.Permission.Count > 0)
                foreach (var perm in configData.settings.Permission)
                    permission.RegisterPermission(perm.Key, this);
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["enabled"] = "<color=#ce422b>AutoFarm placment enabled!</color>",
                ["disabled"] = "<color=#ce422b>AutoFarm placment disabled!</color>",
                ["max"] = "<color=#ce422b>You reached your max AutoFarm placments of {0}!</color>",
                ["rotatePlanter"] = "You can rotate the planter by hitting it with a hammer!"
            }, this);
        }
        #endregion

        #region Config

        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Settings")]
            public Settings settings { get; set; }

            public class Settings
            {
                public int seedStorageSlots { get; set; }
                public bool AddSprinkler { get; set; }
                public bool AddStorageAdapter { get; set; }
                public bool SprinklerNeedsWater { get; set; }
                public bool CallHookOnCollectiblePickup { get; set; }
                public Dictionary<int, int> seedsAllowedAndMultiplier { get; set; }
                public Dictionary<string, int> Permission { get; set; }
            }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                settings = new ConfigData.Settings
                {
                    seedStorageSlots = 6,
                    AddSprinkler = false,
                    AddStorageAdapter = false,
                    SprinklerNeedsWater = true,
                    CallHookOnCollectiblePickup = false,
                    seedsAllowedAndMultiplier = new Dictionary<int, int>(),
                    Permission = new Dictionary<string, int>() { { "autofarm.allow", 2 }, { "autofarm.vip", 4 } }
                },

                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(1, 0, 1))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion Config

        #region Data

        void LoadData()
        {
            try
            {
                pcdData = Interface.GetMod().DataFileSystem.ReadObject<FarmEntity>(Name + "/FarmData");
            }
            catch
            {
                PrintWarning("Couldn't load FarmData, creating new FarmData file");
                pcdData = new FarmEntity();
            }
        }
        class FarmEntity
        {
            public Dictionary<uint, PCDInfo> pEntity = new Dictionary<uint, PCDInfo>();

            public FarmEntity() { }
        }
        class PCDInfo
        {
            public int lifeLimit;
            public int pickedCount;
            public ulong ownerid;
        }
        void SaveData()
        {
            PCDDATA.WriteObject(pcdData);
        }

        #endregion Data

        #region Hooks
        [ChatCommand("autofarm")]
        private void thePath(BasePlayer player, string cmd, string[] args)
        {

            foreach (var key in configData.settings.Permission)
            {
                if (permission.UserHasPermission(player.UserIDString, key.Key))
                {
                    if (disabledFarmPlayer.ContainsKey(player.userID))
                    {
                        disabledFarmPlayer.Remove(player.userID);
                        SendReply(player, lang.GetMessage("enabled", this, player.UserIDString));
                    }
                    else
                    {
                        disabledFarmPlayer.Add(player.userID, true);
                        SendReply(player, lang.GetMessage("disabled", this, player.UserIDString));
                    }
                    return;
                }

            }
            SendReply(player, lang.GetMessage("NoPerms", this, player.UserIDString));
        }

        private void OnEntityBuilt(Planner plan, GameObject gameObject)
        {
            var player = plan?.GetOwnerPlayer();
            PlanterBox planterBox = gameObject.GetComponent<PlanterBox>();
            if (planterBox == null || player == null)
                return;

            if (disabledFarmPlayer.ContainsKey(player.userID))
                return;

            int totals = 0;
            int playertotals = 0;
            foreach (var key in configData.settings.Permission)
            {
                if (permission.UserHasPermission(player.UserIDString, key.Key))
                {
                    totals = key.Value;
                }
            }

            if (_.playerTotals.ContainsKey(planterBox.OwnerID))
                playertotals = playerTotals[player.userID];
            if (playertotals < totals)
            {
                planterBoxBehavior cropStorage = planterBox.gameObject.AddComponent<planterBoxBehavior>();
                pcdData.pEntity.Add(planterBox.net.ID, new PCDInfo());
                SaveData();
                if (!GametipShown.Contains(player.userID))
                {
                    GameTips(player);
                    GametipShown.Add(player.userID);
                } 
            }
            else if (player != null && totals > 0) SendReply(player, lang.GetMessage("max", this, player.UserIDString), totals);
        }

        void OnLootEntityEnd(BasePlayer player, StorageContainer stash)
        {
            planterBoxBehavior planterBox = stash.GetComponentInParent<PlanterBox>()?.GetComponentInParent<planterBoxBehavior>();
            if (planterBox != null) planterBox.autoFill();
        }

        private object OnEntityTakeDamage(StorageContainer stash, HitInfo hitInfo)
        {
            planterBoxBehavior planterBox = stash.GetComponentInParent<PlanterBox>()?.GetComponentInParent<planterBoxBehavior>();
            if (planterBox != null)
            {
                hitInfo.damageTypes = new DamageTypeList();
                return true;
            }

            return null;
        }

        ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            if (container.entityOwner == null) return null;
            if (container.entityOwner is StorageContainer)
            {
                if (container.entityOwner.name != null && container.entityOwner.name != "seedsBox") return null;
                planterBoxBehavior planterBox = container.entityOwner?.GetComponentInParent<PlanterBox>()?.GetComponentInParent<planterBoxBehavior>();
                if (planterBox != null && container.entityOwner.name == "seedsBox")
                {
                    if (!configData.settings.seedsAllowedAndMultiplier.ContainsKey(item.info.itemid))
                        return ItemContainer.CanAcceptResult.CannotAccept;
                }
            }
            return null;
        }

        private object CanPickupEntity(BasePlayer player, Sprinkler entity)
        {
            if (entity == null) return null;
            if (entity != null && entity.name != null && entity.name == "NoPickUp")
            {
                return false;
            }
            return null;
        }
        private object CanPickupEntity(BasePlayer player, Splitter entity)
        {
            if (entity == null) return null;
            if (entity != null && entity.name != null && entity.name == "NoPickUp")
            {
                return false;
            }
            return null;
        }

        private object OnWireClear(BasePlayer player, IOEntity entity1, int connected, IOEntity entity2, bool flag)
        {
            if (entity1 != null && entity1 is Splitter && entity1.name != null && entity1.name == "NoPickUp" && entity2 != null && entity2 is Sprinkler && entity2.name != null && entity2.name == "NoPickUp")
            {
                return false;
            }
            return null;
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            entity?.GetComponent<PlanterBox>()?.GetComponent<planterBoxBehavior>()?.RemoveEntitys();
        }

        private void OnGrowableStateChange(GrowableEntity growableEntity, PlantProperties.State state)
        {
            _.NextTick(() =>
            {
                if (growableEntity != null && growableEntity.planter != null)
                {
                    if (state == PlantProperties.State.Ripe || state == PlantProperties.State.Dying || growableEntity.State == PlantProperties.State.Ripe || growableEntity.State == PlantProperties.State.Dying)
                    {
                        planterBoxBehavior behavior = growableEntity.planter.GetComponent<planterBoxBehavior>();
                        if (behavior != null)
                        {
                            behavior.seeIfCanPick(growableEntity);
                        }
                    }
                }
            });
        }

        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            PlanterBox planterBox = info?.HitEntity as PlanterBox;
            if (planterBox != null)
            {
                planterBoxBehavior behavior = planterBox.GetComponent<planterBoxBehavior>();
                if (behavior == null) return;

                if (behavior.ownerplayer == player.userID && planterBox.health == planterBox.MaxHealth())
                    behavior.Rotate();
            }
        }

        public void GameTips(BasePlayer player)
        {
            if (player != null && player.userID.IsSteamId())
            {
                player?.SendConsoleCommand("gametip.hidegametip");
                player?.SendConsoleCommand("gametip.showgametip", lang.GetMessage("rotatePlanter", this, player.UserIDString));
                _.timer.Once(4f, () => player?.SendConsoleCommand("gametip.hidegametip"));
            }
        }

        public static BaseEntity addStorageadaptor(BaseEntity parent, Quaternion rotoffset, Vector3 posoffset)
        {
            BaseEntity adaptor = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/industrialadaptors/storageadaptor.deployed.prefab", parent.transform.position, parent.transform.rotation);
            adaptor.OwnerID = parent.OwnerID;
            adaptor.Spawn();

            SpawnRefresh(adaptor);
            adaptor.SetParent(parent, true, true);

            if (rotoffset != Quaternion.Euler(0, 0, 0)) { adaptor.transform.rotation = adaptor.transform.rotation * rotoffset; }
            if (posoffset != new Vector3(0, 0, 0)) { adaptor.transform.localPosition = posoffset; }
            adaptor.SendNetworkUpdateImmediate();
            return adaptor;
        }

        public static void SpawnRefresh(BaseEntity entity1)
        {
            if (entity1 != null)
            {
                if (entity1.GetComponentsInChildren<MeshCollider>() != null)
                foreach (var mesh in entity1.GetComponentsInChildren<MeshCollider>())
                { 
                    UnityEngine.Object.DestroyImmediate(mesh);
                }

                if (entity1.GetComponent<Collider>() != null)
                    UnityEngine.Object.DestroyImmediate(entity1.GetComponent<Collider>());
                if (entity1.GetComponent<GroundWatch>() != null)
                    UnityEngine.Object.DestroyImmediate(entity1.GetComponent<GroundWatch>());
                if (entity1.GetComponent<DestroyOnGroundMissing>() != null)
                    UnityEngine.Object.DestroyImmediate(entity1.GetComponent<DestroyOnGroundMissing>());
            }
        }
        #endregion

        #region planerBox
        class planterBoxBehavior : FacepunchBehaviour
        {
            public PlanterBox planterBox { get; set; }
            public ulong ownerplayer { get; set; }
            private StorageContainer container { get; set; }
            private StorageContainer containerSeeds { get; set; }
            private Sprinkler sprinkler { get; set; }
            private Splitter waterSorce { get; set; }
            private ItemDefinition itemDefinition { get; set; }
            private Dictionary<int, int> seeds = _.configData.settings.seedsAllowedAndMultiplier;
            private DateTime lastRotate { get; set; }
            private int totalslotsAvailable = 11;

            private void Awake()
            {
                planterBox = GetComponent<PlanterBox>();
                if (!_.playerTotals.ContainsKey(planterBox.OwnerID))
                    _.playerTotals.Add(planterBox.OwnerID, 1);
                else _.playerTotals[planterBox.OwnerID]++;
                ownerplayer = planterBox.OwnerID;
                _.timer.Once(1, () =>
                {
                    generateStorage();
                    float delay = random.Next(300, 600);
                    InvokeRepeating("isPlanterFull", delay, 601);
                });
                if (planterBox.ShortPrefabName == "planter.small.deployed")
                    totalslotsAvailable = 5;
            }
            public void Rotate()
            {
                if (lastRotate < DateTime.Now)
                {
                    lastRotate = DateTime.Now.AddSeconds(2);
                    planterBox.transform.Rotate(0, 180, 0);
                    planterBox.SendNetworkUpdateImmediate(true);

                    if (waterSorce != null)
                    {
                        Vector3 pos = new Vector3(planterBox.transform.position.x, planterBox.transform.position.y + 0.1f, planterBox.transform.position.z) + planterBox.transform.forward * 1.425f;
                        if (planterBox.ShortPrefabName == "planter.small.deployed")
                            pos = new Vector3(planterBox.transform.position.x, planterBox.transform.position.y + 0.1f, planterBox.transform.position.z) + planterBox.transform.forward * 0.50f;
                        waterSorce.transform.position = pos;
                        waterSorce.transform.rotation = planterBox.transform.rotation;
                        waterSorce.SendNetworkUpdateImmediate(true);

                    }
                }
            }

            private Sprinkler isThereSprinkler(Vector3 position, float size)
            {
                List<Sprinkler> nearby = new List<Sprinkler>();
                Vis.Entities<Sprinkler>(position, size, nearby);           
                if (nearby.Distinct().ToList().Count > 0)
                    return nearby[0];
                return null;
            }

            private Splitter isThereSplitter(Vector3 position, float size)
            {
                List<Splitter> nearby = new List<Splitter>();
                Vis.Entities<Splitter>(position, size, nearby);
                if (nearby.Distinct().ToList().Count > 0)
                    return nearby[0];
                return null;
            }

            private bool hasAdapter(StorageContainer containerEnt)
            {
                if (containerEnt.children.Count > 0)
                    return true;
                return false;
            }

            private void generateStorage()
            {
                if (_.configData.settings.AddSprinkler)
                {
                    sprinkler = isThereSprinkler(planterBox.transform.position + new Vector3(0f, 0.11f, 0f), 0.01f);
                }

                if (_.configData.settings.SprinklerNeedsWater && _.configData.settings.AddSprinkler)
                {
                    Vector3 pos = new Vector3(planterBox.transform.position.x, planterBox.transform.position.y + 0.1f, planterBox.transform.position.z) + planterBox.transform.forward * 1.425f;
                    if (planterBox.ShortPrefabName == "planter.small.deployed")
                        pos = new Vector3(planterBox.transform.position.x, planterBox.transform.position.y + 0.1f, planterBox.transform.position.z) + planterBox.transform.forward * 0.50f;
                    waterSorce = isThereSplitter(pos, 0.5f);
                }

                foreach (BaseEntity child in planterBox.children.ToList())
                {
                    if (child == null)
                        continue;

                    switch (child.GetType().ToString())
                    {
                        case "StorageContainer":
                            {
                                StorageContainer theStash = child as StorageContainer;
                                if (theStash != null && theStash.name != null)
                                {
                                    if (theStash.name == "seedItem" || theStash.transform.localPosition == new Vector3(-0.5f, 0.20f, 0.50f) || theStash.transform.localPosition == new Vector3(-0.5f, 0.20f, 1.40f))
                                    {
                                        container = theStash;
                                        container.name = "seedItem";
                                        SpawnRefresh(theStash);
                                        if (_.configData.settings.AddStorageAdapter)
                                        {
                                            if (!hasAdapter(container) && container.isSpawned)
                                            {
                                                BaseEntity adapter = addStorageadaptor(container, Quaternion.Euler(new Vector3(0, 90, 90)), new Vector3(0.0f, 0.60f, 0.0f));
                                                adapter.name = "NoPickUp";
                                            }
                                        }
                                    }
                                    else if (theStash.name == "seedsBox" || theStash.transform.localPosition == new Vector3(0.5f, 0.20f, 0.50f) || theStash.transform.localPosition == new Vector3(0.5f, 0.20f, 1.40f))
                                    {
                                        containerSeeds = theStash;
                                        containerSeeds.name = "seedsBox";
                                        SpawnRefresh(theStash);
                                        if (_.configData.settings.seedStorageSlots < 12)
                                        {
                                            containerSeeds.inventory.capacity = _.configData.settings.seedStorageSlots;
                                        }

                                        if (_.configData.settings.AddStorageAdapter)
                                        {
                                            if (!hasAdapter(containerSeeds)  && containerSeeds.isSpawned)
                                            {
                                                BaseEntity adapter = addStorageadaptor(containerSeeds, Quaternion.Euler(new Vector3(0, 90, 90)), new Vector3(0.0f, -0.60f, 0.0f));
                                                adapter.name = "NoPickUp";
                                            }
                                        }
                                    }
                                }
                                break;
                            }
                    }
                }

                if (container == null)
                {
                    container = GameManager.server.CreateEntity("assets/prefabs/deployable/hot air balloon/subents/hab_storage.prefab") as StorageContainer;
                    if (container == null) return;
                    container.SetParent(planterBox);
                    container.panelTitle = new Translate.Phrase("seeds", "Seeds");

                    if (planterBox.ShortPrefabName == "planter.small.deployed")
                    {
                        container.transform.localPosition = new Vector3(-0.5f, 0.20f, 0.50f);
                        container.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, 90));
                    }
                    else
                    {
                        container.transform.localPosition = new Vector3(-0.5f, 0.20f, 1.40f);
                        container.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, 90));
                    }
                    container.name = "seedItem";
                    container.Spawn();
                    SpawnRefresh(container);
                    container.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
                    container.SendNetworkUpdateImmediate();
                    if (_.configData.settings.AddStorageAdapter && !hasAdapter(container))
                    {
                        BaseEntity adapter = addStorageadaptor(container, Quaternion.Euler(new Vector3(0, 90, 90)), new Vector3(0.0f, 0.60f, 0.0f));
                        adapter.name = "NoPickUp";
                    }
                }

                if (containerSeeds == null)
                {
                    containerSeeds = GameManager.server.CreateEntity("assets/prefabs/deployable/hot air balloon/subents/hab_storage.prefab") as StorageContainer;
                    if (containerSeeds == null) return;
                    containerSeeds.SetParent(planterBox);
                    if (planterBox.ShortPrefabName == "planter.small.deployed")
                    {
                        containerSeeds.transform.localPosition = new Vector3(0.5f, 0.20f, 0.50f);
                        containerSeeds.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, 90));
                    }
                    else
                    {
                        containerSeeds.transform.localPosition = new Vector3(0.5f, 0.20f, 1.40f);
                        containerSeeds.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, 90));
                    }
                    containerSeeds.name = "seedsBox";
                    containerSeeds.Spawn();
                    SpawnRefresh(containerSeeds);
                    if (_.configData.settings.seedStorageSlots < 12)
                    {
                        containerSeeds.inventory.capacity = _.configData.settings.seedStorageSlots;
                    }

                    containerSeeds.SendNetworkUpdateImmediate();
                    if (_.configData.settings.AddStorageAdapter && !hasAdapter(containerSeeds))
                    {
                        BaseEntity adapter = addStorageadaptor(containerSeeds, Quaternion.Euler(new Vector3(0, 90, 90)), new Vector3(0.0f, -0.60f, 0.0f));
                        adapter.name = "NoPickUp";
                    }
                }

                if (_.configData.settings.SprinklerNeedsWater && waterSorce == null)
                {
                    Vector3 pos = new Vector3(planterBox.transform.position.x, planterBox.transform.position.y + 0.1f, planterBox.transform.position.z) + planterBox.transform.forward * 1.425f;
                    if (planterBox.ShortPrefabName == "planter.small.deployed")
                        pos = new Vector3(planterBox.transform.position.x, planterBox.transform.position.y + 0.1f, planterBox.transform.position.z) + planterBox.transform.forward * 0.50f;
                    waterSorce = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/fluidsplitter/fluidsplitter.prefab", pos, planterBox.transform.rotation) as Splitter;
                    if (waterSorce == null) return;
                    SpawnRefresh(waterSorce);
                    waterSorce.Spawn();
                }

                if (waterSorce != null)
                {
                    waterSorce.name = "NoPickUp";
                    SpawnRefresh(waterSorce);
                }

                if (sprinkler == null && _.configData.settings.AddSprinkler)
                {
                    sprinkler = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/sprinkler/electric.sprinkler.deployed.prefab", planterBox.transform.position + new Vector3(0f, 0.11f, 0f), new Quaternion()) as Sprinkler;
                    if (sprinkler == null) return;
                    sprinkler.Spawn();
                    sprinkler.DecayPerSplash = 0f;
                    //sprinkler.ConsumptionAmount();
                    SpawnRefresh(sprinkler);
                }

                if (sprinkler != null)
                {
                    sprinkler.DecayPerSplash = 0f;
                    SpawnRefresh(sprinkler);
                    if (!_.configData.settings.SprinklerNeedsWater)
                        InvokeRepeating("WaterPlants", 60, 180);
                    else if (waterSorce != null && !sprinkler.IsConnectedToAnySlot(waterSorce, 0, 3))
                    {
                        _.NextTick(() => connectWater(waterSorce, sprinkler));
                    }
                    sprinkler.name = "NoPickUp";
                }

                itemDefinition = ItemManager.FindItemDefinition("water");
                cropPlants();
            }

            private void connectWater(IOEntity entity, IOEntity entity1)
            {
                entity1.ClearConnections();
                _.NextTick(() =>
                {
                    if (entity == null || entity1 == null) return;
                    IOEntity.IOSlot ioOutput = entity.outputs[0];
                    if (ioOutput != null)
                    {
                        ioOutput.connectedTo = new IOEntity.IORef();
                        ioOutput.connectedTo.Set(entity1);
                        ioOutput.connectedToSlot = 0;
                        ioOutput.connectedTo.Init();

                        entity1.inputs[0].connectedTo = new IOEntity.IORef();
                        entity1.inputs[0].connectedTo.Set(entity);
                        entity1.inputs[0].connectedToSlot = 0;
                        entity1.inputs[0].connectedTo.Init();
                        entity.SendNetworkUpdateImmediate(true);
                        entity1.SendNetworkUpdateImmediate(true);
                    }
                });
            }

            private void WaterPlants()
            {
                if (sprinkler == null) return;
                if (planterBox.BelowMinimumSaturationTriggerLevel && !sprinkler.IsOn())
                {
                    sprinkler.SetFuelType(itemDefinition, null);
                    sprinkler.TurnOn();
                    sprinkler.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    sprinkler.SendNetworkUpdateImmediate(true);
                }
                else if (sprinkler.IsOn() && planterBox.soilSaturation >= planterBox.idealSaturation)
                {
                    sprinkler.TurnOff();
                }
            }

            private void cropPlants()
            {
                foreach (BaseEntity child in planterBox.children.ToList())
                {
                    if (child != null && child is GrowableEntity)
                        seeIfCanPick((child as GrowableEntity));                   
                }
            }

            public void seeIfCanPick(GrowableEntity growableEntity)
            {
                if (growableEntity == null) return;
                if (growableEntity.State == PlantProperties.State.Ripe || growableEntity.State == PlantProperties.State.Dying)
                {
                    pickCrop(growableEntity);
                }
                else autoFill();
                  //growableEntity.ChangeState(PlantProperties.State.Ripe, true, false); //test add
            }

            private void pickCrop(GrowableEntity growableEntity)
            {
                if (containerSeeds == null || container == null) generateStorage();
                int amount = growableEntity.CurrentPickAmount;
                if (seeds.ContainsKey(growableEntity.Properties.SeedItem.itemid))
                    amount = growableEntity.CurrentPickAmount * seeds[growableEntity.Properties.SeedItem.itemid];
                if (amount <= 0) return;

                Item obj = ItemManager.Create(growableEntity.Properties.pickupItem, amount, 0UL);
                if (obj != null)
                {
                    Item itemEntity = growableEntity.GetItem();
                    if (itemEntity != null && itemEntity.skin != 0UL)
                    {
                        BasePlayer player = BasePlayer.Find(planterBox.OwnerID.ToString());
                        Interface.CallHook("OnGrowableGathered", growableEntity, obj, player);
                    }
                    else if (_.configData.settings.CallHookOnCollectiblePickup && planterBox != null && planterBox.OwnerID != 0UL)
                    {
                        BasePlayer player = BasePlayer.Find(planterBox.OwnerID.ToString());
                        if (player != null)
                        {
                            if (growableEntity is CollectibleEntity)
                                Interface.CallHook("OnCollectiblePickup", obj, player, growableEntity);
                            else
                                Interface.CallHook("OnGrowableGathered", growableEntity, obj, player);
                        }
                    }
                    if (!obj.MoveToContainer(container.inventory))
                    {
                        Vector3 velocity = Vector3.zero;

                        BasePlayer player = BasePlayer.Find(planterBox.OwnerID.ToString());
                        obj.Drop(growableEntity.transform.position + new Vector3(0f, 2f, 1.5f), velocity);
                    }
                    if (growableEntity.Properties.pickEffect.isValid)
                        Effect.server.Run(growableEntity.Properties.pickEffect.resourcePath, growableEntity.transform.position, Vector3.up);

                    if (growableEntity != null && !growableEntity.IsDestroyed) { growableEntity.Kill(); }

                    autoFill();
                }
            }


            public void autoFill()
            {
                int totalOpen = totalslotsAvailable - planterBox.children.Count;
                if (totalOpen > 0) isPlanterFull();
            }

            private bool checkSpawnPoint(Vector3 position, float size)
            {
                if (position == null)
                    position = new Vector3(-107.3504f, 12.1489f, -107.7641f);
                List<GrowableEntity> nearby = new List<GrowableEntity>();
                Vis.Entities<GrowableEntity>(position, size, nearby);
                if (nearby.Distinct().ToList().Count > 0)
                    return true;
                return false;
            }

            private void isPlanterFull()
            {
                if (containerSeeds == null || container == null)
                    generateStorage();

                if (planterBox == null || planterBox.IsDestroyed)
                    return;

                int freePlacement = totalslotsAvailable - planterBox.children.Count;
                
                if (freePlacement > 0)
                {
                    for (int slot1 = 0; slot1 < containerSeeds.inventory.capacity; ++slot1)
                    {
                        int totalPlacement = 0;
                        Item slot2 = containerSeeds.inventory.GetSlot(slot1);
                        if (slot2 != null && seeds.ContainsKey(slot2.info.itemid))
                        {
                            int amountToConsume = slot2.amount;
                            if (amountToConsume > 0)
                            {
                                if (freePlacement < amountToConsume)
                                    totalPlacement = freePlacement;
                                else totalPlacement = amountToConsume;
                                if (totalPlacement > 0)
                                    fillPlanter(slot2.info.itemid, totalPlacement, slot2);
                            }
                        }
                    }
                }
            }

            private void fillPlanter(int theID, int amount, Item item = null)
            {
                if (planterBox.links != null)
                {
                    Planner plan = item?.GetHeldEntity() as Planner;
                    if (plan == null) return;
                    var deployablePrefab = item.info.GetComponent<ItemModDeployable>()?.entityPrefab?.resourcePath;
                    if (string.IsNullOrEmpty(deployablePrefab)) return;

                    foreach (EntityLink socketBase in planterBox.links)
                    {
                        Socket_Base baseSocket = socketBase.socket;
                        if (baseSocket != null)
                        {
                            if (!baseSocket.female || planterBox.IsOccupied(baseSocket.socketName) || !IsFree(planterBox.transform.TransformPoint(baseSocket.worldPosition)))
                                continue;

                            GrowableEntity growable = GameManager.server.CreateEntity(deployablePrefab, planterBox.transform.position, Quaternion.identity) as GrowableEntity;
                            if (growable != null)
                            {
                                Item itemEntity = growable.GetItem();
                                if (itemEntity != null && item.skin != 0UL)
                                {
                                    itemEntity.skin = item.skin;
                                }

                                var idata = item?.instanceData;
                                    
                                growable.SetParent(planterBox, true);
                                growable.Spawn();

                                if (idata != null)
                                {
                                    (growable as IInstanceDataReceiver).ReceiveInstanceData(idata);
                                }
                                growable.transform.localPosition = baseSocket.worldPosition;

                                planterBox.SendNetworkUpdateImmediate();
                                planterBox.SendChildrenNetworkUpdateImmediate();
                                amount--;
                                item?.UseItem(1);

                                Effect.server.Run("assets/prefabs/plants/plantseed.effect.prefab", planterBox.transform.TransformPoint(baseSocket.worldPosition), planterBox.transform.up);

                                if (itemEntity != null)
                                {
                                    Planner planer = itemEntity.GetHeldEntity() as Planner;
                                    if (planer != null)
                                    {
                                        Interface.CallHook("OnEntityBuilt", planer, planer.gameObject);
                                    }
                                }

                                if (amount <= 0)
                                    break;
                            }
                        }
                    }
                }
            }

            public bool IsFree(Vector3 position)
            {
                float distance = 0.1f;
                List<GrowableEntity> list = new List<GrowableEntity>();
                Vis.Entities<GrowableEntity>(position, distance, list);
                return list.Count <= 0;
            }

            public void OnDestroy()
            {
                if (_.playerTotals.ContainsKey(ownerplayer))
                    _.playerTotals[ownerplayer] = _.playerTotals[ownerplayer] - 1;

                CancelInvoke("WaterPlants");
                CancelInvoke("isPlanterFull");
                if (sprinkler != null && !_.configData.settings.SprinklerNeedsWater) sprinkler.TurnOff();
                if (planterBox == null || planterBox.IsDestroyed)
                {
                    if (sprinkler != null) sprinkler?.Kill();
                    if (waterSorce != null) waterSorce?.Kill();
                }
            }

            public void RemoveEntitys()
            {
                CancelInvoke("WaterPlants");
                CancelInvoke("isPlanterFull");
                if (sprinkler != null) sprinkler?.Kill();
                if (waterSorce != null) waterSorce?.Kill();
            }
        }
        #endregion
    }
}