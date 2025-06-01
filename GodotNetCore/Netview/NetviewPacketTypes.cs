using System;
using System.Collections.Generic;
using ENet;
using MessagePack;
using Godot;

namespace GodotNetCore {
    [MessagePackObject]
    public struct SerializableVector2(float x, float y) {
        [Key(0)]
        public float x = x;

        [Key(1)]
        public float y = y;

        public static explicit operator SerializableVector2(Vector2 vector) => new(vector.X, vector.Y);

        public static explicit operator Vector2(SerializableVector2 svector) => new(svector.x, svector.y);
    }

    [MessagePackObject]
    public struct LoadNetviewPacket(string path, Guid? guid, Guid? parentId = null, Vector2? pos = null) {
        [Key(0)]
        public string path = path;

        [Key(1)]
        public Guid? guid = guid;

        [Key(2)]
        public Guid? parentId = parentId;

        [Key(3)]
        public SerializableVector2? pos = (SerializableVector2?)pos;
    }

    [MessagePackObject]
    public struct NetviewPacket<T>(Guid guid, T data) {
        [Key(0)]
        public Guid guid = guid;

        [Key(1)]
        public T data = data;
    }
}