using System;
using System.Collections.Generic;
using ENet;
using MessagePack;

namespace GodotNetCore {
    [MessagePackObject]
    public struct LoadNetviewPacket(string path, Guid guid) {
        [Key(0)]
        public string path = path;

        [Key(1)]
        public Guid guid = guid;
    }

    public interface INetviewPacket<T> {
        public string NetviewId { get; set; }
        public string Address { get; set; }

        public T Value { get; set; }
    }

    [MessagePackObject]
    public struct NetviewIntPacket : INetviewPacket<Int32> {
        [Key(0)]
        public string NetviewId { get; set; }

        [Key(1)]
        public string Address { get; set; }

        [Key(2)]
        public Int32 Value { get; set; }
    }
}