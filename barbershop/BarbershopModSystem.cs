using Newtonsoft.Json;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Barbershop
{
    [ProtoContract]
    public class BarberUserConfig
    {
        [ProtoMember(1)]
        public double DaysForHairToGrow = 4.5;
    }

    [ProtoContract]
    public class PlayerBarbershopData
    {
        [ProtoMember(1)]
        public bool CanGrowHair = false;

        [ProtoMember(2)]
        public bool CanGrowFacialHair = false;

        [ProtoMember(3)]
        public Dictionary<string, double> timeSinceEdited;

        [ProtoMember(4)]
        public bool ReceiveNotifications = true;
    }

    public class BarberTransform
    {
        public string from;
        public string to;
        public double timeToGrowInDays = 5.0;
    }

    public class BarberProperties
    {
        public List<BarberTransform> hairbase = new();
        public List<BarberTransform> hairextra = new();
        public List<BarberTransform> beard = new();
        public List<BarberTransform> mustache = new();
        public List<BarberTransform> haircolor = new();
    }

    public class BarbershopModSystem : ModSystem
    {
        public ICoreServerAPI sapi;

        public BarberUserConfig UserConfig { get; set; }

        public const string HairBase = "hairbase";
        public const string HairExtra = "hairextra";
        public const string HairColor = "haircolor";
        public const string Mustache = "mustache";
        public const string Beard = "beard";

        // Server-side only
        public BarberProperties HairGrowthProperties;
        public double lastCheckOfElapsedDays;
        public Dictionary<string, BarberProperties> DyeProperties;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterCollectibleBehaviorClass("Barbershop", typeof(CollectibleBehaviorBarber));
            api.RegisterCollectibleBehaviorClass("BarbershopMirror", typeof(CollectibleBehaviorBarberMirror));
            api.RegisterBlockBehaviorClass("BarbershopContainer", typeof(BlockBehaviorBarberLiquidContainer));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            sapi = api;

            UserConfig = sapi.LoadModConfig<BarberUserConfig>($"{Mod.Info.ModID}.json");
            UserConfig ??= new BarberUserConfig();
            sapi.StoreModConfig(UserConfig, $"{Mod.Info.ModID}.json");

            var hairgrowAsset = sapi.Assets.Get("barbershop:config/growth.json");
            HairGrowthProperties = JsonConvert.DeserializeObject<BarberProperties>(hairgrowAsset.ToText());

            var dyeAsset = sapi.Assets.Get("barbershop:config/dye.json");
            DyeProperties = JsonConvert.DeserializeObject<Dictionary<string, BarberProperties>>(dyeAsset.ToText());

            sapi.Event.ServerRunPhase(EnumServerRunPhase.RunGame, OnServerRunGame);

            sapi.ChatCommands.Create("barber")
                .WithDescription("Barbershop main command")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(sapi.ChatCommands.Parsers.WordRange("arg", new List<string> { "hair", "nohair", "facialhair", "nofacialhair" }.ToArray()))
                .HandleWith(onBarberCommand);
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            base.AssetsFinalize(api);

            var bowl = api.World.GetBlock(new AssetLocation("bowl-fired"));
            bowl.CollectibleBehaviors = bowl.CollectibleBehaviors.Append(new BlockBehaviorBarberLiquidContainer(bowl));
        }

        public TextCommandResult onBarberCommand(TextCommandCallingArgs args)
        {
            var targetPlayer = args.Caller.Player as IServerPlayer;

            var skinnable = targetPlayer?.Entity?.GetBehavior<EntityBehaviorExtraSkinnable>();
            if (skinnable == null)
                return new TextCommandResult { Status = EnumCommandStatus.Error, StatusMessage = $"Could not get playerBehaviour EntityBehaviorExtraSkinnable" };

            var saveData = targetPlayer.GetModData<PlayerBarbershopData>(Mod.Info.ModID);
            if (saveData == null)
            {
                saveData = OnCharacterReset(skinnable);
                targetPlayer.SetModdata(Mod.Info.ModID, SerializerUtil.Serialize(saveData));
            }

            switch (args.Parsers[0].GetValue() as string)
            {
                case "notify":
                    saveData.ReceiveNotifications = !saveData.ReceiveNotifications;
                    targetPlayer.SetModdata(Mod.Info.ModID, SerializerUtil.Serialize(saveData));
                    return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = saveData.ReceiveNotifications ? Lang.Get("notify") : Lang.Get("nonotify") };

                case "hair":
                    saveData.CanGrowHair = true;
                    targetPlayer.SetModdata(Mod.Info.ModID, SerializerUtil.Serialize(saveData));
                    return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = Lang.Get("cangrowhairscalp") };

                case "nohair":
                    AttemptTransform(skinnable, HairBase, "*", "bald", ref saveData);
                    AttemptTransform(skinnable, HairExtra, "*", "none", ref saveData);
                    saveData.CanGrowHair = false;
                    targetPlayer.SetModdata(Mod.Info.ModID, SerializerUtil.Serialize(saveData));
                    return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = Lang.Get("cantgrowhairscalp") };

                case "facialhair":
                    saveData.CanGrowFacialHair = true;
                    targetPlayer.SetModdata(Mod.Info.ModID, SerializerUtil.Serialize(saveData));
                    return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = Lang.Get("cangrowhairface") };

                case "nofacialhair":
                    AttemptTransform(skinnable, Beard, "*", "none", ref saveData);
                    AttemptTransform(skinnable, Mustache, "*", "none", ref saveData);
                    saveData.CanGrowFacialHair = false;
                    targetPlayer.SetModdata(Mod.Info.ModID, SerializerUtil.Serialize(saveData));
                    return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = Lang.Get("cantgrowhairface") };
            }

            return new TextCommandResult { Status = EnumCommandStatus.Error, StatusMessage = $"Unknown error in {Mod.Info.ModID}" };
        }

        public void OnServerRunGame()
        {
            lastCheckOfElapsedDays = sapi.World.Calendar.ElapsedDays;
            sapi.World.RegisterCallback(OnTimePassed, 1000);
        }

        public void OnTimePassed(float obj)
        {
            var diff = sapi.World.Calendar.ElapsedDays - lastCheckOfElapsedDays;
            if (diff != 0)
            {
                lastCheckOfElapsedDays = sapi.World.Calendar.ElapsedDays;

                foreach (var targetPlayer in sapi.World.AllOnlinePlayers.Cast<IServerPlayer>())
                {
                    if (targetPlayer.ConnectionState != EnumClientState.Playing)
                        continue;

                    var skinnable = targetPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
                    if (skinnable == null)
                        continue;

                    var saveData = targetPlayer.GetModData<PlayerBarbershopData>(Mod.Info.ModID);
                    if (saveData == null)
                        saveData = OnCharacterReset(skinnable);

                    bool dirty = TryAndGrowHair(targetPlayer, skinnable, HairColor, HairGrowthProperties.haircolor, diff, ref saveData);
                    if (saveData.CanGrowHair)
                    {
                        dirty |= TryAndGrowHair(targetPlayer, skinnable, HairBase, HairGrowthProperties.hairbase, diff, ref saveData);
                        dirty |= TryAndGrowHair(targetPlayer, skinnable, HairExtra, HairGrowthProperties.hairextra, diff, ref saveData);
                    }
                    if (saveData.CanGrowFacialHair)
                    {
                        dirty |= TryAndGrowHair(targetPlayer, skinnable, Mustache, HairGrowthProperties.mustache, diff, ref saveData);
                        dirty |= TryAndGrowHair(targetPlayer, skinnable, Beard, HairGrowthProperties.beard, diff, ref saveData);
                    }
                    if (dirty)
                    {
                        targetPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
                        targetPlayer.BroadcastPlayerData(false);
                    }

                    targetPlayer.SetModdata(Mod.Info.ModID, SerializerUtil.Serialize(saveData));
                }
            }

            sapi.World.RegisterCallback(OnTimePassed, 1000);
        }

        public PlayerBarbershopData OnCharacterReset(EntityBehaviorExtraSkinnable skinnable)
        {
            var saveData = new PlayerBarbershopData
            {
                timeSinceEdited = new Dictionary<string, double>
            {
                { HairBase, 0 },
                { HairExtra, 0 },
                { HairColor, 0},
                { Mustache, 0 },
                { Beard, 0}
            }
            };

            saveData.CanGrowHair |= GetCurrentStyle(skinnable, HairBase) != "bald";
            saveData.CanGrowHair |= GetCurrentStyle(skinnable, HairExtra) != "none";

            saveData.CanGrowFacialHair |= GetCurrentStyle(skinnable, Mustache) != "none";
            saveData.CanGrowFacialHair |= GetCurrentStyle(skinnable, Beard) != "none";

            return saveData;
        }

        public bool TryAndGrowHair(IServerPlayer targetPlayer, EntityBehaviorExtraSkinnable skinnable, string part, List<BarberTransform> barberProps, double diff, ref PlayerBarbershopData saveData)
        {
            saveData.timeSinceEdited[part] += diff;

            // TODO: Should handle fast-forward of more than one day.
            if (saveData.timeSinceEdited[part] > UserConfig.DaysForHairToGrow)
                return ApplyFirstMatchingTransform(targetPlayer, skinnable, part, barberProps, ref saveData);

            return false;
        }

        public string GetScalpString(IServerPlayer targetPlayer)
        {
            var saveData = targetPlayer.GetModData<PlayerBarbershopData>(Mod.Info.ModID);
            if (saveData == null)
                return null;

            var playerBehaviour = targetPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            if (playerBehaviour == null)
                return null;

            var hairStr = Lang.Get("barbershop:hairbase_" + GetCurrentStyle(playerBehaviour, HairBase), Lang.Get("game:color-" + GetCurrentStyle(playerBehaviour, HairColor)).ToLower());
            var hairExtraStr = Lang.Get("barbershop:hairextra_" + GetCurrentStyle(playerBehaviour, HairExtra));
            if (!saveData.CanGrowHair)
                hairStr = hairExtraStr = "";
            return Lang.Get("barbershop:description", hairStr, hairExtraStr);
        }

        public string GetFacialString(IServerPlayer targetPlayer)
        {
            var saveData = targetPlayer.GetModData<PlayerBarbershopData>(Mod.Info.ModID);
            if (saveData == null)
                return null;

            var playerBehaviour = targetPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            if (playerBehaviour == null)
                return null;

            var beardStr = Lang.Get("barbershop:beard_" + GetCurrentStyle(playerBehaviour, Beard), Lang.Get("game:color-" + GetCurrentStyle(playerBehaviour, HairColor)).ToLower());
            var mustacheStr = Lang.Get("barbershop:mustache_" + GetCurrentStyle(playerBehaviour, Mustache));
            if (!saveData.CanGrowFacialHair)
                mustacheStr = beardStr = "";
            return Lang.Get("barbershop:description", beardStr, mustacheStr);
        }

        public void ApplyBarberPropertiesToPlayer(IServerPlayer targetPlayer, BarberProperties barberProperties)
        {
            var saveData = targetPlayer.GetModData<PlayerBarbershopData>(Mod.Info.ModID);
            if (saveData == null)
                return;

            var playerBehaviour = targetPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            if (playerBehaviour == null)
                return;

            bool dirty = false;
            dirty |= ApplyFirstMatchingTransform(targetPlayer, playerBehaviour, HairBase, barberProperties.hairbase, ref saveData);
            dirty |= ApplyFirstMatchingTransform(targetPlayer, playerBehaviour, HairExtra, barberProperties.hairextra, ref saveData);
            dirty |= ApplyFirstMatchingTransform(targetPlayer, playerBehaviour, HairColor, barberProperties.haircolor, ref saveData);
            dirty |= ApplyFirstMatchingTransform(targetPlayer, playerBehaviour, Mustache, barberProperties.mustache, ref saveData);
            dirty |= ApplyFirstMatchingTransform(targetPlayer, playerBehaviour, Beard, barberProperties.beard, ref saveData);

            if (dirty)
            {
                targetPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
                targetPlayer.BroadcastPlayerData(false);
                targetPlayer.SetModdata(Mod.Info.ModID, SerializerUtil.Serialize(saveData));
            }
        }

        public bool ApplyFirstMatchingTransform(IServerPlayer targetPlayer, EntityBehaviorExtraSkinnable playerBehaviour, string part, List<BarberTransform> barberTransforms, ref PlayerBarbershopData saveData)
        {
            if (barberTransforms == null)
                return false;

            foreach (var tf in barberTransforms)
            {
                // Always apply, don't always notify.
                if (AttemptTransform(playerBehaviour, part, tf.from, tf.to, ref saveData) && saveData.ReceiveNotifications)
                {
                    switch (part)
                    {
                        case HairBase:
                            targetPlayer.SendMessage(GlobalConstants.CurrentChatGroup, GetScalpString(targetPlayer), EnumChatType.OwnMessage);
                            break;

                        case HairExtra:
                            targetPlayer.SendMessage(GlobalConstants.CurrentChatGroup, GetScalpString(targetPlayer), EnumChatType.OwnMessage);
                            break;

                        case Mustache:
                            targetPlayer.SendMessage(GlobalConstants.CurrentChatGroup, GetFacialString(targetPlayer), EnumChatType.OwnMessage);
                            break;

                        case Beard:
                            targetPlayer.SendMessage(GlobalConstants.CurrentChatGroup, GetFacialString(targetPlayer), EnumChatType.OwnMessage);
                            break;

                        case HairColor:
                            targetPlayer.SendMessage(GlobalConstants.CurrentChatGroup, GetScalpString(targetPlayer), EnumChatType.OwnMessage);
                            targetPlayer.SendMessage(GlobalConstants.CurrentChatGroup, GetFacialString(targetPlayer), EnumChatType.OwnMessage);
                            break;
                    }
                    return true;
                }
            }

            return false;
        }

        public bool AttemptTransform(EntityBehaviorExtraSkinnable skinnable, string part, string from, string to, ref PlayerBarbershopData saveData)
        {
            string currentStyle = GetCurrentStyle(skinnable, part);
            if (string.IsNullOrEmpty(currentStyle) || !WildcardUtil.Match(from, currentStyle))
                return false;

            // Change style
            foreach (var asp in skinnable.AvailableSkinParts)
            {
                if (asp.Code == part)
                {
                    skinnable.selectSkinPart(asp.Code, to);
                    saveData.timeSinceEdited[part] = 0;
                    return true;
                }
            }

            return false;
        }

        public string GetCurrentStyle(EntityBehaviorExtraSkinnable playerBehaviour, string part)
        {
            foreach (var appliedPart in playerBehaviour.AppliedSkinParts)
                if (appliedPart.PartCode == part)
                    return appliedPart.Code;

            return null;
        }
    }
}
