namespace SimpleInventorySort
{
	public class CommandDebug : CommandHandlerBase
	{
		public override string GetCommandText()
		{
			return "debug";
		}

		public override void HandleCommand(string[] words)
		{
			Core.Debug = !Core.Debug;

			if (Core.Debug) {
				Communication.Message("Sorting Debug Toggled On.");
			}
			else {
				Communication.Message("Sorting Debug Toggled Off.");
			}
		}
	}
}
