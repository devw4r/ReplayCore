using System;
using SniffBrowser.Core;
using System.Collections.Generic;

namespace SniffBrowser.Events
{
    public class SMSG_EVENT_TYPE_LIST : IDisposable
    {
        public uint TotalExpectedSniffedEvents;
        public uint StartTime;
        public uint EndTime;
        public uint EventCount;
        public Dictionary<uint, EventTypeEntry> EventEntries = new Dictionary<uint, EventTypeEntry>();

        public SMSG_EVENT_TYPE_LIST(ByteBuffer packet)
        {
            StartTime = packet.ReadUInt32();
            EndTime = packet.ReadUInt32();
            TotalExpectedSniffedEvents = packet.ReadUInt32();
            EventCount = packet.ReadUInt32();

            for (uint i = 0; i < EventCount; i++)
            {
                var eventEntry = EventTypeEntry.FromPacket(packet);
                EventEntries.Add(eventEntry.EventID, eventEntry);
            }
        }

        public void Dispose()
        {
            EventEntries?.Clear();
        }
    }
}
