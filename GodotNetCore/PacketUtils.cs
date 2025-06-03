using System;
using System.Collections.Generic;
using System.Linq;
using ENet;
using MessagePack;

namespace GodotNetCore {
    [MessagePackObject]
    public struct PacketHeader {
        [Key(0)]
        public UInt16 PacketTypeId;
        [Key(1)]
        public Int64 Timestamp;
    }

    public struct ParsedPacket {
        public PacketHeader Header;
        public byte[] RawData;
    }
    public static class PacketUtils {
        private readonly static Dictionary<string, UInt16> typeNameToId = new Dictionary<string, UInt16>();
        private readonly static Dictionary<UInt16, string> idToTypeName = new Dictionary<UInt16, string>();

        public enum PredefinedPacketTypeId: UInt16 {
            CreateSession = UInt16.MaxValue,
            JoinSession = UInt16.MaxValue - 1,
            Login = UInt16.MaxValue - 2,
            GetServerType = UInt16.MaxValue - 3,
            GetSessionList = UInt16.MaxValue - 4,
        }             

        static PacketUtils() {
            RegisterPacketType((UInt16)PredefinedPacketTypeId.CreateSession, "CreateSession");
            RegisterPacketType((UInt16)PredefinedPacketTypeId.JoinSession, "JoinSession");
            RegisterPacketType((UInt16)PredefinedPacketTypeId.Login, "Login");
            RegisterPacketType((UInt16)PredefinedPacketTypeId.GetServerType, "GetServerType");
            RegisterPacketType((UInt16)PredefinedPacketTypeId.GetSessionList, "GetSessionList");
        }

        public static void RegisterPacketType(UInt16 typeId, string typeName) {
            typeNameToId.Add(typeName, typeId);
            idToTypeName.Add(typeId, typeName);
        }

        public static UInt16? GetPacketTypeId(string typeName)
        {
            if (typeNameToId.TryGetValue(typeName, out var typeId)) return typeId;
            else return null;
        }

        public static string? GetPacketTypeName(UInt16 typeId) {
            if (idToTypeName.TryGetValue(typeId, out var typeName)) return typeName;
            else return null;
        }

        public static Packet CreateEmptyPacket(UInt16 packetType, PacketFlags flags = PacketFlags.None, Int64? timestamp = null) {
            List<byte> bytes = new List<byte>();
            PacketHeader header;
            header.PacketTypeId = packetType;
            header.Timestamp = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            byte[] serializedHeader = MessagePackSerializer.Serialize(header);
            bytes.AddRange(BitConverter.GetBytes(serializedHeader.Length));
            bytes.AddRange(serializedHeader);
            Packet packet = default;
            packet.Create(bytes.ToArray(), flags);
            return packet;
        }

        public static Packet CreateEmptyPacket(string packetTypeName, PacketFlags flags = PacketFlags.None, Int64? timestamp = null) {
            if (typeNameToId.TryGetValue(packetTypeName, out var typeId)) {
                return CreateEmptyPacket(typeId, flags, timestamp);
            } else {
                throw new UnknownPacketTypeException(packetTypeName);
            }
        }

        public static Packet CreatePacket<T>(UInt16 packetType, T data, PacketFlags flags = PacketFlags.None, Int64? timestamp = null) where T : notnull {
            List<byte> bytes = new List<byte>();

            byte[] serializedData = MessagePackSerializer.Serialize(data);

            PacketHeader header;
            header.PacketTypeId = packetType;
            header.Timestamp = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            byte[] serializedHeader = MessagePackSerializer.Serialize(header);

            bytes.AddRange(BitConverter.GetBytes(serializedHeader.Length));
            bytes.AddRange(serializedHeader);
            bytes.AddRange(serializedData);

            Packet packet = default;
            packet.Create(bytes.ToArray(), flags);

            return packet;
        }

        public static Packet CreatePacket<T>(string packetTypeName, T data, PacketFlags flags = PacketFlags.None, Int64? timestamp = null) where T : notnull {
            if (typeNameToId.TryGetValue(packetTypeName, out var typeName)) {
                return CreatePacket(typeName, data, flags, timestamp);
            } else {
                throw new UnknownPacketTypeException(packetTypeName);
            }
        }

        public static ParsedPacket ParsePacket(Packet packet) {
            ParsedPacket ppacket;

            byte[] bytes = new byte[packet.Length];
            packet.CopyTo(bytes);

            Int32 headerSize = BitConverter.ToInt32(bytes);
            bytes = bytes.Skip(sizeof(Int32)).ToArray();
            ppacket.Header = MessagePackSerializer.Deserialize<PacketHeader>(bytes);
            
            ppacket.RawData = bytes.Skip(headerSize).ToArray();

            return ppacket;
        }

        public static T ParseRawData<T>(byte[] rawData) where T : notnull {
            return MessagePackSerializer.Deserialize<T>(rawData);
        }
    }
}
