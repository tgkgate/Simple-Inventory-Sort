using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Common.ObjectBuilders;
using System.Text.RegularExpressions;
using Sandbox.Definitions;
using System.Linq;
using VRage.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace SimpleInventorySort
{
    public static class CubeGridTracker
    {
//        private static Dictionary<long, CubeGridTrackItem> gridList = new Dictionary<long, CubeGridTrackItem>();
        private static DateTime m_lastRebuld = DateTime.Now;
        private static bool m_rebuild = true;

        public static DateTime LastRebuild
        {
            get { return m_lastRebuld; }
        }

        public static bool ShouldRebuild
        {
            get { return m_rebuild; }
        }

        public static void Rebuild()
        {
            //            gridList.Clear();
            DateTime start = DateTime.Now;
            try
            {
                m_rebuild = false;
                m_lastRebuld = DateTime.Now;
                HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
                MyAPIGateway.Entities.GetEntities(entities, x => x is IMyCubeGrid);
                foreach (IMyEntity entity in entities)
                {
                    IMyCubeGrid grid = (IMyCubeGrid)entity;

                    grid.OnBlockAdded -= OnBlockAdded;
                    grid.OnBlockAdded += OnBlockAdded;
                    grid.OnBlockRemoved -= OnBlockRemoved;
                    grid.OnBlockRemoved += OnBlockRemoved;
                    grid.OnBlockOwnershipChanged -= OnBlockOwnershipChanged;
                    grid.OnBlockOwnershipChanged += OnBlockOwnershipChanged;

                    //                gridList.Add(entity.EntityId, item);

                    // So this causes a crash, as CustomNameChanged seems to not be properly working in code.  That is a problem.
                    /*
                    List<Sandbox.ModAPI.IMySlimBlock> slimBlocks = new List<Sandbox.ModAPI.IMySlimBlock>();
                    grid.GetBlocks(slimBlocks, x => x.FatBlock != null);
                    foreach (Sandbox.ModAPI.IMySlimBlock slimBlock in slimBlocks)
                    {
                        if (slimBlock.FatBlock is Sandbox.ModAPI.Ingame.IMyTerminalBlock)
                        {
                            Sandbox.ModAPI.Ingame.IMyTerminalBlock terminalBlock = (Sandbox.ModAPI.Ingame.IMyTerminalBlock)slimBlock.FatBlock;
                            terminalBlock.CustomNameChanged -= CustomNameChanged;
                            terminalBlock.CustomNameChanged += CustomNameChanged;
                        }
                    } 
                     */ 
                }
            }
            catch (Exception ex)
            {
                Logging.Instance.WriteLine(String.Format("CubeGridTracker.Rebuild(): {0}", ex.ToString()));
            }
            finally
            {
                if (Core.Debug)
                    Logging.Instance.WriteLine(string.Format("CubeGridTracker {0}ms", (DateTime.Now - start).Milliseconds));
            }
        }

        public static void TriggerRebuild()
        {
            m_rebuild = true;
        }

        private static void CustomNameChanged(IMyTerminalBlock obj)
        {
//            Logging.Instance.WriteLine("Trigger");
            Inventory.TriggerRebuild();
            //Conveyor.TriggerRebuild();
        }

        private static void OnBlockOwnershipChanged(IMyCubeGrid obj)
        {
            Inventory.TriggerRebuild();
            //Conveyor.TriggerRebuild();
        }

        private static void OnBlockRemoved(IMySlimBlock obj)
        {
            Inventory.TriggerRebuild();
            //Conveyor.TriggerRebuild();
        }

        private static void OnBlockAdded(IMySlimBlock obj)
        {
            Inventory.TriggerRebuild();
            //Conveyor.TriggerRebuild();
        }
    }
}
