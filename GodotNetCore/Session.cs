using ENet;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace GodotNetCore {
    public interface ISessionCreation {
        public string Name { get; set; }
        public string? Password { get; set; }
        public byte MaxPlayers { get; set; }
        public bool IsPrivate { get; set; }
        public string AuthorToken { get; set; }
    }

    public struct SessionIdentifier {
        public string SessionHost { get; set; }
        public UInt16 SessionPort { get; set; }
        public UInt16 SessionNumber { get; set; }
    }

    public interface ISessionInfo {
        public string Name { get; }
        public SessionIdentifier SessionId { get; }
        public byte MaxPlayers { get;}
        public byte CurrentPlayers { get; }
        public bool IsPrivate { get; }
        public bool HasPassword { get; }
        public string AuthorName { get; }
    }

    public interface ISessionList {
        public UInt32 SessionCount { get; }
        public ISessionInfo[] SessionInfoList { get; }
    }

    public interface ISessionJoin {
        public SessionIdentifier SessionId { get; set; }
        public string Password { get; set; }
        public string UserToken { get; set; }
    }

    public interface ISessionResult {
        public bool Success { get; }
        public byte Cause { get; }
        public ISessionInfo? SessionInfo { get; }
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
        public static byte SessionChannel { get; set; } = 0;
        public static PacketFlags SessionFlags { get; set; } = PacketFlags.None;
        public static ISessionInfo? CurrentSession { get; private set; } = null;

        public static SessionResultEventHandler? OnSessionJoinResultHandler { get; private set; } = null;
        public static SessionResultEventHandler? OnSessionCreationResultHandler { get; private set; } = null;
        public static SessionResultEventHandler? OnSessionLeaveResultHandler { get; private set; } = null;

        public static Task<ISessionResult> CreateNewSession(ISessionCreation info) {
            if (CurrentSession != null) {
                throw new SessionExistsException(CurrentSession);
            }

            var tcs = new TaskCompletionSource<ISessionResult>();

            OnSessionCreationResultHandler = (ISessionResult result) => {
                tcs.SetResult(result);
                if (result.Success) {
                    CurrentSession = result.SessionInfo;
                }
            };

            Packet packet = PacketUtils.CreatePacket((UInt16)PacketUtils.PredefinedPacketTypeId.CreateSession, info, SessionFlags);
            NetworkManager.SendPacket(SessionChannel, packet);

            return tcs.Task;
        }

        public static Task<ISessionResult> JoinSession(ISessionJoin info) {
            if (CurrentSession != null) {
                throw new SessionExistsException(CurrentSession);
            }

            var tcs = new TaskCompletionSource<ISessionResult>();

            OnSessionJoinResultHandler = (ISessionResult result) => {
                tcs.SetResult(result);
                if (result.Success) {
                    CurrentSession = result.SessionInfo;
                }
            };

            Packet packet = PacketUtils.CreatePacket((UInt16)PacketUtils.PredefinedPacketTypeId.JoinSession, info, SessionFlags);
            NetworkManager.SendPacket(SessionChannel, packet);

            return tcs.Task;
        }

        public static Task<ISessionResult> LeaveSession() {
            if (CurrentSession == null) {
                throw new SessionNotFoundException();
            }

            var tcs = new TaskCompletionSource<ISessionResult>();

            OnSessionLeaveResultHandler = (ISessionResult result) => {
                tcs.SetResult(result);
                if (result.Success) {
                    CurrentSession = null;
                }
            };

            Packet packet = PacketUtils.CreatePacket((UInt16)PacketUtils.PredefinedPacketTypeId.LeaveSession, SessionFlags);
            NetworkManager.SendPacket(SessionChannel, packet);

            return tcs.Task;
        }
    }
}
