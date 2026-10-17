using System;
using System.Collections.Generic;

namespace Wick.Game
{
    /// <summary>Undo/redo by whole snapshots: vault drafts are tiny, so this is simplest and safest.</summary>
    public sealed class UndoStack<T>
    {
        private readonly Func<T, T> _clone;
        private readonly int _limit;
        private readonly List<T> _undo = new List<T>();
        private readonly List<T> _redo = new List<T>();

        public UndoStack(Func<T, T> clone, int limit = 100)
        {
            _clone = clone;
            _limit = limit;
        }

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        /// <summary>Call with the state as it was *before* a change.</summary>
        public void Record(T before)
        {
            _undo.Add(_clone(before));
            if (_undo.Count > _limit)
            {
                _undo.RemoveAt(0);
            }

            _redo.Clear();
        }

        public T Undo(T current)
        {
            T previous = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            _redo.Add(_clone(current));
            return previous;
        }

        public T Redo(T current)
        {
            T next = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            _undo.Add(_clone(current));
            return next;
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }
    }
}
