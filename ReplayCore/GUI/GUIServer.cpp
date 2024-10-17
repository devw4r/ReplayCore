#include "GUIServer.h"
#include "GUIOpcodes.h"
#include "GUIFilters.h"
#include "../Defines/ByteBuffer.h"
#include "../Defines/Utility.h"
#include "../Input/Config.h"
#include "../World/SniffedEvents.h"
#include "../World/ReplayMgr.h"
#include "../World/WorldServer.h"
#include "../World/ScriptMaker.h"
#include "../Dependencies//include/zlib/zlib.h"

#pragma comment(lib,"WS2_32")

#define MAX_PACKET_SIZE 65535

GUIServer& GUIServer::Instance()
{
    static GUIServer instance;
    return instance;
}

void GUIServer::StartNetwork()
{
    SetupOpcodeHandlers();

    m_socketPrototype = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    int result = 1;
    if (setsockopt(m_socketPrototype, SOL_SOCKET, SO_REUSEADDR, (const char*)&result, sizeof(result)) < 0)
        perror("[GUI] setsockopt(SO_REUSEADDR) failed");

    m_address.sin_family = AF_INET;
    m_address.sin_port = htons(3800);

    #ifdef _WIN32
    m_address.sin_addr.S_un.S_addr = inet_addr(sConfig.GetListenAddress());
    #else
    m_address.sin_addr.s_addr = inet_addr(sConfig.GetListenAddress());
    #endif

    result = bind(m_socketPrototype, (SOCKADDR*)&m_address, sizeof(m_address));
    if (result == SOCKET_ERROR)
    {
        printf("[GUI] bind error: %i\n", SOCKET_ERROR_CODE);
        return;
    }

    result = listen(m_socketPrototype, 1);
    if (result == SOCKET_ERROR)
    {
        printf("[GUI] listen error: %i\n", SOCKET_ERROR_CODE);
        return;
    }

    m_enabled = true;

    m_networkThread = std::thread(&GUIServer::NetworkLoop, this);
}

void GUIServer::StopNetwork()
{
    m_enabled = false;
    shutdown(m_guiSocket, SD_BOTH);
    closesocket(m_guiSocket);
    shutdown(m_socketPrototype, SD_BOTH);
    closesocket(m_socketPrototype);
}

void GUIServer::NetworkLoop()
{
    do
    {
        ResetClientData();

        printf("[GUI] Waiting for connection...\n");
        int addressSize = sizeof(m_address);
        #ifdef _WIN32
        m_guiSocket = accept(m_socketPrototype, (SOCKADDR*)&m_address, &addressSize);
        if (m_guiSocket == INVALID_SOCKET)
            break;
        #else
        m_guiSocket = accept(m_socketPrototype, (sockaddr*)&m_address, (socklen_t*)&addressSize);
        if (m_guiSocket == -1)
            break;
        #endif

        printf("[GUI] Connection established!\n");
        SendEventTypesList();

        do
        {
            ByteBuffer buffer;
            buffer.resize(MAX_PACKET_SIZE);
            int result = recv(m_guiSocket, (char*)buffer.contents(), MAX_PACKET_SIZE, 0);
            
            if (result == SOCKET_ERROR)
            {
                printf("[GUI] recv error: %i\n", SOCKET_ERROR_CODE);
                break;
            }

            if (result == 0)
            {
                printf("[GUI] Connection closed.\n");
                break;
            }

            HandlePacket(buffer);

        } while (m_enabled);

    } while (m_enabled);

    closesocket(m_guiSocket);
}

void GUIServer::ResetClientData()
{

}

void GUIServer::SetupOpcodeHandlers()
{
    m_opcodeHandlers[CMSG_REQUEST_EVENT_DATA] = &GUIServer::HandleRequestEventData;
    m_opcodeHandlers[CMSG_REQUEST_EVENT_DESCRIPTION] = &GUIServer::HandleRequestEventDescription;
    m_opcodeHandlers[CMSG_CHAT_COMMAND] = &GUIServer::HandleChatCommand;
    m_opcodeHandlers[CMSG_GOTO_GUID] = &GUIServer::HandleGoToGuid;
    m_opcodeHandlers[CMSG_MAKE_SCRIPT] = &GUIServer::HandleMakeScript;
}

void GUIServer::HandlePacket(ByteBuffer& buffer)
{
    uint8 opcode;
    buffer >> opcode;

    auto itr = m_opcodeHandlers.find(opcode);
    if (itr == m_opcodeHandlers.end())
    {
        printf("[GUI] Received unhandled opcode %hhu\n", opcode);
        return;
    }

    (this->*(itr->second))(buffer);
}

void GUIServer::SendEventTypesList()
{
    ByteBuffer buffer;
    uint32 sizePos = buffer.wpos();
    buffer << uint16(0);

    size_t packetSize = 0;
    buffer << uint8(SMSG_EVENT_TYPE_LIST);
    packetSize += sizeof(uint8);

    buffer << uint32(sReplayMgr.GetFirstEventTime());
    packetSize += sizeof(uint32);

    buffer << uint32(sReplayMgr.GetLastEventTime());
    packetSize += sizeof(uint32);

    buffer << uint32(sReplayMgr.GetSniffedEventsCount());
    packetSize += sizeof(uint32);

    buffer << uint32(MAX_EVENT_TYPE);
    packetSize += sizeof(uint32);

    for (uint32 i = 0; i < MAX_EVENT_TYPE; i++)
    {
        buffer << uint32(i);
        packetSize += sizeof(uint32);
        buffer << uint32(GetSniffedEventIcon(i));
        packetSize += sizeof(uint32);

        char const* eventName = GetSniffedEventName(SniffedEventType(i));
        buffer << eventName;
        packetSize += strlen(eventName) + sizeof(char);
    }

    assert(packetSize <= MAX_PACKET_SIZE);

    buffer.put<uint16>(sizePos, packetSize);

    SendPacket(buffer, "SMSG_EVENT_TYPE_LIST");
}

void GUIServer::SendPacket(ByteBuffer& buffer, std::string opcode)
{
    size_t pSize = buffer.wpos();  // use real used data size
    ByteBuffer outBuffer;
    uint32 destsize = compressBound(pSize);
    outBuffer.resize(destsize + sizeof(uint32));
    outBuffer.put<uint32>(0, pSize);

    PacketCompressor::Compress(const_cast<uint8*>(outBuffer.contents()) + sizeof(uint32), &destsize, (void*)buffer.contents(), pSize);
    if (destsize == 0)
        return;

    outBuffer.resize(destsize + sizeof(uint32));
    outBuffer.put<uint32>(0, destsize);

    printf("[GUI] %s Sending %u bytes of data.\n", opcode.c_str(), outBuffer.size());
    send(m_guiSocket, (const char*)outBuffer.contents(), outBuffer.size(), 0);
}

void GUIServer::HandleRequestEventDescription(ByteBuffer& buffer)
{
    uint32 sniffedEventId;
    buffer >> sniffedEventId;

    std::string description;
    sReplayMgr.GetEventDescription(sniffedEventId, description);
    SendEventDescription(sniffedEventId, description);
}

void GUIServer::HandleRequestEventData(ByteBuffer& buffer)
{
    uint32 startTime;
    buffer >> startTime;
    uint32 endTime;
    buffer >> endTime;
    uint32 objectsCount;
    buffer >> objectsCount;

    std::vector<ObjectFilterEntry> objectFilters;
    for (uint32 i = 0; i < objectsCount; i++)
    {
        ObjectFilterEntry objectFilter;
        buffer >> objectFilter.guid;
        buffer >> objectFilter.id;
        buffer >> objectFilter.type;

        uint32 eventsCount;
        buffer >> eventsCount;

        for (uint32 j = 0; j < eventsCount; j++)
        {
            uint32 eventType;
            buffer >> eventType;
            objectFilter.eventTypes.insert(eventType);
        }
        objectFilters.push_back(objectFilter);
    }

    std::vector<std::pair<uint64, std::shared_ptr<SniffedEvent>>> eventsList;
    sReplayMgr.GetEventsListForTargets(startTime, endTime, objectFilters, eventsList);
    SendEventDataList(eventsList);
}

void GUIServer::SendEventDescription(uint32 sniffedEventId, std::string const& description)
{
    ByteBuffer buffer;
    buffer << uint16(sizeof(uint8) + sizeof(uint32) + description.size() + sizeof(uint8));
    buffer << uint8(SMSG_EVENT_DESCRIPTION);
    buffer << sniffedEventId;
    buffer << description;

    SendPacket(buffer, "SMSG_EVENT_DESCRIPTION");
}

void GUIServer::SendEventDataList(std::vector<std::pair<uint64, std::shared_ptr<SniffedEvent>>> const& eventsList)
{
    uint32 eventsWrittenTotal = 0;
    std::string opcodeStr = "SMSG_EVENT_DATA_LIST";

    while (eventsWrittenTotal < eventsList.size())
    {       
        ByteBuffer buffer;
        uint32 sizePos = buffer.wpos();
        buffer << uint16(0);

        size_t packetSize = 0;
        buffer << uint8(SMSG_EVENT_DATA_LIST);
        packetSize += sizeof(uint8);

        uint32 countPos = buffer.wpos();
        buffer << uint32(0);
        packetSize += sizeof(uint32);

        uint32 eventsWritten = 0;
        for (uint32 i = eventsWrittenTotal; i < eventsList.size(); i++)
        {
            std::pair<uint64, std::shared_ptr<SniffedEvent>> const& itr = eventsList[i];
            std::string shortDescription = itr.second->GetShortDescription();
            std::string longDescription = itr.second->GetLongDescription();

            uint32 neededSpace =
                sizeof(uint32) + // unique identifier
                sizeof(uint32) + // event type
                sizeof(uint64) + // unit time ms
                sizeof(uint64) + // source guid
                sizeof(uint64) + // target guid
                shortDescription.size() + sizeof(uint8);

            if (sizeof(uint16) /*headerSize*/ + packetSize + neededSpace >= MAX_PACKET_SIZE)
                break;

            buffer << uint32(itr.second->m_uniqueIdentifier);
            buffer << uint32(itr.second->GetType());
            buffer << uint64(itr.first);
            buffer << uint64(itr.second->GetSourceGuid().GetRawValue());
            buffer << uint64(itr.second->GetTargetGuid().GetRawValue());
            buffer << shortDescription;
            packetSize += neededSpace;
            eventsWritten++;
            eventsWrittenTotal++;
        }

        buffer.put<uint16>(sizePos, packetSize);
        buffer.put<uint32>(countPos, eventsWritten);

        SendPacket(buffer, opcodeStr);
    }

    std::this_thread::sleep_for(std::chrono::milliseconds(200));

    ByteBuffer endbuffer;
    endbuffer << uint16(sizeof(uint8));
    endbuffer << uint8(SMSG_EVENT_DATA_END);

    SendPacket(endbuffer, "SMSG_EVENT_DATA_END");
    printf("[GUI] Finished sending %u events.\n", eventsWrittenTotal);
}

void GUIServer::HandleChatCommand(ByteBuffer& buffer)
{
    std::string command;
    buffer >> command;
    sWorld.SetPendingChatCommand(command);
}

void GUIServer::HandleGoToGuid(ByteBuffer& buffer)
{
    ObjectGuid guid;
    buffer >> guid;

    if (WorldObject const* pObject = sWorld.FindObject(guid))
        sWorld.TeleportClient(pObject->GetLocation());
    else if (WorldObjectData const* pData = sReplayMgr.GetObjectSpawnData(guid))
        sWorld.TeleportClient(pData->GetLocation());
    else
        printf("[GUI] Cannot teleport to unknown guid %s.\n", guid.GetString().c_str());
}

void GUIServer::HandleMakeScript(ByteBuffer& buffer)
{
    uint32 mainScriptId;
    buffer >> mainScriptId;
    uint32 genericScriptId;
    buffer >> genericScriptId;
    std::string tableName;
    buffer >> tableName;
    std::string commentPrefix;
    buffer >> commentPrefix;
    ObjectGuid sourceGuid;
    buffer >> sourceGuid;
    ObjectGuid targetGuid;
    buffer >> targetGuid;
    uint8 saveGoSpawnsToDb;
    buffer >> saveGoSpawnsToDb;
    uint8 makeWaypoints;
    buffer >> makeWaypoints;
    uint32 eventsCount;
    buffer >> eventsCount;

    std::set<uint32> eventIdentifiers;
    for (uint32 i = 0; i < eventsCount; i++)
    {
        uint32 uniqueIdentifier;
        buffer >> uniqueIdentifier;
        eventIdentifiers.insert(uniqueIdentifier);
    }

    std::vector<std::pair<uint64, std::shared_ptr<SniffedEvent>>> eventsList;
    sReplayMgr.GetEventsListFromIdentifiers(eventIdentifiers, eventsList);

    if (eventsList.empty())
    {
        printf("[GUI] Cannot make script from empty event list.\n");
        return;
    }

    ScriptMaker scriptMaker;
    scriptMaker.MakeScript(mainScriptId, genericScriptId, tableName, commentPrefix, sourceGuid, targetGuid, saveGoSpawnsToDb != 0, eventsList, makeWaypoints != 0);
}
