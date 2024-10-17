using System;

namespace SniffBrowser.Events
{
    public class SMSG_EVENT_DESCRIPTION : IDisposable
    {
        public uint SniffedEventId = 0;
        public string LongDescription = string.Empty;
        public SMSG_EVENT_DESCRIPTION(ByteBuffer packet)
        {
            SniffedEventId = packet.ReadUInt32();
            LongDescription = packet.ReadCString();
        }

        public void Dispose()
        {
            LongDescription = string.Empty;
        }
    }
}
