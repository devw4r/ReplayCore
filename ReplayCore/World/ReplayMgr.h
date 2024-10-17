#ifndef __REPLAY_MGR_H
#define __REPLAY_MGR_H

#include "../Defines/Common.h"
#include "ObjectGuid.h"
#include "ObjectDefines.h"
#include "UnitDefines.h"
#include "WaypointData.h"
#include "PlayerDefines.h"
#include "SniffedEvents.h"
#include "../GUI/GUIFilters.h"
#include <set>
#include <map>

class Object;
class WorldObject;
class Unit;
class Player;
class GameObject;
class DynamicObject;
class MovementInfo;

#define INVALID_MARKER INT32_MAX
#define VISUAL_MARKER_AURA 1130
#define VISUAL_MARKER_SLOT 31

// Only the mutable update fields.
struct ObjectData
{
    ObjectGuid guid;
    uint32 entry = 0;
    float scale = DEFAULT_OBJECT_SCALE;
    uint32 sourceSniffId = 0;

    void InitializeObject(Object* pObject) const;
};

struct WorldObjectData : public ObjectData
{
    WorldLocation location;
    ObjectGuid transportGuid;
    Position transportPosition;

    void InitializeWorldObject(WorldObject* pObject) const;

    uint32 GetMapId() const { return location.mapId; }
    float GetPositionX() const { return location.x; }
    float GetPositionY() const { return location.y; }
    float GetPositionZ() const { return location.z; }
    float GetOrientation() const { return location.o; }
    Position const& GetPosition() const { return location; }
    WorldLocation const& GetLocation() const { return location; }
};

struct GameObjectData : public WorldObjectData
{
    bool isOnTransport = false;
    bool isTemporary = false;
    ObjectGuid createdBy;
    uint32 displayId = 0;
    uint32 flags = 0;
    float rotation[4] = {};
    uint32 state = 0;
    uint32 dynamicFlags = 0;
    uint32 pathProgress = 0;
    uint32 faction = 0;
    uint32 type = 0;
    uint32 level = 0;
    uint32 artKit = 0;
    uint32 animProgress = 100;

    void InitializeGameObject(GameObject* pGo) const;
};

struct DynamicObjectData : public WorldObjectData
{
    ObjectGuid caster;
    uint32 type = 0;
    uint32 spellId = 0;
    uint32 visualId = 0;
    float radius = 0.0f;

    void InitializeDynamicObject(DynamicObject* pDynObject) const;
};

struct UnitData : public WorldObjectData
{
    ObjectGuid charm;
    ObjectGuid summon;
    ObjectGuid charmedBy;
    ObjectGuid summonedBy;
    ObjectGuid createdBy;
    ObjectGuid demonCreator;
    ObjectGuid target;
    uint32 currentHealth = 1;
    uint32 maxHealth = 1;
    uint32 currentPowers[MAX_POWERS_WOTLK] = {};
    uint32 maxPowers[MAX_POWERS_WOTLK] = {};
    uint32 level = 1;
    uint32 faction = 35;
    uint8 raceId = 0;
    uint8 classId = 0;
    uint8 gender = 0;
    uint8 powerType = 0;
    uint32 virtualItems[MAX_VIRTUAL_ITEM_SLOT] = {};
    uint32 auraState = 0;
    uint32 mainHandAttackTime = 2000;
    uint32 offHandAttackTime = 2000;
    uint32 rangedAttackTime = 2000;
    float boundingRadius = 0.347f;
    float combatReach = 1.5f;
    uint32 displayId = 0;
    uint32 nativeDisplayId = 0;
    uint32 mountDisplayId = 0;
    uint8 standState = 0;
    uint8 shapeShiftForm = 0;
    uint8 visFlags = 0;
    uint8 animTier = 0;
    uint8 pvpFlags = 0;
    uint32 npcFlags = 0;
    uint32 unitFlags = 0;
    uint32 unitFlags2 = 0;
    uint32 dynamicFlags = 0;
    uint32 channelSpell = 0;
    uint32 createdBySpell = 0;
    uint32 emoteState = 0;
    uint8 sheathState = 0;
    uint32 movementFlags = 0;
    float speedRate[MAX_MOVE_TYPE_TBC] = { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f };
    std::vector<uint32> auras;

    void InitializeUnit(Unit* pUnit) const;

    uint8 GetRace() const { return raceId; }
    uint32 GetRaceMask() const { return GetRace() ? 1 << (GetRace() - 1) : 0x0; }
    uint8 GetClass() const { return classId; }
    uint32 GetClassMask() const { return GetClass() ? 1 << (GetClass() - 1) : 0x0; }
    uint8 GetGender() const { return gender; }
    uint32 GetLevel() const { return level; }
};

struct CreatureData : public UnitData
{
    bool isOnTransport = false;
    bool isTemporary = false;
    bool isPet = false;
    bool isVehicle = false;
    bool isHovering = false;
    float wanderDistance = 0.0f;
    uint32 movementType = 0;

    void InitializeCreature(Unit* pUnit) const;
};

struct PlayerData : public UnitData
{
    std::string name;
    uint8 skin = 0;
    uint8 face = 0;
    uint8 hairStyle = 0;
    uint8 hairColor = 0;
    uint8 facialHair = 0;
    uint32 flags = 0;
    uint8 comboPoints = 0;
    int8 pvpTitle = 0;
    uint32 visibleItems[EQUIPMENT_SLOT_END] = {};
    uint32 visibleItemEnchants[EQUIPMENT_SLOT_END] = {};

    void InitializePlayer(Player* pPlayer) const;

    char const* GetName() const { return name.c_str(); }
    uint8 GetSkinColor() const { return skin; }
    uint8 GetFace() const { return face; }
    uint8 GetHairStyle() const { return hairStyle; }
    uint8 GetHairColor() const { return hairColor; }
    uint8 GetFacialHair() const { return facialHair; }

    uint32 GetVisibleItemId(uint32 slot) const { return visibleItems[slot]; }
    uint32 GetVisibleItemEnchant(uint32 slot) const { return visibleItemEnchants[slot]; }
};

typedef std::map<uint32 /*guid*/, std::map<uint32 /*parent_point*/, std::vector<Vector3>>> SplinesMap;

class ReplayMgr
{
public:
    static ReplayMgr& Instance();
    
    void LoadEverything()
    {
        LoadGameObjects();
        LoadDynamicObjects();
        LoadCreatures();
        LoadPlayers();
        LoadActivePlayers();
        LoadInitialWorldStates();
        LoadSniffedEvents();
        AssignTransportDataToWorldObjects();

        if (m_eventsMapBackup.empty())
            EnterMassParseMode();
    }

#pragma region WorldObjects

    ObjectGuid MakeObjectGuidFromSniffData(uint32 guid, uint32 entry, std::string type);
    ObjectGuid MakeObjectGuidFromSniffData(ObjectGuid guid, uint32 entry, std::string type); // trap for wrong arguments
    WorldObjectData* GetObjectSpawnData(ObjectGuid guid)
    {
        return GetObjectSpawnData(guid.GetCounter(), guid.GetTypeId());
    }
    WorldObjectData* GetObjectSpawnData(uint32 guid, uint32 typeId)
    {
        switch (typeId)
        {
            case TYPEID_GAMEOBJECT:
                return GetGameObjectSpawnData(guid);
            case TYPEID_DYNAMICOBJECT:
                return GetDynamicObjectSpawnData(guid);
            case TYPEID_UNIT:
                return GetCreatureSpawnData(guid);
            case TYPEID_PLAYER:
                return GetPlayerSpawnData(guid);
        }
        return nullptr;
    }

    void SpawnAllObjects()
    {
        SpawnPlayers();
        SpawnCreatures();
        SpawnGameObjects();
        SpawnDynamicObjects();
    }
    void LoadSpawnMarkers(char const* tableName, std::map<uint32, int32>& markersTable);
    int32 GetCreatureMarker(uint32 guid)
    {
        auto itr = m_creatureMarkers.find(guid);
        if (itr != m_creatureMarkers.end())
            return itr->second;

        return INT32_MAX;
    }
    void SetCreatureMarker(ObjectGuid guid, int32 marker);
    void ClearCreatureMarker(ObjectGuid guid);
    int32 GetGameObjectMarker(uint32 guid)
    {
        auto itr = m_gameobjectMarkers.find(guid);
        if (itr != m_gameobjectMarkers.end())
            return itr->second;

        return INT32_MAX;
    }
    void SetGameObjectMarker(ObjectGuid guid, int32 marker);
    void ClearGameObjectMarker(ObjectGuid guid);

    void LoadGameObjects();
    void SpawnGameObjects();
    GameObjectData* GetGameObjectSpawnData(uint32 guid)
    {
        auto itr = m_gameObjectSpawns.find(guid);
        if (itr == m_gameObjectSpawns.end())
            return nullptr;

        assert(guid == itr->second.guid.GetCounter());

        return &itr->second;
    }

    void LoadDynamicObjects();
    void SpawnDynamicObjects();
    DynamicObjectData* GetDynamicObjectSpawnData(uint32 guid)
    {
        auto itr = m_dynamicObjectSpawns.find(guid);
        if (itr == m_dynamicObjectSpawns.end())
            return nullptr;

        assert(guid == itr->second.guid.GetCounter());

        return &itr->second;
    }

    void SpawnCreatures();
    void LoadCreatures();
    void LoadCreaturePetNames();
    template<class T>
    void LoadInitialGuidValues(const char* tableName, T& spawnsMap);
    template<class T>
    void LoadInitialPowerValues(const char* tableName, T& spawnsMap, TypeID typeId);
    void AssignTransportDataToWorldObjects();
    CreatureData* GetCreatureSpawnData(uint32 guid)
    {
        auto itr = m_creatureSpawns.find(guid);
        if (itr == m_creatureSpawns.end())
            return nullptr;

        assert(guid == itr->second.guid.GetCounter());

        return &itr->second;
    }
    std::string GetCreaturePetName(uint32 guid)
    {
        auto itr = m_creaturePetNames.find(guid);
        if (itr == m_creaturePetNames.end())
            return std::string();

        return itr->second;
    }
    void GetWaypointsForCreature(uint32 guid, std::vector<WaypointData>& waypoints, bool useStartPosition);

    void SpawnPlayers();
    void LoadPlayers();
    void LoadActivePlayers();
    Player* GetActivePlayer();
    ObjectGuid GetActivePlayerGuid();
    std::set<ObjectGuid> const& GetActivePlayers() const { return m_activePlayers; }
    std::map<uint32, ObjectGuid> const& GetActivePlayerTimes() const { return m_activePlayerTimes; }
    PlayerData* GetPlayerSpawnData(uint32 guid)
    {
        auto itr = m_playerSpawns.find(guid);
        if (itr == m_playerSpawns.end())
            return nullptr;

        assert(guid == itr->second.guid.GetCounter());

        return &itr->second;
    }
    std::string GetPlayerChatName(ObjectGuid guid)
    {
        auto itr = m_playerChatNames.find(guid);
        if (itr == m_playerChatNames.end())
            return std::string();

        return itr->second;
    }
    bool IsPlayerNameTaken(std::string name);
    uint32 GetNewPlayerLowGuid();
    ObjectGuid GetOrCreatePlayerChatGuid(std::string name);

    bool IsInMassParseMode() const { return m_eventsMapBackup.empty(); }
    void EnterMassParseMode();
    template<class T>
    void RemoveDuplicateSpawns(std::map<uint32 /*guid*/, T>& spawnsMap);
    void RemoveTemporaryCreatures();
    void RemoveTemporaryGameObjects();
    void RemoveTransportCreatures();
    void RemoveTransportGameObjects();
    void AddInitialAurasToCreatures();
    void SetCreatureAura(ObjectGuid guid, uint32 slot, uint32 spellId);

#pragma endregion WorldObjects

#pragma region SniffedEvents

    void LoadSniffedEvents();
    void PrepareSniffedEventDataForCurrentClient();
    void PrepareClientSideMovementDataForCurrentClient();
    std::string GuessMovementOpcode(MovementInfo const& oldState, MovementInfo const& newState);
    void LoadUnitClientSideMovement(char const* tableName, uint32 typeId);
    void LoadWeatherUpdates();
    void LoadWorldText();
    void LoadInitialWorldStates();
    std::map<uint32, uint32> GetInitialWorldStatesForCurrentTime();
    void LoadWorldStateUpdates();
    void LoadWorldObjectCreate(char const* tableName, uint32 typeId, bool isSpawn);
    void LoadGameObjectCreate(char const* tableName, uint32 typeId, bool isSpawn);
    void LoadUnitCreate(char const* tableName, uint32 typeId, bool isSpawn);
    void LoadWorldObjectDestroy(char const* tableName, uint32 typeId);
    template <class T>
    void LoadUnitAttackToggle(char const* tableName, uint32 typeId);
    void LoadUnitAttackLog(char const* tableName, uint32 typeId);
    void LoadUnitEmote(char const* tableName, uint32 typeId);
    void LoadServerSideMovement(char const* tableName, TypeID typeId, SplinesMap const& splinesMap, bool isCombatMovements);
    void LoadServerSideMovementSplines(char const* tableName, SplinesMap& splinesMap);
    template <class T>
    void LoadObjectValuesUpdate(char const* tableName, char const* fieldName, uint32 typeId);
    template <class T>
    void LoadObjectValuesUpdate_float(char const* tableName, char const* fieldName, uint32 typeId);
    void LoadUnitGuidValuesUpdate(char const* tableName, uint32 typeId);
    template <class T>
    void LoadUnitPowerValuesUpdate(char const* tableName, char const* fieldName, uint32 typeId);
    void LoadUnitSpeedUpdate(char const* tableName, uint32 typeId);
    void LoadUnitAurasUpdate(char const* tableName, uint32 typeId);
    void LoadCreatureText();
    void LoadCreatureThreatClear();
    void LoadCreatureThreatRemove();
    void LoadCreatureThreatUpdate();
    void LoadCreatureEquipmentUpdate();
    void LoadPlayerChat();
    void LoadPlayerEquipmentUpdate();
    void LoadPlayerMinimapPing();
    void LoadRaidTargetIconUpdates();
    void LoadGameObjectCustomAnim();
    void LoadGameObjectDespawnAnim();
    void LoadPlayMusic();
    void LoadPlaySound();
    void LoadPlaySpellVisualKit();
    void LoadSpellCastFailed();
    void LoadSpellCastStart();
    void LoadSpellCastGo();
    void LoadSpellChannelStart();
    void LoadSpellChannelUpdate();
    void LoadClientAreatriggerEnter();
    void LoadClientAreatriggerLeave();
    void LoadClientQuestAccept();
    void LoadClientQuestComplete();
    void LoadClientCreatureInteract();
    void LoadClientGameObjectUse();
    void LoadClientItemUse();
    void LoadClientReclaimCorpse();
    void LoadClientReleaseSpirit();
    void LoadQuestUpdateComplete();
    void LoadQuestUpdateFailed();
    void LoadXPGainLog();
    void LoadFactionStandingUpdates();
    void LoadLoginTimes();
    void LoadLogoutTimes();
    void LoadCinematicBegin();
    void LoadCinematicEnd();

    std::shared_ptr<SniffedEvent> GetFirstEventForTarget(ObjectGuid guid, SniffedEventType eventType);
    void GetEventsListForTarget(ObjectGuid guid, std::string eventName, std::vector<std::pair<uint64, SniffedEventType>>& eventsList);
    void ReplayMgr::GetEventDescription(uint32 sniffedEventId, std::string& description);
    void GetEventsListForTargets(uint32 startTime, uint32 endTime, std::vector<ObjectFilterEntry> const& objectFilters, std::vector<std::pair<uint64, std::shared_ptr<SniffedEvent>>>& eventsList);
    void GetEventsListFromIdentifiers(std::set<uint32> eventIdentifiers, std::vector<std::pair<uint64, std::shared_ptr<SniffedEvent>>>& eventsList);

#pragma endregion SniffedEvents
    
#pragma region Replay

    void Update(uint32 const diff);
    void StartPlaying();
    void StopPlaying();
    void SetPlayTime(uint32 unixtime);
    void ChangeTime(uint32 unixtime);
    bool IsPlaying() { return m_enabled; }
    bool IsInitialized() { return m_initialized; }
    void Uninitialize();

    uint32 GetSniffedEventsCount() { return (m_eventsMapBackup.empty() ? 0 : (uint32)m_eventsMapBackup.size()); }
    uint32 GetCurrentSniffTime() { return m_currentSniffTime; }
    uint64 GetCurrentSniffTimeMs() { return m_currentSniffTimeMs; }
    uint32 GetStartTimeSniff() { return m_startTimeSniff; }
    uint32 GetTimeDifference() { return m_timeDifference; }
    uint32 GetFirstEventTime() { return (m_eventsMapBackup.empty() ? 0 : uint32(m_eventsMapBackup.begin()->first / IN_MILLISECONDS)); }
    uint64 GetFirstEventTimeMs() { return (m_eventsMapBackup.empty() ? 0 : m_eventsMapBackup.begin()->first); }
    uint32 GetLastEventTime() { return (m_eventsMapBackup.empty() ? 0 : uint32(m_eventsMapBackup.rbegin()->first / IN_MILLISECONDS)); }
    uint64 GetLastEventTimeMs() { return (m_eventsMapBackup.empty() ? 0 : m_eventsMapBackup.begin()->first); }

#pragma endregion Replay
   
private:
    bool m_enabled = false;
    bool m_initialized = false;
    uint32 m_currentSniffTime = 0;
    uint64 m_currentSniffTimeMs = 0;
    uint32 m_startTimeSniff = 0;
    uint32 m_timeDifference = 0;
    SplinesMap m_playerMovementSplines;
    SplinesMap m_creatureMovementSplines;
    SplinesMap m_creatureMovementCombatSplines;
    std::set<ObjectGuid> m_activePlayers;
    std::map<uint32 /*unixtime*/, ObjectGuid> m_activePlayerTimes;
    std::map<uint32 /*guid*/, std::string> m_creaturePetNames;
    std::map<ObjectGuid, std::string> m_playerChatNames; // players that have never been seen but wrote in a chat channel
    std::map<uint32 /*guid*/, PlayerData> m_playerSpawns;
    std::map<uint32 /*guid*/, CreatureData> m_creatureSpawns;
    std::map<uint32 /*guid*/, GameObjectData> m_gameObjectSpawns;
    std::map<uint32 /*guid*/, DynamicObjectData> m_dynamicObjectSpawns;
    std::map<uint32 /*guid*/, int32> m_creatureMarkers;
    std::map<uint32 /*guid*/, int32> m_gameobjectMarkers;
    std::map<uint64 /*unixtimems*/, std::map<uint32 /*variable*/, uint32 /*value*/>> m_initialWorldStates;
    std::multimap<uint64 /*unixtimems*/, std::shared_ptr<SniffedEvent>> m_eventsMap;       // prepared data in the current client's format
    std::multimap<uint64 /*unixtimems*/, std::shared_ptr<SniffedEvent>> m_eventsMapBackup; // stores the original data in sniff client format
};

#define sReplayMgr ReplayMgr::Instance()

#endif