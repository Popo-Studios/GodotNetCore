using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Collections.Generic;
using ENet;
using Godot;
using System.Runtime.InteropServices;
using System.Dynamic;

namespace GodotNetCore {
    public enum NetviewPacketTypeId : UInt16 {
        LoadNetview = 40000,
    }

    public partial class NetviewMaster : Node {
        public static NetviewMaster? Instance { get; private set; }

        public readonly Dictionary<Guid, Netview> netviewsDict = new();
        private readonly HashSet<UInt16> registeredPacketTypeId = new();

        public override void _Ready() {
            Instance = this;

            NetworkManager.RegisterPacketHandler(
                (UInt16)NetviewPacketTypeId.LoadNetview,
                new LoadNetviewPacketHandler()
            );
        }

        public Node? LoadNetview(LoadNetviewPacket data) {
            if (!ResourceLoader.Exists(data.path))
                return null;

            PackedScene scene;
            try {
                scene = ResourceLoader.Load<PackedScene>(data.path);
            }
            catch (InvalidCastException) {
                return null;
            }

            var instance = scene.Instantiate();
            Netview? netview = instance.GetNodeInChildren<Netview>();
            if (netview == null) {
                instance.Free();
                return null;
            }

            bool isMain = false;
            if (data.guid == null) {
                data.guid = Guid.NewGuid();
                isMain = true;

                var packet = PacketUtils.CreatePacket((UInt16)NetviewPacketTypeId.LoadNetview, data);
                NetworkManager.SendPacket(0, packet);
            }
            netview.Initialize((Guid)data.guid!, isMain);

            Node? parent = null;
            if (data.parentId != null) {
                Guid parentId = (Guid)data.parentId!;
                if (netviewsDict.TryGetValue(parentId, out Netview? value)) {
                    parent = value;
                }
            }
            parent ??= GetTree().CurrentScene;
            parent!.CallDeferred("add_child", instance);

            if (data.pos != null) {
                if (instance.IsClass("Node2D")) {
                    ((Node2D)instance).Position = (Vector2)data.pos;
                }
            }

            return netview;
        }

        /// <summary>
        /// 클라이언트 간 공유되는 Netview를 생성합니다. 생성 실패 시 null을 반환합니다.
        /// </summary>
        public Node? LoadNetview(string path, Netview? parent = null, Vector2? pos = null) {
            return LoadNetview(new LoadNetviewPacket(path, null, parent?.guid, pos));
        }

        /// <summary>
        /// 주어진 packetTypeId에 대한 NetviewPacketHandler를 등록합니다. 이미 등록되어 있다면 아무것도 수행하지 않습니다.
        /// </summary>
        public void RegisterNetviewPacketHandler<T>(UInt16 packetTypeId) {
            if (registeredPacketTypeId.Contains(packetTypeId))
                return;

            NetworkManager.RegisterPacketHandler<NetviewPacket<T>>(
                packetTypeId,
                new NetviewPacketHandler<T>(packetTypeId)
            );
            registeredPacketTypeId.Add(packetTypeId);
        }

        public class LoadNetviewPacketHandler : PacketHandler<LoadNetviewPacket> {
            protected override void Handle(LoadNetviewPacket data) {
                Instance!.LoadNetview(data);
            }
        }

        public class NetviewPacketHandler<T>(UInt16 packetTypeId) : PacketHandler<NetviewPacket<T>> {
            UInt16 packetTypeId = packetTypeId;

            protected override void Handle(NetviewPacket<T> packet) {
                NetviewMaster netviewMaster = Instance!;
                if (!netviewMaster.netviewsDict.TryGetValue(packet.guid, out var netview))
                    return;

                netview.Handle<T>(packetTypeId, packet.data);
            }
        }
    }
}