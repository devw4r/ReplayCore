using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using BrightIdeasSoftware;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System.Xml.Linq;
using System.CodeDom;

namespace SniffBrowser.Core
{
    public static class Extensions
    {
        // Compression

        private static readonly byte[] DecompressBuffer = new byte[ushort.MaxValue];
        public static byte[] Decompress(this byte[] buffer)
        {
            using (MemoryStream memory = new MemoryStream(buffer))
            using (InflaterInputStream inflater = new InflaterInputStream(memory))
                return DecompressBuffer.Take(inflater.Read(DecompressBuffer, 0, DecompressBuffer.Length)).ToArray();
        }

        // ObjectGuid

        public static bool IsCreature(this ObjectGuid oGuid) => oGuid.GetHighType() == HighGuid.UNIT;
        public static bool IsPet(this ObjectGuid oGuid) => oGuid.GetHighType() == HighGuid.PET;
        public static bool IsCreatureOrPet(this ObjectGuid oGuid) => oGuid.IsCreature() || oGuid.IsPet();
        public static bool IsPlayer(this ObjectGuid oGuid) => !oGuid.IsEmpty && oGuid.GetHighType() == HighGuid.PLAYER;
        public static bool IsUnit(this ObjectGuid oGuid) => oGuid.IsCreatureOrPet() || oGuid.IsPlayer();
        public static bool IsItem(this ObjectGuid oGuid) => oGuid.GetHighType() == HighGuid.ITEM;
        public static bool IsGameObject(this ObjectGuid oGuid) => oGuid.GetHighType() == HighGuid.GAMEOBJECT;
        public static bool IsDynamicObject(this ObjectGuid oGuid) => oGuid.GetHighType() == HighGuid.DYNAMICOBJECT;
        public static bool IsCorpse(this ObjectGuid oGuid) => oGuid.GetHighType() == HighGuid.CORPSE;
        public static bool IsTransport(this ObjectGuid oGuid) => oGuid.GetHighType() == HighGuid.TRANSPORT;
        public static bool IsMOTransport(this ObjectGuid oGuid) => oGuid.GetHighType() == HighGuid.MO_TRANSPORT;

        public static bool IsGuidFitForObjectTypeFilter(this ObjectGuid guid, ObjectTypeFilter filter)
        {
            switch (filter)
            {
                case ObjectTypeFilter.GameObject:
                    return guid.IsGameObject();
                case ObjectTypeFilter.Transport:
                    if (!guid.IsGameObject() && !guid.IsTransport() && !guid.IsMOTransport())
                        return false;
                    return true;
                case ObjectTypeFilter.Unit:
                    return guid.IsUnit();
                case ObjectTypeFilter.Creature:
                    return guid.IsCreature();
                case ObjectTypeFilter.Pet:
                    return guid.IsPet();
                case ObjectTypeFilter.Player:
                    return guid.IsPlayer();
            }

            return true;
        }

        public static bool HasEntry(this ObjectGuid oGuid)
        {
            switch (oGuid.GetHighType())
            {
                case HighGuid.ITEM:
                case HighGuid.PLAYER:
                case HighGuid.DYNAMICOBJECT:
                case HighGuid.CORPSE:
                case HighGuid.MO_TRANSPORT:
                    return false;
                case HighGuid.GAMEOBJECT:
                case HighGuid.TRANSPORT:
                case HighGuid.UNIT:
                case HighGuid.PET:
                default:
                    return true;
            }
        }

        public static uint GetCounter(this ObjectGuid oGuid)
        {
            return oGuid.HasEntry()
                ? (uint)(oGuid.RawGuid & (0x0000000000FFFFFFul))
                : (uint)(oGuid.RawGuid & (0x00000000FFFFFFFFul));
        }

        public static uint GetEntry(this ObjectGuid oGuid)
        {
            return oGuid.HasEntry()
                ? (uint)((oGuid.RawGuid >> 24) & (0x0000000000FFFFFFul))
                : 0;
        }

        public static HighGuid GetHighType(this ObjectGuid oGuid)
        {
            return (HighGuid)((oGuid.RawGuid >> 48) & 0x0000FFFF);
        }

        public static ObjectType GetObjectType(this ObjectGuid oGuid)
        {
            switch (oGuid.GetHighType())
            {
                case HighGuid.PLAYER:
                    return ObjectType.Player;
                case HighGuid.DYNAMICOBJECT:
                    return ObjectType.DynamicObject;
                case HighGuid.ITEM:
                    return ObjectType.Item;
                case HighGuid.GAMEOBJECT:
                case HighGuid.TRANSPORT:
                case HighGuid.MO_TRANSPORT:
                    return ObjectType.GameObject;
                case HighGuid.UNIT:
                case HighGuid.PET:
                    return ObjectType.Creature;
                default:
                    return ObjectType.Object;
            }
        }

        // FastObjectListView

        public static void UpdateFilteredEventsTimes(this FastObjectListView folv)
        {
            if (!(folv.FilteredObjects is ArrayList sniffedEvents) || sniffedEvents.Count == 0)
                return;

            SniffedEvent previousEvent = null;
            SniffedEvent firstEvent = null;
            foreach (SniffedEvent sEvent in sniffedEvents)
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

        public static void LightRefreshObjects(this FastObjectListView folv)
        {
            if (folv.Objects is ArrayList sniffedEvents && sniffedEvents.Count > 0)
                folv.RefreshObjects(new SniffedEvent[1] { (SniffedEvent)sniffedEvents[0] });
        }

        public static void ClearSelection(this FastObjectListView folv)
        {
            folv.SelectedObject = null;
            folv.SelectedObjects = null;
        }

        // IAsyncResult

        public static byte[] GetBuffer(this IAsyncResult ar)
        {
            return ar.AsyncState as byte[];
        }

        // String

        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source?.IndexOf(toCheck, comp) >= 0;
        }

        public static ObjectTypeFilter GetObjectTypeFilterValueFromString(this string name)
        {
            foreach (var item in EnumUtils<ObjectTypeFilter>.Values)
                if (item.ToString().Equals(name))
                    return item;
            return ObjectTypeFilter.Any;
        }

        public static EventTypeFilter DetermineEventCategory(this string eventName)
        {
            if (eventName.StartsWith("Client"))
                return EventTypeFilter.Client;
            if (eventName.StartsWith("Player"))
                return EventTypeFilter.Player;
            if (eventName.StartsWith("Creature"))
                return EventTypeFilter.Creature;
            if (eventName.StartsWith("Unit"))
                return EventTypeFilter.Unit;
            if (eventName.StartsWith("GameObject"))
                return EventTypeFilter.GameObject;

            return EventTypeFilter.Miscellaneous;
        }

        // SniffedEvent

        /// <summary>
        /// Extract Object names from short descriptions.
        /// Any change on short descriptions at ReplayCore will most likely break this. 
        /// </summary>
        public static string GetObjectGuidName(this SniffedEvent e, bool source)
        {
            var objectGuid = source ? e.SourceGuid : e.TargetGuid;

            if (objectGuid.IsEmpty)
                return string.Empty;

            var objectHighType = objectGuid.GetHighType();
            var eventType = e.EventType;
            if (objectHighType == HighGuid.TRANSPORT)
                return "Transport";

            var objectType = objectGuid.GetObjectType();

            var typeName = objectType.ToString();
            switch(objectHighType)
            {
                case HighGuid.DYNAMICOBJECT:
                    typeName = "DynObject";
                    break;
                case HighGuid.PET:
                    typeName = "Pet";
                    break;
            }

            try
            {
                switch (eventType)
                {
                    case SniffedEventType.SE_WORLDOBJECT_CREATE1:
                    case SniffedEventType.SE_WORLDOBJECT_CREATE2:
                    case SniffedEventType.SE_UNIT_UPDATE_SPEED:
                    case SniffedEventType.SE_UNIT_CLIENTSIDE_MOVEMENT:
                    case SniffedEventType.SE_UNIT_SERVERSIDE_MOVEMENT:
                    case SniffedEventType.SE_PLAY_SOUND:
                    case SniffedEventType.SE_SPELL_CAST_START:
                    case SniffedEventType.SE_UNIT_ATTACK_STOP:
                    case SniffedEventType.SE_UNIT_ATTACK_START:
                    case SniffedEventType.SE_UNIT_ATTACK_LOG:
                    case SniffedEventType.SE_UNIT_UPDATE_GUID_VALUE:
                    case SniffedEventType.SE_SPELL_CAST_GO:
                    case SniffedEventType.SE_UNIT_UPDATE_SCALE:
                    case SniffedEventType.SE_CREATURE_THREAT_UPDATE:
                    case SniffedEventType.SE_WORLDOBJECT_DESTROY:
                    case SniffedEventType.SE_UNIT_UPDATE_ENTRY:
                    case SniffedEventType.SE_UNIT_UPDATE_EMOTE_STATE:
                    case SniffedEventType.SE_UNIT_UPDATE_DISPLAY_ID:
                    case SniffedEventType.SE_UNIT_UPDATE_MOUNT:
                    case SniffedEventType.SE_UNIT_UPDATE_FACTION:
                    case SniffedEventType.SE_UNIT_UPDATE_LEVEL:
                    case SniffedEventType.SE_UNIT_UPDATE_AURA_STATE:
                    case SniffedEventType.SE_UNIT_UPDATE_STAND_STATE:
                    case SniffedEventType.SE_UNIT_UPDATE_VIS_FLAGS:
                    case SniffedEventType.SE_UNIT_UPDATE_ANIM_TIER:
                    case SniffedEventType.SE_UNIT_UPDATE_SHEATH_STATE:
                    case SniffedEventType.SE_UNIT_UPDATE_PVP_FLAGS:
                    case SniffedEventType.SE_UNIT_UPDATE_SHAPESHIFT_FORM:
                    case SniffedEventType.SE_UNIT_UPDATE_NPC_FLAGS:
                    case SniffedEventType.SE_UNIT_UPDATE_UNIT_FLAGS:
                    case SniffedEventType.SE_UNIT_UPDATE_UNIT_FLAGS2:
                    case SniffedEventType.SE_UNIT_UPDATE_DYNAMIC_FLAGS:
                    case SniffedEventType.SE_UNIT_UPDATE_CURRENT_HEALTH:
                    case SniffedEventType.SE_UNIT_UPDATE_MAX_HEALTH:
                    case SniffedEventType.SE_UNIT_UPDATE_POWER_TYPE:
                    case SniffedEventType.SE_UNIT_UPDATE_CURRENT_POWER:
                    case SniffedEventType.SE_UNIT_UPDATE_MAX_POWER:
                    case SniffedEventType.SE_UNIT_UPDATE_BOUNDING_RADIUS:
                    case SniffedEventType.SE_UNIT_UPDATE_COMBAT_REACH:
                    case SniffedEventType.SE_UNIT_UPDATE_MAIN_HAND_ATTACK_TIME:
                    case SniffedEventType.SE_UNIT_UPDATE_OFF_HAND_ATTACK_TIME:
                    case SniffedEventType.SE_UNIT_UPDATE_CHANNEL_SPELL:
                    case SniffedEventType.SE_GAMEOBJECT_UPDATE_FLAGS:
                    case SniffedEventType.SE_GAMEOBJECT_UPDATE_STATE:
                    case SniffedEventType.SE_GAMEOBJECT_UPDATE_ARTKIT:
                    case SniffedEventType.SE_GAMEOBJECT_UPDATE_ANIMPROGRESS:
                    case SniffedEventType.SE_GAMEOBJECT_UPDATE_DYNAMIC_FLAGS:
                    case SniffedEventType.SE_GAMEOBJECT_UPDATE_PATH_PROGRESS:
                    case SniffedEventType.SE_UNIT_UPDATE_AURAS:
                    case SniffedEventType.SE_CREATURE_TEXT:
                    case SniffedEventType.SE_CREATURE_THREAT_CLEAR:
                    case SniffedEventType.SE_CREATURE_THREAT_REMOVE:
                    case SniffedEventType.SE_CREATURE_EQUIPMENT_UPDATE:
                    case SniffedEventType.SE_PLAYER_EQUIPMENT_UPDATE:
                    case SniffedEventType.SE_PLAYER_MINIMAP_PING:
                    case SniffedEventType.SE_GAMEOBJECT_CUSTOM_ANIM:
                    case SniffedEventType.SE_PLAY_SPELL_VISUAL_KIT:
                    case SniffedEventType.SE_SPELL_CAST_FAILED:
                    case SniffedEventType.SE_GAMEOBJECT_DESPAWN_ANIM:
                        var endStr = objectGuid.HasEntry() ? " (Entry" : " (Guid";
                        var start = e.ShortDescription.IndexOf(typeName) + typeName.Length + 1;
                        var end = e.ShortDescription.IndexOf(endStr);
                        var name = e.ShortDescription.Substring(start, end - start);
                        return name;
                    case SniffedEventType.SE_PLAYER_CHAT:
                        start = e.ShortDescription.IndexOf(" [") + 2;
                        if (start == -1 && e.ShortDescription.StartsWith("["))
                            start = 0;
                        end = e.ShortDescription.IndexOf("]", Math.Max(0, start - 2));
                        return e.ShortDescription.Substring(start, end - start);
                    case SniffedEventType.SE_LOGIN:
                        start = e.ShortDescription.IndexOf("with ") + 5;
                        end = e.ShortDescription.LastIndexOf(".");
                        return e.ShortDescription.Substring(start, end - start);
                }
            }
            catch (Exception) { }

            return string.Empty;
        }

        public static EventTypeFilter GetEventTypeFilter(this SniffedEvent e)
        {
            if (DataHolder.TryGetEventTypeEntryById((uint)e.EventType, out var eventType))
                return eventType.EventTypeFilter;
            return EventTypeFilter.All;
        }

        public static string GetEventTypeName(this SniffedEvent e)
        {
            if (DataHolder.TryGetEventTypeEntryById((uint)e.EventType, out var eventType))
                return eventType.EventName;
            return string.Empty;
        }

        public static string FormatTimeString(this SniffedEvent sEvent, int timeType, int timeDisp)
        {
            ulong displayTime = 0;
            switch (timeType)
            {
                case 0: // Exact Time
                    {
                        displayTime = sEvent.EventTime;
                        break;
                    }
                case 1: // Time Since First Event
                    {
                        displayTime = sEvent.EventTime - sEvent.FirstEventTime;
                        break;
                    }
                case 2: // Time Since Previous Event
                    {
                        displayTime = sEvent.EventTime - sEvent.PreviousEventTime;
                        break;
                    }
            }

            string timeString = "";
            switch (timeDisp)
            {
                case 0: // Milliseconds
                    {
                        timeString = string.Format("{0:n0}", displayTime);
                        break;
                    }
                case 1: // Seconds
                    {
                        displayTime = displayTime / 1000;
                        timeString = string.Format("{0:n0}", displayTime);
                        break;
                    }
                case 2: // Formatted
                    {
                        TimeSpan t = TimeSpan.FromMilliseconds(displayTime);
                        timeString = string.Format("{0:D2}h:{1:D2}m:{2:D2}s:{3:D3}ms",
                                        t.Hours,
                                        t.Minutes,
                                        t.Seconds,
                                        t.Milliseconds);
                        break;
                    }
            }
            return timeString;
        }
    }

    // Enum

    public class EnumUtils<T> where T : struct, IConvertible
    {
        public static IEnumerable<T> Values
        {
            get
            {
                if (!typeof(T).IsEnum)
                    throw new ArgumentException("T must be an enumerated type");

                return Enum.GetValues(typeof(T)).Cast<T>();
            }
        }
    }
}
