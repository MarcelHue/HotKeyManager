using HotKeyManager.Models;
using HotKeyManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace HotKeyManager.Views;

/// <summary>
/// Drag and drop of hotkey rows: reorder within a folder (done by the ListView itself) or move
/// to another folder (done here). Holds the state of one running drag.
/// </summary>
internal sealed class HotkeyRowDragDrop(HotkeyListViewModel viewModel)
{
    /// <summary>Own clipboard format; no other app can do anything with it.</summary>
    private const string InternalDragFormat = "HotKeyManager/HotkeyRow";
    private const string HotkeyDragToken = "1";

    private HotkeyDefinition? draggedHotkey;
    private HotkeyGroup? sourceGroup;
    private bool crossGroupDropHandled;

    public void Start(HotkeyGroup? group, DragItemsStartingEventArgs e)
    {
        if (!viewModel.IsReorderEnabled)
        {
            e.Cancel = true;
            return;
        }

        this.draggedHotkey = e.Items.FirstOrDefault() as HotkeyDefinition;
        this.sourceGroup = group;
        this.crossGroupDropHandled = false;

        // Otherwise the ListView puts a text version of the row into the drag package. Dropped outside
        // the window, the other app (e.g. Firefox) would paste or search that text. So the text stays
        // empty and an own format, which only this app understands, carries the drag.
        e.Data.SetText(string.Empty);
        e.Data.SetData(InternalDragFormat, HotkeyDragToken);
        e.Data.RequestedOperation = DataPackageOperation.Move;
    }

    public void Over(DragEventArgs e)
    {
        if (this.draggedHotkey is not null)
        {
            e.AcceptedOperation = DataPackageOperation.Move;
            e.Handled = true;
        }
    }

    public void Drop(HotkeyGroup? targetGroup, DragEventArgs e)
    {
        // Within the same folder the ListView reorders by itself; Complete saves that order.
        if (this.draggedHotkey is not { } hotkey || targetGroup is null || targetGroup == this.sourceGroup)
        {
            return;
        }

        viewModel.MoveToGroup(hotkey, targetGroup);
        this.crossGroupDropHandled = true;
        this.Reset();
        e.Handled = true;
    }

    public void Complete()
    {
        if (!this.crossGroupDropHandled)
        {
            viewModel.ApplyOrderFromGroups();
        }

        this.Reset();
        this.crossGroupDropHandled = false;
    }

    private void Reset()
    {
        this.draggedHotkey = null;
        this.sourceGroup = null;
    }
}
