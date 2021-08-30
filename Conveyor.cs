using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using VRageMath;
using Sandbox.Common.ObjectBuilders;
using SimpleInventorySort;
using System.Text.RegularExpressions;
using Sandbox.Common;

using VRage;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game;
using VRage.Game.ModAPI;

namespace SimpleInventorySort
{
	public static class Conveyor
	{
		private static Dictionary<long, List<long>> m_conveyorCache = new Dictionary<long, List<long>>(10000);
		private static Dictionary<long, long[]> m_conveyorConnected = new Dictionary<long, long[]>();
		private static bool m_rebuild = true;
		private static DateTime m_lastRebuild = DateTime.Now;

		public static bool ShouldRebuild
		{
			get { return m_rebuild; }
		}

		public static DateTime LastRebuild
		{
			get { return m_lastRebuild; }
		}

		/// <summary>
		/// Rebuilds our conveyor dictionary.  This lets us check if two entities are connected by conveyors quickly.
		/// </summary>
		/// <param name="entities"></param>
		public static void RebuildConveyorList(HashSet<IMyEntity> entities)
		{
			m_rebuild = false;
			m_lastRebuild = DateTime.Now;
			DateTime start = DateTime.Now;

			try {
				m_conveyorCache.Clear();
				m_conveyorConnected.Clear();

				foreach (IMyEntity entity in entities)
				{
					if (!(entity is IMyCubeGrid)) {
						continue;
					}

					IMyCubeGrid grid = (IMyCubeGrid)entity;

					MyObjectBuilder_CubeGrid gridObject = (MyObjectBuilder_CubeGrid)grid.GetObjectBuilder();

					if (gridObject == null || gridObject.ConveyorLines == null) {
						continue;
					}

					foreach (MyObjectBuilder_ConveyorLine line in gridObject.ConveyorLines)
					{
						IMySlimBlock slimBlockStart = grid.GetCubeBlock((Vector3I)line.StartPosition);
						
						if (slimBlockStart == null || slimBlockStart.FatBlock == null || !slimBlockStart.FatBlock.IsFunctional) {
							continue;
						}

						IMySlimBlock slimBlockEnd = grid.GetCubeBlock((Vector3I)line.EndPosition);
						
						if (slimBlockEnd == null || slimBlockEnd.FatBlock == null || !slimBlockEnd.FatBlock.IsFunctional) {
							continue;
						}

						ConnectConveyorBlocks(slimBlockStart, slimBlockEnd);
					}

					if (m_conveyorConnected.ContainsKey(grid.EntityId)) {
						long[] connectedBlockId = m_conveyorConnected[grid.EntityId];
						m_conveyorConnected.Remove(grid.EntityId);
						ConnectConveyorBlocks(connectedBlockId);
					}
				}

				foreach (KeyValuePair<long, long[]> p in m_conveyorConnected) {
					ConnectConveyorBlocks(p.Value);
				}
			}
			catch (Exception ex) {
				Logging.Instance.WriteLine(String.Format("RebuildConveyorList: {0}", ex.ToString()));
			}

			if (Core.Debug) {
				Logging.Instance.WriteLine(String.Format("RebuildConveyorList {0}ms", (DateTime.Now - start).Milliseconds));
			}
		}

		private static void ConnectConveyorBlocks(IMySlimBlock slimBlockStart, IMySlimBlock slimBlockEnd)
		{
			List<long> startList = GetConveyorListFromEntity(slimBlockStart.FatBlock);
			List<long> endList = GetConveyorListFromEntity(slimBlockEnd.FatBlock);

			if (startList != null && endList != null && startList == endList)
				return;

			if (startList != null && endList != null)
			{
				// No AddList()?  Damn you extensions!
				foreach (long item in endList) {
					startList.Add(item);
					m_conveyorCache[item] = startList;
				}

				return;
			}

			if (startList != null) {
				startList.Add(slimBlockEnd.FatBlock.EntityId);
				m_conveyorCache.Add(slimBlockEnd.FatBlock.EntityId, startList);
			}
			else if (endList != null) {
				endList.Add(slimBlockStart.FatBlock.EntityId);
				m_conveyorCache.Add(slimBlockStart.FatBlock.EntityId, endList);
			}
			else {
				List<long> newList = new List<long>();
				newList.Add(slimBlockStart.FatBlock.EntityId);
				m_conveyorCache.Add(slimBlockStart.FatBlock.EntityId, newList);
				newList.Add(slimBlockEnd.FatBlock.EntityId);
				m_conveyorCache.Add(slimBlockEnd.FatBlock.EntityId, newList);
			}

			CheckGridConnection(slimBlockStart.FatBlock);
			CheckGridConnection(slimBlockEnd.FatBlock);
		}

		private static void ConnectConveyorBlocks(long[] connectedBlockId)
		{
			IMyEntity startEntity = null;
			IMyEntity endEntity = null;
			
			if (!MyAPIGateway.Entities.TryGetEntityById(connectedBlockId[0], out startEntity)) {
				return;
			}

			if (!MyAPIGateway.Entities.TryGetEntityById(connectedBlockId[1], out endEntity)) {
				return;
			}

			List<long> startList = GetConveyorListFromEntity(startEntity);
			List<long> endList = GetConveyorListFromEntity(endEntity);

			if (startList != null && endList != null && startList == endList) {
				return;
			}

			if (startList != null && endList != null) {
				// No AddList()?  Damn you extensions!
				foreach (long item in endList) {
					startList.Add(item);
					m_conveyorCache[item] = startList;
				}
				
				return;
			}
		}

		private static void CheckGridConnection(IMyEntity block)
		{
			IMyCubeBlock cubeBlock = (IMyCubeBlock)block;

			if (cubeBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_PistonBase)) {
				MyObjectBuilder_PistonBase pistonBase = (MyObjectBuilder_PistonBase)cubeBlock.GetObjectBuilderCubeBlock();
				IMyEntity connectedEntity = null;
				
				if (pistonBase.TopBlockId.HasValue && MyAPIGateway.Entities.TryGetEntityById(pistonBase.TopBlockId.Value, out connectedEntity)) {
					IMyEntity parent = connectedEntity.Parent;

					if (parent is IMyCubeGrid) {
						if(!m_conveyorConnected.ContainsKey(parent.EntityId)) {
							m_conveyorConnected.Add(parent.EntityId, new long[] {pistonBase.TopBlockId.Value, pistonBase.EntityId});
						}
					}
				}
			}
			else if (cubeBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_ExtendedPistonBase)) {
				MyObjectBuilder_PistonBase pistonBase = (MyObjectBuilder_PistonBase)cubeBlock.GetObjectBuilderCubeBlock();
				IMyEntity connectedEntity = null;
				
				if (pistonBase.TopBlockId.HasValue && MyAPIGateway.Entities.TryGetEntityById(pistonBase.TopBlockId.Value, out connectedEntity)) {
					IMyEntity parent = connectedEntity.Parent;

					if (parent is IMyCubeGrid) {
						if (!m_conveyorConnected.ContainsKey(parent.EntityId)) {
							m_conveyorConnected.Add(parent.EntityId, new long[] { pistonBase.TopBlockId.Value, pistonBase.EntityId });
						}
					}
				}
			}
			else if (cubeBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_ShipConnector)) {
				MyObjectBuilder_ShipConnector connector = (MyObjectBuilder_ShipConnector)cubeBlock.GetObjectBuilderCubeBlock();
				IMyEntity connectedEntity = null;
				
				if (connector.Connected && MyAPIGateway.Entities.TryGetEntityById(connector.ConnectedEntityId, out connectedEntity)) {
					if (!m_conveyorConnected.ContainsKey(connectedEntity.Parent.EntityId)) {
						m_conveyorConnected.Add(connectedEntity.Parent.EntityId, new long[] { connectedEntity.EntityId, connector.EntityId });
					}
				}
			}
			else if(cubeBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_MotorAdvancedStator)) {
				MyObjectBuilder_MotorAdvancedStator stator = (MyObjectBuilder_MotorAdvancedStator)cubeBlock.GetObjectBuilderCubeBlock();
				IMyEntity connectedEntity = null;
				
				if (stator.RotorEntityId.HasValue && MyAPIGateway.Entities.TryGetEntityById(stator.RotorEntityId.Value , out connectedEntity)) {
					IMyEntity parent = connectedEntity.Parent;

					if(parent is IMyCubeGrid) {
						if (!m_conveyorConnected.ContainsKey(parent.EntityId)) {
							m_conveyorConnected.Add(parent.EntityId, new long[] { stator.RotorEntityId.Value, stator.EntityId });
						}
					}
				}
			}
		}

		public static void TriggerRebuild()
		{
			m_rebuild = true;
		}

		/// <summary>
		/// Gets the list of blocks connected with this block
		/// </summary>
		/// <param name="entity"></param>
		/// <returns></returns>
		private static List<long> GetConveyorListFromEntity(IMyEntity entity)
		{
			if (m_conveyorCache.ContainsKey(entity.EntityId)) {
				return m_conveyorCache[entity.EntityId];
			}
			else {
				return null;
			}
		}

		/// <summary>
		/// Returns true if both blocks are connected via conveyor, otherwise false
		/// </summary>
		/// <param name="first"></param>
		/// <param name="second"></param>
		/// <returns></returns>
		public static bool AreEntitiesConnected(IMyEntity first, IMyEntity second)
		{
			if (m_conveyorCache.ContainsKey(first.EntityId)) {
				return m_conveyorCache[first.EntityId].Contains(second.EntityId);
			}

			return false;
		}

		private static bool IsFunctional(IMyCubeBlock block)
		{
			return false;
		}
	}
}
