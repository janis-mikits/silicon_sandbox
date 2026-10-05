using System;
using System.Collections.Generic;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Application
{
    public static class OneBitSaveBoundary
    {
        public static WorldSaveSnapshot Capture(OneBitWorldSession session,
            Guid worldId, string worldName, string floorMaterialId,
            string wallStyleId, SavedPlayerPose player,
            IEnumerable<SavedInventoryItem> inventorySlots,
            int selectedHotbarSlot)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            // Process due source/clock actions at the current timestamp without
            // stopping a running clock or copying any transient simulator state.
            if (!session.Scheduler.IsPaused)
                session.Scheduler.AdvanceUntil(session.Scheduler.Now);
            return new WorldSaveSnapshot(worldId, worldName, floorMaterialId,
                wallStyleId, session.Scheduler.FrequencyHz, player,
                inventorySlots, selectedHotbarSlot, session.Design,
                session.ModuleVersions);
        }

        public static OneBitWorldSession Open(WorldSaveSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return new OneBitWorldSession(snapshot.Design,
                snapshot.WorldClockFrequencyHz, snapshot.ModuleVersions);
        }
    }
}
