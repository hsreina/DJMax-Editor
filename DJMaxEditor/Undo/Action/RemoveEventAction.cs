using DJMaxEditor.DJMax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DJMaxEditor.Undo.Action 
{
    /// <summary>
    /// Action to remove an event from player data
    /// </summary>
    public class RemoveEventAction : UndoRedoAction 
    {
        public RemoveEventAction(PlayerData playerData, EventData[] eventsList) 
        {
            if (eventsList == null || eventsList.Count() == 0 || playerData == null)
            {
                Cancel = true;
            }

            m_playerData = playerData;
            m_eventsList = eventsList;
        }

        public override void Undo() 
        {
            var playerDataTracks = m_playerData.Tracks;
            foreach (var enventData in m_eventsList)
            {
                var trackIndex = (int)enventData.TrackId;
                playerDataTracks.GetTrackForEvent(enventData)?.AddEvent(enventData);
            }
        }

        public override void Redo() 
        {
            var playerDataTracks = m_playerData.Tracks;
            foreach (var enventData in m_eventsList)
            {
                playerDataTracks.GetTrackForEvent(enventData)?.RemoveEvent(enventData);
            }
        }

        private readonly EventData[] m_eventsList;

        private readonly PlayerData m_playerData;
    }
}
