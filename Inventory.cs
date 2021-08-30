using Sandbox.Definitions;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using Ingame = VRage.Game.ModAPI.Ingame;
using ModIngame = Sandbox.ModAPI.Ingame;

namespace SimpleInventorySort
{
	public class TransferQueueItem
	{
		public IMyInventory Inventory;
		public IMyInventory InventorySource;
		public SortDefinitionItem Item;
		public List<SortDefinitionItem> compList;
	}

	/// <summary>
	/// This static class does all the actual inventory sorting.  Anything that has to do with Inventory happens here.
	/// </summary>
	public static class Inventory
	{
		private static readonly Dictionary<long, List<SortDefinitionItem>> m_cargoDictionary = new Dictionary<long, List<SortDefinitionItem>>();
		private static readonly Dictionary<MyDefinitionBase, List<IMyEntity>> m_splitGroups = new Dictionary<MyDefinitionBase, List<IMyEntity>>();
		private static readonly Dictionary<MyDefinitionBase, List<IMyEntity>> m_shareGroups = new Dictionary<MyDefinitionBase, List<IMyEntity>>();
		private static readonly HashSet<IMyEntity> m_emptySet = new HashSet<IMyEntity>();
		private static readonly Queue<TransferQueueItem> m_queueItems = new Queue<TransferQueueItem>();
		private static readonly HashSet<IMyInventory> m_inventoryTake = new HashSet<IMyInventory>();
		private static readonly HashSet<IMyInventory> m_inventoryTaken = new HashSet<IMyInventory>();
		private static bool m_rebuild = true;
		private static DateTime m_lastRebuild = DateTime.Now;
		private static int m_entityCount = 0;
		private static volatile bool m_queueReady = false;

		/// <summary>
		/// Should we rebuild our sort list?
		/// </summary>
		public static bool ShouldRebuild => m_rebuild;

		/// <summary>
		/// Is our Queue ready for processing in the game thread?
		/// </summary>
		public static bool QueueReady {
			get => m_queueReady;
			set => m_queueReady = value;
		}

		public static int QueueCount => m_queueItems.Count;

		/// <summary>
		/// Last time we rebuilt our sort list
		/// </summary>
		public static DateTime LastRebuild => m_lastRebuild;

		public static void SortInventory()
		{
			if (m_queueReady) {
				return;
			}

			if (Core.Debug) {
				Logging.Instance.WriteLine("===== BEGIN SORT BLOCK ====");
			}

			try {
				// Debug Timing
				DateTime start = DateTime.Now;

				// Setup up lists and sets

				HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
				HashSet<IMyEntity> pullerProcessed = new HashSet<IMyEntity>();
				HashSet<IMyEntity> pulleeProcessed = new HashSet<IMyEntity>();

				List<IMyTerminalBlock> pullerBlocks = new List<IMyTerminalBlock>();
				List<IMyTerminalBlock> pulleeBlocks = new List<IMyTerminalBlock>();
				List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();

				// Grab all the grids, we're doing them all.
				MyAPIGateway.Entities.GetEntities(entities, x => x is IMyCubeGrid && !x.Closed && x.Physics != null);

				if (Core.Debug) {
					Logging.Instance.WriteLine(string.Format("Total Grids: {0}", entities.Count));
				}

				// Rebuild our grid tracking list
				if (entities.Count != m_entityCount || CubeGridTracker.ShouldRebuild) {
					m_entityCount = entities.Count;
					CubeGridTracker.Rebuild();
				}

				// Rebuild our sort list
				if (ShouldRebuild) {
					RebuildSortListFromEntities(entities);
				}

				// We need to call this to reset split groups each pass.
				ResetSplitGroups();

				if (Core.Debug) {
					Logging.Instance.WriteLine(string.Format("Total Connected Grids: {0}", entities.Count));
				}

				// Loop through our grids
				foreach (IMyEntity entity in entities) {
					DateTime startGridLoop = DateTime.Now;

					try {
						IMyCubeGrid cubeGrid = (IMyCubeGrid)entity;
						pulleeBlocks.Clear();
						pullerBlocks.Clear();
						m_emptySet.Clear();

						// Get a list to all the empty blocks on the grid.  We won't pull from those
						try {
							Sandbox.ModAPI.Ingame.IMyGridTerminalSystem gridSystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(cubeGrid);

							if (gridSystem == null) {
								continue;
							}

							List<ModIngame.IMyTerminalBlock> inBlocks = new List<ModIngame.IMyTerminalBlock>();
							gridSystem.GetBlocks(inBlocks);
							blocks = inBlocks.ConvertAll(x => (IMyTerminalBlock)x);

							foreach (IMyTerminalBlock terminalBlock in blocks.Where(x => IsValidPulleeObjectBuilder(x))) {
								IMyCubeBlock block = terminalBlock;
								IMyTerminalBlock terminal = (IMyTerminalBlock)block;

								MyEntity blockEntity = (MyEntity)block;

								/*
								 * I want refineries to be able to pull from other refineries ore stock.  This is for a priority issue where a refineries
								 * in front of another refinery will pull first without caring about priority.
								if (block.BlockDefinition.TypeId == typeof(MyObjectBuilder_Refinery) && inventoryOwner.GetInventory(1) != null && inventoryOwner.GetInventory(1).CurrentVolume.RawValue == 0)
								{
									m_emptySet.Add(block);
									continue;
								}
								*/

								if (blockEntity is ModIngame.IMyAssembler assembler) {
									if (assembler.Mode == ModIngame.MyAssemblerMode.Disassembly && blockEntity.GetInventoryBase(0) != null && blockEntity.GetInventoryBase(0).CurrentVolume == 0) {
										continue;
									}
									else if (assembler.Mode == ModIngame.MyAssemblerMode.Assembly && blockEntity.GetInventoryBase(1) != null && blockEntity.GetInventoryBase(1).CurrentVolume == 0) {
										continue;
									}
								}

								if (blockEntity is ModIngame.IMyRefinery || blockEntity is ModIngame.IMyAssembler) {
									pulleeBlocks.Add(terminalBlock);
									continue;
								}
								else if (blockEntity.GetInventoryBase(0) != null && blockEntity.GetInventoryBase(0).CurrentVolume == 0) {
									continue;
								}

								pulleeBlocks.Add(terminalBlock);
							}
						}
						catch (Exception ex) {
							Logging.Instance.WriteLine(string.Format("FindEmptyError: {0}", ex.ToString()));
						}

						foreach (IMyTerminalBlock terminalBlock in blocks.Where(x => IsValidPullerObjectBuilder(x)).OrderBy(x => GetHighestPriority(x))) {
							DateTime startBlockLoop = DateTime.Now;

							try {
								MyEntity cubeBlockEntity = (MyEntity)terminalBlock;
								IMyInventory inventory;

								// Check to see if assembler is in disassemble mode
								if (terminalBlock is ModIngame.IMyAssembler assembler) {
									if (assembler.Mode == ModIngame.MyAssemblerMode.Disassembly) {
										inventory = (IMyInventory)cubeBlockEntity.GetInventoryBase(1);
									}
									else {
										inventory = (IMyInventory)cubeBlockEntity.GetInventoryBase(0);
									}
								}
								else {
									inventory = (IMyInventory)cubeBlockEntity.GetInventoryBase(0);
								}

								// Get the components we want to pull
								List<SortDefinitionItem> compList = GetSortComponentsFromEntity(terminalBlock);
								DateTime compStart = DateTime.Now;

								try {
									if (compList.Count > 0) {
										// Pull the components from other cargo holds
										foreach (SortDefinitionItem item in compList) {
											if (item.Ignore) {
												continue;
											}

											FindAndTakeInventoryItem(pulleeBlocks, terminalBlock, inventory, item, compList);
										}
									}
								}
								finally {
									if (Core.Debug && (DateTime.Now - compStart).Milliseconds > 1) {
										Logging.Instance.WriteLine(string.Format("compList Loop: {0}ms", (DateTime.Now - compStart).Milliseconds));
									}
								}
							}
							finally {
								if (Core.Debug && DateTime.Now - startBlockLoop > TimeSpan.FromMilliseconds(1)) {
									Logging.Instance.WriteLine(string.Format("Single Block Loop Took: {0}ms", (DateTime.Now - startBlockLoop).Milliseconds));
								}
							}
						}
					}
					finally {
						if (Core.Debug && DateTime.Now - startGridLoop > TimeSpan.FromMilliseconds(1)) {
							Logging.Instance.WriteLine(string.Format("Single Grid Loop Took: {0}ms - {1}blocks", (DateTime.Now - startGridLoop).Milliseconds, pulleeBlocks.Count));
						}
					}
				}

				if (Core.Debug) {
					Logging.Instance.WriteLine(string.Format("Complete sort took {0}ms", (DateTime.Now - start).Milliseconds));
				}
			}
			catch (Exception ex) {
				Logging.Instance.WriteLine(string.Format("SortInventory(): {0}", ex.ToString()));
			}
			finally {
				m_queueReady = true;
			}

			if (Core.Debug) {
				Logging.Instance.WriteLine(string.Format("Total Items Queued: {0}", m_queueItems.Count));
				Logging.Instance.WriteLine("===== END SORT BLOCK ====");
			}
		}


		/// <summary>
		/// Trigger an inventory rebuild
		/// </summary>
		public static void TriggerRebuild()
		{
			m_rebuild = true;
		}

		/// <summary>
		/// Processes our Transfer Queue.  This occurs in the game thread, so writes are safe.
		/// </summary>
		public static void ProcessQueue()
		{
			DateTime start = DateTime.Now;
			List<TransferQueueItem> queueReset = new List<TransferQueueItem>();

			try {
				m_inventoryTake.Clear();
				m_inventoryTaken.Clear();

				if (Core.Debug) {
					Logging.Instance.WriteLine(string.Format("==== START PROCESS QUEUE BLOCK ===="));
					Logging.Instance.WriteLine(string.Format("Queue: {0}", m_queueItems.Count));
				}

				while (m_queueItems.Count() > 0) {
					TransferQueueItem item = m_queueItems.Dequeue();
					TransferCargo(item.Inventory, item.InventorySource, item.Item, item.compList);
				}

				foreach (TransferQueueItem item in queueReset) {
					m_queueItems.Enqueue(item);
				}

				if (Core.Debug) {
					Logging.Instance.WriteLine(string.Format("ProcessQueue: {0} ms", (DateTime.Now - start).Milliseconds));

					if (MyAPIGateway.Multiplayer.MultiplayerActive && !MyAPIGateway.Multiplayer.IsServer) {
						Logging.Instance.WriteLine(string.Format("Queue Reset: {0}", m_queueItems.Count));
					}

					Logging.Instance.WriteLine(string.Format("==== END PROCESS QUEUE BLOCK ===="));
				}
			}
			catch (Exception ex) {
				Logging.Instance.WriteLine(string.Format("ProcessQueue(): {0}", ex.ToString()));
			}
			finally {
				if (m_queueItems.Count < 1) {
					m_queueReady = false;
				}
			}
		}

		/// <summary>
		/// This extracts the component list from tags in entity custom names, and then stores them in a hashset for use during the sort.
		/// </summary>
		/// <param name="entities">A list of all the entities we want to check</param>
		/// <param name="playerId">The playerId of the local player</param>
		private static void RebuildSortListFromEntities(HashSet<IMyEntity> entities, long playerId)
		{
			m_rebuild = false;
			m_lastRebuild = DateTime.Now;
			DateTime start = DateTime.Now;

			try {
				m_cargoDictionary.Clear();
				m_splitGroups.Clear();
				HashSet<IMyEntity> processedGrids = new HashSet<IMyEntity>();
				List<IMySlimBlock> slimBlocks = new List<IMySlimBlock>();

				foreach (IMyEntity grid in entities) {
					IMyCubeGrid cubeGrid = (IMyCubeGrid)grid;
					slimBlocks.Clear();
					Grid.GetAllConnectedBlocks(processedGrids, cubeGrid, slimBlocks, x => x.FatBlock != null && IsValidPullerObjectBuilder(x) && IsValidOwner(x, playerId));

					foreach (IMySlimBlock slimblock in slimBlocks) {
						IMyEntity entity = slimblock.FatBlock;

						if (!(entity is IMyCubeBlock)) {
							continue;
						}

						List<SortDefinitionItem> components = SortDefinitionItem.CreateFromEntity(entity);
						m_cargoDictionary.Add(entity.EntityId, components);

						foreach (SortDefinitionItem item in components) {
							if (item.SortOperators.ContainsKey(SortOperatorOptions.Split)) {
								AddToSplitGroup(item);
							}
						}
					}
				}
			}
			catch (Exception ex) {
				Logging.Instance.WriteLine(string.Format("RebuildSortListFromEntities(): {0}", ex.ToString()));
			}

			if (Core.Debug) {
				Logging.Instance.WriteLine(string.Format("RebuildSortListFromEntities {0}ms", (DateTime.Now - start).Milliseconds));
			}
		}

		/// <summary>
		/// This extracts the component list from tags in entity custom names, and then stores them in a hashset for use during the sort.
		/// </summary>
		/// <param name="entities">A list of all the entities we want to check</param>
		private static void RebuildSortListFromEntities(HashSet<IMyEntity> entities)
		{
			m_rebuild = false;
			m_lastRebuild = DateTime.Now;
			DateTime start = DateTime.Now;

			try {
				m_cargoDictionary.Clear();
				m_splitGroups.Clear();

				HashSet<IMyEntity> processedGrids = new HashSet<IMyEntity>();
				List<Sandbox.ModAPI.Ingame.IMyTerminalBlock> blocks = new List<Sandbox.ModAPI.Ingame.IMyTerminalBlock>();

				foreach (IMyEntity grid in entities) {
					IMyCubeGrid cubeGrid = (IMyCubeGrid)grid;
					blocks.Clear();
					Sandbox.ModAPI.Ingame.IMyGridTerminalSystem gridSystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid((IMyCubeGrid)grid);

					if (gridSystem == null) {
						continue;
					}

					gridSystem.GetBlocks(blocks);

					foreach (IMyTerminalBlock block in blocks) {
						if (m_cargoDictionary.ContainsKey(block.EntityId)) {
							continue;
						}

						List<SortDefinitionItem> components = SortDefinitionItem.CreateFromEntity(block);
						m_cargoDictionary.Add(block.EntityId, components);

						foreach (SortDefinitionItem item in components) {
							if (item.SortOperators.ContainsKey(SortOperatorOptions.Split)) {
								AddToSplitGroup(item);
							}
						}
					}
				}
			}

			catch (Exception ex) {
				Logging.Instance.WriteLine(string.Format("RebuildSortListFromEntities(): {0}", ex.ToString()));
			}

			if (Core.Debug) {
				Logging.Instance.WriteLine(string.Format("RebuildSortListFromEntities {0}ms", (DateTime.Now - start).Milliseconds));
			}
		}

		private static void ResetSplitGroups()
		{
			foreach (KeyValuePair<MyDefinitionBase, List<IMyEntity>> p in m_splitGroups) {
				foreach (IMyEntity item in p.Value) {
					SortDefinitionItem sortItem = m_cargoDictionary[item.EntityId].FirstOrDefault(x => x.Definition.Equals(p.Key));

					if (sortItem != null) {
						sortItem.splitGroup = p.Value.ToList();
						sortItem.SortOperators[SortOperatorOptions.Split] = sortItem.splitGroup.Count();
					}
				}
			}
		}

		/// <summary>
		/// Add this item to a group of other items for splitting when inventory transfer occurs
		/// </summary>
		/// <param name="item"></param>
		private static void AddToSplitGroup(SortDefinitionItem item)
		{
			if (m_splitGroups.ContainsKey(item.Definition)) {
				m_splitGroups[item.Definition].Add(item.ContainerEntity);
			}
			else {
				List<IMyEntity> list = new List<IMyEntity> {
					item.ContainerEntity
				};
				m_splitGroups.Add(item.Definition, list);
			}
		}

		/// <summary>
		/// Update the amount of sort items in the split group that will apply a divider to the item amount to transfer.
		///
		/// TODO: Reset this each sort
		///
		/// </summary>
		/// <param name="item"></param>
		private static void UpdateSplitGroup(SortDefinitionItem item)
		{
			foreach (IMyEntity entity in item.splitGroup) {
				List<SortDefinitionItem> list = m_cargoDictionary[entity.EntityId];
				SortDefinitionItem check = list.FirstOrDefault(x => x.Definition.Equals(item.Definition) && item != x);

				if (check == null) {
					continue;
				}

				check.Split--;
			}
		}

		/// <summary>
		/// Is the block a valid puller of inventory?
		/// </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		private static bool IsValidPullerObjectBuilder(IMySlimBlock x)
		{
			if (x.FatBlock == null) {
				return false;
			}

			MyEntity entity = x.FatBlock as MyEntity;

			if (!entity.HasInventory) {
				return false;
			}

			if (entity.InventoryCount < 1) {
				return false;
			}

			return true;
		}

		private static bool IsValidPullerObjectBuilder(IMyTerminalBlock x)
		{
			MyEntity entity = x as MyEntity;

			if (!entity.HasInventory) {
				return false;
			}

			if (entity.InventoryCount < 1) {
				return false;
			}

			return true;
		}

		/// <summary>
		/// Is the block a valid target of inventory pulling?
		/// </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		private static bool IsValidPulleeObjectBuilder(IMySlimBlock x)
		{
			if (x.FatBlock == null) {
				return false;
			}

			MyEntity entity = x.FatBlock as MyEntity;

			if (!entity.HasInventory) {
				return false;
			}

			if (entity.InventoryCount < 1) {
				return false;
			}

			return true;
		}

		private static bool IsValidPulleeObjectBuilder(Sandbox.ModAPI.Ingame.IMyTerminalBlock x)
		{
			MyEntity entity = x as MyEntity;

			if (!entity.HasInventory) {
				return false;
			}

			if (entity.InventoryCount < 1) {
				return false;
			}

			return true;
		}

		private static bool IsValidOwner(IMySlimBlock x, long playerId)
		{
			if (x.FatBlock == null || !(x.FatBlock is Sandbox.ModAPI.Ingame.IMyTerminalBlock)) {
				return false;
			}

			ModIngame.IMyTerminalBlock block = (ModIngame.IMyTerminalBlock)x.FatBlock;

			if (Settings.Instance.Faction) {
				return block.HasPlayerAccess(playerId);
			}
			else {
				return block.OwnerId == playerId;
			}
		}

		private static bool IsValidCubeGrid(IMyEntity x, long playerId)
		{
			if (!(x is IMyCubeGrid)) {
				return false;
			}

			IMyCubeGrid grid = (IMyCubeGrid)x;

			if (Settings.Instance.Faction) {
				if (grid.BigOwners.FindAll(p => MyAPIGateway.Session.Player.GetRelationTo(p) == MyRelationsBetweenPlayerAndBlock.FactionShare || MyAPIGateway.Session.Player.GetRelationTo(p) == MyRelationsBetweenPlayerAndBlock.Owner) != null) {
					return true;
				}

				if (grid.SmallOwners.FindAll(p => MyAPIGateway.Session.Player.GetRelationTo(p) == MyRelationsBetweenPlayerAndBlock.FactionShare || MyAPIGateway.Session.Player.GetRelationTo(p) == MyRelationsBetweenPlayerAndBlock.Owner) != null) {
					return true;
				}

				return false;
			}

			if (grid.BigOwners.Contains(playerId) || grid.SmallOwners.Contains(playerId)) {
				return true;
			}

			return false;
		}

		/// <summary>
		/// Finds and takes inventory from other entities on the grid.  This can probably be a FindAndTake(IMyCubeGrid, IMyCubeBlock, MyDefinitionBase def) for
		/// clarity in the future.
		/// </summary>
		/// <param name="slimBlocks">List of blocks in this grid</param>
		/// <param name="slimBlock">The block that is pulling inventory</param>
		/// <param name="inventory">The blocks inventory object</param>
		/// <param name="def">A definition of the component we're pulling</param>
		private static void FindAndTakeInventoryItem(List<IMySlimBlock> slimBlocks, IMySlimBlock slimBlock, IMyInventory inventory, SortDefinitionItem item, List<SortDefinitionItem> compList)
		{
			DateTime start = DateTime.Now;
			MyDefinitionBase def = item.Definition;

			// Loop through the rest of the grid and find other inventories
			foreach (IMySlimBlock slimBlockSource in slimBlocks) {
				// Not this one
				if (slimBlockSource == slimBlock) {
					continue;
				}

				IMyCubeBlock cubeBlockSource = slimBlockSource.FatBlock;
				MyEntity cubeEntity = (MyEntity)cubeBlockSource;
				ModIngame.IMyTerminalBlock terminalSource = (ModIngame.IMyTerminalBlock)cubeBlockSource;
				IMyInventory inventorySource;

				// Special case, let refineries pull ore from other refineries.  This is due to priority (first come first serve will pull ore and
				// not give up that ore once it's pull, ignoring priority)
				if (cubeEntity is ModIngame.IMyRefinery && slimBlock.FatBlock is ModIngame.IMyRefinery) {
					inventorySource = (IMyInventory)cubeEntity.GetInventoryBase(0);
				}
				else if (cubeEntity is ModIngame.IMyRefinery) {
					inventorySource = (IMyInventory)cubeEntity.GetInventoryBase(1);
				}
				else if (cubeEntity is ModIngame.IMyAssembler) {
					ModIngame.IMyAssembler assembler = (ModIngame.IMyAssembler)cubeBlockSource;

					// Check to see if assembler is in disassemble mode, and flip inventories
					if (assembler.Mode == ModIngame.MyAssemblerMode.Disassembly) {
						inventorySource = (IMyInventory)cubeEntity.GetInventoryBase(0);
					}
					else {
						inventorySource = (IMyInventory)cubeEntity.GetInventoryBase(1);
					}
				}
				else {
					inventorySource = (IMyInventory)cubeEntity.GetInventoryBase(0);
				}

				// Check if this inventory also wants an item we're taking, if so, ignore it
				// checking customName
				if (terminalSource.CustomName != null) {
					if (terminalSource.CustomName.ToLower().Contains("exempt")) {
						continue;
					}

					// This needs to change, as it's pretty ugly.
					if (GetSortComponentsFromEntity(cubeBlockSource).Where(x => x.Priority <= item.Priority).FirstOrDefault(f => f.Definition.Equals(def)) != null) {
						continue;
					}
				}

				// while we're at it, check this inventory's customData
				if (terminalSource.CustomData != null) {
					if (terminalSource.CustomData.ToLower().Contains("exempt")) {
						continue;
					}

					// This needs to change, as it's pretty ugly.
					if (GetSortComponentsFromEntity(cubeBlockSource).Where(x => x.Priority <= item.Priority).FirstOrDefault(f => f.Definition.Equals(def)) != null) {
						continue;
					}
				}

				QueueTransferCargo(inventory, inventorySource, item, compList);
			}

			if (Core.Debug && (DateTime.Now - start).Milliseconds > 1) {
				Logging.Instance.WriteLine(string.Format("FindAndTakeInventoryItem {0}ms", (DateTime.Now - start).Milliseconds));
			}
		}

		private static void FindAndTakeInventoryItem(List<IMyTerminalBlock> blocks, IMyTerminalBlock block, IMyInventory inventory, SortDefinitionItem item, List<SortDefinitionItem> compList)
		{
			DateTime start = DateTime.Now;
			MyDefinitionBase def = item.Definition;

			// Loop through the rest of the grid and find other inventories
			foreach (IMyTerminalBlock blockItem in blocks) {
				// Not this one
				if (blockItem == block) {
					continue;
				}

				if (!blockItem.HasPlayerAccess(block.OwnerId)) {
					return;
				}

				ModIngame.IMyTerminalBlock terminalSource = blockItem;
				MyEntity cubeEntity = (MyEntity)blockItem;
				IMyInventory inventorySource;

				// Special case, let refineries pull ore from other refineries.  This is due to priority (first come first serve will pull ore and
				// not give up that ore once it's pull, ignoring priority)
				if (cubeEntity is ModIngame.IMyRefinery && block is ModIngame.IMyRefinery) {
					inventorySource = (IMyInventory)cubeEntity.GetInventoryBase(0);
				}
				else if (cubeEntity is ModIngame.IMyRefinery) {
					inventorySource = (IMyInventory)cubeEntity.GetInventoryBase(1);
				}
				else if (cubeEntity is ModIngame.IMyAssembler) {
					ModIngame.IMyAssembler assembler = (ModIngame.IMyAssembler)cubeEntity;

					// Check to see if assembler is in disassemble mode, and flip inventories
					if (assembler.Mode == ModIngame.MyAssemblerMode.Disassembly) {
						inventorySource = (IMyInventory)cubeEntity.GetInventoryBase(0);
					}
					else {
						inventorySource = (IMyInventory)cubeEntity.GetInventoryBase(1);
					}
				}
				else {
					inventorySource = (IMyInventory)cubeEntity.GetInventoryBase(0);
				}

				// Check if this inventory also wants to item we're taking, if so, ignore it
				if (terminalSource.CustomName != null) {
					if (terminalSource.CustomName.ToLower().Contains("exempt")) {
						continue;
					}

					// This needs to change, as it's pretty ugly.
					if (GetSortComponentsFromEntity(cubeEntity).Where(x => x.Priority <= item.Priority).FirstOrDefault(f => f.Definition.Equals(def)) != null) {
						continue;
					}
				}

				if (terminalSource.CustomData != null) {
					if (terminalSource.CustomData.ToLower().Contains("exempt")) {
						continue;
					}

					// This needs to change, as it's pretty ugly.
					if (GetSortComponentsFromEntity(cubeEntity).Where(x => x.Priority <= item.Priority).FirstOrDefault(f => f.Definition.Equals(def)) != null) {
						continue;
					}
				}

				QueueTransferCargo(inventory, inventorySource, item, compList);
			}

			if (Core.Debug && (DateTime.Now - start).Milliseconds > 1) {
				Logging.Instance.WriteLine(string.Format("FindAndTakeInventoryItem {0}ms", (DateTime.Now - start).Milliseconds));
			}
		}

		private static void QueueTransferCargo(IMyInventory inventory, IMyInventory inventorySource, SortDefinitionItem item, List<SortDefinitionItem> compList)
		{
			MyDefinitionBase def = item.Definition;

			// Transfer cargo
			int indexSource, index;
			double count;

			// These aren't thread safe.  We'll catch the exception, and our state is still safe if this fails
			// as we can just come back to this in another pass.
			Ingame.IMyInventoryItem sourceItem = FindItemInInventory(inventorySource, def, out indexSource);
			Ingame.IMyInventoryItem targetItem = FindItemInInventory(inventory, def, out index, out count);

			if (sourceItem != null) {
				TransferQueueItem queueItem = new TransferQueueItem {
					Inventory = inventory,
					InventorySource = inventorySource,
					Item = item,
					compList = compList
				};
				m_queueItems.Enqueue(queueItem);

				if (Core.Debug) {
					LogTransfer(inventory, inventorySource, item, 0, true);
				}
			}
		}

		/// <summary>
		/// Actually transfer cargo from one IMyInventory to another IMyInventory
		/// </summary>
		/// <param name="inventory"></param>
		/// <param name="inventorySource"></param>
		/// <param name="item"></param>
		/// <param name="compList"></param>
		private static void TransferCargo(IMyInventory inventory, IMyInventory inventorySource, SortDefinitionItem item, List<SortDefinitionItem> compList)
		{
			DateTime start = DateTime.Now;

			try {
				MyDefinitionBase def = item.Definition;
				// Transfer cargo
				int indexSource, index;
				double count;

				Ingame.IMyInventoryItem sourceItem = FindItemInInventory(inventorySource, def, out indexSource);
				Ingame.IMyInventoryItem targetItem = FindItemInInventory(inventory, def, out index, out count);

				if (sourceItem != null) {
					if (ShouldExcludeInventoryItem(compList, sourceItem)) {
						return;
					}

					if ((item.MaxCount > 0) && targetItem != null && count >= item.MaxCount) {
						return;
					}

					double maxAmount = (double)inventory.MaxVolume - (double)inventory.CurrentVolume;
					double itemVolume = MyDefinitionManager.Static.GetPhysicalItemDefinition(def.Id).Volume;

					// Survival
					if (maxAmount / 1000 < 50000000) {
						double countToTransfer = maxAmount / itemVolume;

						// Sanity
						if (countToTransfer < 0) {
							countToTransfer = 0;
						}

						MyFixedPoint amount = (MyFixedPoint)countToTransfer;

						if ((double)amount > (double)sourceItem.Amount) {
							amount = sourceItem.Amount;
						}

						// Huh, this is kind of cheap way of splitting, and doesn't account for inventory size differences.  I think what should happen
						// is I process all items in the group at the same time, hmm
						if (item.Split > 0) {
							if ((double)amount / item.Split > itemVolume) {
								amount = (MyFixedPoint)((double)amount / item.Split);
							}

							// If we're splitting anything other than ore and ignots, we only want integer amounts
							if (!(def.Id.ToString().Contains("Ore/") || def.Id.ToString().Contains("Ingot/"))) {
								amount = MyFixedPoint.Floor(amount);
							}

							UpdateSplitGroup(item);
						}

						if (item.MaxCount > 0) {
							MyFixedPoint maxCount = (MyFixedPoint)((double)item.MaxCount);

							if (targetItem != null && ((double)maxCount) + count >= item.MaxCount) {
								maxCount = (MyFixedPoint)(item.MaxCount - count);
							}

							if ((double)amount > (double)maxCount) {
								amount = maxCount;
							}
						}

						// If we're not dealing with ore or ingots, do not transfer fractional amounts, only integer amounts
						if (amount < 1 && !(def.Id.ToString().Contains("Ore/") || def.Id.ToString().Contains("Ingot/"))) {
							return;
						}

						if ((double)amount < 0.01) {
							return;
						}

						if (inventory.CanItemsBeAdded(amount, new SerializableDefinitionId(item.Definition.Id.TypeId, item.Definition.Id.SubtypeName))) {
							if (inventorySource.TransferItemTo(inventory, indexSource, null, true, amount, true)) {
								LogTransfer(inventory, inventorySource, item, amount);
							}
						}
					}
					// Creative
					else {
						MyFixedPoint amount = sourceItem.Amount;

						if (item.Split > 0) {
							if ((double)amount / item.Split > itemVolume) {
								amount = (MyFixedPoint)((double)amount / item.Split);
							}

							UpdateSplitGroup(item);
						}

						if (item.MaxCount > 0) {
							MyFixedPoint maxCount = (MyFixedPoint)((double)item.MaxCount);

							if (targetItem != null && ((double)maxCount) + count >= item.MaxCount) {
								maxCount = (MyFixedPoint)(item.MaxCount - count);
							}

							if ((double)amount > (double)maxCount) {
								amount = maxCount;
							}
						}

						if (inventory.CanItemsBeAdded(amount, new SerializableDefinitionId(item.Definition.Id.TypeId, item.Definition.Id.SubtypeName))) {
							if (inventorySource.TransferItemTo(inventory, indexSource, null, true, amount, true)) {
								LogTransfer(inventory, inventorySource, item, amount);
							}
						}
					}
				}
			}
			catch (Exception ex) {
				Logging.Instance.WriteLine(string.Format("TransferCargo(): {0}", ex.ToString()));
			}

			if (Core.Debug && (DateTime.Now - start).Milliseconds > 1) {
				Logging.Instance.WriteLine(string.Format("TransferCargo: {0}ms", (DateTime.Now - start).Milliseconds));
			}
		}

		/// <summary>
		/// Log cargo transfer
		/// </summary>
		/// <param name="inventory"></param>
		/// <param name="inventorySource"></param>
		/// <param name="item"></param>
		/// <param name="amount"></param>
		private static void LogTransfer(IMyInventory inventory, IMyInventory inventorySource, SortDefinitionItem item, MyFixedPoint amount, bool queue = false)
		{
			int i = 0;

			try {
				if (inventory == null || inventorySource == null || inventory.Owner == null || inventorySource.Owner == null) {
					return;
				}

				if (MyAPIGateway.Entities.GetEntityById(inventory.Owner.EntityId) is not ModIngame.IMyTerminalBlock terminalEntity) {
					return;
				}

				if (MyAPIGateway.Entities.GetEntityById(inventorySource.Owner.EntityId) is not ModIngame.IMyTerminalBlock terminalSourceEntity) {
					return;
				}

				if (Core.Debug) {
					Logging.Instance.WriteLine(string.Format("{7}Moving {0:F2} {1} from '{2}' ({3}) to '{4}' ({5}) - {6}", (float)amount, item.Definition.Id.SubtypeName, terminalSourceEntity.CustomName, terminalSourceEntity.DisplayNameText, terminalEntity.CustomName, terminalEntity.DisplayNameText, terminalEntity.CubeGrid.EntityId, queue ? "Queued " : ""));
				}
			}

			catch (Exception ex) {
				Logging.Instance.WriteLine(string.Format("Here: {0} - {1}", i, ex.ToString()));
			}
		}

		/// <summary>
		/// Find an IMyInventoryItem in an IMyInventory.
		/// </summary>
		/// <param name="inventory"></param>
		/// <param name="def"></param>
		/// <returns></returns>
		private static Ingame.IMyInventoryItem FindItemInInventory(IMyInventory inventory, MyDefinitionBase def)
		{
			int index;
			double count;
			return FindItemInInventory(inventory, def, out index, out count);
		}

		private static Ingame.IMyInventoryItem FindItemInInventory(IMyInventory inventory, MyDefinitionBase def, out int index)
		{
			double count;
			return FindItemInInventory(inventory, def, out index, out count);
		}

		private static Ingame.IMyInventoryItem FindItemInInventory(IMyInventory inventory, MyDefinitionBase def, out int index, out double count)
		{
			index = 0;
			count = 0d;
			Ingame.IMyInventoryItem foundItem = null;
			bool found = false;

			List<IMyInventoryItem> items;

			try {
				//FIXME: pending...
				items = inventory.GetItems();
			}

			catch (Exception) {
				return null;
			}

			for (int r = 0; r < items.Count; r++) {
				Ingame.IMyInventoryItem item = items[r];

				if (found && item.Content.TypeId == def.Id.TypeId && item.Content.SubtypeId == def.Id.SubtypeId) {
					count += item.Amount.RawValue;
				}

				if (!found && item.Content.TypeId == def.Id.TypeId && item.Content.SubtypeId == def.Id.SubtypeId) {
					index = r;
					found = true;
					foundItem = item;
					count += (double)item.Amount;
				}
			}

			if (found) {
				return foundItem;
			}

			return null;
		}

		/// <summary>
		/// This gets a list of components that an entity wants
		/// </summary>
		/// <param name="entity">The entity to check</param>
		/// <returns>A list of MyDefinitionBase objects that contain definition information of the components that this entity wants</returns>
		private static List<SortDefinitionItem> GetSortComponentsFromEntity(IMyEntity entity)
		{
			if (m_cargoDictionary.ContainsKey(entity.EntityId)) {
				return m_cargoDictionary[entity.EntityId];
			}
			else {
				return new List<SortDefinitionItem>();
			}
		}

		private static long GetHighestPriority(IMyEntity entity)
		{
			long result = long.MaxValue;
			List<SortDefinitionItem> list = GetSortComponentsFromEntity(entity);

			foreach (SortDefinitionItem item in list) {
				result = Math.Min(result, item.Priority);
			}

			return result;
		}

		/// <summary>
		/// Should we exclude this IMyInventoryItem from being transferred?  Basically a not operator check
		/// </summary>
		/// <param name="compList"></param>
		/// <param name="source"></param>
		/// <returns></returns>
		private static bool ShouldExcludeInventoryItem(List<SortDefinitionItem> compList, Ingame.IMyInventoryItem source)
		{
			return compList.FirstOrDefault(x => x.Ignore && x.Definition.Id.TypeId == source.Content.TypeId && x.Definition.Id.SubtypeId == source.Content.SubtypeId) != null;
		}
	}
}
