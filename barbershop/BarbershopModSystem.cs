using ProtoBuf;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Barbershop
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BarbershopPacket
    {
        public string Name;
        public string Value;
    }

    public class BarbershopModSystem : ModSystem
    {
        public IClientNetworkChannel Channel;

        public ICoreClientAPI capi;

        public const string HairBase = "hairbase";
        public const string HairExtra = "hairextra";
        public const string HairColor = "haircolor";
        public const string Moustache = "mustache";
        public const string Beard = "beard";

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.Network
                .RegisterChannel(Mod.Info.ModID)
                .RegisterMessageType<BarbershopPacket>();
        }

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            capi = api;

            Channel = api.Network.GetChannel(Mod.Info.ModID);

            api.Event.PlayerEntitySpawn += Event_PlayerEntitySpawn;
        }

        public void Event_PlayerEntitySpawn(IClientPlayer byPlayer)
        {
            if (byPlayer == null || byPlayer != capi.World.Player)
                return;

            // Always refresh in case something weird happens where the player changes entity or something.
            var skinMod = capi.World.Player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            if (skinMod == null)
                return;

            var cmd = capi.ChatCommands.Create("barber")
                        .WithDescription("Barbershop main command")
                        .RequiresPlayer()
                        .RequiresPrivilege(Privilege.chat);

            foreach (var asp in skinMod.AvailableSkinParts)
            {
                if (asp.Code == HairBase || asp.Code == HairExtra || asp.Code == HairColor || asp.Code == Beard || asp.Code == Moustache)
                {
                    cmd.BeginSubCommand(asp.Code)
                        .WithArgs(capi.ChatCommands.Parsers.WordRange("style", asp.VariantsByCode.Keys.ToArray()))
                        .HandleWith(SendStyleToServer)
                        .EndSubCommand();
                }
            }
        }

        private TextCommandResult SendStyleToServer(TextCommandCallingArgs args)
        {
            Channel.SendPacket(new BarbershopPacket { Name = args.SubCmdCode, Value = args[0] as string });
            return new TextCommandResult { Status = EnumCommandStatus.Success };
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Network.GetChannel(Mod.Info.ModID)
                .SetMessageHandler<BarbershopPacket>(updateStyle);
        }

        public void updateStyle(IServerPlayer fromPlayer, BarbershopPacket packet)
        {
            var bh = fromPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
            if (bh == null) return;
            bh.selectSkinPart(packet.Name, packet.Value);
            fromPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
            fromPlayer.BroadcastPlayerData(true);
        }
    }
}
