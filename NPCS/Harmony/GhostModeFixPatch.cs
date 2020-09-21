﻿using CustomPlayerEffects;
using Exiled.API.Features;
using HarmonyLib;
using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;
using Scp096 = PlayableScps.Scp096;

namespace NPCS.Harmony
{
    [HarmonyPatch(typeof(PlayerPositionManager), nameof(PlayerPositionManager.TransmitData))]
    [HarmonyPriority(Priority.First)]
    internal class GhostModeFixPatch
    {
        private static bool Prefix(PlayerPositionManager __instance)
        {
            try
            {
                if (++__instance._frame != __instance._syncFrequency)
                    return false;

                __instance._frame = 0;

                List<GameObject> players = PlayerManager.players;
                __instance._usedData = players.Count;

                if (__instance._receivedData == null
                    || __instance._receivedData.Length < __instance._usedData)
                {
                    __instance._receivedData = new PlayerPositionData[__instance._usedData * 2];
                }

                for (int index = 0; index < __instance._usedData; ++index)
                    __instance._receivedData[index] = new PlayerPositionData(ReferenceHub.GetHub(players[index]));

                if (__instance._transmitBuffer == null
                    || __instance._transmitBuffer.Length < __instance._usedData)
                {
                    __instance._transmitBuffer = new PlayerPositionData[__instance._usedData * 2];
                }

                foreach (GameObject gameObject in players)
                {

                    if (Npc.Dictionary.ContainsKey(gameObject))
                    {
                        continue;
                    }

                    Player player = Player.Get(gameObject);
                    Array.Copy(__instance._receivedData, __instance._transmitBuffer, __instance._usedData);

                    if (player.Role.Is939())
                    {
                        for (int index = 0; index < __instance._usedData; ++index)
                        {
                            if (__instance._transmitBuffer[index].position.y < 800f)
                            {
                                ReferenceHub hub2 = ReferenceHub.GetHub(players[index]);

                                if (hub2.characterClassManager.CurRole.team != Team.SCP
                                    && hub2.characterClassManager.CurRole.team != Team.RIP
                                    && !players[index]
                                        .GetComponent<Scp939_VisionController>()
                                        .CanSee(player.ReferenceHub.characterClassManager.Scp939))
                                {
                                    __instance._transmitBuffer[index] = new PlayerPositionData(Vector3.up * 6000f, 0.0f, __instance._transmitBuffer[index].playerID);
                                }
                            }
                        }
                    }
                    else if (player.Role != RoleType.Spectator && player.Role != RoleType.Scp079)
                    {
                        for (int index = 0; index < __instance._usedData; ++index)
                        {
                            PlayerPositionData ppd = __instance._transmitBuffer[index];
                            Player currentTarget = Player.Get(players[index]);
                            Scp096 scp096 = player.ReferenceHub.scpsController.CurrentScp as Scp096;

                            bool canSee = true;
                            bool shouldRotate = false;

                            if (currentTarget?.ReferenceHub == null)
                                continue;

#pragma warning disable CS0618 // Type or member is obsolete
                            if (currentTarget.IsInvisible || player.TargetGhostsHashSet.Contains(ppd.playerID) || player.TargetGhosts.Contains(ppd.playerID))
#pragma warning restore CS0618 // Type or member is obsolete
                            {
                                canSee = false;
                            }
                            else
                            {
                                Vector3 vector3 = ppd.position - player.ReferenceHub.playerMovementSync.RealModelPosition;
                                if (Math.Abs(vector3.y) > 35f)
                                {
                                    canSee = false;
                                }
                                else
                                {
                                    float sqrMagnitude = vector3.sqrMagnitude;
                                    if (player.ReferenceHub.playerMovementSync.RealModelPosition.y < 800f)
                                    {
                                        if (sqrMagnitude >= 1764f)
                                        {
                                            canSee = false;
                                        }
                                    }
                                    else if (sqrMagnitude >= 7225f)
                                    {
                                        canSee = false;
                                    }

                                    if (canSee)
                                    {
                                        if (ReferenceHub.TryGetHub(ppd.playerID, out ReferenceHub hub2))
                                        {
                                            if (scp096 != null
                                                && scp096.Enraged
                                                && !scp096.HasTarget(hub2)
                                                && hub2.characterClassManager.CurRole.team != Team.SCP)
                                            {
#if DEBUG
                                                Log.Debug($"[Scp096@GhostModePatch] {player.UserId} can't see {hub2.characterClassManager.UserId}");
#endif
                                                canSee = false;
                                            }
                                            else if (hub2.playerEffectsController.GetEffect<Scp268>().Enabled)
                                            {
                                                bool flag = false;
                                                if (scp096 != null)
                                                    flag = scp096.HasTarget(hub2);

                                                if (player.Role != RoleType.Scp079
                                                    && player.Role != RoleType.Spectator
                                                    && !flag)
                                                {
                                                    canSee = false;
                                                }
                                            }
                                        }

                                        switch (player.Role)
                                        {
                                            case RoleType.Scp173 when (!Exiled.Events.Events.Instance.Config.CanTutorialBlockScp173 && currentTarget.Role == RoleType.Tutorial) || Scp173.TurnedPlayers.Contains(currentTarget):
                                                shouldRotate = true;
                                                break;

                                            case RoleType.Scp096 when !Exiled.Events.Events.Instance.Config.CanTutorialTriggerScp096 && currentTarget.Role == RoleType.Tutorial:
                                                shouldRotate = true;
                                                break;
                                        }
                                    }
                                }
                            }
                            if (!canSee)
                            {
                                ppd = new PlayerPositionData(Vector3.up * 6000f, 0f, ppd.playerID);
                            }
                            else if (shouldRotate)
                            {
                                ppd = new PlayerPositionData(ppd.position, Quaternion.LookRotation(FindLookRotation(player.Position, currentTarget.Position)).eulerAngles.y, ppd.playerID);
                            }

                            __instance._transmitBuffer[index] = ppd;
                        }
                    }

                    NetworkConnection networkConnection = player.ReferenceHub.characterClassManager.netIdentity.isLocalPlayer
                        ? NetworkServer.localConnection
                        : player.ReferenceHub.characterClassManager.netIdentity.connectionToClient;
                    if (__instance._usedData <= 20)
                    {
                        networkConnection.Send(
                            new PlayerPositionManager.PositionMessage(__instance._transmitBuffer, (byte)__instance._usedData, 0), 1);
                    }
                    else
                    {
                        byte part;
                        for (part = 0; part < __instance._usedData / 20; ++part)
                            networkConnection.Send(new PlayerPositionManager.PositionMessage(__instance._transmitBuffer, 20, part), 1);
                        byte count = (byte)(__instance._usedData % (part * 20));
                        if (count > 0)
                            networkConnection.Send(new PlayerPositionManager.PositionMessage(__instance._transmitBuffer, count, part), 1);
                    }
                }

                return false;
            }
            catch (Exception exception)
            {
                Log.Error($"GhostMode error: {exception}");
                return true;
            }
        }

        private static Vector3 FindLookRotation(Vector3 player, Vector3 target) => (target - player).normalized;
    }
}