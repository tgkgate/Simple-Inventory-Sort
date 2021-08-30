using System;
using Sandbox.ModAPI;
using VRage.Game;

namespace SimpleInventorySort
{
    static class Communication
    {
        static public void Message(String text)
        {
            MyAPIGateway.Utilities.ShowMessage("[Inventory Sort]", text);
        }

        static public void Notification(String text, int disappearTimeMS = 2000, string font = "White")
        {
            MyAPIGateway.Utilities.ShowNotification(text, disappearTimeMS, font);
        }
    }
}
