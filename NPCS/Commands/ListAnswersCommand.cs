﻿using CommandSystem;
using Exiled.API.Features;
using RemoteAdmin;
using System;
using System.Linq;
using UnityEngine;

namespace NPCS.Commands
{
    [CommandHandler(typeof(CommandSystem.ClientCommandHandler))]
    internal class ListAnswersCommand : ICommand
    {
        public string Command => "lansw";

        public string[] Aliases => new string[] { "lsa" };

        public string Description => "Command which allows u to get answers list from NPCs";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (sender is PlayerCommandSender player)
            {
                Player s = Player.Get(player.PlayerId);
                bool flag = false;
                foreach (NPCS.Npc obj_npc in NPCS.Npc.List)
                {
                    if (!obj_npc.IsNPC())
                    {
                        continue;
                    }
                    if (Vector3.Distance(obj_npc.PlayerInstance.Position, s.Position) < 3f)
                    {
                        if (obj_npc.TalkingStates.ContainsKey(s))
                        {
                            obj_npc.TalkingStates[s].Send(obj_npc.PlayerInstance.Nickname, s);
                            flag = true;
                            break;
                        }
                    }
                }
                if (!flag)
                {
                    response = Plugin.Instance.Translation.NpcNotFound;
                    return false;
                }
                response = null;
            }
            else
            {
                response = Plugin.Instance.Translation.OnlyPlayers;
                return false;
            }

            return true;
        }
    }
}