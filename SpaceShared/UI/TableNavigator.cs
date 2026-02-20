using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewValley;

#if IS_SPACECORE
namespace SpaceCore.UI
{
    public
#else
namespace SpaceShared.UI
{
    internal
#endif
         class TableNavigator
    {
        /*********
        ** Fields
        *********/
        private readonly Table Table;
        private int FocusedRow = -1;
        private List<int> InteractiveRows;
        private bool NeedsRebuild = true;

        /// <summary>Previous gamepad state for edge detection.</summary>
        private GamePadState PrevPad;

        // Stick navigation hold-to-repeat
        private int StickDir;
        private int StickTick;
        private bool StickInitial;
        private const float StickThreshold = 0.3f;
        private const int InitialDelay = 18; // ~300ms at 60fps
        private const int RepeatDelay = 9;   // ~150ms at 60fps

        // Dropdown navigation state
        private Dropdown OpenDropdown;
        private int DropdownOriginalChoice;


        /*********
        ** Accessors
        *********/
        /// <summary>Called when B is pressed outside a dropdown. Menu should go back/close.</summary>
        public Action OnBack { get; set; }


        /*********
        ** Public methods
        *********/
        public TableNavigator(Table table)
        {
            this.Table = table;
        }

        /// <summary>Mark the interactive rows list as stale (call after Table rows change).</summary>
        public void InvalidateRows()
        {
            this.NeedsRebuild = true;
        }

        /// <summary>Process gamepad input and snap cursor. Call BEFORE Ui.Update() in menu.update().</summary>
        public void Update()
        {
            if (!Game1.options.gamepadControls)
                return;

            if (this.NeedsRebuild)
                this.RebuildInteractiveRows();

            if (this.InteractiveRows.Count == 0)
                return;

            var pad = Game1.input.GetGamePadState();

            // Initialize focus on first interactive row if not set
            if (this.FocusedRow < 0 || !this.InteractiveRows.Contains(this.FocusedRow))
                this.FocusedRow = this.InteractiveRows[0];

            // Track dropdown state from the Dropdown class's static field
            if (Dropdown.ActiveDropdown != null && this.OpenDropdown == null)
            {
                // A dropdown just opened
                this.OpenDropdown = Dropdown.ActiveDropdown;
                this.DropdownOriginalChoice = this.OpenDropdown.ActiveChoice;
            }
            else if (Dropdown.ActiveDropdown == null && this.OpenDropdown != null)
            {
                // Dropdown just closed
                this.OpenDropdown = null;
            }

            this.HandleUpDown(pad);
            this.HandleLeftRight(pad);
            this.HandleB(pad);
            this.SnapCursorToFocused();

            this.PrevPad = pad;
        }


        /*********
        ** Private methods
        *********/
        private void RebuildInteractiveRows()
        {
            this.InteractiveRows = new List<int>();
            var rows = this.Table.RowData;
            for (int i = 0; i < rows.Count; i++)
            {
                if (IsRowInteractive(rows[i]))
                    this.InteractiveRows.Add(i);
            }
            this.NeedsRebuild = false;
        }

        private static bool IsRowInteractive(Element[] row)
        {
            foreach (var el in row)
            {
                if (el is Checkbox) return true;
                if (el is Slider) return true;
                if (el is Dropdown) return true;
                if (el is Label label && label.Callback != null) return true;
            }
            return false;
        }

        /// <summary>Get the element to snap the cursor to in a row.</summary>
        private static Element GetSnapTarget(Element[] row)
        {
            // Prefer non-label interactive elements (checkbox, slider, dropdown)
            foreach (var el in row)
            {
                if (el is Checkbox || el is Slider || el is Dropdown)
                    return el;
            }
            // Fall back to label with callback (mod name, page link, button)
            foreach (var el in row)
            {
                if (el is Label label && label.Callback != null)
                    return el;
            }
            return row.Length > 0 ? row[0] : null;
        }

        /// <summary>Find the position of the current focused row in the interactive rows list.</summary>
        private int FindCurrentPosition()
        {
            return this.InteractiveRows.IndexOf(this.FocusedRow);
        }

        private void HandleUpDown(GamePadState pad)
        {
            // D-pad edge detection
            bool dpadUp = pad.DPad.Up == ButtonState.Pressed && this.PrevPad.DPad.Up == ButtonState.Released;
            bool dpadDown = pad.DPad.Down == ButtonState.Pressed && this.PrevPad.DPad.Down == ButtonState.Released;

            // Left stick discrete navigation with hold-to-repeat
            float lsY = pad.ThumbSticks.Left.Y;
            int newStickDir = 0;
            if (lsY > StickThreshold) newStickDir = 1;       // Up
            else if (lsY < -StickThreshold) newStickDir = 2; // Down

            bool stickUp = false, stickDown = false;
            if (newStickDir == 0)
            {
                this.StickDir = 0;
                this.StickInitial = true;
            }
            else if (newStickDir != this.StickDir)
            {
                this.StickDir = newStickDir;
                this.StickTick = Game1.ticks;
                this.StickInitial = true;
                stickUp = newStickDir == 1;
                stickDown = newStickDir == 2;
            }
            else
            {
                int elapsed = Game1.ticks - this.StickTick;
                int delay = this.StickInitial ? InitialDelay : RepeatDelay;
                if (elapsed >= delay)
                {
                    this.StickTick = Game1.ticks;
                    this.StickInitial = false;
                    stickUp = newStickDir == 1;
                    stickDown = newStickDir == 2;
                }
            }

            bool goUp = dpadUp || stickUp;
            bool goDown = dpadDown || stickDown;
            if (!goUp && !goDown)
                return;

            if (this.OpenDropdown != null)
            {
                // Navigate within open dropdown
                int count = this.OpenDropdown.Choices.Length;
                if (count <= 0)
                    return;

                if (goUp)
                    this.OpenDropdown.ActiveChoice = (this.OpenDropdown.ActiveChoice - 1 + count) % count;
                else
                    this.OpenDropdown.ActiveChoice = (this.OpenDropdown.ActiveChoice + 1) % count;

                this.OpenDropdown.Callback?.Invoke(this.OpenDropdown);

                // Move mouse to the selected item in the dropdown's visual area
                this.SnapCursorToDropdownItem();
                Game1.playSound("shiny4");
                return;
            }

            // Navigate between rows
            int pos = this.FindCurrentPosition();
            if (goUp)
            {
                if (pos <= 0)
                    pos = this.InteractiveRows.Count; // wrap to end
                this.FocusedRow = this.InteractiveRows[pos - 1];
            }
            else
            {
                if (pos < 0 || pos >= this.InteractiveRows.Count - 1)
                    pos = -1; // wrap to start
                this.FocusedRow = this.InteractiveRows[pos + 1];
            }

            this.EnsureFocusedVisible();
            Game1.playSound("shiny4");
        }

        private void HandleLeftRight(GamePadState pad)
        {
            if (this.OpenDropdown != null)
                return; // Left/Right does nothing in dropdown mode

            // D-pad edge detection
            bool dpadLeft = pad.DPad.Left == ButtonState.Pressed && this.PrevPad.DPad.Left == ButtonState.Released;
            bool dpadRight = pad.DPad.Right == ButtonState.Pressed && this.PrevPad.DPad.Right == ButtonState.Released;

            // Left stick horizontal
            float lsX = pad.ThumbSticks.Left.X;
            bool stickLeft = false, stickRight = false;
            // Reuse the same hold-to-repeat tracking (overloaded for horizontal)
            // For simplicity, only handle edge detection for left/right on d-pad
            // Stick left/right will use a simpler threshold check
            if (Math.Abs(lsX) > StickThreshold && Math.Abs(lsX) > Math.Abs(pad.ThumbSticks.Left.Y))
            {
                // Only fire horizontal if stick is more horizontal than vertical
                int hDir = lsX < 0 ? 3 : 4;
                // We need separate tracking for horizontal — check if this is a new direction
                // For now, only D-pad handles left/right repeating. Stick horizontal is edge-only.
                if (Math.Abs(this.PrevPad.ThumbSticks.Left.X) <= StickThreshold)
                {
                    stickLeft = hDir == 3;
                    stickRight = hDir == 4;
                }
            }

            bool goLeft = dpadLeft || stickLeft;
            bool goRight = dpadRight || stickRight;
            if (!goLeft && !goRight)
                return;

            if (this.FocusedRow < 0 || this.FocusedRow >= this.Table.RowData.Count)
                return;

            var target = GetSnapTarget(this.Table.RowData[this.FocusedRow]);
            if (target == null)
                return;

            // Slider: adjust value
            if (target is Slider<int> intSlider)
            {
                int step = intSlider.Interval;
                int newVal = goRight
                    ? Math.Min(intSlider.Value + step, intSlider.Maximum)
                    : Math.Max(intSlider.Value - step, intSlider.Minimum);
                if (newVal != intSlider.Value)
                {
                    intSlider.Value = newVal;
                    intSlider.Callback?.Invoke(intSlider);
                    Game1.playSound("smallSelect");
                }
                return;
            }

            if (target is Slider<float> floatSlider)
            {
                float step = floatSlider.Interval;
                float newVal = goRight
                    ? Math.Min(floatSlider.Value + step, floatSlider.Maximum)
                    : Math.Max(floatSlider.Value - step, floatSlider.Minimum);
                if (Math.Abs(newVal - floatSlider.Value) > 0.0001f)
                {
                    floatSlider.Value = newVal;
                    floatSlider.Callback?.Invoke(floatSlider);
                    Game1.playSound("smallSelect");
                }
                return;
            }
        }

        private void HandleB(GamePadState pad)
        {
            bool bPressed = pad.Buttons.B == ButtonState.Pressed && this.PrevPad.Buttons.B == ButtonState.Released;
            if (!bPressed)
                return;

            if (this.OpenDropdown != null)
            {
                // Cancel dropdown — revert to original choice
                this.OpenDropdown.ActiveChoice = this.DropdownOriginalChoice;
                this.OpenDropdown.Callback?.Invoke(this.OpenDropdown);
                this.OpenDropdown.Dropped = false;
                if (this.OpenDropdown.Parent?.RenderLast == this.OpenDropdown)
                    this.OpenDropdown.Parent.RenderLast = null;
                Dropdown.ActiveDropdown = null;
                this.OpenDropdown = null;
                Game1.playSound("bigDeSelect");
                return;
            }

            this.OnBack?.Invoke();
        }

        private void SnapCursorToFocused()
        {
            if (this.FocusedRow < 0 || this.FocusedRow >= this.Table.RowData.Count)
                return;

            // Don't move cursor if dropdown is open — dropdown item snap handles it
            if (this.OpenDropdown != null)
                return;

            var target = GetSnapTarget(this.Table.RowData[this.FocusedRow]);
            if (target == null)
                return;

            var bounds = target.Bounds;
            Game1.setMousePosition(bounds.Center.X, bounds.Center.Y);
        }

        private void SnapCursorToDropdownItem()
        {
            if (this.OpenDropdown == null)
                return;

            // Position cursor on the currently selected dropdown item
            int selected = this.OpenDropdown.ActiveChoice;
            int activePos = this.OpenDropdown.ActivePosition;
            int itemHeight = this.OpenDropdown.Height; // each item is the dropdown's height

            int tall = Math.Min(this.OpenDropdown.MaxValuesAtOnce, this.OpenDropdown.Choices.Length - activePos) * itemHeight;
            int drawY = Math.Min((int)this.OpenDropdown.Position.Y, Game1.uiViewport.Height - tall);

            int itemIndex = selected - activePos;
            if (itemIndex >= 0 && itemIndex < this.OpenDropdown.MaxValuesAtOnce)
            {
                int itemY = drawY + itemIndex * itemHeight + itemHeight / 2;
                int itemX = (int)this.OpenDropdown.Position.X + this.OpenDropdown.Width / 2;
                Game1.setMousePosition(itemX, itemY);
            }
        }

        private void EnsureFocusedVisible()
        {
            if (this.FocusedRow < 0)
                return;

            // Check if the focused element is currently off-screen
            var rows = this.Table.RowData;
            if (this.FocusedRow >= rows.Count)
                return;

            var target = GetSnapTarget(rows[this.FocusedRow]);
            if (target == null)
                return;

            float tableTop = this.Table.Position.Y;
            float tableBottom = tableTop + this.Table.Height;
            float elementTop = target.Position.Y;
            float elementBottom = elementTop + target.Height;

            if (elementTop < tableTop)
            {
                // Element above visible area — scroll up
                this.Table.Scrollbar.ScrollBy(-1);
                // May need multiple scrolls for large offsets — recursive call next frame
            }
            else if (elementBottom > tableBottom)
            {
                // Element below visible area — scroll down
                this.Table.Scrollbar.ScrollBy(1);
            }
        }
    }
}
