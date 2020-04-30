using System;
using DJMaxEditor.DJMax;

namespace DJMaxEditor.Undo.Action
{
    class AddInstrumentEventAction : UndoRedoAction
    {
        public AddInstrumentEventAction(PlayerData playerData, InstrumentData instrumentData)
        {
            if (playerData == null || instrumentData == null)
            {
                Cancel = true;
            }

            m_playerData = playerData;
            m_instrumentData = instrumentData;
        }

        public override void Redo()
        {
            throw new NotImplementedException();
        }

        public override void Undo()
        {
            throw new NotImplementedException();
        }

        private PlayerData m_playerData;

        private InstrumentData m_instrumentData;

    }
}
