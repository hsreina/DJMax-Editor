namespace DJMaxEditor.Undo.Action 
{
    /// <summary>
    /// Base UndoManager UndoRedo action
    /// </summary>
    public abstract class UndoRedoAction 
    {
        /// <summary>
        /// A cancel flag to cancel the event in some cases
        /// </summary>
        public bool Cancel { get;  protected set; } = false;

        /// <summary>
        /// Execute Undo action
        /// </summary>
        public abstract void Undo();

        /// <summary>
        /// Execure Redo action
        /// </summary>
        public abstract void Redo();

        /// <summary>
        /// Check if the action can be merge with this one
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public virtual bool CanMerge(UndoRedoAction action)
        {
            return false;
        }

        /// <summary>
        /// Merge the current action with the new one
        /// </summary>
        /// <param name="action"></param>
        public virtual void Merge(UndoRedoAction action)
        {
        }
    }
}
