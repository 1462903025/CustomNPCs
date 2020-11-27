﻿using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Enums;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace NPCS.AI
{
    //Runs while target is alive, resets nav
    internal class AIShootTarget : AITarget
    {
        public override string Name => "AIShootTarget";

        public override string[] RequiredArguments => new string[] { "accuracy", "hitboxes", "firerate", "damage", "use_ammo" };

        public override bool Check(Npc npc)
        {
            return npc.CurrentAIPlayerTarget != null && Player.Dictionary.ContainsKey(npc.CurrentAIPlayerTarget.GameObject) && npc.CurrentAIPlayerTarget.IsAlive && !Physics.Linecast(npc.NPCPlayer.Position, npc.CurrentAIPlayerTarget.Position, npc.NPCPlayer.ReferenceHub.playerMovementSync.CollidableSurfaces);
        }

        private int accuracy;
        private readonly Dictionary<HitBoxType, int> hitboxes = new Dictionary<HitBoxType, int>();
        private float firerate;
        private int damage;
        private bool use_ammo;

        public override void Construct()
        {
            accuracy = int.Parse(Arguments["accuracy"]);
            foreach (string val in Arguments["hitboxes"].Split(','))
            {
                string[] splitted = val.Trim().Split(':');
                hitboxes.Add((HitBoxType)Enum.Parse(typeof(HitBoxType), splitted[0]), int.Parse(splitted[1]));
            }
            firerate = float.Parse(Arguments["firerate"].Replace('.', ','));
            damage = int.Parse(Arguments["damage"]);
            use_ammo = bool.Parse(Arguments["use_ammo"]);
        }

        public override float Process(Npc npc)
        {
            if (!npc.NPCPlayer.ReferenceHub.characterClassManager.IsAnyScp())
            {
                if (npc.AvailableWeapons.Count > 0)
                {
                    if (!npc.ItemHeld.IsWeapon(false))
                    {
                        npc.ItemHeld = npc.AvailableWeapons.Keys.ElementAt(0);
                    }

                    AmmoType ammo = AmmoType.Nato9;

                    switch (npc.ItemHeld)
                    {
                        case ItemType.GunE11SR:
                            ammo = AmmoType.Nato556;
                            break;
                        case ItemType.GunProject90:
                            ammo = AmmoType.Nato9;
                            break;
                        case ItemType.GunLogicer:
                            ammo = AmmoType.Nato762;
                            break;
                        case ItemType.GunMP7:
                            ammo = AmmoType.Nato762;
                            break;
                        case ItemType.GunUSP:
                            ammo = AmmoType.Nato9;
                            break;
                        case ItemType.GunCOM15:
                            ammo = AmmoType.Nato9;
                            break;
                    }

                    npc.Stop();
                    Vector3 heading = (npc.CurrentAIPlayerTarget.Position - npc.NPCPlayer.Position);
                    Quaternion lookRot = Quaternion.LookRotation(heading.normalized);
                    npc.NPCPlayer.Rotations = new Vector2(lookRot.eulerAngles.x, lookRot.eulerAngles.y);
                    bool miss = Plugin.Random.Next(0, 100) >= accuracy;
                    int hitbox_value = Plugin.Random.Next(0, 100);
                    HitBoxType hitbox = HitBoxType.NULL;
                    int min = int.MaxValue;
                    foreach (HitBoxType box in hitboxes.Keys)
                    {
                        if (hitbox_value < hitboxes[box] && hitboxes[box] <= min)
                        {
                            min = hitboxes[box];
                            hitbox = box;
                        }
                    }
                    npc.NPCPlayer.ReferenceHub.weaponManager.CallCmdShoot(miss ? npc.gameObject : npc.CurrentAIPlayerTarget.GameObject, hitbox, npc.NPCPlayer.CameraTransform.forward, npc.NPCPlayer.Position, npc.CurrentAIPlayerTarget.Position);

                    if (use_ammo)
                    {
                        npc.AvailableWeapons[0]--;
                        if (npc.AvailableWeapons[0] <= 0 && npc.NPCPlayer.Ammo[(int)ammo] > 0)
                        {
                            npc.NPCPlayer.ReferenceHub.weaponManager.CmdReload(true);
                            npc.AvailableWeapons[0] = Math.Min((int)npc.NPCPlayer.Ammo[(int)ammo], 40);
                            npc.NPCPlayer.Ammo[(int)ammo] -= (uint)npc.AvailableWeapons[0];
                        }
                    }

                    if (!npc.CurrentAIPlayerTarget.IsAlive)
                    {
                        npc.FireEvent(new Events.NPCTargetKilledEvent(npc, npc.CurrentAIPlayerTarget));
                    }
                }
                else
                {
                    IsFinished = true;
                }
                return firerate * Plugin.Instance.Config.NpcFireCooldownMultiplier * npc.NPCPlayer.ReferenceHub.weaponManager._fireCooldown;
            }
            else
            {
                float cd = 0f;
                npc.OnTargetLostBehaviour = Npc.TargetLostBehaviour.STOP;
                npc.Follow(npc.CurrentAIPlayerTarget);
                if (Vector3.Distance(npc.CurrentAIPlayerTarget.Position, npc.NPCPlayer.Position) <= 1.5f)
                {
                    if (npc.NPCPlayer.Role.Is939())
                    {
                        npc.NPCPlayer.GameObject.GetComponent<Scp939PlayerScript>().CallCmdShoot(npc.CurrentAIPlayerTarget.GameObject);
                    }
                    else
                    {
                        switch (npc.NPCPlayer.Role)
                        {
                            case RoleType.Scp106:
                                npc.NPCPlayer.GameObject.GetComponent<Scp106PlayerScript>().CallCmdMovePlayer(npc.CurrentAIPlayerTarget.GameObject, ServerTime.time);
                                cd = 2f;
                                break;

                            case RoleType.Scp173:
                                npc.NPCPlayer.GameObject.GetComponent<Scp173PlayerScript>().CallCmdHurtPlayer(npc.CurrentAIPlayerTarget.GameObject);
                                break;

                            case RoleType.Scp049:
                                npc.CurrentAIPlayerTarget.Hurt(99999f, DamageTypes.Scp049, npc.NPCPlayer.Nickname);
                                cd = PlayableScps.Scp049.KillCooldown;
                                break;

                            case RoleType.Scp0492:
                                npc.NPCPlayer.GameObject.GetComponent<Scp049_2PlayerScript>().CallCmdShootAnim();
                                npc.NPCPlayer.GameObject.GetComponent<Scp049_2PlayerScript>().CallCmdHurtPlayer(npc.CurrentAIPlayerTarget.GameObject);
                                cd = 1f;
                                break;
                        }
                    }
                    if (!npc.CurrentAIPlayerTarget.IsAlive)
                    {
                        npc.AttachedCoroutines.Add(MEC.Timing.CallDelayed(0.1f, () =>
                         {
                             npc.FireEvent(new Events.NPCTargetKilledEvent(npc, npc.CurrentAIPlayerTarget));
                         }));

                        npc.Stop();
                    }
                }
                return cd;
            }
        }

        protected override AITarget CreateInstance()
        {
            return new AIShootTarget();
        }
    }
}