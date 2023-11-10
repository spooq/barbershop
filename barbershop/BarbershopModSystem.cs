using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
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

    public class BarbershopModSystem : ModSystem
    {
        public IClientNetworkChannel Channel;

        public ICoreServerAPI sapi;

        public const string HairBase = "hairbase";
        public const string HairExtra = "hairextra";
        public const string HairColor = "haircolor";
        public const string Moustache = "mustache";
        public const string Beard = "beard";

        // Server-side only, holds each part a player has edited. Stored in the world save file.
        // Player name -> elapsed day that part was set
        double lastCheckOfElapsedDays = -1;
        public Dictionary<string, Dictionary<string, double>> PlayerEditedParts;

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

            sapi.Event.PlayerJoin += OnPlayerJoin;
            sapi.Event.SaveGameLoaded += OnSaveGameLoading;
            sapi.Event.GameWorldSave += OnSaveGameSaving;
            sapi.Event.ServerRunPhase(EnumServerRunPhase.RunGame, OnServerRunGame);

            PlayerEditedParts = new();

            api.Network.GetChannel(Mod.Info.ModID)
                .SetMessageHandler<BarberPacket>(onTransformPart);
        }

        private void OnPlayerJoin(IServerPlayer byPlayer)
        {
            if (!PlayerEditedParts.ContainsKey(byPlayer.PlayerUID))
                PlayerEditedParts[byPlayer.PlayerUID] = new();
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

                foreach (var onlinePlr in sapi.World.AllOnlinePlayers.Cast<IServerPlayer>())
                {
                    bool dirty = false;
                    foreach (var part in PlayerEditedParts[onlinePlr.PlayerUID])
                    {
                        PlayerEditedParts[onlinePlr.PlayerUID][part.Key] += diff;

                        // Grow and reset
                        if (part.Value > 1f)
                        {
                            // when loop over all transforms remember to break after success
                            switch (part.Key)
                            {
                                case Beard:
                                    dirty |= TransformPart(onlinePlr, part.Key, "none", "brd-stubble");
                                    break;
                                case Moustache:
                                    dirty |= TransformPart(onlinePlr, part.Key, "none", "mst-line01-pencil");
                                    break;
                            }
                        }
                    }

                    if (dirty)
                    {
                        onlinePlr.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
                        onlinePlr.BroadcastPlayerData(false);
                    }
                }
            }

            sapi.World.RegisterCallback(OnTimePassed, 1000);
        }

        public void onTransformPart(IServerPlayer fromPlayer, BarberPacket packet)
        {
            var item = sapi.World.GetItem(new AssetLocation(packet.code));
            if (item != null)
            {
                var targetPlayer = sapi.World.PlayerByUid(packet.targetUid) as IServerPlayer;
                if (targetPlayer == null)
                    return;

                var appliedParts = new SortedSet<string>();
                var itemBehaviour = item.GetCollectibleBehavior<CollectibleBehaviorBarber>(true);

                foreach (var tf in itemBehaviour.barberProperties.hairbase.transforms)
                    if (!appliedParts.Contains(HairBase) && TransformPart(targetPlayer, HairBase, tf.from, tf.to))
                        appliedParts.Add(HairBase);

                foreach (var tf in itemBehaviour.barberProperties.hairbase.transforms)
                    if (!appliedParts.Contains(HairExtra) && TransformPart(targetPlayer, HairExtra, tf.from, tf.to))
                        appliedParts.Add(HairExtra);

                foreach (var tf in itemBehaviour.barberProperties.moustache.transforms)
                    if (!appliedParts.Contains(Moustache) && TransformPart(targetPlayer, Moustache, tf.from, tf.to))
                        appliedParts.Add(Moustache);

                foreach (var tf in itemBehaviour.barberProperties.beard.transforms)
                    if (!appliedParts.Contains(Beard) && TransformPart(targetPlayer, Beard, tf.from, tf.to))
                        appliedParts.Add(Beard);

                targetPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
                targetPlayer.BroadcastPlayerData(true);
            }
        }

        public bool TransformPart(IServerPlayer targetPlayer, string part, string from, string to)
        {
            var playerBehaviour = targetPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            if (playerBehaviour == null)
                return false;

            // Find current pieces
            string currentVariant = null;
            foreach (var appliedPart in playerBehaviour.AppliedSkinParts)
            {
                if (appliedPart.PartCode == part)
                {
                    currentVariant = appliedPart.Code;
                    break;
                }
            }
            if (string.IsNullOrEmpty(currentVariant))
                return false;

            if (!WildcardUtil.Match(from, currentVariant))
                return false;

            // Change style
            foreach (var asp in playerBehaviour.AvailableSkinParts)
            {
                if (asp.Code == part)
                {
                    playerBehaviour.selectSkinPart(asp.Code, to);
                    PlayerEditedParts[targetPlayer.PlayerUID][part] = 0;
                    return true;
                }
            }

            return false;
        }

        public void SetPart(IServerPlayer targetPlayer, string part, string to)
        {
            var playerBehaviour = targetPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            if (playerBehaviour == null)
                return;

            // Change style
            foreach (var asp in playerBehaviour.AvailableSkinParts)
            {
                if (asp.Code == part)
                {
                    playerBehaviour.selectSkinPart(asp.Code, to);
                    return;
                }
            }
        }

        public void OnSaveGameSaving()
        {
            sapi.WorldManager.SaveGame.StoreData(Mod.Info.ModID, SerializerUtil.Serialize(PlayerEditedParts));
        }

        public void OnSaveGameLoading()
        {
            byte[] data = sapi.WorldManager.SaveGame.GetData(Mod.Info.ModID);
            PlayerEditedParts = data == null
                ? new()
                : SerializerUtil.Deserialize<Dictionary<string, Dictionary<string, double>>>(data);
        }
    }
}
