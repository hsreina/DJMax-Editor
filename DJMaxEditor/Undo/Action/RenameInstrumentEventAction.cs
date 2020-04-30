using DJMaxEditor.DJMax;

namespace DJMaxEditor.Undo.Action
{
    public class RenameInstrumentEventAction : UndoRedoAction
    {
        public RenameInstrumentEventAction(InstrumentData instrumentData, string newInstrumentName)
        {
            if (instrumentData == null)
            {
                Cancel = true;
            }

            m_instrumentData = instrumentData;
            m_oldInstrumentName = instrumentData.Name;
            m_newInstrumentName = newInstrumentName;
        }

        public override bool CanMerge(UndoRedoAction action)
        {
            return m_instrumentData.Name == m_newInstrumentName;
        }

        public override void Redo()
        {
            m_instrumentData.Name = m_newInstrumentName;
        }

        public override void Undo()
        {
            m_instrumentData.Name = m_oldInstrumentName;
        }

        private InstrumentData m_instrumentData;

        private string m_newInstrumentName;

        private string m_oldInstrumentName;
    }
}
