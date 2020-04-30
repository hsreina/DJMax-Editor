using DJMaxEditor.DJMax;
using System.Collections.Generic;

namespace DJMaxEditor.Undo.Action 
{
    /// <summary>
    /// Action for move eventData
    /// </summary>
    public class MoveEventAction : UndoRedoAction 
    {
        public MoveEventAction(PlayerData playerData, List<EventData> eventsList, int trackDelta, int positionDelta) 
        {
            if (eventsList.Count == 0)
            {
                Cancel = true;
            }

            m_playerData = playerData;
            m_eventsList = new List<EventData>();
            m_eventsList.AddRange(eventsList);
            m_trackDelta = trackDelta;
            m_positionDelta = positionDelta;
        }

        public override bool CanMerge(UndoRedoAction action) 
        {
            return SameItems(action);
        }

        public override void Merge(UndoRedoAction action) 
        {
            var oveEventAction = action as MoveEventAction;
            if (oveEventAction == null)
            {
                return;
            }
            m_trackDelta += oveEventAction.m_trackDelta;
            m_positionDelta += oveEventAction.m_positionDelta;
        }

        public override void Undo() 
        {
            MoveEvents(-m_trackDelta, -m_positionDelta);
        }

        public override void Redo() 
        {
            MoveEvents(m_trackDelta, m_positionDelta);
        }

        /// <summary>
        /// List of events to work with
        /// </summary>
        private readonly List<EventData> m_eventsList;

        /// <summary>
        /// Instance of PlayerData
        /// </summary>
        private readonly PlayerData m_playerData;

        private int m_trackDelta;

        private int m_positionDelta;

        private bool AreListsEqual(List<EventData> a, List<EventData> b) 
        {
            if (a == null || b == null)
            {
                return false;
            }

            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0, l = a.Count; i < l; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        private bool SameItems(UndoRedoAction action) 
        {
            return action is MoveEventAction && AreListsEqual((action as MoveEventAction).m_eventsList, m_eventsList);
        }

        private void MoveEvents(int trackDelta, int posDelta) 
        {
            var playerDatatracks = m_playerData.Tracks;

            foreach (EventData eventData in m_eventsList)
            {
                if (posDelta != 0)
                {
                    if (eventData.VirtualTick == 0 && posDelta < 0)
                    {
                        return;
                    }

                    eventData.VirtualTick += posDelta;
                }

                if (trackDelta != 0)
                {
                    if (eventData.TrackId == 0 && trackDelta < 0)
                    {
                        return;
                    }

                    if ((eventData.TrackId == (playerDatatracks.Count - 1)) && trackDelta > 0)
                    {
                        return;
                    }

                    var currentTrack = playerDatatracks.GetTrackAtIndex(eventData.TrackId);
                    var newTrack = playerDatatracks.GetTrackAtIndex((uint)(eventData.TrackId + trackDelta));
                    currentTrack.RemoveEvent(eventData);
                    newTrack.AddEvent(eventData);
                }
            }
        }
    }
}
