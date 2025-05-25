using System;
using System.Collections.Generic;
using System.Text;

namespace GodotNetCore {
    public class UnknownPacketTypeException : Exception {
        public UnknownPacketTypeException(string packetTypeName) : base($"There is no packet type for \"{packetTypeName}\"") {
        }
    }
    public class SessionExistsException : Exception {
        public SessionExistsException(ISessionInfo info) : base($"Session already exists (Name: {info.name}, Id: {info.sessionId})") {
        }
    }

    public class SessionNotFoundException: Exception {
        public SessionNotFoundException() : base("No session exists") {
        }
    }
}
