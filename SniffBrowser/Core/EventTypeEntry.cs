namespace SniffBrowser.Core
{
    public class EventTypeEntry
    {
        public uint EventID;
        public byte ImageIndex;
        public string EventName;
        public EventTypeFilter EventTypeFilter;

        public static EventTypeEntry FromPacket(ByteBuffer packet)
        {
            var result = new EventTypeEntry()
            {
                EventID = packet.ReadUInt32(),
                ImageIndex = (byte)packet.ReadInt32(),
                EventName = packet.ReadCString(),
            };

            result.EventTypeFilter = result.EventName.DetermineEventCategory();

            return result;
        }
    }
}
