using System.Linq;
using System.Collections.Generic;

namespace SniffBrowser.Core
{
    public class SniffedEventTypeFilterEntry
    {
        public SniffedEventType FilterType { get; set; }
        public bool Enabled { get; set; }

        public override int GetHashCode()
        {
            return FilterType.GetHashCode();
        }
    }

    public class Filter
    {
        public ObjectGuid Guid = ObjectGuid.Empty;
        public Dictionary<SniffedEventType, SniffedEventTypeFilterEntry> EventTypeFilter { get; protected set; }
        public bool Enabled { get; set; } = false;
        public ObjectTypeFilter ObjectTypeFilter = ObjectTypeFilter.Any;

        public Filter() 
        {
            EventTypeFilter = EnumUtils<SniffedEventType>.Values.ToDictionary(k => k, v => new SniffedEventTypeFilterEntry() { FilterType = v, Enabled = true });
        }

        public bool EnableSniffedEventType(SniffedEventType eventType)
        {
            if (!EventTypeFilter[eventType].Enabled)
            {
                EventTypeFilter[eventType].Enabled = true;
                return true;
            }
            return false;
        }

        public bool DisableSniffedEventType(SniffedEventType eventType)
        {
            if (EventTypeFilter[eventType].Enabled)
            {
                EventTypeFilter[eventType].Enabled = false;
                return true;
            }
            return false;
        }

        public void ClearEventTypeFilters()
        {
            foreach (var sFilter in EventTypeFilter)
                sFilter.Value.Enabled = false;
        }

        public void CheckAllEventTypeFilters()
        {
            foreach (var sFilter in EventTypeFilter)
                sFilter.Value.Enabled = true;
        }

        public bool Evaluate(SniffedEvent sEvent)
        {
            if (!EventTypeFilter[sEvent.EventType].Enabled)
                return false;

            if (!Guid.IsEmpty)
            {
                if (sEvent.SourceGuid != Guid && sEvent.TargetGuid != Guid)
                    return false;
            }

            if (ObjectTypeFilter != ObjectTypeFilter.Any)
            {
                var sourceEmpty = sEvent.SourceGuid.IsEmpty;
                var targetEmpty = sEvent.TargetGuid.IsEmpty;

                if (sourceEmpty && targetEmpty)
                    return false;

                bool matchSource = !sourceEmpty;
                bool matchTarget = !targetEmpty;

                if (matchSource && !sEvent.SourceGuid.IsGuidFitForObjectTypeFilter(ObjectTypeFilter))
                    matchSource = false;
                if (matchTarget && !sEvent.TargetGuid.IsGuidFitForObjectTypeFilter(ObjectTypeFilter))
                    matchTarget = false;
                if (!matchSource && !matchTarget)
                    return false;
            }

            return true;
        }
    }
}
