using DJMaxEditor.DJMax;
using System.Collections.Generic;

namespace DJMaxEditor.Undo.Action 
{
    /// <summary>
    /// Action to add an event to player data
    /// </summary>
    public class AddEventAction : UndoRedoAction 
    {
        public AddEventAction(PlayerData playerData, List<EventData> eventsList) 
        {
            if (eventsList == null || eventsList.Count == 0 || playerData == null)
            {
                Cancel = true;
            }

            m_playerData = playerData;
            m_eventsList = new List<EventData>();
            m_eventsList.AddRange(eventsList);
        }

        public override void Undo() 
        {
            foreach (EventData enventData in m_eventsList)
            {
                TrackData track = m_playerData.Tracks.GetTrackAtIndex(enventData.TrackId);
                if (track == null)
                {
                    continue;
                }
                track.RemoveEvent(enventData);
            }
        }

        public override void Redo() 
        {
            foreach (EventData enventData in m_eventsList)
            {
                TrackData track = m_playerData.Tracks.GetTrackAtIndex(enventData.TrackId);
                if (track == null)
                {
                    continue;
                }

                track.AddEvent(enventData);
            }
        }

        private readonly List<EventData> m_eventsList;

        private readonly PlayerData m_playerData;
    }
}
