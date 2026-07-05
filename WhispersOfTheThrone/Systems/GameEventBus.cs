using System;
using System.Collections.Generic;
using System.Linq;

namespace WhispersOfTheThrone.Systems
{
    public interface IGameEvent
    {
        string EventType { get; }
        string Title { get; }
        string Message { get; }
        bool IsImportant { get; }
    }

    public sealed class GameEvent : IGameEvent
    {
        public string EventType { get; set; } = "";
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public bool IsImportant { get; set; }
        public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
    }

    public sealed class GameEventBus
    {
        private readonly Dictionary<string, List<Action<IGameEvent>>> handlers = new Dictionary<string, List<Action<IGameEvent>>>();

        public void Subscribe(string eventType, Action<IGameEvent> handler)
        {
            if (string.IsNullOrWhiteSpace(eventType) || handler == null)
                return;

            if (!handlers.TryGetValue(eventType, out var list))
            {
                list = new List<Action<IGameEvent>>();
                handlers[eventType] = list;
            }

            if (!list.Contains(handler))
                list.Add(handler);
        }

        public void Publish(IGameEvent gameEvent)
        {
            if (gameEvent == null)
                return;

            Dispatch(gameEvent.EventType, gameEvent);
            Dispatch("*", gameEvent);
        }

        private void Dispatch(string eventType, IGameEvent gameEvent)
        {
            if (!handlers.TryGetValue(eventType, out var list))
                return;

            foreach (var handler in list.ToList())
                handler(gameEvent);
        }
    }
}
