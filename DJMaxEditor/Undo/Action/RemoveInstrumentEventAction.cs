using DJMaxEditor.DJMax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DJMaxEditor.Undo.Action
{
    class RemoveInstrumentEventAction : UndoRedoAction
    {
        public RemoveInstrumentEventAction(PlayerData playerData, InstrumentData instrumentData)
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
