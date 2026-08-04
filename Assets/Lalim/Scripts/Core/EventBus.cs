using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lalim.Core
{
    /// <summary>
    /// Lightweight, type-safe publish/subscribe event bus.
    /// Decouples game systems — any script can publish or subscribe to events
    /// without needing direct references to other scripts.
    /// 
    /// Usage:
    ///   EventBus.Subscribe&lt;MyEvent&gt;(OnMyEvent);
    ///   EventBus.Publish(new MyEvent { ... });
    ///   EventBus.Unsubscribe&lt;MyEvent&gt;(OnMyEvent);
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> _eventTable = new Dictionary<Type, Delegate>();

        /// <summary>
        /// Subscribe a callback to an event type.
        /// </summary>
        public static void Subscribe<T>(Action<T> callback) where T : struct
        {
            Type eventType = typeof(T);

            if (_eventTable.TryGetValue(eventType, out Delegate existing))
            {
                _eventTable[eventType] = Delegate.Combine(existing, callback);
            }
            else
            {
                _eventTable[eventType] = callback;
            }
        }

        /// <summary>
        /// Unsubscribe a callback from an event type.
        /// Always unsubscribe in OnDisable/OnDestroy to prevent memory leaks.
        /// </summary>
        public static void Unsubscribe<T>(Action<T> callback) where T : struct
        {
            Type eventType = typeof(T);

            if (_eventTable.TryGetValue(eventType, out Delegate existing))
            {
                Delegate updated = Delegate.Remove(existing, callback);

                if (updated == null)
                {
                    _eventTable.Remove(eventType);
                }
                else
                {
                    _eventTable[eventType] = updated;
                }
            }
        }

        /// <summary>
        /// Publish an event to all subscribers. Synchronous — callbacks execute immediately.
        /// </summary>
        public static void Publish<T>(T eventData) where T : struct
        {
            Type eventType = typeof(T);

            if (_eventTable.TryGetValue(eventType, out Delegate existing))
            {
                (existing as Action<T>)?.Invoke(eventData);
            }
        }

        /// <summary>
        /// Remove all subscriptions. Call during scene cleanup or application quit.
        /// </summary>
        public static void ClearAll()
        {
            _eventTable.Clear();
        }

        /// <summary>
        /// Remove all subscriptions for a specific event type.
        /// </summary>
        public static void Clear<T>() where T : struct
        {
            _eventTable.Remove(typeof(T));
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor utility: log all currently registered event types and subscriber counts.
        /// </summary>
        public static void DebugLogSubscriptions()
        {
            Debug.Log($"[EventBus] Active subscriptions: {_eventTable.Count} event types");
            foreach (var kvp in _eventTable)
            {
                int count = kvp.Value.GetInvocationList().Length;
                Debug.Log($"  {kvp.Key.Name}: {count} subscriber(s)");
            }
        }
#endif
    }
}
