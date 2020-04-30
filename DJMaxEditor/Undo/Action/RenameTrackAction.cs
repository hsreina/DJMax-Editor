using DJMaxEditor.DJMax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DJMaxEditor.Undo.Action
{
    class RenameTrackAction : UndoRedoAction
    {
        public RenameTrackAction(TrackData trackData, string newTrackName)
        {
            if (trackData == null)
            {
                Cancel = true;
            }

            m_trackData = trackData;
            m_oldTrackName = trackData.TrackName;
            m_newTrackName = newTrackName;
        }

        public override bool CanMerge(UndoRedoAction action)
        {
            return m_trackData.TrackName == m_newTrackName;
        }

        public override void Merge(UndoRedoAction action)
        {
        }

        public override void Redo()
        {
            m_trackData.TrackName = m_newTrackName;
        }

        public override void Undo()
        {
            m_trackData.TrackName = m_oldTrackName;
        }

        private readonly TrackData m_trackData;

        private readonly string m_newTrackName;

        private readonly string m_oldTrackName;
    }
}
