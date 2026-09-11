using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;

namespace ArkBoard
{
    public sealed partial class MainWindow : Window
    {
        public readonly BoardDocument Document = new BoardDocument();
        public readonly BoardSurface Board;
        public readonly Grid Root = new Grid();
        TextBlock status, count, zoom, selectionTitle, imageName, imageInfo, emptyInspector;
        StackPanel properties;
        Border inspector;
        ColumnDefinition inspectorColumn;
        Slider opacitySlider;
        WindowTransparency transparency;
        TextBlock opacityValue;
        TextBox rotationBox, scaleBox;
        Button flipXButton, flipYButton;
        Button removeMaskButton;
        StackPanel rotationSection, flipSection;
        MenuItem undoMenuItem, redoMenuItem;
        MenuItem flipXContextItem, flipYContextItem, rotateContextItem, resetRotationContextItem;
        MenuItem topmostItem, gridItem;
        MenuItem autoSortingItem;
        bool busy;
        bool testMode;
        readonly Brush panel = Brush("#232323");
        readonly Brush text = Brush("#ECECEC");
        readonly Brush secondary = Brush("#9B9B9B");
        public MainWindow(bool testing)
        {
            testMode = testing;
            Title = "ArkBoard"; Width = 1280; Height = 820; MinWidth = 900; MinHeight = 600;
            using (Stream icon = typeof(MainWindow).Assembly.GetManifestResourceStream("ArkBoard.AppIcon"))
            {
                var frame = System.Windows.Media.Imaging.BitmapFrame.Create(icon,
                    System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                frame.Freeze(); Icon = frame;
            }
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = panel; Foreground = text; FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
            ApplyStyles();
            Board = new BoardSurface(Document);
            Content = Root; Root.Background = panel;
            Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            BuildMenu(); BuildWorkspace(); BuildStatus(); BuildContextMenu();
            InitializeTextEditing();
            Document.Changed += Refresh;
            Board.ViewChanged += RefreshView;
            Board.PreviewDragOver += OnDragOver;
            Board.Drop += OnDrop;
            PreviewKeyDown += OnKey;
            PreviewKeyUp += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Space) { Board.SpaceDown = false; Board.Cursor = Cursors.Arrow; } };
            Deactivated += delegate { Board.SpaceDown = false; Board.FinishGesture(); };
            Closing += delegate(object s, System.ComponentModel.CancelEventArgs e) { if (!testMode && (busy || !ConfirmDiscard())) e.Cancel = true; };
            SourceInitialized += delegate
            {
                try { int dark = 1; DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, 4); } catch { }
                transparency = new WindowTransparency(HwndSource.FromHwnd(new WindowInteropHelper(this).Handle));
                ApplyWindowOpacity();
            };
            Refresh();
        }
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        static Brush Brush(string color) { return (Brush)new BrushConverter().ConvertFromString(color); }
        void ApplyStyles()
        {
            Resources = (ResourceDictionary)XamlReader.Parse(@"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
 <SolidColorBrush x:Key='{x:Static SystemColors.MenuBrushKey}' Color='#292929'/>
 <SolidColorBrush x:Key='{x:Static SystemColors.MenuTextBrushKey}' Color='#ECECEC'/>
 <SolidColorBrush x:Key='{x:Static SystemColors.HighlightBrushKey}' Color='#4B4B4B'/>
 <SolidColorBrush x:Key='{x:Static SystemColors.HighlightTextBrushKey}' Color='#FFFFFF'/>
 <Style TargetType='Button'>
  <Setter Property='Foreground' Value='#ECECEC'/><Setter Property='Background' Value='#323232'/>
  <Setter Property='BorderBrush' Value='#414141'/><Setter Property='BorderThickness' Value='1'/>
  <Setter Property='Padding' Value='10,3'/><Setter Property='Margin' Value='0,0,6,0'/><Setter Property='Cursor' Value='Hand'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
   <Border x:Name='b' CornerRadius='0' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'>
    <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
   </Border><ControlTemplate.Triggers>
    <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#444444'/></Trigger>
    <Trigger Property='IsPressed' Value='True'><Setter TargetName='b' Property='Background' Value='#5B5B5B'/></Trigger>
    <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.35'/></Trigger>
    <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='#A9A9A9'/></Trigger>
   </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='TextBox'>
  <Setter Property='Background' Value='#191919'/><Setter Property='Foreground' Value='#ECECEC'/>
  <Setter Property='CaretBrush' Value='#FFFFFF'/><Setter Property='BorderBrush' Value='#484848'/>
  <Setter Property='Padding' Value='8,4'/><Setter Property='SelectionBrush' Value='#676767'/>
 </Style>
 <Style TargetType='Menu'><Setter Property='Background' Value='#232323'/><Setter Property='Foreground' Value='#ECECEC'/></Style>
 <Style TargetType='Separator'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Separator'><Border Height='1' Background='#484848' Margin='7,5'/></ControlTemplate></Setter.Value></Setter></Style>
 <Style TargetType='MenuItem'>
  <Setter Property='Foreground' Value='#ECECEC'/><Setter Property='Padding' Value='10,6'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='MenuItem'>
   <Grid><Border x:Name='itemBorder' Background='Transparent' CornerRadius='0' Padding='{TemplateBinding Padding}'>
    <Grid><Grid.ColumnDefinitions><ColumnDefinition Width='20'/><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
     <TextBlock x:Name='check' Text='✓' Visibility='Hidden' VerticalAlignment='Center'/>
     <ContentPresenter Grid.Column='1' ContentSource='Header' RecognizesAccessKey='True' VerticalAlignment='Center'/>
     <TextBlock x:Name='shortcut' Grid.Column='2' Text='{TemplateBinding InputGestureText}' Foreground='#9B9B9B' Margin='28,0,0,0' VerticalAlignment='Center'/>
    </Grid>
   </Border>
   <Popup x:Name='PART_Popup' IsOpen='{TemplateBinding IsSubmenuOpen}' Placement='Right' HorizontalOffset='-1' AllowsTransparency='True' Focusable='False' PopupAnimation='Fade'>
    <Border Background='#292929' BorderBrush='#484848' BorderThickness='1' CornerRadius='0' Padding='4'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Cycle'/></Border>
   </Popup></Grid>
   <ControlTemplate.Triggers>
    <Trigger Property='Role' Value='TopLevelHeader'><Setter TargetName='PART_Popup' Property='Placement' Value='Bottom'/><Setter TargetName='check' Property='Width' Value='0'/><Setter TargetName='shortcut' Property='Visibility' Value='Collapsed'/></Trigger>
    <Trigger Property='IsHighlighted' Value='True'><Setter TargetName='itemBorder' Property='Background' Value='#444444'/></Trigger>
    <Trigger Property='IsSubmenuOpen' Value='True'><Setter TargetName='itemBorder' Property='Background' Value='#444444'/></Trigger>
    <Trigger Property='IsChecked' Value='True'><Setter TargetName='check' Property='Visibility' Value='Visible'/></Trigger>
    <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.35'/></Trigger>
   </ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='ScrollBar'>
  <Setter Property='Width' Value='10'/><Setter Property='Background' Value='#232323'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'>
   <Grid Background='{TemplateBinding Background}'><Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='Vertical'>
    <Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
    <Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='#545454' CornerRadius='0' Margin='2'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
    <Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
   </Track></Grid>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='ContextMenu'>
  <Setter Property='Background' Value='#292929'/><Setter Property='Foreground' Value='#ECECEC'/><Setter Property='BorderBrush' Value='#484848'/>
  <Setter Property='BorderThickness' Value='1'/><Setter Property='Padding' Value='4'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ContextMenu'>
   <Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}' SnapsToDevicePixels='True'>
    <ScrollViewer CanContentScroll='True' VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled'>
     <ItemsPresenter KeyboardNavigation.DirectionalNavigation='Cycle'/>
    </ScrollViewer>
   </Border>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='ToolTip'><Setter Property='Background' Value='#363636'/><Setter Property='Foreground' Value='#FFFFFF'/></Style>
 <Style TargetType='Slider'>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Slider'>
   <Grid Height='22' Background='Transparent'>
    <Border Height='2' Background='#656565' VerticalAlignment='Center' Margin='5,0'/>
    <Track x:Name='PART_Track' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}' Orientation='Horizontal'>
     <Track.DecreaseRepeatButton><RepeatButton Command='Slider.DecreaseLarge' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
     <Track.Thumb><Thumb Width='9' Height='16'><Thumb.Template><ControlTemplate TargetType='Thumb'><Border x:Name='thumb' Background='#C6C6C6' BorderBrush='#EEEEEE' BorderThickness='1'/><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='thumb' Property='Background' Value='#FFFFFF'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
     <Track.IncreaseRepeatButton><RepeatButton Command='Slider.IncreaseLarge' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
    </Track>
   </Grid><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocused' Value='True'><Setter Property='Background' Value='#393939'/></Trigger></ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
</ResourceDictionary>");
        }
        Separator MenuSeparator() { return new Separator { Style = (Style)Resources[typeof(Separator)] }; }
        MenuItem MenuAction(string title, string shortcut, Action action)
        {
            MenuItem item = new MenuItem { Header = title, InputGestureText = shortcut };
            item.Click += delegate { if (!busy) action(); }; return item;
        }
        void BuildMenu()
        {
            DockPanel bar = new DockPanel { Margin = new Thickness(12, 5, 12, 3) };
            TextBlock brand = new TextBlock { Text = "ARKBOARD", FontWeight = FontWeights.SemiBold, FontSize = 12,
                Foreground = Brush("#C2C2C2"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 24, 0) };
            bar.Children.Add(brand);
            Menu menu = new Menu(); bar.Children.Add(menu);
            MenuItem file = new MenuItem { Header = "_File" }; menu.Items.Add(file);
            file.Items.Add(MenuAction("New Project", "Ctrl+N", NewProject));
            file.Items.Add(MenuAction("Open Project...", "Ctrl+O", OpenDialog));
            file.Items.Add(MenuAction("Save", "Ctrl+S", () => Save(false)));
            file.Items.Add(MenuAction("Save As...", "Ctrl+Shift+S", () => Save(true)));
            file.Items.Add(MenuSeparator());
            file.Items.Add(MenuAction("Import Images...", "Ctrl+I", ImportDialog));
            file.Items.Add(MenuSeparator()); file.Items.Add(MenuAction("Exit", "Alt+F4", Close));
            MenuItem edit = new MenuItem { Header = "_Edit" }; menu.Items.Add(edit);
            edit.Items.Add(MenuAction("Undo", "Ctrl+Z", Document.Undo));
            edit.Items.Add(MenuAction("Redo", "Ctrl+Y", Document.Redo));
            edit.Items.Add(MenuSeparator());
            edit.Items.Add(MenuAction("Copy Image", "Ctrl+C", CopyImage));
            edit.Items.Add(MenuAction("Paste", "Ctrl+V", Paste));
            edit.Items.Add(MenuAction("Duplicate Selection", "Ctrl+D", Duplicate));
            edit.Items.Add(MenuAction("Select / Deselect All", "A", ToggleSelectAll));
            edit.Items.Add(MenuAction("Normalize Size", "Ctrl+A", NormalizeSelected));
            edit.Items.Add(MenuAction("Pack Images", "Ctrl+P", PackImages));
            edit.Items.Add(MenuAction("Delete Selection", "Del", DeleteSelection));
            MenuItem view = new MenuItem { Header = "_View" }; menu.Items.Add(view);
            view.Items.Add(MenuAction("Fit All", "F", () => Board.Fit(false)));
            view.Items.Add(MenuAction("Fit Selection", "Shift+F", () => Board.Fit(true)));
            view.Items.Add(MenuAction("Zoom 100%", "1", () => Board.ZoomAt(new Point(Board.ActualWidth / 2, Board.ActualHeight / 2), 1)));
            view.Items.Add(MenuAction("Opacity 100%", "Ctrl+Shift+0", () => SetWindowOpacity(100)));
            gridItem = new MenuItem { Header = "Grid", IsCheckable = true, IsChecked = true };
            gridItem.Click += delegate { Board.ShowGrid = gridItem.IsChecked; Board.InvalidateVisual(); }; view.Items.Add(gridItem);
            topmostItem = new MenuItem { Header = "Always on Top", IsCheckable = true };
            topmostItem.Click += delegate { Topmost = topmostItem.IsChecked; }; view.Items.Add(topmostItem);
            MenuItem settings = new MenuItem { Header = "_Settings" }; menu.Items.Add(settings);
            autoSortingItem = new MenuItem { Header = "Auto-Sorting", IsCheckable = true, IsChecked = true };
            autoSortingItem.Click += delegate
            {
                Board.AutoSorting = autoSortingItem.IsChecked;
                SetStatus("Auto-sorting " + (Board.AutoSorting ? "enabled" : "disabled"));
            };
            settings.Items.Add(autoSortingItem);
            MenuItem help = new MenuItem { Header = "_Help" }; menu.Items.Add(help);
            help.Items.Add(MenuAction("Controls and About", "F1", Help));
            Root.Children.Add(bar);
        }
        Button Button(string label, Action action, string tooltip)
        {
            Button b = new Button { Content = label, ToolTip = tooltip };
            b.Click += delegate { if (!busy) { action(); Board.Focus(); } }; return b;
        }
        TextBlock Label(string value, double size, Brush color)
        { return new TextBlock { Text = value, FontSize = size, Foreground = color, TextWrapping = TextWrapping.Wrap }; }
        void BuildWorkspace()
        {
            Grid area = new Grid(); Grid.SetRow(area, 1); Root.Children.Add(area);
            area.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inspectorColumn = new ColumnDefinition { Width = new GridLength(0) };
            area.ColumnDefinitions.Add(inspectorColumn);
            area.Children.Add(Board);
            textOverlay = new Canvas { ClipToBounds = true };
            Panel.SetZIndex(textOverlay, 10); area.Children.Add(textOverlay);
            textToolButton = Button("Aa", ActivateTextTool, "Add text · Drag to set font size · Ctrl+T");
            textToolButton.FontSize = 16; textToolButton.Padding = new Thickness(8, 0, 8, 0);
            textToolButton.Width = 38; textToolButton.Height = 28;
            textToolButton.HorizontalAlignment = HorizontalAlignment.Left;
            textToolButton.VerticalAlignment = VerticalAlignment.Top;
            textToolButton.Margin = new Thickness(12);
            textToolButton.Background = Brush("#323232"); textToolButton.Foreground = Brush("#B0B0B0");
            System.Windows.Automation.AutomationProperties.SetName(textToolButton, "Text tool");
            Panel.SetZIndex(textToolButton, 20); area.Children.Add(textToolButton);
            inspector = new Border { Background = panel, BorderBrush = Brush("#393939"), BorderThickness = new Thickness(1, 0, 0, 0), Padding = new Thickness(18) };
            Grid.SetColumn(inspector, 1); area.Children.Add(inspector);
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            inspector.Child = scroll;
            StackPanel side = new StackPanel(); scroll.Content = side;
            selectionTitle = Label("SELECTION", 11, secondary); selectionTitle.FontWeight = FontWeights.SemiBold; side.Children.Add(selectionTitle);
            emptyInspector = Label("Select an image to edit it.", 14, text); emptyInspector.Margin = new Thickness(0, 18, 0, 12); side.Children.Add(emptyInspector);
            properties = new StackPanel { Margin = new Thickness(0, 18, 0, 0) }; side.Children.Add(properties);
            imageName = Label("", 16, text); imageName.FontWeight = FontWeights.SemiBold; properties.Children.Add(imageName);
            imageInfo = Label("", 12, secondary); imageInfo.Margin = new Thickness(0, 7, 0, 22); properties.Children.Add(imageInfo);
            rotationSection = new StackPanel(); properties.Children.Add(rotationSection);
            rotationSection.Children.Add(Label("Rotation · degrees", 12, secondary));
            rotationBox = new TextBox { Margin = new Thickness(0, 7, 0, 9), ToolTip = "Enter an angle and press Enter" };
            rotationBox.KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Enter) { ApplyRotation(); e.Handled = true; } };
            rotationSection.Children.Add(rotationBox);
            StackPanel rotations = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 19) };
            rotations.Children.Add(Button("−90°", () => Rotate(-90), "Rotate left"));
            rotations.Children.Add(Button("+90°", () => Rotate(90), "Rotate right")); rotationSection.Children.Add(rotations);
            Button resetRotation = Button("Reset Rotation", ResetRotation, "Reset all selected images to 0°");
            resetRotation.Margin = new Thickness(0, -11, 6, 18); rotationSection.Children.Add(resetRotation);
            properties.Children.Add(Label("Scale · % of original", 12, secondary));
            scaleBox = new TextBox { Margin = new Thickness(0, 7, 0, 9), ToolTip = "Proportional scale: enter a percentage and press Enter" };
            scaleBox.KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Enter) { ApplyScale(); e.Handled = true; } };
            properties.Children.Add(scaleBox);
            properties.Children.Add(Button("Original Size", ResetSize, "Restore original width and height"));
            removeMaskButton = Button("Remove Mask", RemoveMask, "Restore the full area of the selected masked images");
            removeMaskButton.Margin = new Thickness(0, 7, 6, 0); properties.Children.Add(removeMaskButton);
            properties.Children.Add(new Border { Height = 7 });
            properties.Children.Add(Button("Normalize Size", NormalizeSelected, "Match the average longest side of the selected images · Ctrl+A"));
            properties.Children.Add(new Border { Height = 7 });
            properties.Children.Add(Button("Pack Images", PackImages, "Arrange selected images compactly without overlaps · Ctrl+P"));
            properties.Children.Add(new Border { Height = 21 });
            flipSection = new StackPanel(); properties.Children.Add(flipSection);
            flipSection.Children.Add(Label("Flip", 12, secondary));
            StackPanel flips = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 20) };
            flipXButton = Button("↔  X", () => Flip(true), "Flip horizontally · H");
            flipYButton = Button("↕  Y", () => Flip(false), "Flip vertically · V");
            flips.Children.Add(flipXButton); flips.Children.Add(flipYButton); flipSection.Children.Add(flips);
            properties.Children.Add(Button("Bring to Front", () => Reorder(true), "Bring selected images to front · ]"));
            properties.Children.Add(new Border { Height = 7 });
            properties.Children.Add(Button("Send to Back", () => Reorder(false), "Send selected images to back · ["));
            properties.Children.Add(new Border { Height = 18 });
            StackPanel operations = new StackPanel { Orientation = Orientation.Horizontal };
            operations.Children.Add(Button("Duplicate", Duplicate, "Duplicate · Ctrl+D"));
            operations.Children.Add(Button("Delete", DeleteSelection, "Delete · Del")); properties.Children.Add(operations);
            side.Children.Add(new Border { Height = 30 });
            side.Children.Add(new Border { Height = 1, Background = Brush("#393939"), Margin = new Thickness(0, 0, 0, 20) });
            side.Children.Add(Label("QUICK CONTROLS", 11, secondary));
            TextBlock tips = Label("Wheel     Zoom at cursor\nSpace + drag     Pan canvas\nMiddle drag     Pan canvas\nCtrl + click     Multi-select\nDrag empty space     Select\nCorners     Proportional resize\nShift + image edge     Mask\nShift + click masked     Adjust mask\nShift + drag masked     Move mask\nDouble-click text     Edit text\nCircle handle     Rotate images\nShift     Snap rotation to 15°\nCtrl+A     Normalize size\nCtrl+P     Pack images\nA     Select / deselect all\nF     Fit all", 12, secondary);
            tips.LineHeight = 23; tips.Margin = new Thickness(0, 10, 0, 0); side.Children.Add(tips);
        }
        void BuildStatus()
        {
            DockPanel bar = new DockPanel { Background = Brush("#1F1F1F"), LastChildFill = true, Margin = new Thickness(0) };
            Grid.SetRow(bar, 2); Root.Children.Add(bar);
            zoom = Label("100%", 12, text); zoom.VerticalAlignment = VerticalAlignment.Center; zoom.Margin = new Thickness(16, 0, 20, 0); DockPanel.SetDock(zoom, Dock.Right); bar.Children.Add(zoom);
            count = Label("", 12, secondary); count.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(count, Dock.Right); bar.Children.Add(count);
            StackPanel opacityControls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 20, 0) };
            DockPanel.SetDock(opacityControls, Dock.Right); bar.Children.Add(opacityControls);
            TextBlock opacityLabel = Label("Opacity", 12, secondary); opacityLabel.VerticalAlignment = VerticalAlignment.Center; opacityControls.Children.Add(opacityLabel);
            opacitySlider = new Slider { Minimum = 5, Maximum = 100, Value = 100, SmallChange = 1, LargeChange = 5,
                TickFrequency = 1, IsSnapToTickEnabled = true, IsMoveToPointEnabled = true, Width = 110, Margin = new Thickness(10, 0, 8, 0),
                ToolTip = "Window opacity, including images · 5% to 100%" };
            System.Windows.Automation.AutomationProperties.SetName(opacitySlider, "Window opacity");
            opacityControls.Children.Add(opacitySlider);
            opacityValue = Label("100%", 12, text); opacityValue.Width = 36; opacityValue.VerticalAlignment = VerticalAlignment.Center; opacityControls.Children.Add(opacityValue);
            Button opacityReset = Button("100%", () => SetWindowOpacity(100), "Reset opacity · Ctrl+Shift+0");
            opacityReset.FontSize = 11; opacityReset.Padding = new Thickness(6, 1, 6, 1); opacityReset.Margin = new Thickness(6, 0, 0, 0); opacityControls.Children.Add(opacityReset);
            opacitySlider.ValueChanged += delegate { ApplyWindowOpacity(); };
            status = Label("Ready · Drop an image to get started", 12, secondary); status.TextWrapping = TextWrapping.NoWrap; status.TextTrimming = TextTrimming.CharacterEllipsis;
            status.Margin = new Thickness(20, 0, 14, 0); status.VerticalAlignment = VerticalAlignment.Center; bar.Children.Add(status);
        }
        internal void SetWindowOpacity(double percent)
        {
            if (!BoardDocument.Finite(percent)) return;
            opacitySlider.Value = Math.Max(5, Math.Min(100, percent));
        }
        void ApplyWindowOpacity()
        {
            opacityValue.Text = opacitySlider.Value.ToString("0") + "%";
            try { if (transparency != null) transparency.Apply(opacitySlider.Value); }
            catch (Exception ex) { if (testMode) throw; SetStatus("Opacity unavailable: " + ex.Message); }
        }
        void BuildContextMenu()
        {
            ContextMenu menu = new ContextMenu { Resources = Resources };
            menu.Items.Add(MenuAction("Import Images...", "Ctrl+I", ImportDialog));
            menu.Items.Add(MenuAction("Add Text", "Ctrl+T", ActivateTextTool));
            menu.Items.Add(MenuAction("Open Project...", "Ctrl+O", OpenDialog));
            menu.Items.Add(MenuAction("Save Project", "Ctrl+S", () => Save(false)));
            menu.Items.Add(MenuSeparator());
            undoMenuItem = MenuAction("Undo", "Ctrl+Z", Document.Undo);
            redoMenuItem = MenuAction("Redo", "Ctrl+Y", Document.Redo);
            menu.Items.Add(undoMenuItem); menu.Items.Add(redoMenuItem);
            menu.Items.Add(MenuSeparator());
            menu.Items.Add(MenuAction("Paste", "Ctrl+V", Paste)); menu.Items.Add(MenuSeparator());
            menu.Items.Add(MenuAction("Duplicate", "Ctrl+D", Duplicate));
            menu.Items.Add(MenuAction("Normalize Size", "Ctrl+A", NormalizeSelected));
            menu.Items.Add(MenuAction("Pack Images", "Ctrl+P", PackImages));
            flipXContextItem = MenuAction("Flip Horizontally", "H", () => Flip(true)); menu.Items.Add(flipXContextItem);
            flipYContextItem = MenuAction("Flip Vertically", "V", () => Flip(false)); menu.Items.Add(flipYContextItem);
            rotateContextItem = MenuAction("Rotate 90°", "", () => Rotate(90)); menu.Items.Add(rotateContextItem);
            resetRotationContextItem = MenuAction("Reset Rotation", "", ResetRotation); menu.Items.Add(resetRotationContextItem);
            menu.Items.Add(MenuAction("Delete", "Del", DeleteSelection)); menu.Items.Add(MenuSeparator());
            menu.Items.Add(MenuAction("Fit All", "F", () => Board.Fit(false))); Board.ContextMenu = menu;
        }
        void RefreshView() { zoom.Text = (Document.Zoom * 100).ToString("0.#", CultureInfo.CurrentCulture) + "%"; }
        void Refresh()
        {
            Title = (Document.Dirty ? "● " : "") + (Document.Path == null ? "Untitled" : Path.GetFileName(Document.Path)) + " — ArkBoard";
            int textCount = Document.Items.Count(i => i.IsText);
            count.Text = (Document.Items.Count - textCount) + " images" + (textCount > 0 ? " · " + textCount + " texts" : "") + "  ·  " + Document.Selected.Count + " selected";
            undoMenuItem.IsEnabled = Document.CanUndo; redoMenuItem.IsEnabled = Document.CanRedo;
            var items = Document.Selection.ToList(); bool any = items.Count > 0;
            bool anyImages = items.Any(x => !x.IsText);
            bool anyMasks = items.Any(x => x.HasMask);
            rotationSection.Visibility = anyImages ? Visibility.Visible : Visibility.Collapsed;
            flipSection.Visibility = anyImages ? Visibility.Visible : Visibility.Collapsed;
            removeMaskButton.Visibility = anyMasks ? Visibility.Visible : Visibility.Collapsed;
            foreach (MenuItem item in new[] { flipXContextItem, flipYContextItem, rotateContextItem, resetRotationContextItem })
                item.Visibility = anyImages ? Visibility.Visible : Visibility.Collapsed;
            Visibility panelVisibility = any ? Visibility.Visible : Visibility.Collapsed;
            if (inspector.Visibility != panelVisibility || inspectorColumn.Width.Value != (any ? 240 : 0))
            {
                inspector.Visibility = panelVisibility;
                inspectorColumn.Width = new GridLength(any ? 240 : 0);
                // Finish layout before Fit or a drag reads canvas coordinates.
                Root.UpdateLayout();
            }
            properties.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            emptyInspector.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
            selectionTitle.Text = any ? "SELECTION / " + items.Count : "SELECTION";
            if (any)
            {
                ImageItem i = items[0]; Size original = OriginalSize(i);
                imageName.Text = items.Count == 1 ? (i.IsText ? "Text" : i.Name ?? "Image") : items.Count + " objects";
                if (items.Count > 1) imageInfo.Text = "Transforms apply to the selection";
                else if (i.IsText) imageInfo.Text = "Segoe UI · " + (i.FontSize * i.Width / original.Width).ToString("0.#") + " canvas units\nText object";
                else { AssetData a = Document.Assets[i.Asset]; imageInfo.Text = a.Bitmap.PixelWidth + " × " + a.Bitmap.PixelHeight + " px  ·  " + (a.Bytes.Length / 1024.0).ToString("N0") + " KB\nEmbedded image" + (i.HasMask ? " · Masked" : ""); }
                if (!rotationBox.IsKeyboardFocused) rotationBox.Text = items.Count == 1 ? i.Rotation.ToString("0.##") : "";
                if (!scaleBox.IsKeyboardFocused) scaleBox.Text = items.Count == 1 ? (100 * i.Width / original.Width).ToString("0.##") : "";
                flipXButton.Background = items.All(x => x.FlipX) ? Brush("#484848") : Brush("#323232");
                flipYButton.Background = items.All(x => x.FlipY) ? Brush("#484848") : Brush("#323232");
            }
            RefreshView();
        }
        public void SetStatus(string value) { status.Text = value; }
        void EditSelection(Action<ImageItem> action)
        { if (!Document.Selection.Any()) return; Board.FinishGesture(); Document.Change(() => { foreach (ImageItem i in Document.Selection) action(i); }); }
        void EditImageSelection(Action<ImageItem> action)
        {
            ImageItem[] images = Document.Selection.Where(i => !i.IsText).ToArray();
            if (images.Length == 0) return;
            Board.FinishGesture(); Document.Change(() => { foreach (ImageItem i in images) action(i); });
        }
        internal void Flip(bool x) { EditImageSelection(i => { if (x) i.FlipX = !i.FlipX; else i.FlipY = !i.FlipY; }); }
        internal void Rotate(double amount) { EditImageSelection(i => i.Rotation = BoardSurface.NormalizeAngle(i.Rotation + amount)); }
        internal void ResetRotation() { EditImageSelection(i => i.Rotation = 0); }
        void ApplyRotation()
        {
            double value;
            if (!Number(rotationBox.Text, out value)) { SetStatus("Enter a valid numeric angle."); return; }
            Board.Focus(); EditImageSelection(i => i.Rotation = BoardSurface.NormalizeAngle(value));
        }
        static bool Number(string s, out double value)
        { return (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out value) || double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) && BoardDocument.Finite(value); }
        void ApplyScale()
        {
            double value;
            if (!Number(scaleBox.Text, out value) || value < .1 || value > 10000) { SetStatus("Scale must be between 0.1% and 10,000%."); return; }
            Board.Focus(); EditSelection(i => { Size s = OriginalSize(i); i.Width = s.Width * value / 100; i.Height = s.Height * value / 100; });
        }
        Size OriginalSize(ImageItem i) { if (i.IsText) return TextLayout.Measure(i); AssetData a = Document.Assets[i.Asset]; return new Size(a.Bitmap.PixelWidth, a.Bitmap.PixelHeight); }
        void ResetSize() { EditSelection(i => { Size s = OriginalSize(i); i.Width = s.Width; i.Height = s.Height; }); }
        internal void RemoveMask()
        {
            ImageItem[] masked = Document.Selection.Where(i => i.HasMask).ToArray(); if (masked.Length == 0) return;
            Board.FinishGesture(); Document.Change(() =>
            {
                foreach (ImageItem i in masked) i.MaskLeft = i.MaskTop = i.MaskRight = i.MaskBottom = 0;
            });
            SetStatus(masked.Length == 1 ? "Mask removed" : "Masks removed");
        }
        void ToggleSelectAll()
        {
            if (Document.Items.Count == 0) return;
            if (Document.Items.All(i => Document.Selected.Contains(i.Id))) Document.Selected.Clear();
            else Document.Selected.UnionWith(Document.Items.Select(i => i.Id));
            Document.Notify();
        }
        void DeleteSelection()
        {
            if (!Document.Selection.Any()) return;
            Board.FinishGesture(); Document.Change(() => { Document.Items.RemoveAll(i => Document.Selected.Contains(i.Id)); Document.Selected.Clear(); });
        }
        void Duplicate()
        {
            var items = Document.Selection.ToArray(); if (items.Length == 0) return;
            Document.Change(() =>
            {
                Document.Selected.Clear();
                foreach (ImageItem i in items)
                { ImageItem copy = i.Copy(); copy.Id = Guid.NewGuid().ToString("N"); copy.X += 30 / Document.Zoom; copy.Y += 30 / Document.Zoom; Document.Items.Add(copy); Document.Selected.Add(copy.Id); }
            });
        }
        void Reorder(bool front)
        {
            var items = Document.Selection.ToList(); if (items.Count == 0) return;
            Document.Change(() => { Document.Items.RemoveAll(i => Document.Selected.Contains(i.Id)); if (front) Document.Items.AddRange(items); else Document.Items.InsertRange(0, items); });
        }
        internal void NormalizeSelected()
        {
            var items = Document.Selection.Where(i => !i.IsText).ToList();
            if (items.Count < 2) { SetStatus("Select at least two images to normalize their size."); return; }
            Board.FinishGesture();
            Document.Change(() => ImageLayout.Normalize(items));
            SetStatus("Size normalized · Aspect ratios preserved");
        }
        internal void PackImages()
        {
            var items = (Document.Selected.Count > 0 ? Document.Selection : Document.Items).ToList(); if (items.Count == 0) return;
            Board.FinishGesture();
            if (items.Count > 1) Document.Change(() => ImageLayout.Pack(items, 16));
            Board.Fit(Document.Selected.Count > 0);
            SetStatus("Images packed · Sizes and rotations preserved");
        }
        bool ConfirmDiscard()
        {
            CommitText();
            if (!Document.Dirty) return true;
            MessageBoxResult result = MessageBox.Show(this, "Save changes to this project?", "ArkBoard", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            return result == MessageBoxResult.No || (result == MessageBoxResult.Yes && Save(false));
        }
        void NewProject() { if (ConfirmDiscard()) { Board.FinishGesture(); Document.Reset(); SetStatus("New Project"); } }
        void OpenDialog()
        {
            var dialog = new OpenFileDialog { Title = "Open Project", Filter = "ArkBoard projects|*.arkboard;*.refcanvas;*.zip|All files|*.*" };
            if (dialog.ShowDialog(this) == true) OpenProject(dialog.FileName);
        }
        public void OpenProject(string path)
        {
            if (busy || !ConfirmDiscard()) return;
            try { Board.FinishGesture(); Mouse.OverrideCursor = Cursors.Wait; Document.Load(path); SetStatus("Project opened · All images are embedded"); }
            catch (Exception ex) { Error("Unable to open project", ex); }
            finally { Mouse.OverrideCursor = null; }
        }
        bool Save(bool saveAs)
        {
            CommitText();
            string path = Document.Path;
            if (saveAs || path == null)
            {
                var dialog = new SaveFileDialog { Title = "Save Project", Filter = "ArkBoard project|*.arkboard", DefaultExt = ".arkboard", AddExtension = true,
                    FileName = path == null ? "Untitled.arkboard" : Path.GetFileName(path) };
                if (dialog.ShowDialog(this) != true) return false; path = dialog.FileName;
            }
            try { Mouse.OverrideCursor = Cursors.Wait; Document.Save(path); SetStatus("Saved · Images embedded in the project"); return true; }
            catch (Exception ex) { Error("Unable to save project", ex); return false; }
            finally { Mouse.OverrideCursor = null; }
        }
        async void ImportDialog()
        {
            var dialog = new OpenFileDialog { Title = "Import Images", Multiselect = true,
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.ico|All files|*.*" };
            if (dialog.ShowDialog(this) == true)
                await ImportSources(dialog.FileNames.Select(p => new ImportSource { Location = p, Name = Path.GetFileName(p) }).ToList(), Board.CenterWorld);
        }
        void OnDragOver(object sender, DragEventArgs e)
        { e.Effects = !busy && Importer.CanRead(e.Data) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
        async void OnDrop(object sender, DragEventArgs e)
        {
            e.Handled = true; if (busy) return;
            try
            {
                Point center = Board.ToWorld(e.GetPosition(Board));
                var sources = Importer.Extract(e.Data);
                if (sources.Count == 1 && sources[0].Location != null && (sources[0].Location.EndsWith(".arkboard", StringComparison.OrdinalIgnoreCase) || sources[0].Location.EndsWith(".refcanvas", StringComparison.OrdinalIgnoreCase)))
                    OpenProject(sources[0].Location);
                else await ImportSources(sources, center);
            }
            catch (Exception ex) { Error("Import failed", ex); }
        }
        async void Paste()
        {
            try
            {
                IDataObject data = Clipboard.GetDataObject(); ImageItem item; AssetData asset;
                if (ArkBoardClipboard.TryRead(data, out item, out asset)) { PasteClipboardImage(item, asset, Board.CenterWorld); return; }
                await ImportSources(Importer.Extract(data), Board.CenterWorld);
            }
            catch (Exception ex) { Error("Unable to paste", ex); }
        }
        internal ImageItem PasteClipboardImage(ImageItem source, AssetData asset, Point center)
        {
            ImageItem pasted = source.Copy(); pasted.Id = Guid.NewGuid().ToString("N"); pasted.Asset = asset.Key;
            pasted.X = center.X; pasted.Y = center.Y;
            Document.Change(() =>
            {
                Document.Assets[asset.Key] = asset; Document.Items.Add(pasted);
                Document.Selected.Clear(); Document.Selected.Add(pasted.Id);
            });
            SetStatus(pasted.HasMask ? "Masked image pasted" : "Image pasted"); Board.Focus(); return pasted;
        }
        void CopyImage()
        {
            ImageItem i = Document.Selection.LastOrDefault(); if (i == null) return;
            if (i.IsText) { Clipboard.SetText(i.Text); SetStatus("Text copied"); return; }
            try
            {
                Clipboard.SetDataObject(ArkBoardClipboard.Create(i, Document.Assets[i.Asset]), true);
                SetStatus("Image copied · Mask and transforms preserved in ArkBoard");
            }
            catch (Exception ex) { Error("Unable to copy", ex); }
        }
        public async Task ImportSources(List<ImportSource> sources, Point center)
        {
            if (busy) return;
            if (sources.Count == 0) { SetStatus("No image found · Drop an image or use Copy Image in your browser"); return; }
            if (sources.Count > 100) { SetStatus("Import up to 100 images at a time."); return; }
            busy = true; Board.IsEnabled = false;
            var loaded = new List<Tuple<AssetData, string>>(); var errors = new List<string>();
            try
            {
                foreach (ImportSource source in sources)
                {
                    SetStatus("Importing " + (loaded.Count + errors.Count + 1) + "/" + sources.Count + " · " + source.Name);
                    try { loaded.Add(Tuple.Create(await Importer.LoadAsync(source), source.Name)); }
                    catch (Exception ex) { errors.Add(source.Name + ": " + ex.Message); }
                }
                if (loaded.Count > 0)
                {
                    Document.Checkpoint(); Document.Selected.Clear();
                    int columns = (int)Math.Ceiling(Math.Sqrt(loaded.Count));
                    double x = center.X, y = center.Y, rowHeight = 0; int col = 0;
                    foreach (var pair in loaded)
                    {
                        ImageItem i = Document.Add(pair.Item1, pair.Item2, center);
                        if (loaded.Count > 1) { i.X = x + i.Width / 2; i.Y = y + i.Height / 2; }
                        Document.Selected.Add(i.Id); x += i.Width + 28; rowHeight = Math.Max(rowHeight, i.Height);
                        if (++col >= columns) { col = 0; x = center.X; y += rowHeight + 28; rowHeight = 0; }
                    }
                    Document.CollectAssets(); Document.Notify(); if (loaded.Count > 1) Board.Fit(true);
                }
                SetStatus(loaded.Count + " images imported" + (errors.Count > 0 ? " · " + errors.Count + " failed" : " · Images embedded"));
                if (errors.Count > 0 && !testMode) MessageBox.Show(this, string.Join("\n\n", errors.Take(5)) + (errors.Count > 5 ? "\n\nAdditional errors: " + (errors.Count - 5) : ""), "Some images could not be imported", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally { busy = false; Board.IsEnabled = true; Board.Focus(); }
        }
        void Error(string title, Exception ex)
        { SetStatus(title); if (!testMode) MessageBox.Show(this, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning); }
        void OnKey(object sender, KeyEventArgs e)
        {
            if (HandleTextToolKey(e)) return;
            if (e.Key == Key.D0 && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            { SetWindowOpacity(100); e.Handled = true; return; }
            if (busy || Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is Slider) return;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) return;
            if (Board.IsMouseCaptured && e.Key != Key.Space && e.Key != Key.Escape) return;
            bool handled = true;
            if (ctrl)
            {
                switch (e.Key)
                {
                    case Key.N: NewProject(); break; case Key.O: OpenDialog(); break;
                    case Key.S: Save(shift); break; case Key.I: ImportDialog(); break;
                    case Key.Z: if (shift) Document.Redo(); else Document.Undo(); break;
                    case Key.Y: Document.Redo(); break; case Key.A: if (!shift) NormalizeSelected(); else handled = false; break;
                    case Key.P: PackImages(); break;
                    case Key.D: Duplicate(); break; case Key.V: Paste(); break; case Key.C: CopyImage(); break;
                    default: handled = false; break;
                }
            }
            else
            {
                switch (e.Key)
                {
                    case Key.Space: Board.SpaceDown = true; Board.Cursor = Cursors.ScrollAll; break;
                    case Key.A: if (shift) handled = false; else if (!e.IsRepeat) ToggleSelectAll(); break;
                    case Key.Delete: DeleteSelection(); break;
                    case Key.Escape: Board.FinishGesture(); Board.CancelMaskEditing(); Document.Selected.Clear(); Document.Notify(); break;
                    case Key.H: Flip(true); break; case Key.V: Flip(false); break;
                    case Key.R: Rotate(shift ? -15 : 15); break;
                    case Key.F: Board.Fit(shift); break;
                    case Key.D1: Board.ZoomAt(new Point(Board.ActualWidth / 2, Board.ActualHeight / 2), 1); break;
                    case Key.OemPlus: case Key.Add: Board.ZoomAt(new Point(Board.ActualWidth / 2, Board.ActualHeight / 2), Document.Zoom * 1.2); break;
                    case Key.OemMinus: case Key.Subtract: Board.ZoomAt(new Point(Board.ActualWidth / 2, Board.ActualHeight / 2), Document.Zoom / 1.2); break;
                    case Key.OemCloseBrackets: Reorder(true); break; case Key.OemOpenBrackets: Reorder(false); break;
                    case Key.Left: EditSelection(i => i.X -= shift ? 10 : 1); break;
                    case Key.Right: EditSelection(i => i.X += shift ? 10 : 1); break;
                    case Key.Up: EditSelection(i => i.Y -= shift ? 10 : 1); break;
                    case Key.Down: EditSelection(i => i.Y += shift ? 10 : 1); break;
                    case Key.F1: Help(); break;
                    default: handled = false; break;
                }
            }
            if (handled) e.Handled = true;
        }
        void Help()
        {
            MessageBox.Show(this, "ArkBoard 1.7.0\n\nPortable reference canvas for Windows.\n\nDrop images from File Explorer or a browser. If dragging is blocked, try Copy Image and Ctrl+V.\n\nDrag corners to resize proportionally. Shift+drag an image edge to mask it; Remove Mask restores the full image. Shift+click a masked image shows its solid mask outline and dashed original bounds; Shift+drag the visible area to move the mask. Ctrl+C / Ctrl+V preserves masks inside ArkBoard.\n\nDouble-click text to edit it. Text supports movement and proportional scaling only. Drag an image's circle handle to rotate; hold Shift to snap to 15°.\n\nSettings → Auto-Sorting brings an image to the top when its drag begins and is enabled by default.\n\nCtrl+A: normalize selected images to their average longest side.\nCtrl+P: pack selected images, or all images if none are selected.\nA: select all; press A again to deselect.\nCtrl+Z / Ctrl+Y: undo / redo.\nCtrl+Shift+0: restore full opacity.\n\nCtrl+S saves images and layout in one .arkboard file.\n\nPNG, JPEG, BMP, TIFF, ICO and first GIF frame. WebP depends on installed Windows codecs.\n\nPureRef .pur files are not supported. See README.md for details.", "ArkBoard · Help", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

