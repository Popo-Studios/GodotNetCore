using System;
using System.Collections.Concurrent;
using System.Threading;
using ENet;

namespace GodotNetCore {
    public delegate void EventHandler(UInt32 data);
    public delegate void PacketReceiveEventHandler(Packet packet, byte channel);

    public struct QueuedPacket {
        public byte channel;
        public Packet packet;
    }

    public static class NetworkManager {
        private static Thread? clientThread = null;
        private static volatile bool clientRunning = false;
        private static volatile UInt32 disconnectCause = 0;
        private static int timeout { get; set; } = 15;

        private static ConcurrentQueue<QueuedPacket> packetQueue = new ConcurrentQueue<QueuedPacket>();

        private static EventHandler? OnConnectHandler { get; set; }
        private static EventHandler? OnDisconnectHandler { get; set; }
        private static EventHandler? OnTimeoutHandler { get; set; }
        private static PacketReceiveEventHandler? OnPacketReceiveHandler { get; set; }

        public static void Activate() {
            Library.Initialize();
        }

        public static void Deactivate() {
            Library.Deinitialize();
        }

        public static void sendPacket(byte channel, Packet packet) {
            QueuedPacket qpacket;
            qpacket.channel = channel;
            qpacket.packet = packet;
            packetQueue.Enqueue(qpacket);
        }

        private static void CreateClientThread(string hostName, UInt16 port) {
            clientRunning = true;
            using (Host client = new Host()) {
                Address address = new Address();

                address.SetHost(hostName);
                address.Port = port;
                client.Create();

                Peer peer = client.Connect(address);

                Event netEvent;

                while (clientRunning) {
                    bool polled = false;

                    while (!polled) {
                        if (!clientRunning) {
                            peer.Disconnect(disconnectCause);
                            if (OnDisconnectHandler != null) OnDisconnectHandler(disconnectCause);
                            break;
                        }

                        if (client.CheckEvents(out netEvent) <= 0) {
                            if (client.Service(timeout, out netEvent) <= 0) break;

                            polled = true;
                        }

                        switch (netEvent.Type) {
                            case EventType.None: break;
                            case EventType.Connect: 
                                if (OnConnectHandler != null) OnConnectHandler(netEvent.Data);  
                                break;
                            case EventType.Disconnect:
                                if (OnDisconnectHandler != null) OnDisconnectHandler(netEvent.Data);
                                break;
                            case EventType.Timeout:
                                if (OnTimeoutHandler != null) OnTimeoutHandler(netEvent.Data);
                                break;
                            case EventType.Receive:
                                if (OnPacketReceiveHandler != null) OnPacketReceiveHandler(netEvent.Packet, netEvent.ChannelID);
                                netEvent.Packet.Dispose();
                                break;
                        }

                        while (!packetQueue.IsEmpty) {
                            if (packetQueue.TryDequeue(out QueuedPacket packet)) {
                                peer.Send(packet.channel, ref packet.packet);
                            } else break;                            
                        }
                    }  
                }
                client.Flush();
            }
        }

        public static void Connect(string hostName, UInt16 port) {
            new Thread(() => {
                if (clientThread != null) {
                    clientRunning = false;
                    clientThread.Join();
                }

                clientThread = new Thread(() => CreateClientThread(hostName, port));
                clientThread.Start();
            }).Start();
        }

        public static void Disconnect(UInt32 cause = 0) {
            if (clientThread != null) {
                disconnectCause = cause;
                clientRunning = false;
            }
        }
    }
}
