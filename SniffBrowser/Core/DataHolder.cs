using System;
using System.Linq;
using System.Collections.Generic;
using SniffBrowser.Events;

namespace SniffBrowser.Core
{
    public static class DataHolder
    {
        public static HashSet<SniffedEvent> SniffedEvents = new HashSet<SniffedEvent>();
        private static readonly List<Filter> Filters = new List<Filter>();
        private static SMSG_EVENT_TYPE_LIST EventsTypeList;
        public static Dictionary<ulong, ObjectGuid> ObjectGuidMap = new Dictionary<ulong, ObjectGuid>();

        static DataHolder()
        {
            Filters.Add(new Filter() { Enabled = true });
        }

        public static void SetEventTypeList(SMSG_EVENT_TYPE_LIST eventTypeList)
        {
            SniffedEvents?.Clear();
            ObjectGuidMap?.Clear();
            EventsTypeList = eventTypeList;
            Console.WriteLine("Clear Sniffed Events");
        }

        public static void PushSniffedEvent(SniffedEvent sEvent)
        {
            if (SniffedEvents.Contains(sEvent))
                return;

            SniffedEvents.Add(sEvent);

            // Map ObjectGuid -> Name. This is only done once per object guid.
            if (!sEvent.SourceGuid.IsEmpty && !ObjectGuidMap.ContainsKey(sEvent.SourceGuid.RawGuid))
            {
                var name = sEvent.GetObjectGuidName(true);
                ObjectGuidMap.Add(sEvent.SourceGuid.RawGuid, sEvent.SourceGuid);
                sEvent.SourceGuid.ObjectName = name;
            }

            if (!sEvent.TargetGuid.IsEmpty && !ObjectGuidMap.ContainsKey(sEvent.TargetGuid.RawGuid))
            {
                var name = sEvent.GetObjectGuidName(false);
                ObjectGuidMap.Add(sEvent.TargetGuid.RawGuid, sEvent.TargetGuid);
                sEvent.TargetGuid.ObjectName = name;
            }

            // Reference this sniffed event inside ObjectGuid.
            if (ObjectGuidMap.ContainsKey(sEvent.SourceGuid.RawGuid))
                ObjectGuidMap[sEvent.SourceGuid.RawGuid].SniffedEvents.Add(sEvent);

            if (ObjectGuidMap.ContainsKey(sEvent.TargetGuid.RawGuid))
                ObjectGuidMap[sEvent.TargetGuid.RawGuid].SniffedEvents.Add(sEvent);
        }

        public static int GetProgress()
        {
            return Convert.ToInt32(SniffedEvents.Count * 100 / GetExpectedTotalSniffedEvents());
        }

        public static bool HasEvents()
        {
            return EventsTypeList != null && EventsTypeList.EventCount > 0;
        }

        public static bool HasActiveFilters()
        {
            return Filters.Any(f => f.Enabled);
        }

        public static IEnumerable<Filter> GetFilters()
        {
            foreach (var filter in Filters)
                if (filter.Enabled)
                    yield return filter;
        }

        public static void AddFilter(Filter filter)
        {
            Filters.Add(filter);
        }

        public static void RemoveFilter(Filter filter)
        {
            Filters.Remove(filter);
        }

        public static IEnumerable<EventTypeEntry> GetEvents()
        {
            foreach (var e in EventsTypeList.EventEntries.Values)
                yield return e;
        }

        public static uint GetExpectedTotalSniffedEvents()
        {
            if (EventsTypeList == null)
                return 0;

            return EventsTypeList.TotalExpectedSniffedEvents;
        }

        public static uint GetStartTime()
        {
            if (EventsTypeList == null)
                return 0;

            return EventsTypeList.StartTime;
        }

        public static uint GetEndTime()
        {
            if (EventsTypeList == null)
                return 0;

            return EventsTypeList.EndTime;
        }

        public static uint GetEventCount()
        {
            if (EventsTypeList == null)
                return 0;

            return EventsTypeList.EventCount;
        }

        private static readonly Dictionary<uint, int> IdxCache = new Dictionary<uint, int>();
        public static bool TryGetImageIndexForEventType(uint eventType, out int value)
        {
            if (IdxCache.ContainsKey(eventType))
            {
                value = IdxCache[eventType];
                return true;
            }

            value = 0;
            if (EventsTypeList == null)
                return false;

            EventTypeEntry eventEntry;
            if (!EventsTypeList.EventEntries.TryGetValue(eventType, out eventEntry))
                return false;

            value = eventEntry.ImageIndex;

            IdxCache[eventType] = value;
            return true;
        }

        public static bool TryGetEventTypeEntryById(uint id, out EventTypeEntry value)
        {
            value = null;
            if (EventsTypeList == null)
                return false;

            if (!EventsTypeList.EventEntries.TryGetValue(id, out value))
                return false;

            return true;
        }

        public static void UpdateEventTimes()
        {
            SniffedEvent previousEvent = null;
            SniffedEvent firstEvent = null;
            foreach (var sEvent in SniffedEvents)
            {
                if (previousEvent == null)
                {
                    sEvent.FirstEventTime = sEvent.EventTime;
                    sEvent.PreviousEventTime = sEvent.EventTime;
                    firstEvent = sEvent;
                }
                else
                {
                    sEvent.FirstEventTime = firstEvent.EventTime;
                    sEvent.PreviousEventTime = previousEvent.EventTime;
                }

                previousEvent = sEvent;
            }
        }

        public static void FlushSniffedEvents()
        {
            foreach (var sEvent in SniffedEvents)
                sEvent?.Dispose();
            SniffedEvents?.Clear();
            GC.Collect();
        }
    }
}
