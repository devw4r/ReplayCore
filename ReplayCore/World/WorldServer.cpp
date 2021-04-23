#include "WorldServer.h"
#include "../Defines//ByteBuffer.h"
#include "../Defines//WorldPacket.h"
#include "../Defines/Utility.h"
#include "../Defines/GameAccount.h"
#include "../Defines/ClientVersions.h"
#include "../Crypto/Hmac.h"
#include "../Crypto/base32.h"
#include "../Crypto/Sha1.h"
#include "../Auth/AuthServer.h"
#include "Opcodes.h"
#include "UpdateFields.h"
#include "ReplayMgr.h"
#include "../Input//Config.h"

#include <array>
#include <chrono>

//#define WORLD_DEBUG

WorldServer& WorldServer::Instance()
{
    static WorldServer instance;
    return instance;
}

void WorldServer::StartWorld()
{
    if (m_worldThreadStarted)
        return;

    m_worldThreadStarted = true;
    m_worldThread = std::thread(&WorldServer::WorldLoop, this);
}

#define WORLD_UPDATE_TIME 100

void WorldServer::WorldLoop()
{
    do
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(WORLD_UPDATE_TIME));

        // Don't update world before client connects.
        if (!m_sessionData.connected || !m_sessionData.isInWorld || m_sessionData.isTeleportPending || !m_clientPlayer)
            continue;

        uint64 ms = std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::system_clock::now().time_since_epoch()).count();
        uint32 diff = uint32(m_lastUpdateTimeMs ? ms - m_lastUpdateTimeMs : 0);
        m_msTimeSinceServerStart += diff;
        m_lastUpdateTimeMs = ms;

        BuildAndSendObjectUpdates();

    } while (m_enabled);
}

void WorldServer::BuildAndSendObjectUpdates()
{
    // limit amount of objects updated so we don't exceed maximum packet size
    uint8 updatesCount = 0;
    uint8 outOfRangeCount = 0;
    UpdateData updateData;

    for (auto& itr : m_players)
    {
        bool visible = m_clientPlayer->IsWithinVisibilityDistance(&itr.second);

        if (visible)
        {
            if (updatesCount < 5)
            {
                if (m_sessionData.visibleObjects.find(itr.first) == m_sessionData.visibleObjects.end())
                {
                    itr.second.BuildCreateUpdateBlockForPlayer(&updateData, m_clientPlayer.get());
                    m_sessionData.visibleObjects.insert(itr.first);
                    updatesCount++;
                }
                else if (itr.second.IsMarkedForClientUpdate())
                {
                    if (itr.second.BuildValuesUpdateBlockForPlayer(&updateData, m_clientPlayer.get())) 
                        updatesCount++;
                    itr.second.ClearUpdateMask();
                }
            }
        }
        else
        {
            if (outOfRangeCount < 30)
            {
                if (m_sessionData.visibleObjects.find(itr.first) != m_sessionData.visibleObjects.end())
                {
                    itr.second.BuildOutOfRangeUpdateBlock(&updateData);
                    m_sessionData.visibleObjects.erase(itr.first);
                    outOfRangeCount++;
                }
            }
        }
    }

    if (updatesCount || outOfRangeCount)
        updateData.Send();
}

void WorldServer::SpawnWorldObjects()
{
    m_players.clear();
    m_creatures.clear();
    m_clientPlayer.reset();
    sReplayMgr.SpawnPlayers();
    m_worldSpawned = true;
}

void WorldServer::StartNetwork()
{
    m_socketPrototype = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    int result = 1;
    if (setsockopt(m_socketPrototype, SOL_SOCKET, SO_REUSEADDR, (const char*)&result, sizeof(result)) < 0)
        perror("[WORLD] setsockopt(SO_REUSEADDR) failed");

    m_address.sin_family = AF_INET;
    m_address.sin_port = htons(sConfig.GetWorldServerPort());
    m_address.sin_addr.S_un.S_addr = inet_addr(sConfig.GetListenAddress());

    result = bind(m_socketPrototype, (SOCKADDR*)&m_address, sizeof(m_address));
    if (result == SOCKET_ERROR)
    {
        printf("[WORLD] bind error: %i\n", WSAGetLastError());
        return;
    }

    result = listen(m_socketPrototype, 1);
    if (result == SOCKET_ERROR)
    {
        printf("[WORLD] listen error: %i\n", WSAGetLastError());
        return;
    }

    m_enabled = true;

    m_networkThread = std::thread(&WorldServer::NetworkLoop, this);
    m_packetProcessingThread = std::thread(&WorldServer::ProcessIncomingPackets, this);
}

void WorldServer::StopNetwork()
{
    m_enabled = false;
    shutdown(m_worldSocket, SD_BOTH);
    closesocket(m_worldSocket);
    shutdown(m_socketPrototype, SD_BOTH);
    closesocket(m_socketPrototype);
}

void WorldServer::ResetClientData()
{
    m_lastSessionBuild = m_sessionData.build;
    m_sessionData = WorldSessionData();
    m_sessionData.connected = true;
    m_sessionData.build = sAuth.GetClientBuild();
    m_sessionData.sessionKey = sAuth.GetSessionKey();
}

void WorldServer::NetworkLoop()
{
    do
    {
        printf("[WORLD] Waiting for connection...\n");
        int addressSize = sizeof(m_address);
        m_worldSocket = accept(m_socketPrototype, (SOCKADDR*)&m_address, &addressSize);
        if (m_worldSocket == INVALID_SOCKET)
            break;

        printf("[WORLD] Connection established!\n");

        ResetClientData();
        if (m_lastSessionBuild && m_lastSessionBuild != m_sessionData.build)
            SpawnWorldObjects();

        if (!Opcodes::GetOpcodesNamesMapForBuild(m_sessionData.build))
        {
            printf("[WORLD] Unsupported client version!\n");
            shutdown(m_worldSocket, SD_BOTH);
            closesocket(m_worldSocket);
            std::this_thread::sleep_for(std::chrono::milliseconds(1000));
            continue;
        }

        SetupOpcodeHandlers();
        SendAuthChallenge();

        do
        {
            uint8 headerBuffer[sizeof(ClientPktHeader)];
            int result = recv(m_worldSocket, (char*)headerBuffer, sizeof(ClientPktHeader), MSG_PEEK);

            if (result == SOCKET_ERROR)
            {
                printf("[WORLD] recv error: %i\n", WSAGetLastError());;
                m_sessionData.connected = false;
                break;
            }

            if (result == 0)
            {
                printf("[WORLD] Connection closed.\n");
                m_sessionData.connected = false;
                break;
            }

            if (result < sizeof(ClientPktHeader))
            {
                printf("[WORLD] Received packet with invalid size.\n");
                continue;
            }

            ClientPktHeader& header = *((ClientPktHeader*)headerBuffer);
            m_sessionData.encryption.DecryptRecv((uint8*)&header, sizeof(ClientPktHeader));

            EndianConvertReverse(header.size);
            EndianConvert(header.cmd);

            uint8* buffer = new uint8[header.size + sizeof(uint16)];
            result = recv(m_worldSocket, (char*)buffer, header.size + sizeof(uint16), 0);

            if (result == SOCKET_ERROR)
            {
                printf("[WORLD] recv error: %i\n", WSAGetLastError());
                m_sessionData.connected = false;
                delete[] buffer;
                break;
            }

            if (result == 0)
            {
                printf("[WORLD] Connection closed.\n");
                m_sessionData.connected = false;
                delete[] buffer;
                break;
            }

            if (result < sizeof(ClientPktHeader))
            {
                printf("[WORLD] Received packet with invalid size.\n");
                delete[] buffer;
                continue;
            }

            memcpy(buffer, headerBuffer, sizeof(ClientPktHeader));

            m_incomingPacketQueue.push(buffer);

        } while (m_enabled);

    } while (m_enabled);

    closesocket(m_worldSocket);
}

void WorldServer::ProcessIncomingPackets()
{
    do
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(10));
        if (m_incomingPacketQueue.empty())
            continue;

        uint8* buffer = m_incomingPacketQueue.front();
        m_incomingPacketQueue.pop();
        HandlePacket(buffer);
        delete[] buffer;
    } while (m_enabled);
}

void WorldServer::HandlePacket(uint8* buffer)
{
    ClientPktHeader& header = *((ClientPktHeader*)buffer);

    uint16 opcode = header.cmd;
    auto itr = m_opcodeHandlers.find(opcode);
    if (itr == m_opcodeHandlers.end())
    {
        printf("[WORLD] Received unhandled opcode %hu (%s), size %hu\n", opcode, GetOpcode(opcode).c_str(), header.size);
        return;
    }
#ifdef WORLD_DEBUG
    else
        printf("[WORLD] Received opcode %hu (%s), size %hu\n", opcode, GetOpcode(opcode).c_str(), header.size);
#endif

    header.size -= 4;

    WorldPacket packet(opcode, header.size);
    if (header.size > 0)
        packet.append(buffer + sizeof(ClientPktHeader), header.size);

    (this->*(itr->second))(packet);
}

void WorldServer::SetOpcodeHandler(const char* opcodeName, WorldOpcodeHandler handler)
{
    if (uint16 opcode = GetOpcode(opcodeName))
        m_opcodeHandlers[opcode] = handler;
}

bool WorldServer::IsExistingOpcode(uint16 opcode)
{
    return Opcodes::IsExistingOpcode(opcode, m_sessionData.build);
}

uint16 WorldServer::GetOpcode(std::string name)
{
    return Opcodes::GetOpcodeValue(name, m_sessionData.build);
}

std::string WorldServer::GetOpcode(uint16 opcode)
{
    return Opcodes::GetOpcodeName(opcode, m_sessionData.build);
}

uint16 WorldServer::GetUpdateField(std::string name)
{
    return UpdateFields::GetUpdateFieldValue(name, m_sessionData.build);
}

std::string WorldServer::GetUpdateField(uint16 opcode)
{
    return UpdateFields::GetUpdateFieldName(opcode, m_sessionData.build);
}

uint16 WorldServer::GetUpdateFieldFlags(uint8 objectTypeId, uint16 id)
{
    return UpdateFields::GetUpdateFieldFlags(objectTypeId, id, m_sessionData.build);
}

void  WorldServer::SendPacket(WorldPacket& packet)
{
#ifdef WORLD_DEBUG
    printf("[WORLD] Sending opcode %u (%s)\n", packet.GetOpcode(), GetOpcode(packet.GetOpcode()).c_str());
#endif

    if (m_sessionData.build >= CLIENT_BUILD_3_3_5a)
    {
        ServerPktHeaderWotlk header(packet.size() + 2, packet.GetOpcode());
        m_sessionData.encryption.EncryptSend((uint8*)header.header, header.getHeaderLength());

        std::vector<uint8> buffer;
        uint32 packetSize = packet.size() + header.getHeaderLength();
        buffer.resize(packetSize);
        memcpy(buffer.data(), header.header, header.getHeaderLength());
        if (!packet.empty())
            memcpy(buffer.data() + header.getHeaderLength(), packet.contents(), packet.size());
        send(m_worldSocket, (char*)buffer.data(), buffer.size(), 0);
    }
    else
    {
        ServerPktHeader header;
        header.cmd = packet.GetOpcode();
        header.size = (uint16)packet.size() + 2;

        EndianConvertReverse(header.size);
        EndianConvert(header.cmd);

        m_sessionData.encryption.EncryptSend((uint8*)& header, sizeof(header));

        std::vector<uint8> buffer;
        uint32 packetSize = packet.size() + sizeof(header);
        buffer.resize(packetSize);
        memcpy(buffer.data(), (uint8*)&header, sizeof(header));
        if (!packet.empty())
            memcpy(buffer.data() + sizeof(header), packet.contents(), packet.size());
        send(m_worldSocket, (char*)buffer.data(), buffer.size(), 0);
    }
}

void WorldServer::SendAuthChallenge()
{
    if (m_sessionData.build >= CLIENT_BUILD_3_3_5a)
    {
        WorldPacket packet(GetOpcode("SMSG_AUTH_CHALLENGE"), 40);
        packet << uint32(1);
        packet << m_sessionData.seed;
        BigNumber seed1;
        seed1.SetRand(16 * 8);
        packet.append(seed1.AsByteArray(16).data(), 16);               // new encryption seeds
        BigNumber seed2;
        seed2.SetRand(16 * 8);
        packet.append(seed2.AsByteArray(16).data(), 16);               // new encryption seeds
        SendPacket(packet);
    }
    else
    {
        WorldPacket packet(GetOpcode("SMSG_AUTH_CHALLENGE"), 4);
        packet << m_sessionData.seed;
        SendPacket(packet);
    }
}

void WorldServer::HandleAuthSession(WorldPacket& packet)
{
    // Read the content of the packet
    uint32 build;
    packet >> build;
    uint32 serverId;
    packet >> serverId;
    std::string account;
    packet >> account;

    if (build >= CLIENT_BUILD_3_0_2)
    {
        uint32 loginServerType;
        packet >> loginServerType;
    }

    uint32 clientSeed;
    packet >> clientSeed;

    if (build >= CLIENT_BUILD_3_3_5a)
    {
        uint32 regionId;
        packet >> regionId;
        uint32 battlegroupId;
        packet >> battlegroupId;
        uint32 realm;
        packet >> realm;
    }

    if (build >= CLIENT_BUILD_3_2_0)
    {
        uint64 dosResponse;
        packet >> dosResponse;
    }

    uint8 digest[20];
    packet.read(digest, 20);

#ifdef WORLD_DEBUG
    printf("\n");
    printf("[WORLD] CMSG_AUTH_SESSION data:\n");
    printf("Client Build: %u\n", build);
    printf("Server Id: %u\n", serverId);
    printf("Account: %s\n", account.c_str());
    printf("Client Seed: %u\n", clientSeed);
    printf("Digest: ");
    for (int i = 0; i < sizeof(digest); i++)
        printf("%hhx ", digest[i]);
    printf("\n\n");
#endif

    BigNumber v, s, g, N, K;

    N.SetHexStr("894B645E89E1535BBDAD5B8B290650530801B18EBFBF5E8FAB3C82872A3E9BB7");
    g.SetDword(7);

    v.SetHexStr(accountPasswordV.c_str());
    s.SetHexStr(accountPasswordS.c_str());

    char const* sStr = s.AsHexStr();                        //Must be freed by OPENSSL_free()
    char const* vStr = v.AsHexStr();                        //Must be freed by OPENSSL_free()

    OPENSSL_free((void*)sStr);
    OPENSSL_free((void*)vStr);

    K.SetHexStr(m_sessionData.sessionKey.c_str());
    if (K.AsByteArray().empty())
    {
        printf("[WORLD] Error in HandleAuthSession - K.AsByteArray().empty()\n");
        return;
    }

    // Check that Key and account name are the same on client and server
    Sha1Hash sha;

    uint32 t = 0;
    uint32 seed = m_sessionData.seed;

    sha.UpdateData(account);
    sha.UpdateData((uint8 *)& t, 4);
    sha.UpdateData((uint8 *)& clientSeed, 4);
    sha.UpdateData((uint8 *)& seed, 4);
    sha.UpdateBigNumbers(&K, nullptr);
    sha.Finalize();

    if (memcmp(sha.GetDigest(), digest, 20))
    {
        WorldPacket response(GetOpcode("SMSG_AUTH_RESPONSE"), 1);
        response << uint8(13);
        SendPacket(response);
        printf("[WORLD] Error in HandleAuthSession - memcmp(sha.GetDigest(), digest, 20)\n");
        return;
    }

    printf("[WORLD] Authentication successful.\n");
    if (m_sessionData.build >= CLIENT_BUILD_3_3_5a)
        m_sessionData.encryption.InitWOTLK(&K);
    else if (m_sessionData.build >= CLIENT_BUILD_2_0_1)
        m_sessionData.encryption.InitTBC(&K);
    else
        m_sessionData.encryption.InitVanilla(&K);

    WorldPacket response(GetOpcode("SMSG_AUTH_RESPONSE"), 1 + 4 + 1 + 4 + (m_sessionData.build >= CLIENT_BUILD_2_0_1 ? 1 : 0));
    response << uint8(12);
    response << uint32(0);                                    // BillingTimeRemaining
    response << uint8(0);                                     // BillingPlanFlags
    response << uint32(0);                                    // BillingTimeRested
    if (m_sessionData.build >= CLIENT_BUILD_2_0_1)
        response << uint8(1);                                 // Expansion
    SendPacket(response);
}
