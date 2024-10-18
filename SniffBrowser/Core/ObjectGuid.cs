using System;
using System.Collections.Generic;

namespace SniffBrowser.Core
{
    public class ObjectGuid : IEquatable<ObjectGuid>
    {
        /// <summary>
        /// Only set to the first ocurrance of this object.
        /// </summary>
        public string ObjectName { get; set; }

        public ulong RawGuid { get; protected set; }
        public HashSet<SniffedEvent> SniffedEvents = new HashSet<SniffedEvent>();
        public bool IsEmpty { get; set; }

        private static ObjectGuid empty;
        public static ObjectGuid Empty
        {
            get 
            {
                if (empty == null)
                    empty = new ObjectGuid(0);

                return empty; 
            }
        } 

        public ObjectGuid(ulong id)
        {        
            RawGuid = id;
            this.IsEmpty = id == 0;
        }

        public static bool operator ==(ObjectGuid first, ObjectGuid other)
        {
            if (ReferenceEquals(first, other))
                return true;

            if (((object)first == null) || ((object)other == null))
                return false;

            return first.Equals(other);
        }

        public static bool operator !=(ObjectGuid first, ObjectGuid other)
        {
            return !(first == other);
        }

        public override bool Equals(object obj)
        {
            var oGuid = obj as ObjectGuid;
            return oGuid != null && Equals(oGuid);
        }

        public bool Equals(ObjectGuid other)
        {
            return other.RawGuid == RawGuid;
        }

        public override int GetHashCode()
        {
            return RawGuid.GetHashCode();
        }

        public override string ToString()
        {
            if (IsEmpty)
                return "None";

            string str = ObjectName + " | ";
            str += this.GetObjectType().ToString() + " (";
            if (this.HasEntry())
                str += "Entry: " + this.GetEntry().ToString() + " ";
            str += "Guid: " + this.GetCounter().ToString() + ")";

            return str;
        }
    }
}
