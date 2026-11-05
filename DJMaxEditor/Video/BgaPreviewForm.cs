using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using DJMaxEditor.DJMax;
using DJMaxEditor.Editor;
using DJMaxEditor.Files.Tech;
using DJMaxEditor.UI;
using WeifenLuo.WinFormsUI.Docking;

namespace DJMaxEditor.Video
{
    /// <summary>
    /// Dockable BGA video preview. Synchronises frames to the chart playhead via
    /// <see cref="BgaClockMap"/> rather than the player's wall-clock accumulator.
    /// </summary>
    public sealed class BgaPreviewForm : ToolWindow
    {
        private readonly BgaPreviewControl _preview;
        private readonly Label _status;
        private readonly Button _loadButton;
        private readonly Button _clearButton;
        private readonly Timer _syncTimer;
        private readonly BgaClockMap _clock = new BgaClockMap();

        private EditorDocumentContext _document;
        private Func<int> _playheadVirtualTick;
        private string _bgaPath;
        private int _attachGeneration;
        private int _lastSyncedVirtualTick = int.MinValue;

        public BgaPreviewForm()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = StudioDesignSystem.Void;
            ClientSize = new Size(420, 320);
            MinimumSize = new Size(240, 180);
            ShowHint = DockState.DockRight;
            TabText = "BGA Preview";
            Text = "BGA Preview";

            var header = new Panel
            {
                BackColor = StudioDesignSystem.Deck,
                Dock = DockStyle.Top,
                Height = 56,
                Padding = new Padding(10, 8, 10, 8)
            };

            var title = new Label
            {
                AutoSize = true,
                Font = StudioDesignSystem.DisplayFont(10f),
                ForeColor = StudioDesignSystem.Frost,
                Location = new Point(10, 6),
                Text = "BGA"
            };

            _status = new Label
            {
                AutoEllipsis = true,
                Font = StudioDesignSystem.UtilityFont(8f),
                ForeColor = StudioDesignSystem.Muted,
                Location = new Point(10, 28),
                Size = new Size(280, 20),
                Text = "NO VIDEO LOADED"
            };

            _loadButton = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Font = StudioDesignSystem.UtilityFont(8f),
                ForeColor = StudioDesignSystem.Frost,
                Location = new Point(300, 10),
                Size = new Size(52, 24),
                Text = "Load",
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _loadButton.FlatAppearance.BorderColor = StudioDesignSystem.Border;
            _loadButton.Click += OnLoadClicked;

            _clearButton = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Font = StudioDesignSystem.UtilityFont(8f),
                ForeColor = StudioDesignSystem.Frost,
                Location = new Point(356, 10),
                Size = new Size(52, 24),
                Text = "Clear",
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _clearButton.FlatAppearance.BorderColor = StudioDesignSystem.Border;
            _clearButton.Click += delegate { ResetAttachment(); };

            header.Controls.Add(title);
            header.Controls.Add(_status);
            header.Controls.Add(_loadButton);
            header.Controls.Add(_clearButton);
            header.Resize += delegate
            {
                _status.Width = Math.Max(80, header.ClientSize.Width - 140);
                _clearButton.Left = header.ClientSize.Width - _clearButton.Width - 10;
                _loadButton.Left = _clearButton.Left - _loadButton.Width - 4;
            };

            _preview = new BgaPreviewControl();
            Controls.Add(_preview);
            Controls.Add(header);

            _syncTimer = new Timer { Interval = 33 };
            _syncTimer.Tick += OnSyncTick;
            VisibleChanged += delegate
            {
                if (Visible)
                {
                    _syncTimer.Start();
                    SyncNow();
                }
                else
                {
                    _syncTimer.Stop();
                }
            };
        }

        /// <summary>
        /// Binds the active document and a playhead provider (typically the editor surface).
        /// </summary>
        public void Bind(EditorDocumentContext document, Func<int> playheadVirtualTick)
        {
            _document = document;
            _playheadVirtualTick = playheadVirtualTick;
            _lastSyncedVirtualTick = int.MinValue;

            ResetAttachment();

            if (document == null || document.Model == null)
            {
                _clock.Clear();
                return;
            }

            PlayerData model = document.Model;
            _clock.Load(model);
            _clock.Offset = ResolveBgaOffset(model, _clock);

            string discovered = DiscoverPath(document);
            if (!string.IsNullOrEmpty(discovered))
            {
                _bgaPath = discovered;
                _ = AttachAsync(_bgaPath);
            }
        }

        public void SyncNow()
        {
            if (!Visible || IsDisposed || _preview.Source == null || !_preview.Source.IsOpen)
            {
                return;
            }

            int virtualTick = ReadPlayhead();
            _lastSyncedVirtualTick = virtualTick;
            _preview.Seek(_clock.TimeForVirtualTick(virtualTick));
        }

        public void RefreshPlayback()
        {
            if (!Visible || IsDisposed)
            {
                return;
            }

            int virtualTick = ReadPlayhead();
            if (virtualTick == _lastSyncedVirtualTick &&
                _preview.Source != null && _preview.Source.IsOpen)
            {
                // Still push during playback so sub-frame timing stays live even when the
                // integer virtual tick has not advanced yet.
            }
            _lastSyncedVirtualTick = virtualTick;
            if (_preview.Source != null && _preview.Source.IsOpen)
            {
                _preview.Seek(_clock.TimeForVirtualTick(virtualTick));
            }
        }

        private void OnSyncTick(object sender, EventArgs e)
        {
            if (!Visible || IsDisposed)
            {
                return;
            }

            int virtualTick = ReadPlayhead();
            if (virtualTick == _lastSyncedVirtualTick)
            {
                return;
            }
            _lastSyncedVirtualTick = virtualTick;

            if (_preview.Source != null && _preview.Source.IsOpen)
            {
                _preview.Seek(_clock.TimeForVirtualTick(virtualTick));
            }
        }

        private int ReadPlayhead()
        {
            if (_playheadVirtualTick != null)
            {
                try
                {
                    return _playheadVirtualTick();
                }
                catch (Exception)
                {
                }
            }

            if (_document != null && _document.Model != null)
            {
                return _document.Model.VirtualCurrentTick;
            }

            return 0;
        }

        private void OnLoadClicked(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter =
                    "Video|*.mp4;*.wmv;*.avi;*.mkv;*.mov;*.bik|" +
                    "Technika preview (Bink)|*.bik;*.bik2;*.bk2|" +
                    "All files|*.*";
                dialog.Title = "Load BGA video";
                if (!string.IsNullOrEmpty(_bgaPath))
                {
                    try
                    {
                        dialog.InitialDirectory = Path.GetDirectoryName(_bgaPath);
                    }
                    catch (Exception)
                    {
                    }
                }
                else if (_document != null && !string.IsNullOrEmpty(_document.SourcePath))
                {
                    try
                    {
                        dialog.InitialDirectory = Path.GetDirectoryName(_document.SourcePath);
                    }
                    catch (Exception)
                    {
                    }
                }

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                _bgaPath = dialog.FileName;
                _ = AttachAsync(_bgaPath);
            }
        }

        private void ResetAttachment()
        {
            _attachGeneration++;
            _bgaPath = null;
            _preview.Clear();
            _status.Text = "NO VIDEO LOADED";
            _status.ForeColor = StudioDesignSystem.Muted;
        }

        private async Task AttachAsync(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            int generation = ++_attachGeneration;
            string playable = path;
            bool converted = false;

            if (BgaSourceResolver.RequiresTranscode(path))
            {
                _status.Text = "Converting " + Path.GetFileName(path) + "...";
                _status.ForeColor = StudioDesignSystem.Muted;

                string resolved = null;
                string failure = null;
                Stopwatch clock = Stopwatch.StartNew();
                await Task.Run(() =>
                {
                    if (!BgaSourceResolver.TryResolve(path, out resolved, out failure))
                    {
                        resolved = null;
                    }
                }).ConfigureAwait(true);

                if (generation != _attachGeneration)
                {
                    return;
                }

                if (resolved == null)
                {
                    _status.Text = failure ?? "conversion failed";
                    _status.ForeColor = Color.Salmon;
                    Logs.Write("BGA: transcode failed: {0} for {1}", failure, path);
                    return;
                }

                converted = !string.Equals(resolved, path, StringComparison.OrdinalIgnoreCase);
                Logs.Write(
                    "BGA: transcoded {0} -> {1} in {2}ms",
                    Path.GetFileName(path), resolved, clock.ElapsedMilliseconds);
                playable = resolved;
            }

            if (generation != _attachGeneration)
            {
                return;
            }

            string error;
            if (!_preview.TryOpen(playable, out error))
            {
                _status.Text = error;
                _status.ForeColor = Color.Salmon;
                Logs.Write("BGA: open failed: {0} for {1}", error, playable);
                return;
            }

            _status.Text = converted
                ? _preview.StatusText + " - converted"
                : _preview.StatusText;
            _status.ForeColor = StudioDesignSystem.Frost;
            SyncNow();
        }

        private static string DiscoverPath(EditorDocumentContext document)
        {
            if (document == null)
            {
                return null;
            }

            PlayerData model = document.Model;
            TechMetadata tech = model != null ? model.TechMetadata : null;
            if (tech != null && !string.IsNullOrEmpty(tech.Bga) &&
                !string.IsNullOrEmpty(document.SourcePath))
            {
                try
                {
                    string folder = Path.GetDirectoryName(document.SourcePath);
                    string candidate = Path.Combine(folder ?? string.Empty, tech.Bga);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (Exception)
                {
                }
            }

            return BgaSourceResolver.FindForChart(document.SourcePath);
        }

        private static TimeSpan ResolveBgaOffset(PlayerData model, BgaClockMap clock)
        {
            if (clock != null && clock.HasVideoStartMarker)
            {
                return TimeSpan.Zero;
            }

            if (model == null || model.TechMetadata == null)
            {
                return TimeSpan.Zero;
            }

            double seconds = model.TechMetadata.BgaOffset;
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds == 0.0)
            {
                return TimeSpan.Zero;
            }

            // TimeForVirtualTick adds Offset; positive bgaOffset must delay → negate.
            return TimeSpan.FromSeconds(-seconds);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _syncTimer.Stop();
                _syncTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
