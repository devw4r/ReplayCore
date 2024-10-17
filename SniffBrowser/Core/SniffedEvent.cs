using System;

namespace SniffBrowser.Core
{
    public class SniffedEvent : IDisposable
    {
        public uint UUID;
        public SniffedEventType EventType;
        public ulong EventTime;
        public ObjectGuid SourceGuid;
        public ObjectGuid TargetGuid;
        public ulong FirstEventTime;
        public ulong PreviousEventTime;
        public string ShortDescription;
        public bool Discarded = false;
        public uint UnixTime = 0;

        public override int GetHashCode()
        {
            return UUID.GetHashCode();
        }

        public static SniffedEvent FromPacket(ByteBuffer packet)
        {
            var result = new SniffedEvent
            {
                UUID = packet.ReadUInt32(),
                EventType = (SniffedEventType)packet.ReadUInt32(),
                EventTime = packet.ReadUInt64(),
            };

            result.UnixTime = (uint)(result.EventTime / 1000);

            var sourceGuid = packet.ReadUInt64();
            if (sourceGuid == 0)
                result.SourceGuid = ObjectGuid.Empty;
            else if (DataHolder.ObjectGuidMap.TryGetValue(sourceGuid, out ObjectGuid sGuid))
                result.SourceGuid = sGuid;
            else
                result.SourceGuid = new ObjectGuid(sourceGuid);

            var targetGuid = packet.ReadUInt64();
            if (targetGuid == 0)
                result.TargetGuid = ObjectGuid.Empty;
            else if (DataHolder.ObjectGuidMap.TryGetValue(targetGuid, out ObjectGuid tGuid))
                result.TargetGuid = tGuid;
            else
                result.TargetGuid = new ObjectGuid(targetGuid);

            result.ShortDescription = packet.ReadCString();

            return result;
        }

        public override string ToString()
        {
            return "-- Generic Information --" + Environment.NewLine
                + "Event Type: " + this.GetEventTypeName() + Environment.NewLine
                + "Event Time: " + EventTime.ToString() + " (" + Utility.GetDateTimeFromUnixTimeMs(EventTime).ToString() + ")" + Environment.NewLine
                + "Source: " + SourceGuid.ToString() + Environment.NewLine
                + "Target: " + TargetGuid.ToString() + Environment.NewLine + Environment.NewLine
                + "-- Event Specific Data --";
        }

        public void Dispose()
        {
            SourceGuid = null;
            TargetGuid = null;
            ShortDescription = string.Empty;
        }
    }
}
