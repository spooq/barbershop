using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Barbershop
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BarberPacket
    {
        public string targetUid;
        public string code;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class PlayerBarbershopData
    {
        public bool CanGrowHair = false;
        public bool CanGrowFacialHair = false;
        public Dictionary<string, double> timeSinceEdited;
    }

    public class BarbershopModSystem : ModSystem
    {
        public IClientNetworkChannel Channel;

        public ICoreServerAPI sapi;

        public const string HairBase = "hairbase";
        public const string HairExtra = "hairextra";
        public const string HairColor = "haircolor";
        public const string Mustache = "mustache";
        public const string Beard = "beard";

        // Server-side only
        BarberProperties hairGrowth;
        double lastCheckOfElapsedDays;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterCollectibleBehaviorClass("Barbershop", typeof(CollectibleBehaviorBarber));

            api.Network
                .RegisterChannel(Mod.Info.ModID)
                .RegisterMessageType<BarberPacket>();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            Channel = api.Network.GetChannel(Mod.Info.ModID);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            sapi = api;

            var hairgrowAsset = sapi.Assets.Get("barbershop:config/hairgrowth.json");
            hairGrowth = JsonConvert.DeserializeObject<BarberProperties>(hairgrowAsset.ToText());

            sapi.Event.ServerRunPhase(EnumServerRunPhase.RunGame, OnServerRunGame);

            sapi.Network.GetChannel(Mod.Info.ModID)
                .SetMessageHandler<BarberPacket>(ApplyBarberItemToPlayer);

            sapi.ChatCommands.Create("barber")
                .WithDescription("Barbershop main command")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(sapi.ChatCommands.Parsers.WordRange("arg", new List<string>{ "show", "hair", "nohair", "facialhair", "nofacialhair" }.ToArray()))
                .HandleWith(onBarberCommand);
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
                case "show":
                    var cangrowhair = saveData.CanGrowHair ? Lang.Get("cangrowhairscalp") : Lang.Get("cantgrowhairscalp");
                    var cangrowfacial = saveData.CanGrowFacialHair ? Lang.Get("cangrowhairface") : Lang.Get("cantgrowhairface");

                    var message = cangrowhair + Environment.NewLine
                                + cangrowfacial + Environment.NewLine
                                + $"{HairBase} {GetCurrentStyle(skinnable, HairBase)} {saveData.timeSinceEdited[HairBase]}" + Environment.NewLine
                                + $"{HairExtra} {GetCurrentStyle(skinnable, HairExtra)} {saveData.timeSinceEdited[HairExtra]}" + Environment.NewLine
                                + $"{HairColor} {GetCurrentStyle(skinnable, HairColor)} {saveData.timeSinceEdited[HairColor]}" + Environment.NewLine
                                + $"{Mustache} {GetCurrentStyle(skinnable, Mustache)} {saveData.timeSinceEdited[Mustache]}" + Environment.NewLine
                                + $"{Beard} {GetCurrentStyle(skinnable, Beard)} {saveData.timeSinceEdited[Beard]}" + Environment.NewLine;
                    return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = message };

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

                    bool dirty = TryAndGrowHair(skinnable, HairColor, hairGrowth.haircolor, diff, ref saveData);
                    if (saveData.CanGrowHair)
                    {
                        dirty |= TryAndGrowHair(skinnable, HairBase, hairGrowth.hairbase, diff, ref saveData);
                        dirty |= TryAndGrowHair(skinnable, HairExtra, hairGrowth.hairextra, diff, ref saveData);
                    }
                    if (saveData.CanGrowFacialHair)
                    {
                        dirty |= TryAndGrowHair(skinnable, Mustache, hairGrowth.mustache, diff, ref saveData);
                        dirty |= TryAndGrowHair(skinnable, Beard, hairGrowth.beard, diff, ref saveData);
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

        public bool TryAndGrowHair(EntityBehaviorExtraSkinnable skinnable, string part, List<BarberTransform> barberProps, double diff, ref PlayerBarbershopData saveData)
        {
            saveData.timeSinceEdited[part] += diff;

            // TODO: Should handle fast-forward of more than one day.
            if (saveData.timeSinceEdited[part] > 1)
                return ApplyFirstMatchingTransform(skinnable, part, barberProps, ref saveData);

            return false;
        }

        public void ApplyBarberItemToPlayer(IServerPlayer fromPlayer, BarberPacket packet)
        {
            var targetPlayer = sapi.World.PlayerByUid(packet.targetUid) as IServerPlayer;
            if (targetPlayer == null)
                return;

            var saveData = targetPlayer.GetModData<PlayerBarbershopData>(Mod.Info.ModID);
            if (saveData == null)
                return;

            var playerBehaviour = targetPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            if (playerBehaviour == null)
                return;

            var item = sapi.World?.GetItem(new AssetLocation(packet.code));
            if (item == null)
                return;

            var itemBehaviour = item.GetCollectibleBehavior<CollectibleBehaviorBarber>(true);
            if (itemBehaviour == null)
                return;

            bool dirty = false;
            dirty |= ApplyFirstMatchingTransform(playerBehaviour, HairBase, itemBehaviour.barberProperties.hairbase, ref saveData);
            dirty |= ApplyFirstMatchingTransform(playerBehaviour, HairExtra, itemBehaviour.barberProperties.hairextra, ref saveData);
            dirty |= ApplyFirstMatchingTransform(playerBehaviour, HairColor, itemBehaviour.barberProperties.haircolor, ref saveData);
            dirty |= ApplyFirstMatchingTransform(playerBehaviour, Mustache, itemBehaviour.barberProperties.mustache, ref saveData);
            dirty |= ApplyFirstMatchingTransform(playerBehaviour, Beard, itemBehaviour.barberProperties.beard, ref saveData);

            if (dirty)
            {
                targetPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
                targetPlayer.BroadcastPlayerData(false);
                targetPlayer.SetModdata(Mod.Info.ModID, SerializerUtil.Serialize(saveData));
            }
        }

        public bool ApplyFirstMatchingTransform(EntityBehaviorExtraSkinnable playerBehaviour, string part, List<BarberTransform> barberTransforms, ref PlayerBarbershopData saveData)
        {
            if (barberTransforms == null)
                return false;

            foreach (var tf in barberTransforms)
                if (AttemptTransform(playerBehaviour, part, tf.from, tf.to, ref saveData))
                    return true;

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
