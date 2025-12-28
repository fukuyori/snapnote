using System.Windows;
using SnapNoteStudio.Models;

namespace SnapNoteStudio.Services;

public interface IUndoableAction
{
    void Execute();
    void Undo();
    string Description { get; }
}

public class AddAnnotationAction : IUndoableAction
{
    private readonly List<Annotation> _annotations;
    private readonly Annotation _annotation;

    public string Description => $"Add {_annotation.Type}";

    public AddAnnotationAction(List<Annotation> annotations, Annotation annotation)
    {
        _annotations = annotations;
        _annotation = annotation;
    }

    public void Execute()
    {
        if (!_annotations.Contains(_annotation))
            _annotations.Add(_annotation);
    }

    public void Undo()
    {
        _annotations.Remove(_annotation);
    }
}

public class RemoveAnnotationAction : IUndoableAction
{
    private readonly List<Annotation> _annotations;
    private readonly Annotation _annotation;
    private int _index;

    public string Description => $"Remove {_annotation.Type}";

    public RemoveAnnotationAction(List<Annotation> annotations, Annotation annotation)
    {
        _annotations = annotations;
        _annotation = annotation;
        _index = _annotations.IndexOf(annotation);
    }

    public void Execute()
    {
        _index = _annotations.IndexOf(_annotation);
        _annotations.Remove(_annotation);
    }

    public void Undo()
    {
        if (_index >= 0 && _index <= _annotations.Count)
            _annotations.Insert(_index, _annotation);
        else
            _annotations.Add(_annotation);
    }
}

public class MoveAnnotationAction : IUndoableAction
{
    private readonly Annotation _annotation;
    private readonly Vector _delta;

    public string Description => $"Move {_annotation.Type}";

    public MoveAnnotationAction(Annotation annotation, Vector delta)
    {
        _annotation = annotation;
        _delta = delta;
    }

    public void Execute()
    {
        _annotation.Move(_delta);
    }

    public void Undo()
    {
        _annotation.Move(-_delta);
    }
}

public class UndoRedoService
{
    private readonly Stack<IUndoableAction> _undoStack = new();
    private readonly Stack<IUndoableAction> _redoStack = new();

    public event EventHandler? StateChanged;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void Execute(IUndoableAction action)
    {
        action.Execute();
        _undoStack.Push(action);
        _redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (!CanUndo) return;

        var action = _undoStack.Pop();
        action.Undo();
        _redoStack.Push(action);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (!CanRedo) return;

        var action = _redoStack.Pop();
        action.Execute();
        _undoStack.Push(action);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
