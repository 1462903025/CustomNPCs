﻿using Exiled.API.Features;
using System.Collections.Generic;

namespace NPCS.Actions
{
    internal class HeldItemAction : NPCS.Talking.NodeAction
    {
        public override string Name => "HeldItemAction";

        public override void Process(Npc npc, Player player, Dictionary<string, string> args)
        {
            npc.PlayerInstance.Inventory.Network_curItemSynced = ((ItemType)int.Parse(args["item_type"]));
        }
    }
}