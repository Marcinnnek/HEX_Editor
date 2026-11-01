namespace HexEditor.UI;

/// <summary>Coordinates the per-document confirmation sequence for an application close attempt.</summary>
public static class DirtyDocumentCloseWorkflow
{
    public static async Task<bool> ConfirmAllAsync(EditorDocumentWorkspace workspace,
        Func<EditorDocument, Task<bool>> confirmClose)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(confirmClose);
        foreach (var document in workspace.GetDirtyEditableDocuments())
            if (!await confirmClose(document)) return false;
        return true;
    }

    public static bool ConfirmAll(EditorDocumentWorkspace workspace, Func<EditorDocument, bool> confirmClose)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(confirmClose);
        foreach (var document in workspace.GetDirtyEditableDocuments())
            if (!confirmClose(document)) return false;
        return true;
    }
}
