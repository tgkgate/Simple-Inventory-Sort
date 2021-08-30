using Sandbox.Definitions;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;

namespace SimpleInventorySort
{
	/// <summary>
	/// Valid Sort Operations
	/// </summary>
	public enum SortOperatorOptions
	{
		Ignore,         // :Ignore Option - This item will not be pulled
		MaxCount,       // :MaxCount Option - Maximum Items to pull
		Split,          // :Split Option - Splits items between multiple other SortDefinitionItems with the same Priority
		Prioirity,      // :Priority Option - Higher priority pulls from lower priority entities
		Override,       // :Override Option - When used in tandem with a wildcard, this operator is run instead of the wildcard operator explicitly
		Share           // :Share Option - Actively equalizes items between other entities in share
	}

	/// <summary>
	/// This class is the definition class for a sort operation
	/// </summary>
	public class SortDefinitionItem
	{
		private readonly Dictionary<SortOperatorOptions, long> m_sortOperators;
		private static HashSet<string> m_validDefinitions;
		private MyDefinitionBase m_definition;
		private IMyEntity m_entity;
		private string m_sortDefinitionString;

		public List<IMyEntity> splitGroup;
		public List<IMyEntity> shareGroup;

		// Initializer
		public SortDefinitionItem()
		{
			m_sortOperators = new Dictionary<SortOperatorOptions, long>();
		}

		// Properties

		/// <summary>
		/// Actual MyDefinitionBase of this sort
		/// </summary>
		public MyDefinitionBase Definition {
			get => m_definition;

			set => m_definition = value;
		}

		/// <summary>
		/// This is the entity that is using this sort definition.
		/// </summary>
		public IMyEntity ContainerEntity {
			get => m_entity;

			set => m_entity = value;
		}

		public Dictionary<SortOperatorOptions, long> SortOperators => m_sortOperators;

		/// <summary>
		/// Raw definition string that created this item
		/// </summary>
		public string SortDefinitionString => m_sortDefinitionString;

		/// <summary>
		/// This is the maximum amount of this item this sort operation wants
		/// </summary>
		public long MaxCount {
			get {
				if (m_sortOperators.ContainsKey(SortOperatorOptions.MaxCount)) {
					return m_sortOperators[SortOperatorOptions.MaxCount];
				}

				return 0;
			}
		}

		/// <summary>
		/// Priority of this item.  Higher priority sort items can take items from lower priority sort items
		/// </summary>
		public long Priority {
			get {
				if (m_sortOperators.ContainsKey(SortOperatorOptions.Prioirity)) {
					return m_sortOperators[SortOperatorOptions.Prioirity];
				}

				return long.MaxValue;
			}
		}

		/// <summary>
		/// The amount of splits we're doing.  The more we're splitting the higher this value
		/// </summary>
		public long Split {
			get {
				if (m_sortOperators.ContainsKey(SortOperatorOptions.Split)) {
					return m_sortOperators[SortOperatorOptions.Split];
				}

				return 0;
			}

			set {
				if (m_sortOperators.ContainsKey(SortOperatorOptions.Split)) {
					m_sortOperators[SortOperatorOptions.Split] = value;
				}
			}
		}

		/// <summary>
		/// Are we ignoring this object?
		/// </summary>
		public bool Ignore {
			get {
				if (m_sortOperators.ContainsKey(SortOperatorOptions.Ignore)) {
					return m_sortOperators[SortOperatorOptions.Ignore] != 0 ? true : false;
				}

				return false;
			}
		}

		// Utility
		public static List<SortDefinitionItem> CreateFromEntity(IMyEntity entity)
		{
			List<SortDefinitionItem> result = new List<SortDefinitionItem>();

			try {
				if (entity is not IMyTerminalBlock terminal) {
					return result;
				}

				string customName = terminal.CustomName;
				string customData = terminal.CustomData;

				if ((customName == null || customName == "") && (customData == null || customData == "")) {
					return result;
				}

				Regex regexObj = new Regex(".*[[|(](.*)[]|)]", RegexOptions.Singleline);
				Match matchResults = regexObj.Match(customName);

				// check customName for definitions
				while (matchResults.Success) {
					if (matchResults.Groups.Count < 2 || matchResults.Groups[1].Value == "") {
						matchResults = matchResults.NextMatch();
						continue;
					}

					string componentList = matchResults.Groups[1].Value;
					Regex splitRegexObj = new Regex("\"(?:[^\"]|\"\")*\"|[^,]*", RegexOptions.Singleline);
					Match splitResults = splitRegexObj.Match(componentList);

					while (splitResults.Success) {
						if (splitResults.Value == "") {
							splitResults = splitResults.NextMatch();
							continue;
						}

						string componentName = splitResults.Value.Replace(" ", "");

						if (componentName != "") {
							TryParseSortItem(result, componentName, entity);
						}

						splitResults = splitResults.NextMatch();
					}

					matchResults = matchResults.NextMatch();
				}

				// Now check customData for definitions
				matchResults = regexObj.Match(customData);

				while (matchResults.Success) {
					if (matchResults.Groups.Count < 2 || matchResults.Groups[1].Value == "") {
						matchResults = matchResults.NextMatch();
						continue;
					}

					string componentList = matchResults.Groups[1].Value;
					Regex splitRegexObj = new Regex("\"(?:[^\"]|\"\")*\"|[^,]*", RegexOptions.Singleline);
					Match splitResults = splitRegexObj.Match(componentList);

					while (splitResults.Success) {
						if (splitResults.Value == "") {
							splitResults = splitResults.NextMatch();
							continue;
						}

						string componentName = splitResults.Value.Replace(" ", "");

						if (componentName != "") {
							TryParseSortItem(result, componentName, entity);
						}

						splitResults = splitResults.NextMatch();
					}

					matchResults = matchResults.NextMatch();
				}

			}

			catch (Exception ex) {
				Logging.Instance.WriteLine(string.Format("BuildSortListFromEntity(): {0}", ex.ToString()));
			}

			return result;
		}

		public static void TryParseSortItem(List<SortDefinitionItem> currentDefinitions, string definition, IMyEntity entity)
		{
			try {
				MyEntity testEntity = entity as MyEntity;

				if (!testEntity.HasInventory) {
					return;
				}

				if (testEntity.InventoryCount < 1) {
					return;
				}

				List<SortDefinitionItem> newList = new List<SortDefinitionItem>();

				if (m_validDefinitions == null) {
					m_validDefinitions = new HashSet<string> {
						"MyObjectBuilder_Component",
						"MyObjectBuilder_Ore",
						"MyObjectBuilder_PhysicalGunObject",
						"MyObjectBuilder_PhysicalObject",
						"MyObjectBuilder_Ingot",
						"MyObjectBuilder_AmmoMagazine",
						"MyObjectBuilder_OxygenContainerObject",
						"MyObjectBuilder_GasContainerObject",
						"MyObjectBuilder_ConsumableItem"
					};
				}

				bool not = false;
				bool split = false;
				bool opOverride = false;
				long maxCount = 0;
				long priority = long.MaxValue;

				if (definition.Contains(":")) {
					string[] compItems = definition.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
					definition = compItems[0];

					for (int r = 1; r < compItems.Length; r++) {
						if (compItems[r].ToLower().Equals("ignore")) {
							not = true;
						}
						else if (compItems[r].ToLower().Equals("split")) {
							split = true;
						}
						else if (compItems[r].ToLower().Equals("override")) {
							opOverride = true;
						}
						else if (compItems[r].Length > 1 && compItems[r].ToLower()[0].Equals('p')) {
							long.TryParse(compItems[r].ToLower()[1..], out priority);
						}
						else {
							long.TryParse(compItems[r], out maxCount);

							if (maxCount > long.MaxValue / 1000000) {
								maxCount = 0;
							}
						}
					}
				}

				if (definition.ToLower().Equals("ore")) {
					definition = "ore/";
				}

				int count = 0;

				foreach (MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions()) {
					if (!m_validDefinitions.Contains(def.Id.TypeId.ToString())) {
						continue;
					}

					if (def.Id.ToString().ToLower().Contains(definition.ToLower())) {
						// A sort item that has operators always overrides one without.  Check if we have a definition
						// that has operators already.  If we do, then we can skip creating a new definition, otherwise, this new definition will
						// override the old (based on suggestion from Dark)

						SortDefinitionItem current = currentDefinitions.FirstOrDefault(x => x.Definition.Equals(def));

						if (current != null) {
							// Implicit overriding by order.  Items later in the definition will override things earlier.
							if (current.m_sortOperators.Count() > 0 && !opOverride) {
								if (current.m_sortOperators.ContainsKey(SortOperatorOptions.Split)) {
									split = true;
								}

								if (current.MaxCount > 0 && maxCount == 0) {
									maxCount = current.MaxCount;
								}

								if (current.Priority != long.MaxValue && priority == long.MaxValue) {
									priority = current.Priority;
								}

								currentDefinitions.Remove(current);
							}
							else {
								currentDefinitions.Remove(current);
							}
						}

						count++;
						SortDefinitionItem sortItem = new SortDefinitionItem {
							m_sortDefinitionString = definition,
							m_definition = def,
							m_entity = entity
						};

						if (not) {
							sortItem.m_sortOperators.Add(SortOperatorOptions.Ignore, 1);
						}

						if (split) {
							sortItem.m_sortOperators.Add(SortOperatorOptions.Split, 0);
						}

						if (maxCount != 0 || opOverride) {
							sortItem.m_sortOperators.Add(SortOperatorOptions.MaxCount, maxCount);
						}

						if (priority != long.MaxValue) {
							sortItem.m_sortOperators.Add(SortOperatorOptions.Prioirity, priority);
						}

						newList.Add(sortItem);
					}
				}

				string debug = "";

				foreach (SortDefinitionItem item in newList) {
					if (Core.Debug) {
						if (debug != "") {
							debug += ", ";
						}

						debug += item.Definition.ToString();
					}

					currentDefinitions.Add(item);
				}

				if (Core.Debug) {
					Logging.Instance.WriteLine(string.Format("'{0}' wants '{2}' - '{1}'", ((Sandbox.ModAPI.Ingame.IMyTerminalBlock)entity).CustomName, debug, definition));
				}
			}

			catch (Exception ex) {
				Logging.Instance.WriteLine(string.Format("TryParseSortDefinitionItem(): {0}", ex.ToString()));
			}
		}
	}
}
