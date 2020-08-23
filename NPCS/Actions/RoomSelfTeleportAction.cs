﻿using Exiled.API.Features;
using NPCS.Talking;
using System;
using System.Collections.Generic;
using System.Text;

using System.Linq;

namespace NPCS.Actions
{
    class RoomSelfTeleportAction : NodeAction
    {
        public override string Name => "RoomSelfTeleportAction";

        public override void Process(Npc npc, Player player, Dictionary<string, string> args)
        {
            Room r = Map.Rooms.Where(rm => rm.Name.Equals(args["room"], StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if(r != null)
            {
                npc.NPCPlayer.Position = r.Position;
            }
            else
            {
                Log.Error($"Room {args["room"]} not found!");
            }
        }
    }
}
