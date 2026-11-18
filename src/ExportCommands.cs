using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ArkBoard
{
    public sealed partial class MainWindow
    {
        readonly List<MenuItem> selectionFileCommands = new List<MenuItem>();
        void AddSelectionFileCommands(MenuItem file)
        {
            foreach (var command in new[] {
                MenuAction("Save Selected", "", SaveSelected),
                MenuAction("Export Canvas", "", ExportCanvas),
                MenuAction("Export Images", "", ExportImages) })
            { command.IsEnabled = false; selectionFileCommands.Add(command); file.Items.Add(command); }
        }
        void RefreshSelectionFileCommands(bool any, bool anyImages)
        {
            foreach (MenuItem item in selectionFileCommands)
                item.IsEnabled = !busy && !saving && (Equals(item.Tag, "Export Images") ? anyImages : any);
        }
        bool PrepareSelectionExport(bool imagesOnly)
        {
            if (busy || saving || checkingUpdates) return false;
            CommitText(); Board.FinishGesture();
            if (Document.Selection.Any(i => !imagesOnly || !i.IsText)) return true;
            DarkDialog.ShowNotice(this, "ArkBoard", Localization.T(imagesOnly ? "Select images first." : "Select objects first."));
            return false;
        }
        async void SaveSelected()
        {
            if (!PrepareSelectionExport(false)) return;
            var dialog = new SaveFileDialog { Title = Localization.T("Save Selected"), Filter = "ArkBoard project|*.arkboard",
                DefaultExt = ".arkboard", AddExtension = true, FileName = "Selection.arkboard" };
            if (dialog.ShowDialog(this) != true) return;
            saving = true; ShowActivity("Saving selection...", 0);
            var progress = new Progress<double>(value => saveProgress.Value = value * 100);
            try
            {
                await Document.SaveSelectedAsync(dialog.FileName, progress);
                AddRecentProject(dialog.FileName); SetStatus("Selection saved");
            }
            catch (Exception ex) { Error("Unable to save selection", ex); }
            finally { saving = false; HideActivity(); Board.Focus(); }
        }
        async void ExportCanvas()
        {
            if (!PrepareSelectionExport(false)) return;
            try
            {
                var settings = DarkDialog.ConfigureCanvasExport(this, SelectionExport.Bounds(Document.Selection));
                if (settings == null) return;
                var dialog = new SaveFileDialog { Title = Localization.T("Export Canvas"), Filter = "PNG image|*.png",
                    DefaultExt = ".png", AddExtension = true, FileName = "Canvas.png" };
                if (dialog.ShowDialog(this) != true) return;
                ExportDrawing snapshot = SelectionExport.Capture(Document, Document.Selection);
                await RunExport(() => SelectionExport.SavePng(dialog.FileName,
                    SelectionExport.Render(snapshot, settings.Width, settings.Height, settings.Transparent), true), "Canvas exported");
            }
            catch (Exception ex) { Error("Unable to export images", ex); }
        }
        async void ExportImages()
        {
            if (!PrepareSelectionExport(true)) return;
            try
            {
                string folder = FolderPicker.Show(this);
                if (folder == null) return;
                List<ExportDrawing> snapshots = Document.Selection.Where(i => !i.IsText)
                    .Select(i => SelectionExport.Capture(Document, new[] { i })).ToList();
                var progress = new Progress<double>(value => saveProgress.Value = value * 100);
                await RunExport(() =>
                {
                    int done = 0;
                    foreach (ExportDrawing snapshot in snapshots)
                    {
                        SizeToPng(snapshot, folder);
                        ((IProgress<double>)progress).Report((double)++done / snapshots.Count);
                    }
                }, "Images exported");
            }
            catch (Exception ex) { Error("Unable to export images", ex); }
        }
        static void SizeToPng(ExportDrawing snapshot, string folder)
        {
            System.Windows.Size size = SelectionExport.DefaultSize(snapshot.Bounds, snapshot.PixelScale);
            SelectionExport.SavePng(SelectionExport.AvailableImagePath(folder, snapshot.Name),
                SelectionExport.Render(snapshot, (int)size.Width, (int)size.Height, true), false);
        }
        async Task RunExport(Action action, string success)
        {
            saving = true; ShowActivity("Exporting images...", 0); saveProgress.IsIndeterminate = true;
            try { await SelectionExport.RunAsync(action); SetStatus(success); }
            finally { saving = false; saveProgress.IsIndeterminate = false; HideActivity(); Board.Focus(); }
        }
    }
}
