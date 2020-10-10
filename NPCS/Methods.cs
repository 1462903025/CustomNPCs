﻿using Exiled.API.Extensions;
using Exiled.API.Features;
using MEC;
using Mirror;
using NPCS.AI;
using NPCS.Events;
using NPCS.Navigation;
using NPCS.Talking;
using RemoteAdmin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.TypeInspectors;
using YamlDotNet.Serialization.NamingConventions;

namespace NPCS
{
    public class Methods
    {
        public static Npc CreateNPC(Vector3 pos, Vector2 rot, Vector3 scale, RoleType type = RoleType.ClassD, ItemType itemHeld = ItemType.None, string name = "(EMPTY)", string root_node = "default_node.yml")
        {
            GameObject obj =
                UnityEngine.Object.Instantiate(
                    NetworkManager.singleton.spawnPrefabs.FirstOrDefault(p => p.gameObject.name == "Player"));
            CharacterClassManager ccm = obj.GetComponent<CharacterClassManager>();

            pos = new Vector3(pos.x, pos.y - (1f - scale.y) * Plugin.Instance.Config.NpcSizePositionMultiplier, pos.z);

            obj.transform.localScale = scale;
            obj.transform.position = pos;

            QueryProcessor processor = obj.GetComponent<QueryProcessor>();

            processor.NetworkPlayerId = QueryProcessor._idIterator++;
            processor._ipAddress = "127.0.0.WAN";

            ccm.CurClass = type;
            obj.GetComponent<PlayerStats>().SetHPAmount(ccm.Classes.SafeGet(type).maxHP);

            obj.GetComponent<NicknameSync>().Network_myNickSync = name;

            ServerRoles roles = obj.GetComponent<ServerRoles>();

            roles.MyText = "NPC";
            roles.MyColor = "red";

            NetworkServer.Spawn(obj);
            PlayerManager.AddPlayer(obj); //I'm not sure if I need this

            Player ply_obj = new Player(obj);
            Player.Dictionary.Add(obj, ply_obj);

            Player.IdsCache.Add(ply_obj.Id, ply_obj);

            Npc npcc = obj.AddComponent<Npc>();

            npcc.MovementSpeed = CharacterClassManager._staticClasses[(int)type].walkSpeed;

            npcc.ItemHeld = itemHeld;
            npcc.RootNode = TalkNode.FromFile(Path.Combine(Config.NPCs_nodes_path, root_node));

            npcc.NPCPlayer.ReferenceHub.transform.localScale = scale;

            npcc.AttachedCoroutines.Add(Timing.CallDelayed(0.3f, () =>
            {
                npcc.NPCPlayer.ReferenceHub.playerMovementSync.OverridePosition(pos, 0, true);
                npcc.NPCPlayer.Rotations = rot;
            }));

            npcc.AttachedCoroutines.Add(Timing.CallDelayed(0.4f, () =>
            {
                npcc.FireEvent(new NPCOnCreatedEvent(npcc, null));
            }));

            return npcc;
        }

        //NPC format
        //name: SomeName
        //health: -1
        //role: Scientist
        //scale: [1, 1, 1]
        //item_held: GunLogicer
        //root_node: default_node.yml
        //god_mode: false
        //is_exclusive: true
        //events: []
        //ai_enabled: false
        //ai: []

        private class NpcNodeSerializationInfo
        {
            public string Token { get; set; }
        }

        private class NpcEventSerializationInfo : NpcNodeSerializationInfo
        {
            public List<NpcNodeWithArgsSerializationInfo> Actions { get; set; }
        }

        private class NpcNodeWithArgsSerializationInfo : NpcNodeSerializationInfo
        {
            public Dictionary<string, string> Args { get; set; }
        }

        private class NpcSerializationInfo
        {
            public string Name { get; set; }
            public int Health { get; set; }
            public RoleType Role { get; set; }
            public float[] Scale { get; set; }

            [YamlMember(Alias = "item_held")]
            public ItemType ItemHeld { get; set; }

            [YamlMember(Alias = "root_node")]
            public string RootNode { get; set; }

            [YamlMember(Alias = "god_mode")]
            public bool GodMode { get; set; }

            [YamlMember(Alias = "is_exclusive")]
            public bool IsExclusive { get; set; }

            public NpcEventSerializationInfo[] Events { get; set; }

            [YamlMember(Alias = "ai_enabled")]
            public bool AiEnabled { get; set; }

            public NpcNodeWithArgsSerializationInfo[] Ai { get; set; }
        }

        public static Npc CreateNPC(Vector3 pos, Vector2 rot, string path)
        {
            try
            {
                var input = new StringReader(File.ReadAllText(Path.Combine(Config.NPCs_root_path, path)));

                var deserializer = new DeserializerBuilder()
                                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                                    // Workaround to remove YamlAttributesTypeInspector
                                    .WithTypeInspector(inner => inner, s => s.InsteadOf<YamlAttributesTypeInspector>())
                                    .WithTypeInspector(
                                        inner => new YamlAttributesTypeInspector(inner),
                                        s => s.Before<NamingConventionTypeInspector>()
                                    )
                                    .Build();

                NpcSerializationInfo raw_npc = deserializer.Deserialize<NpcSerializationInfo>(input);

                Npc n = CreateNPC(pos, rot, new Vector3(raw_npc.Scale[0], raw_npc.Scale[1], raw_npc.Scale[2]), raw_npc.Role, raw_npc.ItemHeld, raw_npc.Name, raw_npc.RootNode);

                n.NPCPlayer.IsGodModeEnabled = raw_npc.GodMode;

                n.IsExclusive = raw_npc.IsExclusive;

                n.SaveFile = path;

                int health = raw_npc.Health;

                if (health > 0)
                {
                    n.NPCPlayer.MaxHealth = health;
                    n.NPCPlayer.Health = health;
                }

                Log.Info("Parsing events...");

                foreach (NpcEventSerializationInfo info in raw_npc.Events)
                {
                    Dictionary<NodeAction, Dictionary<string, string>> actions_mapping = new Dictionary<NodeAction, Dictionary<string, string>>();
                    foreach (NpcNodeWithArgsSerializationInfo action in info.Actions)
                    {
                        NodeAction act = NodeAction.GetFromToken(action.Token);
                        if (act != null)
                        {
                            actions_mapping.Add(act, action.Args);
                        }
                        else
                        {
                            Log.Error($"Failed to event action: {info.Token} (invalid token)");
                        }
                    }
                    n.Events.Add(info.Token, actions_mapping);
                }

                n.AIEnabled = raw_npc.AiEnabled;

                foreach (NpcNodeWithArgsSerializationInfo info in raw_npc.Ai)
                {
                    AI.AITarget act = AITarget.GetFromToken(info.Token);
                    if (act != null)
                    {
                        Log.Debug($"Recognized ai target: {act.Name}", Plugin.Instance.Config.VerboseOutput);
                        act.Arguments = info.Args;
                        n.AIQueue.AddLast(act);
                    }
                    else
                    {
                        Log.Error($"Failed to parse ai node: {info.Token} (invalid token)");
                    }
                }

                return n;
            }
            catch (Exception e)
            {
                Log.Error($"Failed to load NPC from {path}: {e}");
                return null;
            }
        }

        //Oh... More shitcode!
        public static void GenerateNavGraph()
        {
            Log.Info("[NAV] Generating navigation graph...");
            StreamReader sr = File.OpenText(Config.NPCs_nav_mappings_path);
            var deserializer = new DeserializerBuilder().Build();
            Dictionary<string, List<NavigationNode.NavNodeSerializationInfo>> manual_mappings = deserializer.Deserialize<Dictionary<string, List<NavigationNode.NavNodeSerializationInfo>>>(sr);
            sr.Close();
            foreach (Room r in Map.Rooms)
            {
                string rname = r.Name.RemoveBracketsOnEndOfName();
                if (!manual_mappings.ContainsKey(rname))
                {
                    NavigationNode node = NavigationNode.Create(r.Position, $"AUTO_Room_{r.Name}".Replace(' ', '_'));
                    foreach (Door d in r.Doors)
                    {
                        if (d.gameObject.transform.position == Vector3.zero)
                        {
                            continue;
                        }
                        NavigationNode new_node = NavigationNode.Create(d.gameObject.transform.position, $"AUTO_Door_{(d.DoorName.IsEmpty() ? d.gameObject.transform.position.ToString() : d.DoorName)}".Replace(' ', '_'));
                        if (new_node == null)
                        {
                            new_node = NavigationNode.AllNodes[$"AUTO_Door_{(d.DoorName.IsEmpty() ? d.gameObject.transform.position.ToString() : d.DoorName)}".Replace(' ', '_')];
                        }
                        else
                        {
                            new_node.AttachedDoor = d;
                        }
                        node.LinkedNodes.Add(new_node);
                        new_node.LinkedNodes.Add(node);
                    }
                }
                else
                {
                    Log.Debug($"Loading manual mappings for room {r.Name}");
                    List<NavigationNode.NavNodeSerializationInfo> nodes = manual_mappings[rname];
                    int i = 0;
                    foreach (Door d in r.Doors)
                    {
                        if (d.gameObject.transform.position == Vector3.zero)
                        {
                            continue;
                        }
                        NavigationNode new_node = NavigationNode.Create(d.gameObject.transform.position, $"AUTO_Door_{(d.DoorName.IsEmpty() ? d.gameObject.transform.position.ToString() : d.DoorName)}".Replace(' ', '_'));
                        if (new_node != null)
                        {
                            new_node.AttachedDoor = d;
                        }
                        else
                        {
                            new_node = NavigationNode.AllNodes[$"AUTO_Door_{(d.DoorName.IsEmpty() ? d.gameObject.transform.position.ToString() : d.DoorName)}".Replace(' ', '_')];
                        }
                    }
                    foreach (NavigationNode.NavNodeSerializationInfo info in nodes)
                    {
                        NavigationNode node = NavigationNode.Create(info, $"MANUAL_Room_{r.Name}_{i}", rname);
                        foreach (NavigationNode d in NavigationNode.AllNodes.Values.Where(nd => nd != node && Vector3.Distance(nd.Position, node.Position) < 3f))
                        {
                            node.LinkedNodes.Add(d);
                            d.LinkedNodes.Add(node);

                            Log.Info($"Linked {node.Name} and {d.Name}");
                        }
                        i++;
                    }
                }
            }
        }
    }
}