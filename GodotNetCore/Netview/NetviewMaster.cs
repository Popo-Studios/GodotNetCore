using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Collections.Generic;
using ENet;
using Godot;

namespace GodotNetCore {
    public enum NetviewPacketTypeId : UInt16 {
        LoadNetview = 40000,
    }
    
    public partial class NetviewMaster : Node {
        public static NetviewMaster? Instance { get; private set; }

        public readonly Dictionary<Guid, Netview> guidToNetview = new();

        public override void _Ready() {
            Instance = this;

            NetworkManager.RegisterPacketHandler(
                (UInt16)NetviewPacketTypeId.LoadNetview,
                new LoadNetviewReciever()
            );

            GD.Print("NetviewMaster Initialized");
        }

        /// <summary>
        /// 클라이언트 간 공유되는 Netview를 생성합니다. <br/>
        /// <br/>
        /// guid가 null인 경우 현재 클라이언트 소유로 Netview를 생성합니다.
        /// 다른 클라이언트 소유인 경우, guid에 null이 아닌 값이 할당됩니다.
        /// </summary>
        public Node? LoadNetview(string path, Guid? guid = null) {
            if (!ResourceLoader.Exists(path))
                return null;

            PackedScene scene;
            try {
                scene = ResourceLoader.Load<PackedScene>(path);
            }
            catch (InvalidCastException) {
                return null;
            }

            Netview instance = scene.Instantiate<Netview>();
            bool isMain = false;
            if (guid == null) {
                guid = Guid.NewGuid();
                isMain = true;

                var data = new LoadNetviewPacket(path, (Guid)guid);
                var packet = PacketUtils.CreatePacket((UInt16)NetviewPacketTypeId.LoadNetview, data);
                NetworkManager.SendPacket(0, packet);
            }

            Node parent = GetTree().CurrentScene;
            parent.CallDeferred("add_child", instance);

            instance.Initialize((Guid)guid, isMain);

            return instance;
        }

        public class LoadNetviewReciever : PacketHandler<LoadNetviewPacket> {
            protected override void Handle(LoadNetviewPacket data) {
                GD.Print($"LoadNetview {data.path}");
                Instance!.LoadNetview(data.path, data.guid);
            }
        }
    }
}