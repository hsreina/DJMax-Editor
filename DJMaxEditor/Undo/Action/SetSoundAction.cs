using DJMaxEditor.DJMax;

namespace DJMaxEditor.Undo.Action 
{

    class SetSoundAction : UndoRedoAction 
    {

        public SetSoundAction(EventData eventData, InstrumentData instrumentData) 
        {
            if (null == eventData || null == instrumentData) {
                Cancel = true;
            }

            m_eventData = eventData;
            m_nextInstrumentData = instrumentData;
            m_previousInstrumentData = m_eventData.Instrument;
        }

        public override void Undo() 
        {
            m_eventData.Instrument = m_previousInstrumentData;
        }

        public override void Redo() 
        {
            m_eventData.Instrument = m_nextInstrumentData;
        }

        public override bool CanMerge(UndoRedoAction action) 
        {
            return action is SetSoundAction && AreItemsTheSame(action);;
        }

        public override void Merge(UndoRedoAction action) 
        {
            SetSoundAction setSoundAction = (action as SetSoundAction);
            if (null == setSoundAction)
            {
                return;
            }

            m_nextInstrumentData = 
                m_eventData.Instrument =
                setSoundAction.m_nextInstrumentData;            
        }

        private readonly EventData m_eventData;

        private InstrumentData m_nextInstrumentData;

        private readonly InstrumentData m_previousInstrumentData;

        private bool AreItemsTheSame(UndoRedoAction action) 
        {
            return ((action as SetSoundAction)?.m_eventData == m_eventData);
        }
    }
}
