#include "ScriptMaker.h"
#include "../Defines/Databases.h"
#include "GameDataMgr.h"
#include "GameObjectDefines.h"
#include "UpdateFields.h"
#include "../Defines/ClientVersions.h"
#include "../World/ReplayMgr.h"
#include <iostream>
#include <fstream>

inline bool IsSniffEventTypeWithIrrelevantSource(uint32 eventType)
{
    switch (eventType)
    {
        case SE_WORLDOBJECT_CREATE1:
        case SE_WORLDOBJECT_CREATE2:
            return true;
    }
    return false;
}

void ScriptMaker::SetScriptTargetParams(ScriptInfo& script, ObjectGuid target)
{
    if (target.IsCreature())
    {
        if (m_targetByGuidGuids.find(target) != m_targetByGuidGuids.end())
        {
            script.target_type = TARGET_T_CREATURE_WITH_GUID;
            script.target_param1 = target.GetCounter();
        }
        else
        {
            script.target_type = TARGET_T_NEAREST_CREATURE_WITH_ENTRY;
            script.target_param1 = target.GetEntry();
            script.target_param2 = 30;
        }
    }
    else if (target.IsGameObject())
    {
        if (m_targetByGuidGuids.find(target) != m_targetByGuidGuids.end())
        {
            script.target_type = TARGET_T_GAMEOBJECT_WITH_GUID;
            script.target_param1 = target.GetCounter();
        }
        else
        {
            script.target_type = TARGET_T_NEAREST_GAMEOBJECT_WITH_ENTRY;
            script.target_param1 = target.GetEntry();
            script.target_param2 = 30;
        }
    }
    else if (target.IsPlayer())
    {
        script.target_type = TARGET_T_NEAREST_PLAYER;
        script.target_param1 = 30;
    }
}

struct WaypointRow
{
    uint64 unixTimeMs = 0;
    Vector3 pos;
    float orientation;
    uint32 waitTime = 0;
    uint32 travelTime = 0;
    uint32 scriptId = 0;
};

#define UNKNOWN_TEXTS_START 200000

void ScriptMaker::MakeScript(uint32 defaultScriptId, uint32 genericScriptStartId, std::string tableName, std::string commentPrefix, ObjectGuid defaultSource, ObjectGuid defaultTarget, bool saveGoSpawnsToDb, std::vector<std::pair<uint64, std::shared_ptr<SniffedEvent>>> const& eventsList, bool makeWaypoints)
{
    m_saveGoSpawnsToDb = saveGoSpawnsToDb;
    CheckGuidsThatNeedSeparateScript(defaultSource, defaultTarget, eventsList);

    std::vector<WaypointRow> waypoints;
    std::set<ObjectGuid> involvedGuids;

    uint64 const firstEventTime = eventsList.begin()->first;
    for (auto const& itr : eventsList)
    {
        if (makeWaypoints && defaultSource == itr.second->GetSourceGuid() && itr.second->GetType() == SE_UNIT_SERVERSIDE_MOVEMENT)
        {
            auto ptr = std::static_pointer_cast<SniffedEvent_ServerSideMovement>(itr.second);
            if (ptr->m_isCombatMovement)
            {
                printf("[ScriptMaker] Skipping combat movement.\n");
                continue;
            }

            WaypointRow row;
            row.pos = ptr->m_startPosition;
            row.orientation = ptr->m_finalOrientation;
            row.unixTimeMs = itr.first;
            row.travelTime = ptr->m_moveTime;

            if (!waypoints.empty())
            {
                WaypointRow& lastWP = waypoints.back();
                uint64 timeDiff = itr.first - lastWP.unixTimeMs;
                if (timeDiff > lastWP.travelTime)
                    lastWP.waitTime = timeDiff - lastWP.travelTime;
            }
            else
            {
                auto script = std::make_shared<ScriptInfo>();
                script->command = SCRIPT_COMMAND_START_WAYPOINTS;
                script->delay = (itr.first - firstEventTime) / IN_MILLISECONDS;
                script->comment = itr.second->GetSourceGuid().GetName() + " - " + "Start Waypoints";
                m_mainScript.push_back(script);
            }

            waypoints.push_back(row);
            continue;
        }

        bool isSeparateScript = false;
        ObjectGuid currentSource = defaultSource;
        ObjectGuid currentTarget = defaultTarget;
        ObjectGuid const eventSource = itr.second->GetSourceGuid();
        ObjectGuid const eventTarget = itr.second->GetTargetGuid();

        uint32 waypointScriptId = 0;
        uint32 waypointScriptDelay = 0;

        std::vector<std::shared_ptr<ScriptInfo>>* scriptActions;
        if (!IsSniffEventTypeWithIrrelevantSource(itr.second->GetType()) && m_separateScriptGuids.find(eventSource) != m_separateScriptGuids.end())
        {
            isSeparateScript = true;
            currentSource = eventSource;
            scriptActions = &m_genericScripts[eventSource];
        }
        else
        {
            if (makeWaypoints && !waypoints.empty())
            {
                for (int32 j = int32(waypoints.size()) - 1; j >= 0; j--)
                {
                    WaypointRow& lastWP = waypoints[j];
                    uint64 const arrivalTime = lastWP.unixTimeMs + lastWP.travelTime;
                    if (itr.first >= arrivalTime)
                    {
                        waypointScriptId = defaultSource.GetEntry() * 100 + waypoints.size();
                        waypointScriptDelay = (itr.first - arrivalTime) / IN_MILLISECONDS;
                        lastWP.scriptId = waypointScriptId;
                        break;
                    }
                }
            }

            if (waypointScriptId)
                scriptActions = &m_waypointScripts;
            else
                scriptActions = &m_mainScript;
        }

        size_t const oldSize = scriptActions->size();
        GetScriptInfoFromSniffedEvent(itr.first, itr.second, *scriptActions);
        size_t const newSize = scriptActions->size();

        if (oldSize < newSize)
        {
            if (!itr.second->GetSourceGuid().IsEmpty())
                involvedGuids.insert(itr.second->GetSourceGuid());
            if (!itr.second->GetTargetGuid().IsEmpty())
                involvedGuids.insert(itr.second->GetTargetGuid());

            for (size_t i = oldSize; i < newSize; i++)
            {
                std::shared_ptr<ScriptInfo>& scriptAction = (*scriptActions)[i];

                if (waypointScriptId)
                {
                    scriptAction->id = waypointScriptId;
                    scriptAction->delay = waypointScriptDelay;
                }
                else
                    scriptAction->delay = (itr.first - firstEventTime) / IN_MILLISECONDS;

                if (!eventSource.IsEmpty() &&
                    !IsSniffEventTypeWithIrrelevantSource(itr.second->GetType()))
                {
                    if (eventSource.IsCreature() || eventSource.IsGameObject())
                        scriptAction->comment = eventSource.GetName() + " - " + scriptAction->comment;

                    if (eventSource != currentSource)
                    {
                        if (eventSource == currentTarget)
                        {
                            std::swap(currentSource, currentTarget);
                            scriptAction->raw.data[4] |= SF_GENERAL_SWAP_INITIAL_TARGETS;
                            if (eventSource == eventTarget)
                            {
                                currentTarget = currentSource;
                                scriptAction->raw.data[4] |= SF_GENERAL_TARGET_SELF;
                            }
                        }
                        else
                        {
                            SetScriptTargetParams(*scriptAction, eventSource);
                            scriptAction->raw.data[4] |= SF_GENERAL_SWAP_FINAL_TARGETS;
                            currentTarget = currentSource;
                            currentSource = eventSource;
                        }
                    }
                }
                if (!eventTarget.IsEmpty() && eventTarget != currentTarget)
                {
                    if (eventTarget == currentSource)
                        scriptAction->raw.data[4] |= SF_GENERAL_TARGET_SELF;
                    else if (isSeparateScript && eventTarget == defaultSource)
                        m_genericScriptWithSwappedTargets.insert(currentSource);
                    else if (!eventTarget.IsEmpty())
                        SetScriptTargetParams(*scriptAction, eventTarget);
                }
            }
        }
    }

    std::ofstream log("script.sql");
    if (!log.is_open())
    {
        printf("[ScriptMaker] Cannot create sql file.");
        return;
    }

    if (!m_unknownScriptTexts.empty())
    {
        log << "/*\nFollowing texts are using a placeholder id:\n";
        for (uint32 i = 0; i < m_unknownScriptTexts.size(); i++)
        {
            log << uint32(UNKNOWN_TEXTS_START + i + 1) << " - " << m_unknownScriptTexts[i] << "\n";
        }
        log << "*/\n\n";
    }

    if (!involvedGuids.empty())
    {
        log << "/*\nFollowing spawns are involved in the script:\n";
        log << "\nCreatures:\n\n";
        for (auto const& guid : involvedGuids)
        {
            if (!guid.IsCreature())
                continue;

            uint32 dbGuid = sGameDataMgr.FindCreatureSpawnNearSniffedSpawn(guid.GetCounter());
            log << "Sniff " << guid.GetCounter() << " - Mangos " << dbGuid << " - " << guid.GetName() << "\n";
        }
        log << "\nGameObjects:\n\n";
        for (auto const& guid : involvedGuids)
        {
            if (!guid.IsGameObject())
                continue;

            uint32 dbGuid = sGameDataMgr.FindGameObjectSpawnNearSniffedSpawn(guid.GetCounter());
            log << "Sniff " << guid.GetCounter() << " - Mangos " << dbGuid << " - " << guid.GetName() << "\n";
        }
        log << "\n*/\n\n";
    }

    if (!m_gameObjectGuidsToExport.empty())
    {
        log << "-- GameObjects spawned during script.\n";
        log << "INSERT INTO `gameobject` (`guid`, `id`, `map`, `position_x`, `position_y`, `position_z`, `orientation`, `rotation0`, `rotation1`, `rotation2`, `rotation3`, `spawntimesecsmin`, `spawntimesecsmax`, `state`, `animprogress`) VALUES\n";
        uint32 count = 0;
        for (auto const& guid : m_gameObjectGuidsToExport)
        {
            if (GameObjectData const* pSpawn = sReplayMgr.GetGameObjectSpawnData(guid))
            {
                if (count)
                    log << ",\n";

                log << "(" << guid << ", " << pSpawn->entry << ", " << pSpawn->location.mapId << ", " <<
                    pSpawn->location.x << ", " << pSpawn->location.y << ", " << pSpawn->location.z << ", " <<
                    pSpawn->location.o << ", " << ", " << pSpawn->rotation[0] << ", " << pSpawn->rotation[1] << ", " <<
                    pSpawn->rotation[2] << ", " << pSpawn->rotation[3] << ", -1, -1, 1, 100)";

                count++;
            }
            else
                printf("[ScriptMaker] Error: GameObject %u does not exist!\n", guid);
        }
        log << ";\n\n";
    }

    std::vector<std::pair<ObjectGuid, uint32>> genericScriptsThatNeedStartScriptCommand;
    uint32 currentGenericScriptId = genericScriptStartId;
    for (auto const& itr : m_genericScripts)
    {
        uint32 delayOffset = 0;
        bool needStartScript = true;
        if (auto spawnScript = GetSpawnScriptForGuid(itr.first))
        {
            delayOffset = (spawnScript->first - firstEventTime) / IN_MILLISECONDS;

            if (spawnScript->second->command == SCRIPT_COMMAND_TEMP_SUMMON_CREATURE)
            {
                needStartScript = false;
                spawnScript->second->summonCreature.scriptId = currentGenericScriptId;
            }
        }
        if (needStartScript)
            genericScriptsThatNeedStartScriptCommand.insert(genericScriptsThatNeedStartScriptCommand.begin(), std::make_pair(itr.first, currentGenericScriptId));

        log << "-- Generic script for " + itr.first.GetString(true) + "\n";
        SaveScriptToFile(log, currentGenericScriptId, "generic_scripts", commentPrefix, itr.second, delayOffset);
        currentGenericScriptId++;
    }

    for (auto const& itr : genericScriptsThatNeedStartScriptCommand)
    {
        auto script = std::make_shared<ScriptInfo>();
        script->command = SCRIPT_COMMAND_START_SCRIPT;
        script->startScript.scriptId[0] = itr.second;
        script->startScript.chance[0] = 100;
        script->comment = "Start Script";
        SetScriptTargetParams(*script, itr.first);
        script->raw.data[4] |= SF_GENERAL_SWAP_FINAL_TARGETS;
        if (m_genericScriptWithSwappedTargets.find(itr.first) != m_genericScriptWithSwappedTargets.end())
            script->raw.data[4] |= SF_GENERAL_SWAP_INITIAL_TARGETS;
        m_mainScript.push_back(script);
    }

    log << "-- Main script\n";
    SaveScriptToFile(log, defaultScriptId, tableName, commentPrefix, m_mainScript, 0);

    if (!waypoints.empty())
    {
        log << "-- Waypoints for " << defaultSource.GetString(true) << "\n";
        log << "INSERT INTO `creature_movement` (`entry`, `point`, `position_x`, `position_y`, `position_z`, `orientation`, `waittime`, `wander_distance`, `script_id`) VALUES\n";
        for (uint32 i = 0; i < waypoints.size(); i++)
        {
            if (i)
                log << ",\n";

            WaypointRow const& wp = waypoints[i];
            log << "(" <<
                std::to_string(defaultSource.GetEntry()) << ", " <<
                std::to_string(i+1) << ", " <<
                std::to_string(wp.pos.x) << ", " <<
                std::to_string(wp.pos.y) << ", " <<
                std::to_string(wp.pos.z) << ", " <<
                std::to_string(wp.orientation) << ", " <<
                std::to_string(wp.waitTime) << ", " <<
                std::to_string(0.0f) << ", " <<
                std::to_string(wp.scriptId) <<
                ")";
        }
        log << ";\n";
        
        if (!m_waypointScripts.empty())
        {
            log << "\n-- Waypoint scripts\n";
            SaveScriptToFile(log, 0, "creature_movement_scripts", commentPrefix, m_waypointScripts, 0);
        }
    }

    printf("[ScriptMaker] Script saved to file.\n");
}

void ScriptMaker::SaveScriptToFile(std::ofstream& log, uint32 scriptId, std::string tableName, std::string commentPrefix, std::vector<std::shared_ptr<ScriptInfo>>const& vScripts, uint32 delayOffset)
{
    if (!commentPrefix.empty())
        commentPrefix += ": ";

    uint32 count = 0;
    log << "DELETE FROM `" << tableName << "` WHERE `id`=" << scriptId << ";\n";
    log << "INSERT INTO `" << tableName << "` (`id`, `delay`, `command`, `datalong`, `datalong2`, `datalong3`, `datalong4`, `target_param1`, `target_param2`, `target_type`, `data_flags`, `dataint`, `dataint2`, `dataint3`, `dataint4`, `x`, `y`, `z`, `o`, `condition_id`, `comments`) VALUES\n";
    for (const auto& script : vScripts)
    {
        std::string comment = commentPrefix + script->comment;

        if (count > 0)
            log << ",\n";
        log << "(" << (script->id ? script->id : scriptId) << ", " << script->delay - delayOffset << ", " << script->command << ", "
            << script->raw.data[0] << ", " << script->raw.data[1] << ", " << script->raw.data[2] << ", " << script->raw.data[3] << ", "
            << script->target_param1 << ", " << script->target_param2 << ", " << script->target_type << ", "
            << script->raw.data[4] << ", " << (int32)script->raw.data[5] << ", " << (int32)script->raw.data[6] << ", " << (int32)script->raw.data[7] << ", "
            << (int32)script->raw.data[8] << ", " << script->x << ", " << script->y << ", " << script->z << ", " << script->o << ", "
            << script->condition << ", '" << EscapeString(comment) << "')";

        count++;
    }
    log << ";\n\n";
}

void ScriptMaker::CheckGuidsThatNeedSeparateScript(ObjectGuid defaultSource, ObjectGuid defaultTarget, std::vector<std::pair<uint64, std::shared_ptr<SniffedEvent>>> const& eventsList)
{
    std::set<ObjectGuid> summonedByScript;
    std::map<uint32 /*typeId*/, std::map<uint32 /*entry*/, std::set<ObjectGuid>>> sameEntrySpawns;
    std::map<ObjectGuid, std::set<uint32>> eventTypesPerGuid;

    for (auto const& itr : eventsList)
    {
        ObjectGuid source = itr.second->GetSourceGuid();
        ObjectGuid target = itr.second->GetTargetGuid();

        if (!source.IsCreature() && !source.IsGameObject())
            continue;

        // a separate script is needed for these guids
        // because the source and target are both different than default ones
        if (!source.IsEmpty() &&
            !target.IsEmpty() &&
            source != target &&
            source != defaultSource &&
            target != defaultSource &&
            source != defaultTarget &&
            target != defaultTarget &&
            !IsSniffEventTypeWithIrrelevantSource(itr.second->GetType()) &&
            !(itr.second->GetType() == SE_WORLDOBJECT_DESTROY && summonedByScript.find(source) != summonedByScript.end()))
        {
            m_separateScriptGuids.insert(source);
        }
        else if (!source.IsEmpty() &&
                 source != defaultSource &&
                 itr.second->GetType() == SE_WORLDOBJECT_CREATE1 ||
                 itr.second->GetType() == SE_WORLDOBJECT_CREATE2)
        {
            summonedByScript.insert(source);
        }

        sameEntrySpawns[source.GetTypeId()][source.GetEntry()].insert(source);
        eventTypesPerGuid[source].insert(itr.second->GetType());

        if (!target.IsEmpty())
        {
            sameEntrySpawns[target.GetTypeId()][target.GetEntry()].insert(target);
            eventTypesPerGuid[target].insert(itr.second->GetType());
        }
    }

    // these guids need to be targeted by guid not entry,
    // because there are multiple objects with same entry
    for (auto const& itr : sameEntrySpawns)
    {
        for (auto const& itr2 : itr.second)
        {
            if (itr2.second.size() > 1)
            {
                for (auto const& itr3 : itr2.second)
                {
                    // for despawn commands for objects spawned during script we assign
                    // the despawn time on the spawn command instead of generating separate
                    // despawn action
                    bool willGenerateScript = false;
                    for (auto const& eventType : eventTypesPerGuid[itr3])
                    {
                        if (IsSniffEventTypeWithIrrelevantSource(eventType))
                            continue;

                        if (eventType == SE_WORLDOBJECT_DESTROY &&
                            summonedByScript.find(itr3) != summonedByScript.end())
                            continue;

                        willGenerateScript = true;
                        break;
                    }

                    if (!willGenerateScript)
                        continue;

                    m_targetByGuidGuids.insert(itr3);

                    // if its summoned by script then we can't target the creature by guid
                    // instead we need a separate generic script attached to summon command
                    if (summonedByScript.find(itr3) != summonedByScript.end())
                        m_separateScriptGuids.insert(itr3);
                }
            }
        }
    }
}

uint32 ScriptMaker::GetCreatureFieldValueBeforeTime(uint32 guid, uint64 unixtimems, char const* fieldName)
{
    uint32 field = 0;
    if (std::shared_ptr<QueryResult> result = SniffDatabase.Query("SELECT `%s` FROM `creature` WHERE `guid`=%u", fieldName, guid))
    {
        do
        {
            DbField* pFields = result->fetchCurrentRow();

            field = pFields[0].GetUInt32();

        } while (result->NextRow());
    }

    if (std::shared_ptr<QueryResult> result = SniffDatabase.Query("SELECT `%s` FROM `creature_values_update` WHERE (`guid` = %u) && (`unixtimems` < %llu) && (`%s` IS NOT NULL) ORDER BY `unixtimems`", fieldName, guid, unixtimems, fieldName))
    {
        do
        {
            DbField* pFields = result->fetchCurrentRow();

            field = pFields[0].GetUInt32();

        } while (result->NextRow());
    }
    return field;
}

uint32 ScriptMaker::GetTemporaryIdForUnknownBroadcastText(std::string text)
{
    auto itr = std::find(m_unknownScriptTexts.begin(), m_unknownScriptTexts.end(), text);
    if (itr == m_unknownScriptTexts.end())
    {
        m_unknownScriptTexts.push_back(text);
        return UNKNOWN_TEXTS_START + m_unknownScriptTexts.size();
    }

    return UNKNOWN_TEXTS_START + std::distance(m_unknownScriptTexts.begin(), itr);
}

void ScriptMaker::GetScriptInfoFromSniffedEvent(uint64 unixtimems, std::shared_ptr<SniffedEvent> sniffedEvent, std::vector<std::shared_ptr<ScriptInfo>>& scriptActions)
{
    if (sniffedEvent->GetType() == SE_WORLDOBJECT_CREATE1 || sniffedEvent->GetType() == SE_WORLDOBJECT_CREATE2)
    {
        auto ptr = std::static_pointer_cast<SniffedEvent_WorldObjectCreate>(sniffedEvent);
        if (ptr->GetSourceGuid().IsCreature())
        {
            auto script = std::make_shared<ScriptInfo>();
            script->command = SCRIPT_COMMAND_TEMP_SUMMON_CREATURE;
            script->summonCreature.attackTarget = -1;
            script->summonCreature.despawnType = TEMPSUMMON_TIMED_OR_DEAD_DESPAWN;
            script->summonCreature.despawnDelay = 60000;
            script->summonCreature.creatureEntry = ptr->GetSourceGuid().GetEntry();
            script->x = ptr->m_location.x;
            script->y = ptr->m_location.y;
            script->z = ptr->m_location.z;
            script->o = ptr->m_location.o;
            script->comment = "Summon Creature " + sGameDataMgr.GetCreatureName(ptr->GetSourceGuid().GetEntry());
            m_spawnScripts[ptr->GetSourceGuid()] = std::make_pair(unixtimems, script);
            scriptActions.push_back(script);
        }
        else if (ptr->GetSourceGuid().IsGameObject())
        {
            auto script = std::make_shared<ScriptInfo>();
            if (m_saveGoSpawnsToDb)
            {
                script->command = SCRIPT_COMMAND_RESPAWN_GAMEOBJECT;
                script->respawnGo.goGuid = ptr->GetSourceGuid().GetCounter();
                script->respawnGo.despawnDelay = 60;
                script->comment = "Respawn GameObject " + sGameDataMgr.GetGameObjectName(ptr->GetSourceGuid().GetEntry());
                m_gameObjectGuidsToExport.insert(ptr->GetSourceGuid().GetCounter());
            }
            else
            {
                script->command = SCRIPT_COMMAND_SUMMON_OBJECT;
                script->summonObject.gameobject_entry = ptr->GetSourceGuid().GetEntry();
                script->summonObject.respawn_time = 60;
                script->x = ptr->m_location.x;
                script->y = ptr->m_location.y;
                script->z = ptr->m_location.z;
                script->o = ptr->m_location.o;
                script->comment = "Summon GameObject " + sGameDataMgr.GetGameObjectName(ptr->GetSourceGuid().GetEntry());
            }
            m_spawnScripts[ptr->GetSourceGuid()] = std::make_pair(unixtimems, script);
            scriptActions.push_back(script);
        }
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_WorldObjectDestroy>(sniffedEvent))
    {
        if (ptr->GetSourceGuid().IsCreature())
        {
            if (auto spawnScript = GetSpawnScriptForGuid(ptr->GetSourceGuid()))
            {
                assert(spawnScript->second->command == SCRIPT_COMMAND_TEMP_SUMMON_CREATURE);
                assert(unixtimems > spawnScript->first);
                spawnScript->second->summonCreature.despawnDelay = unixtimems - spawnScript->first;
            }
            else
            {
                auto script = std::make_shared<ScriptInfo>();
                script->command = SCRIPT_COMMAND_DESPAWN_CREATURE;
                script->comment = "Despawn Creature";
                scriptActions.push_back(script);
            }
        }
        else if (ptr->GetSourceGuid().IsGameObject())
        {
            if (auto spawnScript = GetSpawnScriptForGuid(ptr->GetSourceGuid()))
            {
                assert(unixtimems > spawnScript->first);
                float respawnTimeSecs = ceilf(float(unixtimems - spawnScript->first) / float(IN_MILLISECONDS));

                if (m_saveGoSpawnsToDb)
                {
                    assert(spawnScript->second->command == SCRIPT_COMMAND_RESPAWN_GAMEOBJECT);
                    spawnScript->second->respawnGo.despawnDelay = (uint32)respawnTimeSecs;
                }
                else
                {
                    assert(spawnScript->second->command == SCRIPT_COMMAND_SUMMON_OBJECT);
                    spawnScript->second->summonObject.respawn_time = (uint32)respawnTimeSecs;
                }
            }
            else
            {
                auto script = std::make_shared<ScriptInfo>();
                script->command = SCRIPT_COMMAND_REMOVE_OBJECT;
                script->raw.data[4] = SF_GENERAL_TARGET_SELF; // this command takes target first
                script->comment = "Despawn GameObject";
                scriptActions.push_back(script);
            }
        }
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_CreatureText>(sniffedEvent))
    {
        auto script = std::make_shared<ScriptInfo>();
        script->command = SCRIPT_COMMAND_TALK;
        uint32 textId = sGameDataMgr.GetMatchingBroadcastTextId(ptr->m_text);
        if (!textId)
            textId = GetTemporaryIdForUnknownBroadcastText(ptr->m_text);
        script->talk.textId[0] = textId;
        script->comment = "Say Text";
        scriptActions.push_back(script);
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_UnitEmote>(sniffedEvent))
    {
        auto script = std::make_shared<ScriptInfo>();
        script->command = SCRIPT_COMMAND_EMOTE;
        script->emote.emoteId[0] = ptr->m_emoteId;
        script->comment = "Emote " + ptr->m_emoteName;
        scriptActions.push_back(script);
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_UnitAttackStart>(sniffedEvent))
    {
        auto script = std::make_shared<ScriptInfo>();
        script->command = SCRIPT_COMMAND_ATTACK_START;
        script->comment = "Attack Start";
        scriptActions.push_back(script);
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_UnitAttackStop>(sniffedEvent))
    {
        auto script = std::make_shared<ScriptInfo>();
        script->command = SCRIPT_COMMAND_ENTER_EVADE_MODE;
        script->comment = "Enter Evade Mode";
        scriptActions.push_back(script);
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_ClientSideMovement>(sniffedEvent))
    {
        if (ptr->m_opcodeName.find("TELEPORT") != std::string::npos)
        {
            auto script = std::make_shared<ScriptInfo>();
            script->command = SCRIPT_COMMAND_TELEPORT_TO;
            script->x = ptr->m_location.x;
            script->y = ptr->m_location.y;
            script->z = ptr->m_location.z;
            script->o = ptr->m_location.o;
            script->comment = "Teleport";
            scriptActions.push_back(script);
        }
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_ServerSideMovement>(sniffedEvent))
    {
        auto script = std::make_shared<ScriptInfo>();
        if (ptr->m_splines.empty() && ptr->m_finalOrientation != 100)
        {
            script->command = SCRIPT_COMMAND_TURN_TO;
            script->turnTo.facingLogic = SO_TURNTO_PROVIDED_ORIENTATION;
            script->o = ptr->m_finalOrientation;
            script->comment = "Set Orientation";
        }
        else if (ptr->m_splines.empty() && ptr->m_finalOrientation == 100)
        {
            script->command = SCRIPT_COMMAND_MOVE_TO;
            script->moveTo.flags = SF_MOVETO_POINT_MOVEGEN;
            script->moveTo.travelTime = ptr->m_moveTime;
            script->x = ptr->m_startPosition.x;
            script->y = ptr->m_startPosition.y;
            script->z = ptr->m_startPosition.z;
            script->comment = "Move";
        }
        else
        {
            script->command = SCRIPT_COMMAND_MOVE_TO;
            script->moveTo.flags = SF_MOVETO_POINT_MOVEGEN;
            script->moveTo.travelTime = ptr->m_moveTime;
            script->x = ptr->m_splines.rbegin()->x;
            script->y = ptr->m_splines.rbegin()->y;
            script->z = ptr->m_splines.rbegin()->z;

            if (ptr->m_splines.size() > 1)
                script->moveTo.movementOptions = MOVE_PATHFINDING;

            if (ptr->m_finalOrientation != 100)
                script->o = ptr->m_finalOrientation;

            script->comment = "Move";
        }
        scriptActions.push_back(script);
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_UnitUpdate_entry>(sniffedEvent))
    {
        auto script = std::make_shared<ScriptInfo>();
        script->command = SCRIPT_COMMAND_UPDATE_ENTRY;
        script->updateEntry.creatureEntry = ptr->m_value;
        script->comment = "Update Entry to " + sGameDataMgr.GetCreatureName(ptr->m_value);
        scriptActions.push_back(script);
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_UnitUpdate_display_id>(sniffedEvent))
    {
        auto script = std::make_shared<ScriptInfo>();
        script->command = SCRIPT_COMMAND_MORPH_TO_ENTRY_OR_MODEL;
        script->morph.creatureOrModelEntry = ptr->m_value;
        script->morph.isDisplayId = 1;
        script->comment = "Morph to " + std::to_string(ptr->m_value);
        scriptActions.push_back(script);
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_UnitUpdate_faction>(sniffedEvent))
    {
        auto script = std::make_shared<ScriptInfo>();
        script->command = SCRIPT_COMMAND_SET_FACTION;
        script->faction.factionId = ptr->m_value;
        script->faction.flags = TEMPFACTION_RESTORE_RESPAWN;
        script->comment = "Set Faction to " + sGameDataMgr.GetFactionTemplateName(ptr->m_value);
        scriptActions.push_back(script);
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_UnitUpdate_emote_state>(sniffedEvent))
    {
        auto script = std::make_shared<ScriptInfo>();
        script->command = SCRIPT_COMMAND_EMOTE;
        script->emote.emoteId[0] = ptr->m_value;
        script->comment = "Set Emote State to " + EmoteToString(ptr->m_value);
        scriptActions.push_back(script);
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_UnitUpdate_stand_state>(sniffedEvent))
    {
        auto script = std::make_shared<ScriptInfo>();
        script->command = SCRIPT_COMMAND_STAND_STATE;
        script->standState.standState = ptr->m_value;
        script->comment = "Set Stand State to " + StandStateToString(ptr->m_value);
        scriptActions.push_back(script);
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_CreatureEquipmentUpdate>(sniffedEvent))
    {
        auto script = std::make_shared<ScriptInfo>();
        script->command = SCRIPT_COMMAND_SET_EQUIPMENT;
        for (int i = 0; i < 3; i++)
            script->setEquipment.slot[i] = -1;
        script->setEquipment.slot[ptr->m_slot] = ptr->m_itemId;
        script->comment = "Set Equipment";
        scriptActions.push_back(script);
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_UnitUpdate_npc_flags>(sniffedEvent))
    {
        uint32 oldFlags = GetCreatureFieldValueBeforeTime(ptr->GetSourceGuid().GetCounter(), unixtimems, "npc_flags");
        uint32 removedFlags = 0;
        uint32 addedFlags = 0;
        for (uint32 i = 0; i < 32; i++)
        {
            uint32 flag = (uint32)pow(2, i);
            if ((oldFlags & flag) && !(ptr->m_value & flag))
            {
                removedFlags |= sGameDataMgr.ConvertNpcFlagsForBuild(flag, CLIENT_BUILD_1_12_1);
            }
            else if (!(oldFlags & flag) && (ptr->m_value & flag))
            {
                addedFlags |= sGameDataMgr.ConvertNpcFlagsForBuild(flag, CLIENT_BUILD_1_12_1);
            }
        }

        if (addedFlags)
        {
            auto script = std::make_shared<ScriptInfo>();
            script->command = SCRIPT_COMMAND_MODIFY_FLAGS;
            script->modFlags.fieldId = UpdateFields::GetUpdateFieldValue("UNIT_NPC_FLAGS", CLIENT_BUILD_1_12_1);
            script->modFlags.fieldValue = addedFlags;
            script->modFlags.mode = SO_MODIFYFLAGS_SET;
            script->comment = "Add Npc Flags";
            scriptActions.push_back(script);
        }

        if (removedFlags)
        {
            auto script = std::make_shared<ScriptInfo>();
            script->command = SCRIPT_COMMAND_MODIFY_FLAGS;
            script->modFlags.fieldId = UpdateFields::GetUpdateFieldValue("UNIT_NPC_FLAGS", CLIENT_BUILD_1_12_1);
            script->modFlags.fieldValue = removedFlags;
            script->modFlags.mode = SO_MODIFYFLAGS_REMOVE;
            script->comment = "Remove Npc Flags";
            scriptActions.push_back(script);
        }
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_UnitUpdate_unit_flags>(sniffedEvent))
    {
        uint32 oldFlags = GetCreatureFieldValueBeforeTime(ptr->GetSourceGuid().GetCounter(), unixtimems, "unit_flags");
        uint32 removedFlags = 0;
        uint32 addedFlags = 0;
        for (uint32 i = 0; i < 32; i++)
        {
            uint32 flag = (uint32)pow(2, i);
            if ((oldFlags & flag) && !(ptr->m_value & flag))
            {
                if (IsScriptRelevantUnitFlag(flag))
                    removedFlags |= flag;
            }
            else if (!(oldFlags & flag) && (ptr->m_value & flag))
            {
                if (IsScriptRelevantUnitFlag(flag))
                    addedFlags |= flag;
            }
        }

        if (addedFlags)
        {
            auto script = std::make_shared<ScriptInfo>();
            script->command = SCRIPT_COMMAND_MODIFY_FLAGS;
            script->modFlags.fieldId = UpdateFields::GetUpdateFieldValue("UNIT_FIELD_FLAGS", CLIENT_BUILD_1_12_1);
            script->modFlags.fieldValue = addedFlags;
            script->modFlags.mode = SO_MODIFYFLAGS_SET;
            script->comment = "Add Unit Flags";
            scriptActions.push_back(script);
        }

        if (removedFlags)
        {
            auto script = std::make_shared<ScriptInfo>();
            script->command = SCRIPT_COMMAND_MODIFY_FLAGS;
            script->modFlags.fieldId = UpdateFields::GetUpdateFieldValue("UNIT_FIELD_FLAGS", CLIENT_BUILD_1_12_1);
            script->modFlags.fieldValue = removedFlags;
            script->modFlags.mode = SO_MODIFYFLAGS_REMOVE;
            script->comment = "Remove Unit Flags";
            scriptActions.push_back(script);
        }
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_GameObjectUpdate_state>(sniffedEvent))
    {
        auto script = std::make_shared<ScriptInfo>();
        script->command = SCRIPT_COMMAND_SET_GO_STATE;
        script->setGoState.state = ptr->m_value;
        script->comment = "Set Go State to " + GameObjectStateToString(ptr->m_value);
        scriptActions.push_back(script);
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_GameObjectCustomAnim>(sniffedEvent))
    {
        auto script = std::make_shared<ScriptInfo>();
        script->command = SCRIPT_COMMAND_PLAY_CUSTOM_ANIM;
        script->playCustomAnim.animId = ptr->m_animId;
        script->comment = "Activate Object";
        scriptActions.push_back(script);
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_PlaySound>(sniffedEvent))
    {
        auto script = std::make_shared<ScriptInfo>();
        script->command = SCRIPT_COMMAND_PLAY_SOUND;
        script->playSound.soundId = ptr->m_soundId;
        script->comment = "Play Sound " + sGameDataMgr.GetSoundName(ptr->m_soundId);
        scriptActions.push_back(script);
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_SpellCastStart>(sniffedEvent))
    {
        auto script = std::make_shared<ScriptInfo>();
        script->command = SCRIPT_COMMAND_CAST_SPELL;
        script->castSpell.spellId = ptr->m_spellId;
        if (ptr->GetTargetGuid().IsEmpty())
            script->raw.data[4] = SF_GENERAL_TARGET_SELF;
        script->comment = "Cast Spell " + sGameDataMgr.GetSpellName(ptr->m_spellId);
        scriptActions.push_back(script);
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_SpellCastGo>(sniffedEvent))
    {
        auto script = std::make_shared<ScriptInfo>();
        script->command = SCRIPT_COMMAND_CAST_SPELL;
        script->castSpell.spellId = ptr->m_spellId;
        if (ptr->GetTargetGuid().IsEmpty() && (ptr->m_hitTargets.empty() || ptr->HasHitTarget(ptr->GetSourceGuid())))
            script->raw.data[4] = SF_GENERAL_TARGET_SELF;
        script->comment = "Cast Spell " + sGameDataMgr.GetSpellName(ptr->m_spellId);
        scriptActions.push_back(script);
    }
    else if (auto ptr = std::dynamic_pointer_cast<SniffedEvent_QuestUpdateComplete>(sniffedEvent))
    {
        auto script = std::make_shared<ScriptInfo>();
        script->command = SCRIPT_COMMAND_QUEST_EXPLORED;
        script->questExplored.questId = ptr->m_questId;
        script->comment = "Complete Quest " + sGameDataMgr.GetQuestName(ptr->m_questId);
        scriptActions.push_back(script);
    }
}