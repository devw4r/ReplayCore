using SniffBrowser.Core;
using System.Collections.Generic;

namespace SniffBrowser.Events
{
    public static class SMSG_EVENT_DATA_LIST
    {
        public static IEnumerable<SniffedEvent> BuildGetEvents(ByteBuffer packet)
        {
            uint eventsCount = packet.ReadUInt32();
            for (uint i = 0; i < eventsCount; i++)
                yield return SniffedEvent.FromPacket(packet);
        }
    }
}
