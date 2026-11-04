using System;
using UnityEngine;
using UnityEngine.Events;

namespace Starter.Extensions.UI
{
    public sealed class AnimationEventRelay : MonoBehaviour
    {
        [Serializable] public sealed class StringEvent : UnityEvent<string> { }
        [Serializable] public sealed class IntEvent : UnityEvent<int> { }
        public StringEvent onString = new StringEvent();
        public IntEvent onIndex = new IntEvent();
        public event Action<AnimationEvent> Received;

        // Select this method in a Unity Animation Event. It intentionally has no Lua dependency.
        public void OnAnimationEvent(AnimationEvent data)
        {
            if (data == null) return;
            onString.Invoke(data.stringParameter);
            onIndex.Invoke(data.intParameter);
            Received?.Invoke(data);
        }
    }
}
