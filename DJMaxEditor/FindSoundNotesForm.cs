using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DJMaxEditor.DJMax;
using DJMaxEditor.Editor;
using DJMaxEditor.UI;

namespace DJMaxEditor
{
    /// <summary>
    /// Modeless Ctrl+F dialog: finds every note referencing a sound (instrument)
    /// file name and jumps the editor surface to the chosen match. The document
    /// is reached through delegates so the dialog stays correct across document
    /// switches while it is open.
    /// </summary>
    public sealed class FindSoundNotesForm : Form
    {
        private readonly Func<PlayerData> _getModel;
        private readonly Func<int> _getCurrentVirtualTick;
        private readonly Action<EventData> _locateNote;

        private readonly StudioDropdown _search;
        private readonly DataGridView _results;
        private readonly Label _status;
        private readonly Button _findNext;
        private readonly Button _findPrevious;
        private readonly Button _closeButton;

        private List<EventData> _matches = new List<EventData>();
        private int _lastMatchIndex = -1;

        public FindSoundNotesForm(
            Func<PlayerData> getModel,
            Func<int> getCurrentVirtualTick,
            Action<EventData> locateNote)
        {
            if (getModel == null) throw new ArgumentNullException("getModel");
            if (getCurrentVirtualTick == null) throw new ArgumentNullException("getCurrentVirtualTick");
            if (locateNote == null) throw new ArgumentNullException("locateNote");

            _getModel = getModel;
            _getCurrentVirtualTick = getCurrentVirtualTick;
            _locateNote = locateNote;

            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(560, 420);
            MinimumSize = new Size(480, 320);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Find Notes by Sound";

            var searchLabel = new Label
            {
                AutoSize = true,
                Location = new Point(0, 3),
                Text = "Sound file name:"
            };
            _search = new StudioDropdown
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                Editable = true,
                Location = new Point(0, 22),
                Width = 536
            };
            var searchHost = new Panel { Dock = DockStyle.Fill };
            searchHost.Controls.Add(searchLabel);
            searchHost.Controls.Add(_search);

            _results = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                Dock = DockStyle.Fill,
                MultiSelect = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            var tickColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Tick",
                FillWeight = 20,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            var trackColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Track",
                FillWeight = 30,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            var soundColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "Sound",
                FillWeight = 50,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            _results.Columns.AddRange(tickColumn, trackColumn, soundColumn);

            _status = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _closeButton = new Button
            {
                DialogResult = DialogResult.Cancel,
                Dock = DockStyle.Right,
                Text = "Close",
                Width = 90
            };
            _findNext = new Button
            {
                Dock = DockStyle.Right,
                Text = "Find Next",
                Width = 90
            };
            _findPrevious = new Button
            {
                Dock = DockStyle.Right,
                Text = "Find Previous",
                Width = 100
            };
            var bottomHost = new Panel { Dock = DockStyle.Fill };
            bottomHost.Controls.Add(_status);
            bottomHost.Controls.Add(_closeButton);
            bottomHost.Controls.Add(_findNext);
            bottomHost.Controls.Add(_findPrevious);

            var root = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                RowCount = 3
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            root.Controls.Add(searchHost, 0, 0);
            root.Controls.Add(_results, 0, 1);
            root.Controls.Add(bottomHost, 0, 2);
            Controls.Add(root);

            AcceptButton = _findNext;
            CancelButton = _closeButton;

            _search.TextChanged += delegate { RefreshMatches(); };
            _findNext.Click += delegate { FindNext(); };
            _findPrevious.Click += delegate { FindPrevious(); };
            _closeButton.Click += delegate { Close(); };
            _results.CellDoubleClick += delegate (object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0)
                {
                    LocateMatch(e.RowIndex);
                }
            };
            _results.KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && _results.CurrentRow != null)
                {
                    LocateMatch(_results.CurrentRow.Index);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            Activated += delegate
            {
                RefreshInstruments();
                RefreshMatches();
            };
            Shown += delegate
            {
                _search.Focus();
                _search.SelectAll();
            };

            StudioTheme.ApplyToForm(this);
            RefreshInstruments();
            RefreshMatches();
        }

        private void RefreshInstruments()
        {
            var model = _getModel();
            var names = model == null
                ? new string[0]
                : model.Instruments
                    .Where(instrument => !string.IsNullOrEmpty(instrument.Name))
                    .Select(instrument => instrument.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            string current = _search.Text;
            _search.BeginUpdate();
            _search.Items.Clear();
            _search.Items.AddRange(names);
            _search.EndUpdate();
            _search.Text = current;
        }

        private void RefreshMatches()
        {
            _matches = SoundNoteSearch.FindNotesByInstrumentName(_getModel(), _search.Text);
            _lastMatchIndex = -1;

            var model = _getModel();
            _results.Rows.Clear();
            foreach (var ev in _matches)
            {
                var track = model == null ? null : model.Tracks.GetTrackAtIndex(ev.TrackId);
                string trackName = track == null ? ev.TrackId.ToString() : track.DisplayedTrackName;
                _results.Rows.Add(ev.Tick.ToString(), trackName, ev.Instrument.Name);
            }

            if (_matches.Count == 0)
            {
                _status.Text = string.IsNullOrWhiteSpace(_search.Text)
                    ? "Type a sound file name to search."
                    : "No notes reference this sound.";
            }
            else
            {
                _status.Text = _matches.Count + " matching note(s)";
            }
        }

        private void FindNext()
        {
            if (_matches.Count == 0)
            {
                _status.Text = "No matches.";
                return;
            }

            int next = _lastMatchIndex >= 0
                ? (_lastMatchIndex + 1) % _matches.Count
                : SoundNoteSearch.FindNextIndex(_matches, CurrentVirtualTick());
            LocateMatch(next);
        }

        private void FindPrevious()
        {
            if (_matches.Count == 0)
            {
                _status.Text = "No matches.";
                return;
            }

            int previous = _lastMatchIndex >= 0
                ? (_lastMatchIndex - 1 + _matches.Count) % _matches.Count
                : SoundNoteSearch.FindPreviousIndex(_matches, CurrentVirtualTick());
            LocateMatch(previous);
        }

        private int CurrentVirtualTick()
        {
            try
            {
                return _getCurrentVirtualTick();
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private void LocateMatch(int index)
        {
            if (index < 0 || index >= _matches.Count)
            {
                return;
            }

            _lastMatchIndex = index;
            var ev = _matches[index];
            _locateNote(ev);

            if (index < _results.Rows.Count)
            {
                _results.ClearSelection();
                _results.Rows[index].Selected = true;
                _results.CurrentCell = _results.Rows[index].Cells[0];
                _results.FirstDisplayedScrollingRowIndex = Math.Max(0, index - 3);
            }

            _status.Text = string.Format(
                "Match {0} of {1} — tick {2}",
                index + 1,
                _matches.Count,
                ev.Tick);
        }
    }
}
