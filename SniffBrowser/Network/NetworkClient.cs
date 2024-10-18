using System;
using System.Net;
using System.Linq;
using System.Net.Sockets;
using SniffBrowser.Core;

namespace SniffBrowser.Network
{
    public class NetworkClient : IDisposable
    {
        public EventHandler<EventArgs> OnConnectSuccess;
        public EventHandler<EventArgs> OnConnectFail;
        public EventHandler<EventArgs> OnReadError;
        public EventHandler<EventArgs> OnWriteError;
        public EventHandler<EventArgs> OnPacketReceived;

        private byte[] PacketBuffer = new byte[0];
        private readonly IPEndPoint IPEndPoint;
        private Socket ClientSocket;

        public NetworkClient(string ip, int port)
        {
            IPEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveBufferSize = 65535
            };
        }

        public void Connect()
        {
            ClientSocket?.BeginConnect(IPEndPoint, ConnectCallback, new byte[sizeof(uint)]);
        }

        public void RequestEventDescription(uint sniffedEventId)
        {
            ByteBuffer packet = new ByteBuffer();
            packet.WriteUInt8((byte)GUIOpcode.CMSG_REQUEST_EVENT_DESCRIPTION);
            packet.WriteUInt32(sniffedEventId);
            SendPacket(packet);
        }

        public void RequestAllEvents()
        {
            ByteBuffer packet = new ByteBuffer();
            packet.WriteUInt8((byte)GUIOpcode.CMSG_REQUEST_EVENT_DATA);
            packet.WriteUInt32(DataHolder.GetStartTime());
            packet.WriteUInt32(DataHolder.GetEndTime());
            packet.WriteUInt32(1); // 1 Object filter -> All.
            packet.WriteUInt32(0); // Object guid, any.
            packet.WriteUInt32(0); // Object entry, any.
            packet.WriteUInt32(0); // Object type, any.

            var eventTypes = Enum.GetValues(typeof(SniffedEventType));
            packet.WriteUInt32((uint)eventTypes.Length);
            for(uint eventType = 0; eventType < eventTypes.Length; eventType++)
                packet.WriteUInt32(eventType);

            SendPacket(packet);
        }

        public void SendPacket(ByteBuffer packet)
        {
            try
            {
                ClientSocket?.BeginSend(packet.GetData(), 0, (int)packet.GetSize(), SocketFlags.None, SendCallback, packet);
            }
            catch (Exception ex)
            {
                OnWriteError?.Invoke(ex.Message, EventArgs.Empty);
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                var sent = ClientSocket?.EndSend(ar);
                if (ar.AsyncState is ByteBuffer b)
                {
                    b?.Dispose();
                }
            }
            catch (Exception ex)
            {
                OnWriteError?.Invoke(ex.Message, EventArgs.Empty);
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                int received = ClientSocket.EndReceive(ar);
                if (received == 0)
                    return;

                byte[] headerBuffer = ar.GetBuffer();
                uint packetSize = 0;

                switch(received)
                {
                    // Header.
                    case sizeof(uint):
                        packetSize = BitConverter.ToUInt16(headerBuffer, 0);

                        if (packetSize == 0)
                        {
                            OnReadError?.Invoke("Received an empty packet!", EventArgs.Empty);
                            return;
                        }

                        if (packetSize > ClientSocket.ReceiveBufferSize)
                        {
                            OnReadError?.Invoke("Packet size is greater than max buffer size!", EventArgs.Empty);
                            return;
                        }

                        Array.Resize(ref PacketBuffer, (int)packetSize);
                        received = 0;
                        while (received != packetSize)
                        {
                            int receivedNow = ClientSocket.Receive(PacketBuffer, received, (int)packetSize - received, SocketFlags.None);
                            if (receivedNow == 0)
                                break;

                            received += receivedNow;
                        }

                        OnPacketReceived?.Invoke(PacketBuffer.Decompress(), EventArgs.Empty);
                        break;
                }

                // Next packet.
                ClientSocket.BeginReceive(headerBuffer, 0, headerBuffer.Length, SocketFlags.None, out SocketError err, ReceiveCallback, headerBuffer);
            }
            // Avoid Pokemon exception handling in cases like these.
            catch (Exception ex)
            {
                OnReadError?.Invoke(ex.Message, EventArgs.Empty);
            }
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                ClientSocket.EndConnect(ar);
                OnConnectSuccess?.Invoke(this, EventArgs.Empty);
                var headerBuffer = ar.GetBuffer();
                ClientSocket?.BeginReceive(headerBuffer, 0, headerBuffer.Length, SocketFlags.None, ReceiveCallback, headerBuffer);
            }
            catch (Exception ex)
            {
                OnConnectFail?.Invoke(ex.Message, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            if (ClientSocket.Connected)
            {
                try { ClientSocket?.Shutdown(SocketShutdown.Both); } catch { }
                try { ClientSocket?.Close(0); } catch { }              
            }
            try { ClientSocket?.Dispose(); } catch { }
            ClientSocket = null;
        }
    }
}
