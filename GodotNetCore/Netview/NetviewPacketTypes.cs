using System;
using System.Collections.Generic;
using ENet;
using MessagePack;
using Godot;

namespace GodotNetCore {
    [MessagePackObject]
    public struct LoadNetviewPacket(string path, Guid? guid, Guid? parentId = null, Vector2? pos = null) {
        [Key(0)]
        public string path = path;

        [Key(1)]
        public Guid? guid = guid;

        [Key(2)]
        public Guid? parentId = parentId;

        [Key(3)]
        public Vector2? pos = pos;
    }

    [MessagePackObject]
    public struct NetviewPacket<T>(Guid guid, T data) {
        [Key(0)]
        public Guid guid = guid;

        [Key(1)]
        public T data = data;
    }
}