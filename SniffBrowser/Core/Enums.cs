namespace SniffBrowser.Core
{
    public enum ScrollBarDirection : byte
    {
        SB_HORZ = 0,
        SB_VERT = 1,
        SB_CTL = 2,
        SB_BOTH = 3
    }

    // for SMSG packets header contains uint16 size, then uint8 opcode
    // for CMSG packets header contains only the uint8 opcode
    public enum GUIOpcode : byte
    {
        SMSG_EVENT_TYPE_LIST = 1,
        CMSG_REQUEST_EVENT_DATA = 2,
        SMSG_EVENT_DATA_LIST = 3,
        SMSG_EVENT_DATA_END = 4,
        CMSG_CHAT_COMMAND = 5,
        CMSG_GOTO_GUID = 6,
        CMSG_MAKE_SCRIPT = 7,
        CMSG_REQUEST_EVENT_DESCRIPTION = 8,
        SMSG_EVENT_DESCRIPTION = 9,
        SMSG_EVENT_DATA_LIST_COUNT = 10
    }

    public enum ObjectTypeFilter : byte
    {
        Any = 0,
        GameObject = 1,
        Transport = 2,
        Unit = 3,
        Creature = 4,
        Pet = 5,
        Player = 6,
    };

    public enum ObjectType : byte
    {
        Object = 0,
        Item = 1,
        Container = 2,
        Creature = 3,
        Player = 4,
        GameObject = 5,
        DynamicObject = 6,
        Corpse = 7
    };

    public enum EventTypeFilter : byte
    {
        All,
        GameObject,
        Unit,
        Creature,
        Player,
        Client,
        Miscellaneous
    }

    public enum RowColorType : byte
    {
        None = 0,
        Alternating = 1,
        SourceBased = 2
    }

    public enum SniffedEventType : byte
    {
        SE_WEATHER_UPDATE,
        SE_WORLD_TEXT,
        SE_WORLD_STATE_UPDATE,
        SE_WORLDOBJECT_CREATE1,
        SE_WORLDOBJECT_CREATE2,
        SE_WORLDOBJECT_DESTROY,
        SE_UNIT_ATTACK_LOG,
        SE_UNIT_ATTACK_START,
        SE_UNIT_ATTACK_STOP,
        SE_UNIT_EMOTE,
        SE_UNIT_CLIENTSIDE_MOVEMENT,
        SE_UNIT_SERVERSIDE_MOVEMENT,
        SE_UNIT_UPDATE_ENTRY,
        SE_UNIT_UPDATE_SCALE,
        SE_UNIT_UPDATE_DISPLAY_ID,
        SE_UNIT_UPDATE_MOUNT,
        SE_UNIT_UPDATE_FACTION,
        SE_UNIT_UPDATE_LEVEL,
        SE_UNIT_UPDATE_AURA_STATE,
        SE_UNIT_UPDATE_EMOTE_STATE,
        SE_UNIT_UPDATE_STAND_STATE,
        SE_UNIT_UPDATE_VIS_FLAGS,
        SE_UNIT_UPDATE_ANIM_TIER,
        SE_UNIT_UPDATE_SHEATH_STATE,
        SE_UNIT_UPDATE_PVP_FLAGS,
        SE_UNIT_UPDATE_SHAPESHIFT_FORM,
        SE_UNIT_UPDATE_NPC_FLAGS,
        SE_UNIT_UPDATE_UNIT_FLAGS,
        SE_UNIT_UPDATE_UNIT_FLAGS2,
        SE_UNIT_UPDATE_DYNAMIC_FLAGS,
        SE_UNIT_UPDATE_CURRENT_HEALTH,
        SE_UNIT_UPDATE_MAX_HEALTH,
        SE_UNIT_UPDATE_POWER_TYPE,
        SE_UNIT_UPDATE_CURRENT_POWER,
        SE_UNIT_UPDATE_MAX_POWER,
        SE_UNIT_UPDATE_BOUNDING_RADIUS,
        SE_UNIT_UPDATE_COMBAT_REACH,
        SE_UNIT_UPDATE_MAIN_HAND_ATTACK_TIME,
        SE_UNIT_UPDATE_OFF_HAND_ATTACK_TIME,
        SE_UNIT_UPDATE_CHANNEL_SPELL,
        SE_UNIT_UPDATE_GUID_VALUE,
        SE_UNIT_UPDATE_SPEED,
        SE_UNIT_UPDATE_AURAS,
        SE_CREATURE_TEXT,
        SE_CREATURE_THREAT_CLEAR,
        SE_CREATURE_THREAT_REMOVE,
        SE_CREATURE_THREAT_UPDATE,
        SE_CREATURE_EQUIPMENT_UPDATE,
        SE_PLAYER_CHAT,
        SE_PLAYER_EQUIPMENT_UPDATE,
        SE_PLAYER_MINIMAP_PING,
        SE_RAID_TARGET_ICON_UPDATE,
        SE_GAMEOBJECT_CUSTOM_ANIM,
        SE_GAMEOBJECT_DESPAWN_ANIM,
        SE_GAMEOBJECT_UPDATE_FLAGS,
        SE_GAMEOBJECT_UPDATE_STATE,
        SE_GAMEOBJECT_UPDATE_ARTKIT,
        SE_GAMEOBJECT_UPDATE_ANIMPROGRESS,
        SE_GAMEOBJECT_UPDATE_DYNAMIC_FLAGS,
        SE_GAMEOBJECT_UPDATE_PATH_PROGRESS,
        SE_PLAY_MUSIC,
        SE_PLAY_SOUND,
        SE_PLAY_SPELL_VISUAL_KIT,
        SE_SPELL_CAST_FAILED,
        SE_SPELL_CAST_START,
        SE_SPELL_CAST_GO,
        SE_SPELL_CHANNEL_START,
        SE_SPELL_CHANNEL_UPDATE,
        SE_CLIENT_AREATRIGGER_ENTER,
        SE_CLIENT_AREATRIGGER_LEAVE,
        SE_CLIENT_QUEST_ACCEPT,
        SE_CLIENT_QUEST_COMPLETE,
        SE_CLIENT_CREATURE_INTERACT,
        SE_CLIENT_GAMEOBJECT_USE,
        SE_CLIENT_ITEM_USE,
        SE_CLIENT_RECLAIM_CORPSE,
        SE_CLIENT_RELEASE_SPIRIT,
        SE_QUEST_UPDATE_COMPLETE,
        SE_QUEST_UPDATE_FAILED,
        SE_XP_GAIN_LOG,
        SE_FACTION_STANDING_UPDATE,
        SE_LOGIN,
        SE_LOGOUT,
        SE_CINEMATIC_BEGIN,
        SE_CINEMATIC_END,
    }

    public enum HighGuid
    {
        Item = 0x4000,                       // blizz 4000
        Container = 0x4000,                       // blizz 4000
        Player = 0x0000,                       // blizz 0000
        GameObject = 0xF110,                       // blizz F110
        Transport = 0xF120,                       // blizz F120 (for GAMEOBJECT_TYPE_TRANSPORT)
        Creature = 0xF130,                       // blizz F130
        Pet = 0xF140,                       // blizz F140
        DynamicObject = 0xF100,                       // blizz F100
        Corpse = 0xF101,                       // blizz F100
        MoTransport = 0x1FC0,                       // blizz 1FC0 (for GAMEOBJECT_TYPE_MO_TRANSPORT)
    };
}
