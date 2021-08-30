using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using Sandbox.ModAPI;
//using Sandbox.ModAPI.Interfaces;
using VRageMath;
using Sandbox.Common.ObjectBuilders;
using SimpleInventorySort;
using System.Text.RegularExpressions;
//using Sandbox.Definitions;
using VRage;
using VRage.ObjectBuilders;
using VRage.ModAPI;

namespace SimpleInventorySort
{
    public class CommandDebug : CommandHandlerBase
    {
        public override String GetCommandText()
        {
            return "debug";
        }

        public override void HandleCommand(String[] words)
        {
            Core.Debug = !Core.Debug;

            if (Core.Debug)
                Communication.Message("Sorting Debug Toggled On.");
            else
                Communication.Message("Sorting Debug Toggled Off.");
        }
    }
}
