namespace VoxelEngine
{
    using UnityEngine;

    [System.Serializable]
    public sealed class InterfaceReference<T> where T : class
    {
        [SerializeField]
        private UnityEngine.Object _objectValue;

        public UnityEngine.Object ObjectValue => _objectValue;

        // Choose one of the following Value getters:

        // Fails silently and returns null on inappropriate object assignment.
        public T Value => (_objectValue == null) ? null : _objectValue as T;
    }
}