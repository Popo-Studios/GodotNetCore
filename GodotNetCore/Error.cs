using System;

namespace GodotNetCore {
    public class UnknownPacketTypeException : Exception {
        public UnknownPacketTypeException(string packetTypeName) : base($"There is no packet type for \"{packetTypeName}\"") {
        }
    }
    public class SessionExistsException : Exception {
        public SessionExistsException(SessionInfo info)
            : base($"Session already exists (Name: {info.Name}, Id: {info.Identifier.SessionPort}:{info.Identifier.SessionNumber})") {
        }
    }

    public class SessionNotFoundException: Exception {
        public SessionNotFoundException() : base("No session exists") {
        }
    }
}
