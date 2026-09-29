using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// SLICE-10 Block 6 part 1: the four-button tool switcher (Select/Draw Wall/Draw Door/Draw Window),
    /// a small, focused presenter class by exact precedent of <see cref="RoleSelectorPresenter"/> -- not
    /// folded into <see cref="BoardScreenPresenter"/>, which already owns enough of its own concerns.
    /// Visibility is a presentation-only decision (<see cref="BoardScreenPresenter.LocalActorIsMainGm"/>,
    /// itself already documented as not a real authorization source -- the server re-checks every command
    /// this task's drawing tools issue); this presenter never authorizes anything itself.
    /// </summary>
    public sealed class BoardToolbarPresenter
    {
        private readonly Action<BoardTool> _onToolSelected;
        private readonly Dictionary<BoardTool, Button> _buttonsByTool = new Dictionary<BoardTool, Button>();
        private VisualElement? _root;

        public BoardToolbarPresenter(Action<BoardTool> onToolSelected)
        {
            _onToolSelected = onToolSelected ?? throw new ArgumentNullException(nameof(onToolSelected));
        }

        public VisualElement BuildView()
        {
            _root = new VisualElement { name = "board-toolbar" };
            _root.style.flexDirection = FlexDirection.Row;
            _root.style.marginTop = 4;
            AddButton(BoardTool.Select, "Select");
            AddButton(BoardTool.DrawWall, "Draw Wall");
            AddButton(BoardTool.DrawDoor, "Draw Door");
            AddButton(BoardTool.DrawWindow, "Draw Window");
            return _root;
        }

        private void AddButton(BoardTool tool, string label)
        {
            var button = new Button(() => _onToolSelected(tool)) { name = "board-tool-" + tool.ToString().ToLowerInvariant(), text = label };
            button.style.marginRight = 4;
            _root!.Add(button);
            _buttonsByTool[tool] = button;
        }

        /// <summary>Highlights the currently active tool's button (a border color change -- no finalized art, task non-goal).</summary>
        public void SetActiveTool(BoardTool tool)
        {
            foreach (KeyValuePair<BoardTool, Button> entry in _buttonsByTool)
            {
                bool isActive = entry.Key == tool;
                entry.Value.style.borderTopWidth = isActive ? 2 : 1;
                entry.Value.style.borderBottomWidth = isActive ? 2 : 1;
                entry.Value.style.borderLeftWidth = isActive ? 2 : 1;
                entry.Value.style.borderRightWidth = isActive ? 2 : 1;
                var borderColor = new StyleColor(isActive ? new Color(1f, 0.85f, 0.1f) : new Color(0.4f, 0.4f, 0.4f));
                entry.Value.style.borderTopColor = borderColor;
                entry.Value.style.borderBottomColor = borderColor;
                entry.Value.style.borderLeftColor = borderColor;
                entry.Value.style.borderRightColor = borderColor;
            }
        }

        /// <summary>SLICE-10 Block 6 part 1: the toolbar is visible only for the presentation-selected MainGM role -- an ordinary player never sees drawing tools they could not use anyway (the server would deny <c>CreateObstacle</c>).</summary>
        public void SetVisible(bool visible)
        {
            if (_root != null) _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
