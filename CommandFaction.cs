namespace SimpleInventorySort
{
	public class CommandFaction : CommandHandlerBase
	{
		public override string GetCommandText()
		{
			return "faction";
		}

		public override void HandleCommand(string[] words)
		{
			Settings.Instance.Faction = !Settings.Instance.Faction;

			if (Settings.Instance.Faction) {
				Communication.Message("Sorting all faction and shared blocks toggled ON.");
			}
			else {
				Communication.Message("Sorting all faction and shared blocks toggled OFF.");
			}
		}
	}
}
