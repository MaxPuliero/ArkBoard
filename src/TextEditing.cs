using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArkBoard
{
    public sealed partial class MainWindow
    {
        Button textToolButton;
        Canvas textOverlay;
        TextBox textEditor;
        Point textOrigin;
        double draftFontSize;
        internal TextBox ActiveTextEditor { get { return textEditor; } }

        void InitializeTextEditing()
        {
            Board.TextPlacementRequested += StartTextEditor;
            Board.ViewChanged += PositionTextEditor;
            Board.SizeChanged += delegate { PositionTextEditor(); };
            PreviewMouseDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (textEditor == null || textEditor.IsMouseOver) return;
                bool canvasClick = Board.IsMouseOver;
                CommitText();
                if (canvasClick) e.Handled = true;
            };
            PreviewMouseWheel += delegate { if (textEditor != null) CommitText(); };
        }
        internal void ActivateTextTool()
        {
            if (busy) return;
            CommitText(); Document.Selected.Clear(); Document.Notify();
            Board.ArmTextTool(); textToolButton.Background = Brush("#505050");
            SetStatus("Text tool · Drag to choose font size, release, then type · Esc to exit");
        }
        internal void StartTextEditor(Point origin, double fontSize)
        {
            textOrigin = origin; draftFontSize = Math.Max(1, Math.Min(Math.Min(8192, 512 / Document.Zoom), fontSize));
            Board.TextToolArmed = false; Board.TextInputActive = true;
            textEditor = new TextBox { FontFamily = new FontFamily("Segoe UI"), Foreground = TextLayout.Foreground,
                CaretBrush = TextLayout.Foreground, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Padding = new Thickness(0), Margin = new Thickness(0), AcceptsReturn = true, AcceptsTab = false,
                TextWrapping = TextWrapping.NoWrap, MaxLength = 10000, FocusVisualStyle = null,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
            System.Windows.Automation.AutomationProperties.SetName(textEditor, "Canvas text editor");
            textEditor.TextChanged += delegate { PositionTextEditor(); };
            textOverlay.Children.Add(textEditor); PositionTextEditor();
            textEditor.Focus(); Keyboard.Focus(textEditor); textEditor.CaretIndex = 0;
            textToolButton.Background = Brush("#505050"); Board.InvalidateVisual();
            SetStatus("Type text · Enter: new line · Esc or click outside: confirm");
        }
        void PositionTextEditor()
        {
            if (textEditor == null) return;
            Point screen = Board.ToScreen(textOrigin);
            double font = draftFontSize * Document.Zoom;
            textEditor.FontSize = Math.Max(1, Math.Min(8192, font));
            Size content = TextLayout.Measure(textEditor.Text, textEditor.FontSize);
            textEditor.Width = Math.Max(20, Math.Min(Math.Max(font * 2, content.Width + font), Math.Max(20, Board.ActualWidth - screen.X)));
            textEditor.Height = Math.Max(font * 1.5, Math.Min(content.Height + 8, Math.Max(font * 1.5, Board.ActualHeight - screen.Y)));
            Canvas.SetLeft(textEditor, screen.X); Canvas.SetTop(textEditor, screen.Y);
        }
        internal ImageItem CommitText()
        {
            bool wasActive = textEditor != null || Board.TextToolArmed;
            ImageItem result = null;
            if (textEditor != null)
            {
                string value = textEditor.Text;
                textOverlay.Children.Remove(textEditor); textEditor = null;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    Document.Change(() =>
                    {
                        result = Document.AddText(value, draftFontSize, textOrigin);
                        Document.Selected.Clear(); Document.Selected.Add(result.Id);
                    });
                }
            }
            if (wasActive)
            {
                Board.CancelTextTool(); textToolButton.Background = Brush("#323232"); Board.Focus();
                SetStatus(result == null ? "Text tool closed" : "Text added · Drag to move · Ctrl+S to save");
            }
            return result;
        }
        bool HandleTextToolKey(KeyEventArgs e)
        {
            if (!busy && e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
            { if (!e.IsRepeat) ActivateTextTool(); e.Handled = true; return true; }
            if (e.Key == Key.Escape && (textEditor != null || Board.TextToolArmed))
            { CommitText(); e.Handled = true; return true; }
            if (textEditor != null && e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            { Save((Keyboard.Modifiers & ModifierKeys.Shift) != 0); e.Handled = true; return true; }
            return false;
        }
    }
}
