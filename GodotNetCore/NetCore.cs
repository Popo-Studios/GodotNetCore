using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Collections.Generic;
using ENet;
using System.Threading.Tasks;

namespace GodotNetCore {
    public delegate void EventHandler(UInt32 data);
    public delegate void PacketReceiveEventHandler(Packet packet, byte channel);
   
    public interface IPacketHandler {
        void RawHandle(byte[] rawData);
    }

    public abstract class PacketHandler<T> : IPacketHandler where T : notnull {
        public void RawHandle(byte[] rawData) {
            T data = PacketUtils.ParseRawData<T>(rawData);
            Handle(data);
        }

        protected abstract void Handle(T data);
    }

    public struct QueuedPacket {
        public byte channel;
        public Packet packet;
    }

    public static class NetworkManager {
        private static Thread? ClientThread = null;
        private static volatile bool ClientRunning = false;
        public static volatile UInt32 DisconnectCause = 0;
        private static int Timeout { get; set; } = 15;

        private readonly static ConcurrentQueue<QueuedPacket> packetQueue = new ConcurrentQueue<QueuedPacket>();

        public static EventHandler? OnConnectHandler { get; set; }
        public static EventHandler? OnDisconnectHandler { get; set; }
        public static EventHandler? OnTimeoutHandler { get; set; }
        public static PacketReceiveEventHandler? OnPacketReceiveHandler { get; set; }

        private static readonly UInt16 MaxPacketTypeId = UInt16.MaxValue;

        private readonly static List<IPacketHandler>[] packetHandlers = new List<IPacketHandler>[MaxPacketTypeId + 1];

        public static void Activate() {
            Library.Initialize();
            packetQueue.Clear();

            OnConnectHandler = null;
            OnDisconnectHandler = null;
            OnTimeoutHandler = null;
            OnPacketReceiveHandler = null;

            Array.Fill(packetHandlers, null);

            RegisterPacketHandler((UInt16)PacketUtils.PredefinedPacketTypeId.CreateSession, new CreateSessionPacketHandler());
            RegisterPacketHandler((UInt16)PacketUtils.PredefinedPacketTypeId.JoinSession, new JoinSessionPacketHandler());
        }

        public static void Deactivate() {
            Library.Deinitialize();
        }

        public static bool RegisterPacketHandler<T>(UInt16 packetTypeId, PacketHandler<T> handler) where T : notnull {
            if (packetHandlers[packetTypeId] != null && packetHandlers[packetTypeId].Contains(handler)) {
                packetHandlers[packetTypeId].Add(handler);
            }
            return true;
        }

        public static bool RegisterPacketHandler<T>(string packetTypeName, PacketHandler<T> handler) where T : notnull {
            UInt16? type = PacketUtils.GetPacketTypeId(packetTypeName);
            if (type != null) {
                return RegisterPacketHandler((UInt16)type, handler);
            } else {
                return false;
            }
        }

        public static bool RemovePacketHandler<T>(UInt16 packetTypeId, PacketHandler<T> handler) where T : notnull {
            if (packetHandlers[packetTypeId].Contains(handler)) {
                packetHandlers[packetTypeId].Remove(handler);
                return true;
            } else return false;
        }

        public static bool RemovePacketHandler<T>(string packetTypeName, PacketHandler<T> handler) where T : notnull {
            UInt16? type = PacketUtils.GetPacketTypeId(packetTypeName);
            if (type != null) {
                return RemovePacketHandler((UInt16)type, handler);
            } else {
                return false;
            }
        }

        public static void SendPacket(byte channel, Packet packet) {
            QueuedPacket qpacket;
            qpacket.channel = channel;
            qpacket.packet = packet;
            packetQueue.Enqueue(qpacket);
        }

        private static void CreateClientThread(string hostName, UInt16 port, ref TaskCompletionSource<bool> tcs) {
            ClientRunning = true;
            using Host client = new Host();
            Address address = new Address();

            address.SetHost(hostName);
            address.Port = port;
            client.Create();

            Peer peer = client.Connect(address);

            Event netEvent;

            while (ClientRunning) {
                bool polled = false;

                while (!polled) {
                    if (!ClientRunning) {
                        peer.Disconnect(DisconnectCause);
                        OnDisconnectHandler?.Invoke(DisconnectCause);
                        break;
                    }

                    if (client.CheckEvents(out netEvent) <= 0) {
                        if (client.Service(Timeout, out netEvent) <= 0) break;

                        polled = true;
                    }

                    switch (netEvent.Type) {
                        case EventType.None: break;
                        case EventType.Connect:
                            tcs.SetResult(true);
                            OnConnectHandler?.Invoke(netEvent.Data);
                            break;
                        case EventType.Disconnect:
                            OnDisconnectHandler?.Invoke(netEvent.Data);
                            break;
                        case EventType.Timeout:
                            tcs.SetResult(false);
                            OnTimeoutHandler?.Invoke(netEvent.Data);
                            break;
                        case EventType.Receive:
                            OnPacketReceiveHandler?.Invoke(netEvent.Packet, netEvent.ChannelID);

                            ParsedPacket ppacket = PacketUtils.ParsePacket(netEvent.Packet);
                            packetHandlers[ppacket.Header.PacketTypeId].ForEach((IPacketHandler handler) => {
                                handler.RawHandle(ppacket.RawData);
                            });

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

        public static Task<bool> Connect(string hostName, UInt16 port) {
            var tcs = new TaskCompletionSource<bool>();

            new Thread(() => {
                if (ClientThread != null) {
                    ClientRunning = false;
                    ClientThread.Join();
                }

                ClientThread = new Thread(() => CreateClientThread(hostName, port, ref tcs));
                ClientThread.Start();
            }).Start();

            return tcs.Task;
        }

        public static void Disconnect(UInt32 cause = 0) {
            if (ClientThread != null) {
                DisconnectCause = cause;
                ClientRunning = false;
            }
        }
    }
}
