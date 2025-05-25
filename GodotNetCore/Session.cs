using ENet;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace GodotNetCore {
    public interface ISessionCreation {
        public string name { get; set; }
        public string? password { get; set; }
        public byte maxPlayers { get; set; }
        public bool isPrivate { get; set; }
        public string authorToken { get; set; }
    }

    public interface ISessionInfo {
        public string name { get; }
        public string sessionId { get; }
        public byte maxPlayers { get;}
        public byte currentPlayers { get; }
        public bool isPrivate { get; }
        public bool hasPassword { get; }
        public string authorName { get; }
    }

    public interface ISessionList {
        public UInt32 sessionCount { get; }
        public ISessionInfo[] sessionInfoList { get; }
    }

    public interface ISessionJoin {
        public string sessionId { get; set; }
        public string password { get; set; }
        public string userToken { get; set; }
    }

    public interface ISessionResult {
        public bool success { get; }
        public byte cause { get; }
        public ISessionInfo? sessionInfo { get; }
    }

    public delegate void SessionResultEventHandler(ISessionResult data);

    public class CreateSessionPacketHandler: PacketHandler<ISessionResult> {
        protected override void Handle(ISessionResult data) {
            SessionManager.OnSessionCreationResultHandler?.Invoke(data);
        }
    }

    public class JoinSessionPacketHandler: PacketHandler<ISessionResult> {
        protected override void Handle(ISessionResult data) {
            SessionManager.OnSessionJoinResultHandler?.Invoke(data);
        }
    }

    public static class SessionManager {
        public static byte sessionChannel { get; set; } = 0;
        public static PacketFlags sessionFlags { get; set; } = PacketFlags.None;
        public static ISessionInfo? currentSession { get; private set; } = null;

        public static SessionResultEventHandler? OnSessionJoinResultHandler { get; private set; } = null;
        public static SessionResultEventHandler? OnSessionCreationResultHandler { get; private set; } = null;
        public static SessionResultEventHandler? OnSessionLeaveResultHandler { get; private set; } = null;

        public static Task<ISessionResult> CreateNewSession(ISessionCreation info) {
            if (currentSession != null) {
                throw new SessionExistsException(currentSession);
            }

            var tcs = new TaskCompletionSource<ISessionResult>();

            OnSessionCreationResultHandler = (ISessionResult result) => {
                tcs.SetResult(result);
                if (result.success) {
                    currentSession = result.sessionInfo;
                }
            };

            Packet packet = PacketUtils.CreatePacket((UInt16)PacketUtils.PredefinedPacketTypeId.CreateSession, info, sessionFlags);
            NetworkManager.SendPacket(sessionChannel, packet);

            return tcs.Task;
        }

        public static Task<ISessionResult> JoinSession(ISessionJoin info) {
            if (currentSession != null) {
                throw new SessionExistsException(currentSession);
            }

            var tcs = new TaskCompletionSource<ISessionResult>();

            OnSessionJoinResultHandler = (ISessionResult result) => {
                tcs.SetResult(result);
                if (result.success) {
                    currentSession = result.sessionInfo;
                }
            };

            Packet packet = PacketUtils.CreatePacket((UInt16)PacketUtils.PredefinedPacketTypeId.JoinSession, info, sessionFlags);
            NetworkManager.SendPacket(sessionChannel, packet);

            return tcs.Task;
        }

        public static Task<ISessionResult> LeaveSession() {
            if (currentSession == null) {
                throw new SessionNotFoundException();
            }

            var tcs = new TaskCompletionSource<ISessionResult>();

            OnSessionLeaveResultHandler = (ISessionResult result) => {
                tcs.SetResult(result);
                if (result.success) {
                    currentSession = null;
                }
            };

            Packet packet = PacketUtils.CreatePacket((UInt16)PacketUtils.PredefinedPacketTypeId.LeaveSession, sessionFlags);
            NetworkManager.SendPacket(sessionChannel, packet);

            return tcs.Task;
        }
    }
}
