using Sandbox.ModAPI;

namespace SimpleInventorySort
{
	static internal class Communication
	{
		public static void Message(string text)
		{
			MyAPIGateway.Utilities.ShowMessage("[Inventory Sort]", text);
		}

		public static void Notification(string text, int disappearTimeMS = 2000, string font = "White")
		{
			MyAPIGateway.Utilities.ShowNotification(text, disappearTimeMS, font);
		}
	}
}
