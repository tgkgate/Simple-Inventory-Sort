using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Common.ObjectBuilders;
using System.Text.RegularExpressions;
using Sandbox.Definitions;
using System.Linq;
using System.Collections;
using System.Threading;

using VRage;
using VRage.ModAPI;
using VRage.Game.ModAPI;

namespace SimpleInventorySort
{
	/// <summary>
	/// This static
	/// </summary>
	public static class Grid
	{
		/// <summary>
		/// This only returns one grid per connected grid.  So if a grid has a connector and 4 pistons, it will count as 1 grid, not 5.
		/// </summary>
		/// <param name="grids"></param>
		/// <param name="collect"></param>
		public static void GetConnectedGrids(HashSet<IMyEntity> grids, Func<IMyEntity, bool> collect = null)
		{
			List<IMySlimBlock> currentBlocks = new List<IMySlimBlock>();
			List<IMyCubeGrid> connectedGrids = new List<IMyCubeGrid>();
			HashSet<IMyEntity> gridsProcessed = new HashSet<IMyEntity>();
			HashSet<IMyEntity> entities = new HashSet<IMyEntity>();

			MyAPIGateway.Entities.GetEntities(entities, collect);

			foreach (IMyEntity entity in entities) {
				if (!(entity is IMyCubeGrid)) {
					continue;
				}

				IMyCubeGrid grid = (IMyCubeGrid)entity;
				
				if (gridsProcessed.Contains(grid)) {
					continue;
				}

				grids.Add(grid);
				GetGridBlocks(grid, currentBlocks);
				
				foreach (IMyCubeGrid connectedGrid in GetConnectedGridList(gridsProcessed, currentBlocks)) {
					gridsProcessed.Add(connectedGrid);
				}
			}
		}

		/// <summary>
		/// Gets all the blocks from all valid connected grids.  So a grid connected to another grid that also has a few pistons with blocks on it will return
		/// all the blocks for the connected grids as well as all the blocks for any connected pistons.  (ug)
		/// </summary>
		/// <param name="gridsProcessed"></param>
		/// <param name="grid"></param>
		/// <param name="allBlocks"></param>
		/// <param name="collect"></param>
		public static void GetAllConnectedBlocks(HashSet<IMyEntity> gridsProcessed, IMyCubeGrid grid, List<IMySlimBlock> allBlocks, Func<IMySlimBlock, bool> collect = null)
		{
			List<IMySlimBlock> currentBlocks = new List<IMySlimBlock>();
			List<IMyCubeGrid> connectedGrids = new List<IMyCubeGrid>();

			connectedGrids.Add(grid);
			
			while (connectedGrids.Count > 0) {
				IMyCubeGrid currentGrid = connectedGrids.First();
				connectedGrids.Remove(currentGrid);
				
				if (gridsProcessed.Contains(currentGrid)) {
					continue;
				}

				gridsProcessed.Add(currentGrid);
				GetGridBlocks(currentGrid, currentBlocks);
				
				foreach (IMyCubeGrid connectedGrid in GetConnectedGridList(gridsProcessed, currentBlocks)) {
					connectedGrids.Add(connectedGrid);
				}

				if (collect != null) {
					foreach (IMySlimBlock slimBlock in currentBlocks.FindAll(s => collect(s))) {
						allBlocks.Add(slimBlock);
					}
				}
				else {
					foreach (IMySlimBlock slimBlock in currentBlocks) {
						allBlocks.Add(slimBlock);
					}
				}
			}
		}

		private static List<IMyCubeGrid> GetConnectedGridList(HashSet<IMyEntity> checkedGrids, List<IMySlimBlock> blocks)
		{
			List<IMyCubeGrid> connectedGrids = new List<IMyCubeGrid>();
			
			foreach (IMySlimBlock slimBlock in blocks) {
				try {
					if (slimBlock.FatBlock != null && slimBlock.FatBlock is IMyCubeBlock) {
						IMyCubeBlock cubeBlock = (IMyCubeBlock)slimBlock.FatBlock;

						// Check for Piston
						if (cubeBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_PistonBase)) {
							MyObjectBuilder_PistonBase pistonBase = (MyObjectBuilder_PistonBase)cubeBlock.GetObjectBuilderCubeBlock();
							IMyEntity entity = null;
							
							if (MyAPIGateway.Entities.TryGetEntityById(pistonBase.TopBlockId, out entity)) {
								IMyCubeGrid parent = (IMyCubeGrid)entity.Parent;
								
								if (!checkedGrids.Contains(parent)) {
									connectedGrids.Add(parent);
								}
							}
						}
						else if (cubeBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_ExtendedPistonBase)) {
							MyObjectBuilder_PistonBase pistonBase = (MyObjectBuilder_PistonBase)cubeBlock.GetObjectBuilderCubeBlock();
							IMyEntity entity = null;
							
							if (MyAPIGateway.Entities.TryGetEntityById(pistonBase.TopBlockId, out entity)) {
								IMyCubeGrid parent = (IMyCubeGrid)entity.Parent;
								
								if (!checkedGrids.Contains(parent)) {
									connectedGrids.Add(parent);
								}
							}
						}
						// Connector
						else if (cubeBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_ShipConnector)) {
							MyObjectBuilder_ShipConnector connector = (MyObjectBuilder_ShipConnector)cubeBlock.GetObjectBuilderCubeBlock();
							IMyEntity entity = null;
							
							if (MyAPIGateway.Entities.TryGetEntityById(connector.ConnectedEntityId, out entity)) {
								IMyCubeGrid parent = (IMyCubeGrid)entity.Parent;
								
								if (!checkedGrids.Contains(parent)) {
									connectedGrids.Add(parent);
								}
							}
						}
						else if (cubeBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_MotorAdvancedStator)) {
							MyObjectBuilder_MotorAdvancedStator stator = (MyObjectBuilder_MotorAdvancedStator)cubeBlock.GetObjectBuilderCubeBlock();
							IMyEntity connectedEntity = null;
							
							if (stator.RotorEntityId.HasValue && MyAPIGateway.Entities.TryGetEntityById(stator.RotorEntityId.Value, out connectedEntity)) {
								IMyCubeGrid parent = (IMyCubeGrid)connectedEntity.Parent;
								
								if (!checkedGrids.Contains(parent)) {
									connectedGrids.Add(parent);
								}
							}
						}
					}
				}
				catch {
					continue;
				}
			}

			return connectedGrids;
		}

		private static void GetGridBlocks(IMyCubeGrid grid, List<IMySlimBlock> blockList, Func<IMySlimBlock, bool> collect = null)
		{
			blockList.Clear();
			List<IMySlimBlock> blocks = new List<IMySlimBlock>();
			grid.GetBlocks(blocks, collect);
			
			foreach (IMySlimBlock block in blocks) {
				blockList.Add(block);
			}
		}
	}
}