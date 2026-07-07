namespace ScreenPaste.Editor;

/// <summary>A minimal undo/redo stack of reversible edit commands.</summary>
public sealed class EditHistory
{
    public readonly record struct Command(Action Undo, Action Redo);

    private readonly Stack<Command> _undo = new();
    private readonly Stack<Command> _redo = new();

    public event Action? Changed;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Record an already-applied command.</summary>
    public void Push(Action undo, Action redo)
    {
        _undo.Push(new Command(undo, redo));
        _redo.Clear();
        Changed?.Invoke();
    }

    /// <summary>Drop all history (used when returning to the selection step).</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke();
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        var cmd = _undo.Pop();
        cmd.Undo();
        _redo.Push(cmd);
        Changed?.Invoke();
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        var cmd = _redo.Pop();
        cmd.Redo();
        _undo.Push(cmd);
        Changed?.Invoke();
    }
}
