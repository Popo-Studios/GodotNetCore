using System;
using System.Collections.Generic;
using System.Linq;
using ENet;
using MessagePack;

namespace GodotNetCore {
    public struct ParsedPacket {
        public UInt16 packetTypeId;
        public byte[] rawData;
    }

    public static class PacketUtils {
        private readonly static Dictionary<string, UInt16> typeNameToId = new Dictionary<string, UInt16>();
        private readonly static Dictionary<UInt16, string> idToTypeName = new Dictionary<UInt16, string>();

        public static void RegisterPacketType(UInt16 typeId, string typeName) {
            typeNameToId.Add(typeName, typeId);
            idToTypeName.Add(typeId, typeName);
        }

        public static UInt16? GetPacketTypeId(string typeName) {
            if (typeNameToId.TryGetValue(typeName, out var typeId)) return typeId;
            else return null;
        }

        public static string? GetPacketTypeName(UInt16 typeId) {
            if (idToTypeName.TryGetValue(typeId, out var typeName)) return typeName;
            else return null;
        }

        public static Packet CreatePacket<T>(UInt16 packetType, T data, PacketFlags flags = PacketFlags.None) where T : notnull {
            List<byte> bytes = new List<byte>();

            bytes.AddRange(BitConverter.GetBytes(packetType));
            bytes.AddRange(MessagePackSerializer.Serialize(data));

            Packet packet = default;
            packet.Create(bytes.ToArray(), flags);

            return packet;
        }

        public static Packet createPacket<T>(string packetTypeName, T data) where T : notnull {
            if (typeNameToId.TryGetValue(packetTypeName, out var typeName)) {
                return CreatePacket(typeName, data);
            } else {
                throw new KeyNotFoundException($"There is no packet type for \"{packetTypeName}\"");
            }
        }

        public static ParsedPacket ParsePacket(Packet packet) {
            ParsedPacket ppacket;

            byte[] bytes = new byte[packet.Length];
            packet.CopyTo(bytes);

            ppacket.packetTypeId = BitConverter.ToUInt16(bytes, 0);

            const int headerSize = sizeof(UInt16);
            ppacket.rawData = bytes.Skip(headerSize).ToArray();

            return ppacket;
        }

        public static T ParseRawData<T>(byte[] rawData) where T : notnull {
            return MessagePackSerializer.Deserialize<T>(rawData);
        }
    }
}
