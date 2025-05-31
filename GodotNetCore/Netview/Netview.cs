using System;
using Godot;

namespace GodotNetCore {
    public partial class Netview : Node {
        public Guid? guid = null;
        public bool isMain;

        public void Initialize(Guid guid, bool isMain) {
            this.guid = guid;
            NetviewMaster.Instance!.guidToNetview.Add(guid, this);

            this.isMain = isMain;
        }
    }
}