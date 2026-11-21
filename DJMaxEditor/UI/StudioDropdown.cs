using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace DJMaxEditor.UI
{
    /// <summary>
    /// DropDownList-style picker whose popup is a managed ToolStripDropDown instead of the
    /// native combobox listbox. On machines running desktop-hook software (remote-desktop
    /// tools, overlay/input utilities) the native CB_SHOWDROPDOWN path burns ~90ms inside
    /// USER32 per open and per close - every dropdown in every application stutters there -
    /// while a managed popup opens and closes in ~3ms. The control keeps the flat, dark
    /// studio look and the same contract as the ComboBox it replaces: an Items collection,
    /// SelectedIndex/SelectedIndexChanged, arrow-key selection while focused.
    ///
    /// <para>With <see cref="Editable"/> set it also covers the ComboBox DropDown style: an
    /// embedded text box with prefix auto-suggestion from the items (the SuggestAppend the
    /// search dialogs use), committed with Enter or a click.</para>
    /// </summary>
    internal sealed class StudioDropdown : Control
    {
        private const int ArrowWidth = 16;

        private readonly ItemCollection _items;
        private int _selectedIndex = -1;
        private bool _hot;
        private ToolStripDropDown _popup;

        private TextBox _editor;
        private bool _editable;
        private int _updateCount;
        private bool _completing;
        private bool _suggesting;
        private int _highlightIndex = -1;

        public StudioDropdown()
        {
            _items = new ItemCollection(this);
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.StandardClick |
                ControlStyles.UserPaint, true);
            BackColor = StudioDesignSystem.Deck;
            ForeColor = StudioDesignSystem.Frost;
            Font = StudioDesignSystem.UtilityFont(8f);
            Size = new Size(126, 21);
            TabStop = true;
        }

        public event EventHandler SelectedIndexChanged;

        public ItemCollection Items
        {
            get { return _items; }
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (value < -1 || value >= _items.Count)
                {
                    throw new ArgumentOutOfRangeException("value");
                }
                if (_selectedIndex == value)
                {
                    return;
                }
                _selectedIndex = value;
                Invalidate();
                EventHandler handler = SelectedIndexChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        public bool DroppedDown
        {
            get { return _popup != null; }
        }

        /// <summary>ComboBox DropDown style: an embedded text box with item suggestions
        /// instead of a fixed pick list. Text/TextChanged/SelectAll forward to the editor.</summary>
        public bool Editable
        {
            get { return _editable; }
            set
            {
                if (_editable == value)
                {
                    return;
                }
                _editable = value;
                EnsureEditor();
                Invalidate();
            }
        }

        /// <summary>The picked label (DropDownList) or the editor content (Editable).</summary>
        public new string Text
        {
            get
            {
                if (_editable)
                {
                    return Editor.Text;
                }
                return _selectedIndex >= 0 && _selectedIndex < _items.Count
                    ? _items[_selectedIndex].ToString()
                    : string.Empty;
            }
            set
            {
                if (_editable)
                {
                    Editor.Text = value ?? string.Empty;
                    return;
                }
                for (int i = 0; i < _items.Count; i++)
                {
                    if (string.Equals(_items[i].ToString(), value, StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedIndex = i;
                        return;
                    }
                }
            }
        }

        public void SelectAll()
        {
            if (_editable)
            {
                Editor.Focus();
                Editor.SelectAll();
            }
        }

        /// <summary>Batches structural item changes; the repaint waits for
        /// <see cref="EndUpdate"/>.</summary>
        public void BeginUpdate()
        {
            _updateCount++;
        }

        public void EndUpdate()
        {
            if (_updateCount > 0)
            {
                _updateCount--;
            }
            if (_updateCount == 0)
            {
                Invalidate();
            }
        }

        private TextBox Editor
        {
            get
            {
                EnsureEditor();
                return _editor;
            }
        }

        private void EnsureEditor()
        {
            if (_editor != null)
            {
                return;
            }
            _editor = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = StudioDesignSystem.Deck,
                ForeColor = StudioDesignSystem.Frost,
                Font = Font
            };
            _editor.TextChanged += EditorTextChanged;
            _editor.KeyDown += EditorKeyDown;
            Controls.Add(_editor);
            LayoutEditor();
        }

        private void LayoutEditor()
        {
            if (_editor == null)
            {
                return;
            }
            int height = Math.Min(_editor.PreferredHeight, Height - 2);
            _editor.Bounds = new Rectangle(
                4, (Height - height) / 2, Width - ArrowWidth - 8, height);
        }

        /// <summary>Structural item change: keep the selection inside the new bounds and
        /// repaint, firing SelectedIndexChanged when the index actually moved.</summary>
        internal void OnItemsChanged()
        {
            if (_selectedIndex >= _items.Count)
            {
                SelectedIndex = _items.Count - 1;
            }
            else if (_updateCount == 0)
            {
                Invalidate();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutEditor();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hot = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hot = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }
            if (_editable && e.X < Width - ArrowWidth)
            {
                // Text area click: the embedded editor receives it and places the caret.
                return;
            }
            Focus();
            if (DroppedDown)
            {
                CloseDropdown();
            }
            else
            {
                OpenDropdown(null);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_editable)
            {
                return; // the embedded editor owns the keys
            }
            if (e.Alt && e.KeyCode == Keys.Down)
            {
                OpenDropdown(null);
                e.Handled = true;
                return;
            }
            if (e.KeyCode == Keys.Escape && DroppedDown)
            {
                CloseDropdown();
                e.Handled = true;
                return;
            }
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
            {
                if (_items.Count == 0)
                {
                    return;
                }
                int delta = e.KeyCode == Keys.Down ? 1 : -1;
                int next = _selectedIndex < 0
                    ? (delta > 0 ? 0 : _items.Count - 1)
                    : (_selectedIndex + delta + _items.Count) % _items.Count;
                SelectedIndex = next;
                e.Handled = true;
            }
        }

        private void EditorKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Alt && e.KeyCode == Keys.Down)
            {
                if (DroppedDown) { CloseDropdown(); } else { OpenDropdown(null); }
                e.Handled = true;
                return;
            }
            if (DroppedDown && _suggesting)
            {
                if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
                {
                    MoveHighlight(e.KeyCode == Keys.Down ? 1 : -1);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }
                if (e.KeyCode == Keys.Enter)
                {
                    CommitHighlight();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }
                if (e.KeyCode == Keys.Escape)
                {
                    CloseDropdown();
                    e.Handled = true;
                    return;
                }
            }
            else if (e.KeyCode == Keys.Down)
            {
                OpenDropdown(null);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void EditorTextChanged(object sender, EventArgs e)
        {
            if (!_completing)
            {
                ApplyAutoSuggest();
            }
            OnTextChanged(e);
            Invalidate();
        }

        /// <summary>SuggestAppend: extend the typed prefix to the first matching item and
        /// list every prefix match in the popup. Nothing happens on a zero-length or
        /// unmatched prefix - the popup simply stays closed.</summary>
        private void ApplyAutoSuggest()
        {
            if (!_editable)
            {
                return;
            }
            string prefix = Editor.Text;
            if (prefix.Length == 0)
            {
                CloseDropdown();
                return;
            }

            var matches = new List<int>();
            for (int i = 0; i < _items.Count; i++)
            {
                string candidate = _items[i].ToString();
                if (candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(i);
                }
            }
            if (matches.Count == 0)
            {
                CloseDropdown();
                return;
            }

            string first = _items[matches[0]].ToString();
            if (first.Length > prefix.Length && !string.Equals(first, prefix, StringComparison.Ordinal))
            {
                _completing = true;
                try
                {
                    Editor.Text = first;
                    Editor.Select(prefix.Length, first.Length - prefix.Length);
                }
                finally
                {
                    _completing = false;
                }
            }
            OpenDropdown(matches);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            if (_editable && _editor != null && !_editor.Focused)
            {
                _editor.Focus();
            }
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (_editor != null)
            {
                _editor.Font = Font;
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (var brush = new SolidBrush(StudioDesignSystem.Deck))
            {
                g.FillRectangle(brush, ClientRectangle);
            }

            Color border = !Enabled
                ? StudioDesignSystem.Border
                : _hot || DroppedDown || Focused || (_editor != null && _editor.Focused)
                    ? StudioDesignSystem.Hover
                    : StudioDesignSystem.Border;
            using (var pen = new Pen(border))
            {
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }

            Color textColor = Enabled ? ForeColor : StudioDesignSystem.Disabled;
            if (!_editable)
            {
                string label = _selectedIndex >= 0 && _selectedIndex < _items.Count
                    ? _items[_selectedIndex].ToString()
                    : string.Empty;
                var textBounds = new Rectangle(4, 0, Width - ArrowWidth - 8, Height);
                TextRenderer.DrawText(g, label, Font, textBounds, textColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            }

            int arrowCenterX = Width - ArrowWidth / 2;
            int arrowCenterY = Height / 2;
            var arrow = new[]
            {
                new Point(arrowCenterX - 4, arrowCenterY - 2),
                new Point(arrowCenterX + 4, arrowCenterY - 2),
                new Point(arrowCenterX, arrowCenterY + 3),
            };
            using (var brush = new SolidBrush(textColor))
            {
                g.FillPolygon(brush, arrow);
            }
        }

        /// <summary>The whole-catalogue popup (filter null) or the suggestion list for the
        /// typed prefix. Suggestion popups navigate with the arrows and commit with Enter;
        /// both close on click, Escape or an outside click.</summary>
        private void OpenDropdown(IList<int> filter)
        {
            int count = filter == null ? _items.Count : filter.Count;
            if (count == 0)
            {
                return;
            }
            CloseDropdown();

            var dropDown = new ToolStripDropDown
            {
                BackColor = StudioDesignSystem.Deck,
                ForeColor = StudioDesignSystem.Frost,
                Renderer = DarkPopupRenderer.Instance,
                Padding = new Padding(2),
                MaximumSize = new Size(0, 22 * 10 + 8)
            };

            int width = Width;
            for (int position = 0; position < count; position++)
            {
                int index = filter == null ? position : filter[position];
                string text = _items[index].ToString();
                Size textSize = TextRenderer.MeasureText(
                    text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine);
                width = Math.Max(width, textSize.Width + 28);

                var item = new ToolStripMenuItem(text)
                {
                    Tag = index,
                    Padding = new Padding(6, 2, 6, 2),
                    Margin = Padding.Empty,
                    AutoSize = true
                };
                if (filter == null)
                {
                    item.Checked = index == _selectedIndex;
                    int picked = index;
                    item.Click += delegate
                    {
                        SelectedIndex = picked;
                        CloseDropdown();
                    };
                }
                else
                {
                    if (position == 0)
                    {
                        item.Select();
                    }
                }
                dropDown.Items.Add(item);
            }
            dropDown.MinimumSize = new Size(width, 0);
            dropDown.Closed += delegate
            {
                if (_popup == dropDown)
                {
                    _popup = null;
                    _suggesting = false;
                    _highlightIndex = -1;
                    Invalidate();
                }
            };
            _popup = dropDown;
            _suggesting = filter != null;
            _highlightIndex = filter == null ? -1 : 0;
            Invalidate();
            dropDown.Show(this, new Point(0, Height));
        }

        private void MoveHighlight(int delta)
        {
            if (_popup == null || !_suggesting)
            {
                return;
            }
            int count = _popup.Items.Count;
            if (count == 0)
            {
                return;
            }
            int next = _highlightIndex < 0
                ? (delta > 0 ? 0 : count - 1)
                : (_highlightIndex + delta + count) % count;
            _highlightIndex = next;
            _popup.Items[next].Select();
        }

        private void CommitHighlight()
        {
            if (_popup == null || !_suggesting || _highlightIndex < 0 ||
                _highlightIndex >= _popup.Items.Count)
            {
                CloseDropdown();
                return;
            }
            int index = (int)_popup.Items[_highlightIndex].Tag;
            CommitItem(index);
        }

        private void CommitItem(int index)
        {
            _completing = true;
            try
            {
                string text = _items[index].ToString();
                Editor.Text = text;
                Editor.Select(text.Length, 0);
                SelectedIndex = index;
            }
            finally
            {
                _completing = false;
            }
            CloseDropdown();
        }

        private void CloseDropdown()
        {
            ToolStripDropDown dropDown = _popup;
            if (dropDown == null)
            {
                return;
            }
            _popup = null;
            _suggesting = false;
            _highlightIndex = -1;
            Invalidate();
            dropDown.Close();
        }

        /// <summary>The dark palette for the popup menu, matching the studio dropdowns it
        /// replaces: deck background, hover/selected highlight, frost text.</summary>
        private sealed class DarkPopupRenderer : ToolStripProfessionalRenderer
        {
            internal static readonly DarkPopupRenderer Instance = new DarkPopupRenderer();

            private DarkPopupRenderer()
            {
            }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using (var brush = new SolidBrush(StudioDesignSystem.Deck))
                {
                    e.Graphics.FillRectangle(brush, e.AffectedBounds);
                }
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var bounds = new Rectangle(Point.Empty, e.Item.Size);
                Color fill = e.Item.Selected || e.Item.Pressed
                    ? StudioDesignSystem.Hover
                    : StudioDesignSystem.Deck;
                using (var brush = new SolidBrush(fill))
                {
                    e.Graphics.FillRectangle(brush, bounds);
                }
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = StudioDesignSystem.Frost;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using (var pen = new Pen(StudioDesignSystem.Border))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0,
                        e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                }
            }
        }

        /// <summary>Object item list with the small mutation surface the dropdowns use
        /// (Add/AddRange/Clear/indexer). Structural changes clamp the selection and repaint.</summary>
        public sealed class ItemCollection : IList
        {
            private readonly StudioDropdown _owner;
            private readonly List<object> _items = new List<object>();

            internal ItemCollection(StudioDropdown owner)
            {
                _owner = owner;
            }

            public int Count
            {
                get { return _items.Count; }
            }

            public bool IsReadOnly
            {
                get { return false; }
            }

            public bool IsFixedSize
            {
                get { return false; }
            }

            public bool IsSynchronized
            {
                get { return false; }
            }

            public object SyncRoot
            {
                get { return this; }
            }

            public object this[int index]
            {
                get { return _items[index]; }
                set { _items[index] = value; _owner.Invalidate(); }
            }

            public int Add(object value)
            {
                int index = _items.Count;
                _items.Add(value);
                _owner.OnItemsChanged();
                return index;
            }

            public void AddRange(IEnumerable values)
            {
                foreach (object value in values)
                {
                    _items.Add(value);
                }
                _owner.OnItemsChanged();
            }

            public void Clear()
            {
                if (_items.Count == 0)
                {
                    return;
                }
                _items.Clear();
                _owner.OnItemsChanged();
            }

            public bool Contains(object value)
            {
                return _items.Contains(value);
            }

            public int IndexOf(object value)
            {
                return _items.IndexOf(value);
            }

            public void Insert(int index, object value)
            {
                _items.Insert(index, value);
                _owner.OnItemsChanged();
            }

            public void Remove(object value)
            {
                if (_items.Remove(value))
                {
                    _owner.OnItemsChanged();
                }
            }

            public void RemoveAt(int index)
            {
                _items.RemoveAt(index);
                _owner.OnItemsChanged();
            }

            public void CopyTo(Array array, int index)
            {
                ((ICollection)_items).CopyTo(array, index);
            }

            public IEnumerator GetEnumerator()
            {
                return _items.GetEnumerator();
            }
        }
    }
}
