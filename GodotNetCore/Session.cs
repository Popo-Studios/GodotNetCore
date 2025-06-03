using ENet;
using MessagePack;
using System;
using System.Threading.Tasks;

namespace GodotNetCore {
    [MessagePackObject]
    public struct SessionCreationOption {
        [Key(0)]
        public string Name { get; set; }
        [Key(1)]
        public string? Password { get; set; }
        [Key(2)]
        public byte MaxPlayers { get; set; }
        [Key(3)]
        public bool IsPrivate { get; set; }
        [Key(4)]
        public UserIdentifier UserIdentifier { get; set; }
        [Key(5)]
        public string SessionType { get; set; }
    }

    [MessagePackObject]
    public struct SessionIdentifier {
        [Key(0)]
        public UInt16 SessionPort { get; set; }
        [Key(1)]
        public UInt16 SessionNumber { get; set; }
    }

    [MessagePackObject]
    public struct SessionInfo {
        [Key(0)]
        public string Name { get; set; }
        [Key(1)]
        public SessionIdentifier Identifier { get; set; }
        [Key(2)]
        public byte MaxPlayers { get; set; }
        [Key(3)]
        public byte CurrentPlayers { get; set; }
        [Key(4)]
        public bool IsPrivate { get; set; }
        [Key(5)]
        public bool HasPassword { get; set; }
        [Key(6)]
        public string AuthorName { get; set; }
        [Key(7)]
        public string sessionType { get; set; }
    }

    [MessagePackObject]
    public struct SessionListResult {
        [Key(0)]
        public UInt32 TotalSessionCount { get; set; }
        [Key(1)]
        public SessionInfo[] SessionInfoList { get; set; }
    }

    [MessagePackObject]
    public struct SessionListOption {
        [Key(0)]
        public string? NameFilter { get; set; }
        [Key(1)]
        public UInt32 Page { get; set; }
        [Key(2)]
        public UInt32 SessionPerPage { get; set; }
        [Key(3)]
        public string SessionType { get; set; }
    }

    [MessagePackObject]
    public struct SessionJoinOption {
        [Key(0)]
        public UserIdentifier UserIdentifier { get; set; }
        [Key(1)]
        public UInt16 SessionNumber { get; set; }
        [Key(2)]
        public string? Password { get; set; }
    }

    [MessagePackObject]
    public struct SessionCreationResult {
        [Key(0)]
        public bool Success { get; set; }
        [Key(1)]
        public byte ErrorCode { get; set; }
        [Key(2)]
        public SessionInfo? SessionInfo { get; set; }
    }

    [MessagePackObject]
    public struct SessionJoinResult {
        [Key(0)]
        public bool Success { get; set; }
        [Key(1)]
        public byte ErrorCode { get; set; }
    }

    public delegate void SessionCreationResultEventHandler(SessionCreationResult data);
    public delegate void SessionJoinResultEventHandler(SessionJoinResult data);
    public delegate void SessionListResultEventHandler(SessionListResult data);

    public class CreateSessionPacketHandler: PacketHandler<SessionCreationResult> {
        protected override void Handle(SessionCreationResult data) {
            SessionManager.OnSessionCreationResultHandler?.Invoke(data);
        }
    }

    public class JoinSessionPacketHandler: PacketHandler<SessionJoinResult> {
        protected override void Handle(SessionJoinResult data) {
            SessionManager.OnSessionJoinResultHandler?.Invoke(data);
        }
    }

    public class SessionListPacketHandler : PacketHandler<SessionListResult> {
        protected override void Handle(SessionListResult data) {
            SessionManager.OnSessionListHandler?.Invoke(data);
        }
    }

    public static class SessionManager {
        public static byte SessionChannel { get; set; } = 0;
        public static PacketFlags SessionFlags { get; set; } = PacketFlags.Reliable;
        public static SessionInfo? CurrentSession { get; private set; } = null;

        public static SessionJoinResultEventHandler? OnSessionJoinResultHandler { get; private set; } = null;
        public static SessionCreationResultEventHandler? OnSessionCreationResultHandler { get; private set; } = null;
        public static SessionListResultEventHandler? OnSessionListHandler { get; private set; } = null;

        static SessionManager() {
            NetworkManager.OnDisconnectHandler += (UInt32 data) => {
                CurrentSession = null;
            };
        }

        /**
         * <summary>Create a new session</summary>
         */
        public static async Task<SessionCreationResult> CreateNewSession(SessionCreationOption opt) {
            if (CurrentSession != null) {
                throw new SessionExistsException((SessionInfo)CurrentSession);
            }

            string serverType = await NetworkManager.GetConnectedServerType();
            if (serverType != "MAIN_SERVER") {
                throw new InvalidOperationException("This method should be processed when connected with main server.");
            }

            var tcs = new TaskCompletionSource<SessionCreationResult>();

            OnSessionCreationResultHandler = (SessionCreationResult result) => {
                tcs.SetResult(result);
            };

            Packet packet = PacketUtils.CreatePacket((UInt16)PacketUtils.PredefinedPacketTypeId.CreateSession, opt, SessionFlags);
            NetworkManager.SendPacket(SessionChannel, packet);

            return await tcs.Task;
        }

        public static async Task<SessionJoinResult> JoinSession(SessionInfo info, SessionJoinOption opt) {
            if (CurrentSession != null) {
                throw new SessionExistsException((SessionInfo)CurrentSession);
            }

            string serverType = await NetworkManager.GetConnectedServerType();
            if (serverType != "SESSION_SERVER") {
                throw new InvalidOperationException("This method should be processed when connected with session server");
            }

            var tcs = new TaskCompletionSource<SessionJoinResult>();

            OnSessionJoinResultHandler = (SessionJoinResult result) => {
                if (result.Success) {
                    CurrentSession = info;
                }
                tcs.SetResult(result);
            };
       
            Packet packet = PacketUtils.CreatePacket((UInt16)PacketUtils.PredefinedPacketTypeId.JoinSession, opt, SessionFlags);
            NetworkManager.SendPacket(SessionChannel, packet);
            return await tcs.Task;
        }

        public static async Task<SessionListResult> GetSessionList(SessionListOption option) {
            string serverType = await NetworkManager.GetConnectedServerType();
            if (serverType != "MAIN_SERVER") {
                throw new InvalidOperationException("This method should be processed when connected with main server.");
            }

            var tcs = new TaskCompletionSource<SessionListResult>();
            OnSessionListHandler = (SessionListResult data) => {
                tcs.SetResult(data);
            };
            Packet packet = PacketUtils.CreatePacket((UInt16)PacketUtils.PredefinedPacketTypeId.GetSessionList, option, SessionFlags);
            NetworkManager.SendPacket(SessionChannel, packet);
            return await tcs.Task;
        }
    }
}
