using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Collections.Generic;
using ENet;
using System.Threading.Tasks;
using MessagePack;

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

    public abstract class EmptyPacketHandler : IPacketHandler {
        public void RawHandle(byte[] rawData) {
            Handle();
        }
        protected abstract void Handle();
    }

    public class LoginResultHandler : PacketHandler<LoginResult> {
        protected override void Handle(LoginResult result) {
            NetworkManager.OnLoginResultHandler?.Invoke(result);
        }
    }

    public class ServerTypeHandler : PacketHandler<string> {
        protected override void Handle(string serverType) {
            NetworkManager.OnServerTypeHandler?.Invoke(serverType);
        }
    }

    public struct QueuedPacket {
        public byte channel;
        public Packet packet;
    }

    [MessagePackObject]
    public struct LoginData {
        [Key(0)]
        public string Id { get; set; }
        [Key(1)]
        public string Password { get; set; }
    }

    [MessagePackObject]
    public struct UserIdentifier {
        [Key(0)]
        public UInt64 UserId { get; set; }
        [Key(1)]
        public string UserToken { get; set; }
    }

    [MessagePackObject]
    public struct LoginResult {
        [Key(0)]
        public bool Success { get; set; }
        [Key(1)]
        public UserIdentifier? UserIdentifier { get; set; }
        [Key(2)]
        public byte? ErrorCode { get; set; }
    }

    public delegate void LoginResultEventHandler(LoginResult result);
    public delegate void ServerTypeEventHandler(string serverType);

    public static class NetworkManager {
        private static List<Thread> ClientThreads = new List<Thread>();
        private static volatile List<bool> ClientRunning = new List<bool>();
        private static volatile List<Peer?> peers = new List<Peer?>();
        private static volatile List<Host?> clients = new List<Host?>();
        private static volatile int latestIndex = -1;

        public static volatile UInt32 DisconnectCause = 0;
        private static int Timeout { get; set; } = 15;

        private static List<BlockingCollection<QueuedPacket>> packetQueues = new List<BlockingCollection<QueuedPacket>>();

        public static event EventHandler? OnConnectHandler;
        public static event EventHandler? OnDisconnectHandler;
        public static event EventHandler? OnTimeoutHandler;
        public static event PacketReceiveEventHandler? OnPacketReceiveHandler;
        internal static LoginResultEventHandler? OnLoginResultHandler;
        internal static ServerTypeEventHandler? OnServerTypeHandler;

        public static byte LoginChannel { get; set; } = 0;
        public static PacketFlags LoginFlags { get; set; } = PacketFlags.Reliable;
        public static byte AuthenticateChannel { get; set; } = 0;
        public static PacketFlags AuthenticateFlags { get; set; } = PacketFlags.Reliable;

        private static readonly UInt16 MaxPacketTypeId = UInt16.MaxValue;

        private readonly static List<IPacketHandler>[] packetHandlers = new List<IPacketHandler>[MaxPacketTypeId + 1];

        static NetworkManager() {
            Library.Initialize();

            OnConnectHandler = null;
            OnDisconnectHandler = null;
            OnTimeoutHandler = null;
            OnPacketReceiveHandler = null;
            OnLoginResultHandler = null;
            OnServerTypeHandler = null;

            Array.Fill(packetHandlers, null);

            RegisterPacketHandler((UInt16)PacketUtils.PredefinedPacketTypeId.CreateSession, new CreateSessionPacketHandler());
            RegisterPacketHandler((UInt16)PacketUtils.PredefinedPacketTypeId.JoinSession, new JoinSessionPacketHandler());
            RegisterPacketHandler((UInt16)PacketUtils.PredefinedPacketTypeId.Login, new LoginResultHandler());
            RegisterPacketHandler((UInt16)PacketUtils.PredefinedPacketTypeId.GetServerType, new ServerTypeHandler());
            RegisterPacketHandler((UInt16)PacketUtils.PredefinedPacketTypeId.GetSessionList, new SessionListPacketHandler());
        }

        public static void Deactivate() {
            Library.Deinitialize();
        }

        public static bool RegisterPacketHandler<T>(UInt16 packetTypeId, PacketHandler<T> handler) where T : notnull {
            if (packetHandlers[packetTypeId] == null) packetHandlers[packetTypeId] = new List<IPacketHandler>();

            if (!packetHandlers[packetTypeId].Contains(handler)) {
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
            if (packetHandlers[packetTypeId] == null) return false;
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
            if (latestIndex < 0 || !ClientRunning[latestIndex]) {
                throw new InvalidOperationException("Network client is not running. Please connect before sending packets.");
            }

            QueuedPacket qpacket;
            qpacket.channel = channel;
            qpacket.packet = packet;
            packetQueues[latestIndex].Add(qpacket);
        }

        public static Task<string> GetConnectedServerType() {
            Packet packet = PacketUtils.CreateEmptyPacket("GetServerType", PacketFlags.Reliable);

            var tcs = new TaskCompletionSource<string>();

            OnServerTypeHandler = (string serverType) => {
                tcs.SetResult(serverType);
            };

            SendPacket(0, packet);

            return tcs.Task;
        }

        /** 
         * <summary>Login with a given user ID and password in main server.</summary>
         */
        public static async Task<LoginResult> Login(string id, string password) {
            string serverType = await GetConnectedServerType();

            if (serverType != "MAIN_SERVER") {
                throw new InvalidOperationException("This method should be processed when connected with main server.");
            }

            Packet packet = PacketUtils.CreatePacket("Login", new LoginData {
                Id = id,
                Password = password
            }, LoginFlags, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            var tcs = new TaskCompletionSource<LoginResult>();

            OnLoginResultHandler = (LoginResult result) => {
                tcs.SetResult(result);
            };

            SendPacket(LoginChannel, packet);

            return await tcs.Task;
        }

        private static void Run(string hostName, UInt16 port, ref TaskCompletionSource<bool> tcs, int idx) {
            ClientRunning[idx] = true;
            clients[idx] = new Host();
            Address address = new Address();

            address.SetHost(hostName);
            address.Port = port;
            clients[idx]!.Create();

            peers[idx] = clients[idx]!.Connect(address);

            bool connected = false;
            Event netEvent;
            bool checkEvent;

            while (ClientRunning[idx]) {
                if (clients[idx]!.CheckEvents(out netEvent) <= 0) {
                    if (clients[idx]!.Service(Timeout, out netEvent) <= 0)
                        continue;
                }

                if (checkEvent) {
                    switch (netEvent.Type) {
                        case EventType.None:
                            break;

                    case EventType.Connect:
                        if (!tcs.Task.IsCompleted)
                            tcs.SetResult(true);
                        OnConnectHandler?.Invoke(netEvent.Data);
                        break;

                    case EventType.Disconnect:
                        OnDisconnectHandler?.Invoke(netEvent.Data);
                        ClientRunning[idx] = false;
                        break;

                    case EventType.Timeout:
                        if (!tcs.Task.IsCompleted)
                            tcs.SetResult(false);
                        OnTimeoutHandler?.Invoke(netEvent.Data);
                        ClientRunning[idx] = false;
                        break;

                        case EventType.Receive:
                            OnPacketReceiveHandler?.Invoke(netEvent.Packet, netEvent.ChannelID);

                            ParsedPacket ppacket = PacketUtils.ParsePacket(netEvent.Packet);
                            if (packetHandlers[ppacket.Header.PacketTypeId] != null) {
                                packetHandlers[ppacket.Header.PacketTypeId].ForEach(handler => {
                                    handler.RawHandle(ppacket.RawData);
                                });
                            }

                            netEvent.Packet.Dispose();
                            break;
                    }
                }

                while (packetQueues[idx].Count > 0) {
                    if (packetQueues[idx].TryTake(out QueuedPacket packet)) {
                        peers[idx]?.Send(packet.channel, ref packet.packet);
                    } else break;
                }
            }

            if (peers[idx]?.State == PeerState.Connected) {
                peers[idx]?.DisconnectNow(DisconnectCause);
                clients[idx]?.Flush();
            }

            clients[idx]?.Dispose();
            packetQueues[idx].Dispose();
        }

        public static Task<bool> Connect(string hostName, UInt16 port) {
            var tcs = new TaskCompletionSource<bool>();

            int idx = 0;
            bool selected = false;
            for (; idx < ClientThreads.Count; idx++) {
                if (!ClientThreads[idx].IsAlive) {
                    selected = true;
                    break;
                }
            }

            var thread = new Thread(() => {
                Thread.CurrentThread.Name = $"ClientThread{idx}";
                Run(hostName, port, ref tcs, idx);
            });

            if (selected) {
                packetQueues[idx] = new BlockingCollection<QueuedPacket>();
                ClientThreads[idx] = thread;
                ClientRunning[idx] = false;
            } else {
                packetQueues.Add(new BlockingCollection<QueuedPacket>());
                ClientThreads.Add(thread);
                ClientRunning.Add(false);
                peers.Add(null);
                clients.Add(null);
            }

            latestIndex = idx;
            thread.Start();

            return tcs.Task;
        }

        public static void Disconnect(UInt32 cause = 0) {
            if (latestIndex < 0) throw new InvalidOperationException();

            packetQueues[latestIndex].CompleteAdding();

            while (packetQueues[latestIndex].Count > 0) {
                if (packetQueues[latestIndex].TryTake(out QueuedPacket packet)) {
                    peers[latestIndex]?.Send(packet.channel, ref packet.packet);
                } else break;
            }

            peers[latestIndex]?.DisconnectNow(cause);
            clients[latestIndex]?.Flush();

            DisconnectCause = cause;
        }
    }
}