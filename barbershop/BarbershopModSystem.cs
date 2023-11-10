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

        private TextCommandResult onBarberCommand(TextCommandCallingArgs args)
        {
            var targetPlayer = args.Caller.Player as IServerPlayer;

            var saveData = targetPlayer.GetModData<PlayerBarbershopData>(Mod.Info.ModID);
            if (saveData == null)
            {
                OnCharacterReset(targetPlayer);
                saveData = targetPlayer.GetModData<PlayerBarbershopData>(Mod.Info.ModID);
            }

            switch (args.Parsers[0].GetValue() as string)
            {
                case "show":
                    var cangrowhair = saveData.CanGrowHair ? Lang.Get("cangrowhairscalp") : Lang.Get("cantgrowhairscalp");
                    targetPlayer.SendMessage(GlobalConstants.CurrentChatGroup, cangrowhair, EnumChatType.Notification);
                    var cangrowfacial = saveData.CanGrowFacialHair ? Lang.Get("cangrowhairface") : Lang.Get("cantgrowhairface");
                    targetPlayer.SendMessage(GlobalConstants.CurrentChatGroup, cangrowfacial, EnumChatType.Notification);
                    break;
                case "hair":
                    saveData.CanGrowHair = true;
                    break;
                case "nohair":
                    AttemptTransform(targetPlayer, HairBase, "*", "bald");
                    AttemptTransform(targetPlayer, HairExtra, "*", "none");
                    saveData.CanGrowHair = false;
                    break;
                case "facialhair":
                    saveData.CanGrowFacialHair = true;
                    break;
                case "nofacialhair":
                    AttemptTransform(targetPlayer, Beard, "*", "none");
                    AttemptTransform(targetPlayer, Mustache, "*", "none");
                    saveData.CanGrowFacialHair = false;
                    break;
            }

            targetPlayer.SetModdata(Mod.Info.ModID, SerializerUtil.Serialize(saveData));

            return TextCommandResult.Success();
        }

        private void OnServerRunGame()
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

                    var saveData = targetPlayer.GetModData<PlayerBarbershopData>(Mod.Info.ModID);
                    if (saveData == null)
                    {
                        OnCharacterReset(targetPlayer);
                        continue;
                    }

                    bool dirty = TryAndGrowHair(targetPlayer, HairColor, hairGrowth.haircolor, diff, ref saveData);
                    if (saveData.CanGrowHair)
                    {
                        dirty |= TryAndGrowHair(targetPlayer, HairBase, hairGrowth.hairbase, diff, ref saveData);
                        dirty |= TryAndGrowHair(targetPlayer, HairExtra, hairGrowth.hairextra, diff, ref saveData);
                    }
                    if (saveData.CanGrowFacialHair)
                    {
                        dirty |= TryAndGrowHair(targetPlayer, Mustache, hairGrowth.mustache, diff, ref saveData);
                        dirty |= TryAndGrowHair(targetPlayer, Beard, hairGrowth.beard, diff, ref saveData);
                    }
                    if (dirty)
                    {
                        targetPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
                        targetPlayer.BroadcastPlayerData(false);
                    }
                }
            }

            sapi.World.RegisterCallback(OnTimePassed, 1000);
        }

        public void OnCharacterReset(IServerPlayer byPlayer)
        {
            var playerBehaviour = byPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            if (playerBehaviour == null)
                return;

            var savedata = new PlayerBarbershopData
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

            savedata.CanGrowHair |= GetCurrentStyle(playerBehaviour, HairBase) != "none";
            savedata.CanGrowHair |= GetCurrentStyle(playerBehaviour, HairExtra) != "none";

            savedata.CanGrowFacialHair |= GetCurrentStyle(playerBehaviour, Mustache) != "none";
            savedata.CanGrowFacialHair |= GetCurrentStyle(playerBehaviour, Beard) != "none";

            byPlayer.SetModdata(Mod.Info.ModID, SerializerUtil.Serialize(savedata));
        }

        public bool TryAndGrowHair(IServerPlayer targetPlayer, string part, List<BarberTransform> barberProps, double diff, ref PlayerBarbershopData saveData)
        {
            saveData.timeSinceEdited[part] += diff;

            // TODO: Should handle fast-forward of more than one day.
            if (saveData.timeSinceEdited[part] > 1)
                return ApplyFirstMatchingTransform(targetPlayer, part, barberProps);

            return false;
        }

        public void ApplyBarberItemToPlayer(IServerPlayer fromPlayer, BarberPacket packet)
        {
            var item = sapi.World?.GetItem(new AssetLocation(packet.code));
            if (item == null)
                return;

            var targetPlayer = sapi.World.PlayerByUid(packet.targetUid) as IServerPlayer;
            if (targetPlayer == null)
                return;

            var itemBehaviour = item.GetCollectibleBehavior<CollectibleBehaviorBarber>(true);
            if (itemBehaviour == null)
                return;

            bool dirty = false;
            dirty |= ApplyFirstMatchingTransform(targetPlayer, HairBase, itemBehaviour.barberProperties.hairbase);
            dirty |= ApplyFirstMatchingTransform(targetPlayer, HairExtra, itemBehaviour.barberProperties.hairextra);
            dirty |= ApplyFirstMatchingTransform(targetPlayer, HairColor, itemBehaviour.barberProperties.haircolor);
            dirty |= ApplyFirstMatchingTransform(targetPlayer, Mustache, itemBehaviour.barberProperties.mustache);
            dirty |= ApplyFirstMatchingTransform(targetPlayer, Beard, itemBehaviour.barberProperties.beard);
            if (dirty)
            {
                targetPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
                targetPlayer.BroadcastPlayerData(false);
            }
        }

        public bool ApplyFirstMatchingTransform(IServerPlayer targetPlayer, string part, List<BarberTransform> barberTransforms)
        {
            if (barberTransforms == null)
                return false;

            foreach (var tf in barberTransforms)
                if (AttemptTransform(targetPlayer, part, tf.from, tf.to))
                    return true;

            return false;
        }

        public bool AttemptTransform(IServerPlayer targetPlayer, string part, string from, string to)
        {
            var playerBehaviour = targetPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            if (playerBehaviour == null)
                return false;

            string currentStyle = GetCurrentStyle(playerBehaviour, part);
            if (string.IsNullOrEmpty(currentStyle) || !WildcardUtil.Match(from, currentStyle))
                return false;

            // Change style
            foreach (var asp in playerBehaviour.AvailableSkinParts)
            {
                if (asp.Code == part)
                {
                    playerBehaviour.selectSkinPart(asp.Code, to);

                    var saveData = targetPlayer.GetModData<PlayerBarbershopData>(Mod.Info.ModID);
                    saveData.timeSinceEdited[part] = 0;
                    targetPlayer.SetModdata(Mod.Info.ModID, SerializerUtil.Serialize(saveData));

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
