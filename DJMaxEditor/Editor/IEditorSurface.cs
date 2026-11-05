using System.Windows.Forms;

namespace DJMaxEditor.Editor
{
    public interface IEditorSurface
    {
        Control View { get; }

        bool SupportsEditing { get; }

        void Bind(EditorDocumentContext document);

        void InvalidateView();

        bool TrySetTimeZoom(float zoom);

        EditorViewState CaptureViewState();

        void RestoreViewState(EditorViewState state);

        int PlayheadVirtualTick { get; set; }

        /// <summary>
        /// The playhead position in virtual ticks at sub-tick precision. The integer
        /// <see cref="PlayheadVirtualTick"/> only moves at the sequencer's whole-tick
        /// rate, which is below the display's refresh rate at low tempos; a surface that
        /// wants pixel-smooth motion reads this instead. Surfaces without sub-tick
        /// support round it and behave exactly as before.
        /// </summary>
        double PlayheadPositionVirtualTick { get; set; }

        /// <summary>
        /// Scrolls the surface so the given virtual tick (and, when the surface
        /// has per-track lanes, the given track lane) becomes visible.
        /// </summary>
        void RevealPosition(int virtualTick, int trackIndex);
    }
}
