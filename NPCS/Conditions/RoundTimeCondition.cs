﻿using Exiled.API.Features;
using NPCS.Talking;
using System.Collections.Generic;

namespace NPCS.Conditions
{
    internal class RoundTimeCondition : NodeCondition
    {
        public override string Name => "RoundTimeCondition";

        public override bool Check(Player player, Dictionary<string, string> args)
        {
            float value = float.Parse(args["value"].Replace('.', ','));
            return Utils.Utils.CompareWithType(args["comparsion_type"], (float)Round.ElapsedTime.TotalSeconds, value);
        }
    }
}