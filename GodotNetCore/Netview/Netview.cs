using System;
using System.Collections.Generic;
using Godot;

namespace GodotNetCore {
    public partial class Netview : Node {
        public Guid guid { get; private set; }
        public bool isMain { get; private set; }

        private readonly Dictionary<UInt16, object> handlersDict = new();

        private NetviewMaster? netviewMaster;

        public void Initialize(Guid guid, bool isMain) {
            this.guid = guid;
            netviewMaster = NetviewMaster.Instance;
            netviewMaster!.netviewsDict.Add(guid, this);

            this.isMain = isMain;
        }

        public void RegisterHandler<T>(UInt16 packetTypeId, Action<T> action) {
            netviewMaster!.RegisterNetviewPacketHandler<T>(packetTypeId);

            if (handlersDict.TryGetValue(packetTypeId, out object? value)) {
                if (value.GetType().IsAssignableTo(typeof(Action<T>))) {
                    Action<T> originalAction = (Action<T>)value;
                    originalAction += action;
                    handlersDict[packetTypeId] = originalAction;
                }
            }
            else {
                handlersDict.Add(packetTypeId, action);
            }
        }

        public void RemoveHandler<T>(UInt16 packetTypeId, Action<T> action) {
            if (handlersDict.TryGetValue(packetTypeId, out object? value)) {
                if (value.GetType().IsAssignableTo(typeof(Action<T>))) {
                    Action<T>? originalAction = (Action<T>)value;
                    originalAction -= action;

                    if (originalAction != null) {
                        handlersDict[packetTypeId] = originalAction;
                    }
                    else {
                        handlersDict.Remove(packetTypeId);
                    }
                }
            }
        }

        public void Handle<T>(UInt16 packetTypeId, T data) {
            if (handlersDict.TryGetValue(packetTypeId, out object? value)) {
                if (value.GetType().IsAssignableTo(typeof(Action<T>))) {
                    Action<T> action = (Action<T>)value;
                    action.Invoke(data);
                }
            }
        }

        public void Send<T>(UInt16 packetTypeId, T data) {
            var packet = PacketUtils.CreatePacket(
                packetTypeId,
                new NetviewPacket<T>(guid, data)
            );
            NetworkManager.SendPacket(0, packet);
        }

        public override void _Notification(int what) {
            if (what == NotificationPredelete) {
                netviewMaster!.netviewsDict.Remove(guid);
            }
        }
    }
}