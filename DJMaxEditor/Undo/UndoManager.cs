using System.Collections.Generic;
using DJMaxEditor.Undo.Action;
using DJMaxEditor.libs;

namespace DJMaxEditor 
{    
    /// <summary>
    /// Undo redo manager
    /// </summary>
    public class UndoManager : Singleton<UndoManager> 
    {
        /// <summary>
        /// Event called when an actions is done in UndoManager
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="action"></param>
        public delegate void OnUndoRedoDelegate(object sender, UndoManager.Action action);

        /// <summary>
        /// OnUndoRedo event
        /// </summary>
        public event OnUndoRedoDelegate OnUndoRedo;

        /// <summary>
        /// possible actions sent to the event
        /// </summary>
        public enum Action { 
            None, Exec, Undo, Redo
        }

        /// <summary>
        /// Check if undo can be done
        /// </summary>
        /// <returns></returns>
        public bool CanUndo => m_undoActions.Count > 0;

        /// <summary>
        /// Check if redo can be node
        /// </summary>
        /// <returns></returns>
        public bool CanRedo => m_redoActions.Count > 0;

        public UndoManager()
        {
            m_undoActions = new Stack<UndoRedoAction>();
            m_redoActions = new Stack<UndoRedoAction>();
        }

        /// <summary>
        /// Execute a new action and add/merge it in the Undo stack
        /// </summary>
        /// <param name="action"></param>
        public void ExecAction(UndoRedoAction action) 
        {
            if (action.Cancel)
            {
                return;
            }

            // Execute the redo action
            action.Redo();

            // Retrieve the last action in the list, and check if we can merge actions
            if (m_undoActions.Count > 0 && m_undoActions.Peek().CanMerge(action))
            {
                m_undoActions.Peek().Merge(action);
            }
            else
            {
                m_undoActions.Push(action);
            }

            if (CanRedo)
            {
                m_redoActions.Clear();
            }

            OnUndoRedo?.Invoke(this, UndoManager.Action.Exec);
        }

        /// <summary>
        /// If possible, Perform an undo action
        /// </summary>
        public void Undo() 
        {
            if (CanUndo)
            {
                m_undoActions.Peek().Undo();

                m_redoActions.Push(m_undoActions.Pop());

                OnUndoRedo?.Invoke(this, UndoManager.Action.Undo);
            }        
        }

        /// <summary>
        /// If possible, perform a redo action
        /// </summary>
        public void Redo() 
        {
            if (CanRedo)
            {
                m_redoActions.Peek().Redo();

                m_undoActions.Push(m_redoActions.Pop());

                OnUndoRedo?.Invoke(this, UndoManager.Action.Redo);
            }
        }

        /// <summary>
        /// List of undo actions
        /// </summary>
        private readonly Stack<UndoRedoAction> m_undoActions;

        /// <summary>
        /// List of redo actions
        /// </summary>
        private readonly Stack<UndoRedoAction> m_redoActions;
    }
}
