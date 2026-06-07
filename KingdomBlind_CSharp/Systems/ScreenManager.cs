using System;
using System.Collections.Generic;

namespace KingdomBlind_CSharp.Systems
{
    public sealed class ScreenDefinition
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public Action Render { get; set; } = () => { };
    }

    public sealed class ScreenManager
    {
        private readonly Dictionary<string, ScreenDefinition> screens = new Dictionary<string, ScreenDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Stack<string> history = new Stack<string>();

        public ScreenDefinition? Current { get; private set; }

        public void Register(string id, string title, Action render)
        {
            if (string.IsNullOrWhiteSpace(id) || render == null)
                return;

            screens[id] = new ScreenDefinition
            {
                Id = id,
                Title = title ?? id,
                Render = render
            };
        }

        public bool Navigate(string id, bool rememberCurrent = true)
        {
            if (!screens.TryGetValue(id, out var next))
                return false;

            if (rememberCurrent && Current != null)
                history.Push(Current.Id);

            Current = next;
            next.Render();
            return true;
        }

        public bool Back()
        {
            if (history.Count == 0)
                return false;

            return Navigate(history.Pop(), rememberCurrent: false);
        }
    }
}
