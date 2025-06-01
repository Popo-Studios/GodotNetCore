using Godot;

namespace GodotNetCore {
    public static class NetviewUtils {
        public static T? GetNodeInChildren<T>(this Node parent) where T : Node {
            if (parent.GetType().IsAssignableTo(typeof(T))) return (T)parent;
            
            foreach (Node child in parent.GetChildren()) {
                if (child is T matched)
                    return matched;

                var result = GetNodeInChildren<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}