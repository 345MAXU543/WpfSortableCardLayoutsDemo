using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WpfSortableCardLayoutsDemo.Models;

namespace WpfSortableCardLayoutsDemo.Controls;

public partial class VerticalCardBoard : UserControl
{
    private const double HeaderHeight = 70;
    private const double ParameterHeight = 236;
    private const double CardGap = 12;
    private const double StartY = 18;
    private const double SideMargin = 18;
    private const double AutoScrollEdge = 66;

    private readonly Dictionary<string, Border> _cardContainers = new();
    private readonly Dictionary<string, Border> _parameterPanels = new();
    private readonly Dictionary<string, RotateTransform> _arrowTransforms = new();
    private readonly DispatcherTimer _autoScrollTimer;
    private ObservableCollection<CardItemModel> _cards = new();
    private Border? _placeholder;
    private Border? _pendingHeader;
    private CardItemModel? _pendingCard;
    private CardItemModel? _draggedCard;
    private CardItemModel? _expandedCard;
    private Point _pointerDown;
    private Point _grabOffset;
    private Point _lastPointer;
    private int _originalIndex = -1;
    private int _currentInsertIndex = -1;
    private double _draggedHeight = HeaderHeight;
    private bool _isDragging;
    private bool _isInitialized;

    public VerticalCardBoard()
    {
        InitializeComponent();
        _autoScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(24) };
        _autoScrollTimer.Tick += AutoScrollTimer_Tick;
        Loaded += (_, _) =>
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                ResetDemo();
            }
        };
    }

    public void ResetDemo()
    {
        CancelActiveDrag(updateStatus: false);
        _cards = CardItemModel.CreateDefaults();
        _expandedCard = null;
        RenderCards();
        SetStatus("Ready. Click a card to edit its parameters.", false);
    }

    public bool CancelActiveDrag() => CancelActiveDrag(updateStatus: true);

    private bool CancelActiveDrag(bool updateStatus)
    {
        bool hadDrag = _isDragging || _pendingHeader != null;
        _isDragging = false;
        _autoScrollTimer.Stop();
        _pendingHeader?.ReleaseMouseCapture();
        _pendingHeader = null;
        _pendingCard = null;
        _draggedCard = null;
        _originalIndex = -1;
        _currentInsertIndex = -1;
        ClearDragPreviewAnimations();
        DragPreview.Visibility = Visibility.Collapsed;
        OutsideWarning.Visibility = Visibility.Collapsed;

        if (_isInitialized)
        {
            RenderCards();
        }

        if (hadDrag && updateStatus)
        {
            SetStatus("Drag cancelled. The original order was restored.", false);
        }

        return hadDrag;
    }

    private void RenderCards()
    {
        TrackCanvas.Children.Clear();
        _cardContainers.Clear();
        _parameterPanels.Clear();
        _arrowTransforms.Clear();

        for (int i = 0; i < _cards.Count; i++)
        {
            CardItemModel card = _cards[i];
            card.SequenceNumber = i + 1;
            Border container = BuildCard(card);
            TrackCanvas.Children.Add(container);
            _cardContainers[card.Id] = container;
        }

        _placeholder = CreatePlaceholder();
        _placeholder.Visibility = Visibility.Collapsed;
        TrackCanvas.Children.Add(_placeholder);
        UpdateCardWidths();
        LayoutCards(animated: false);
        UpdateSummary();
    }

    private Border BuildCard(CardItemModel card)
    {
        var container = new Border
        {
            Background = Brushes.White,
            BorderBrush = card.IsExpanded ? BrushFrom("#2563EB") : BrushFrom("#CBD5E1"),
            BorderThickness = new Thickness(card.IsExpanded ? 2 : 1),
            CornerRadius = new CornerRadius(10),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(30, 41, 59),
                BlurRadius = 12,
                ShadowDepth = 2,
                Opacity = 0.09
            },
            DataContext = card
        };

        var stack = new StackPanel();
        var header = BuildHeader(card);
        stack.Children.Add(header);

        Border parameters = BuildParameterPanel(card);
        parameters.Height = card.IsExpanded ? ParameterHeight : 0;
        parameters.Opacity = card.IsExpanded ? 1 : 0;
        stack.Children.Add(parameters);
        _parameterPanels[card.Id] = parameters;

        container.Child = stack;
        container.SizeChanged += (_, _) =>
        {
            if (!_isDragging)
            {
                LayoutCards(animated: false);
            }
        };
        return container;
    }

    private Border BuildHeader(CardItemModel card)
    {
        var header = new Border
        {
            Height = HeaderHeight,
            Background = card.IsExpanded ? BrushFrom("#EFF6FF") : BrushFrom("#F8FAFC"),
            CornerRadius = card.IsExpanded ? new CornerRadius(9, 9, 0, 0) : new CornerRadius(9),
            Cursor = Cursors.Hand,
            Tag = card
        };
        header.PreviewMouseLeftButtonDown += Header_MouseLeftButtonDown;
        header.PreviewMouseMove += Header_MouseMove;
        header.PreviewMouseLeftButtonUp += Header_MouseLeftButtonUp;

        var grid = new Grid { Margin = new Thickness(13, 0, 12, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(39) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });

        var badge = new Border
        {
            Width = 31,
            Height = 31,
            Background = BrushFrom("#2563EB"),
            CornerRadius = new CornerRadius(7),
            VerticalAlignment = VerticalAlignment.Center
        };
        badge.Child = new TextBlock
        {
            Text = card.SequenceText,
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(new TextBlock
        {
            Text = card.Title,
            Foreground = BrushFrom("#0F172A"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 14
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = card.IsExpanded ? "Parameters open" : "Click to edit parameters",
            Foreground = BrushFrom("#64748B"),
            FontSize = 10.5,
            Margin = new Thickness(0, 3, 0, 0)
        });
        Grid.SetColumn(titleStack, 1);

        var arrow = new TextBlock
        {
            Text = "⌄",
            Foreground = BrushFrom("#475569"),
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        var rotate = new RotateTransform(card.IsExpanded ? 180 : 0);
        arrow.RenderTransform = rotate;
        _arrowTransforms[card.Id] = rotate;
        Grid.SetColumn(arrow, 2);

        var handle = new TextBlock
        {
            Text = "⋮⋮",
            Foreground = BrushFrom("#64748B"),
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Drag to reorder"
        };
        Grid.SetColumn(handle, 3);

        grid.Children.Add(badge);
        grid.Children.Add(titleStack);
        grid.Children.Add(arrow);
        grid.Children.Add(handle);
        header.Child = grid;
        return header;
    }

    private Border BuildParameterPanel(CardItemModel card)
    {
        var outer = new Border
        {
            Background = Brushes.White,
            BorderBrush = BrushFrom("#E2E8F0"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            CornerRadius = new CornerRadius(0, 0, 9, 9),
            ClipToBounds = true
        };

        var content = new StackPanel { Margin = new Thickness(15, 13, 15, 14) };

        var enabled = new CheckBox
        {
            Content = "Enabled",
            Foreground = BrushFrom("#334155"),
            FontSize = 12.5,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        enabled.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, new Binding(nameof(CardItemModel.Enabled))
        {
            Mode = BindingMode.TwoWay
        });
        content.Children.Add(enabled);

        content.Children.Add(FieldLabel("Value"));
        var valueGrid = new Grid();
        valueGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        valueGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        valueGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        var minus = new Button { Content = "−", Style = (Style)FindResource("CompactButtonStyle"), Tag = card };
        minus.Click += (_, _) => card.Value--;
        var value = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = BrushFrom("#0F172A"),
            FontWeight = FontWeights.SemiBold
        };
        value.SetBinding(TextBlock.TextProperty, new Binding(nameof(CardItemModel.Value)));
        Grid.SetColumn(value, 1);
        var plus = new Button { Content = "+", Style = (Style)FindResource("CompactButtonStyle"), Tag = card };
        plus.Click += (_, _) => card.Value++;
        Grid.SetColumn(plus, 2);
        valueGrid.Children.Add(minus);
        valueGrid.Children.Add(value);
        valueGrid.Children.Add(plus);
        content.Children.Add(valueGrid);

        content.Children.Add(FieldLabel("Mode"));
        var combo = new ComboBox
        {
            ItemsSource = new[] { "Standard", "Advanced", "Custom" },
            Style = (Style)FindResource("FieldComboBoxStyle")
        };
        combo.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(CardItemModel.Mode)) { Mode = BindingMode.TwoWay });
        content.Children.Add(combo);

        content.Children.Add(FieldLabel("Description"));
        var description = new TextBox
        {
            Style = (Style)FindResource("FieldTextBoxStyle"),
            Height = 35
        };
        description.SetBinding(TextBox.TextProperty, new Binding(nameof(CardItemModel.Description))
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        content.Children.Add(description);

        outer.Child = content;
        return outer;
    }

    private static TextBlock FieldLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = BrushFrom("#475569"),
            FontSize = 11.5,
            Margin = new Thickness(0, 11, 0, 5)
        };
    }

    private Border CreatePlaceholder()
    {
        return new Border
        {
            Height = HeaderHeight,
            Background = BrushFrom("#EAF2FF"),
            BorderBrush = BrushFrom("#2563EB"),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Opacity = 0.80,
            Child = new TextBlock
            {
                Text = "Drop here",
                Foreground = BrushFrom("#1D4ED8"),
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border header || header.Tag is not CardItemModel card)
        {
            return;
        }

        _pendingHeader = header;
        _pendingCard = card;
        _pointerDown = e.GetPosition(TrackRegion);
        _lastPointer = _pointerDown;
        _grabOffset = e.GetPosition(header);
        header.CaptureMouse();
        e.Handled = true;
    }

    private void Header_MouseMove(object sender, MouseEventArgs e)
    {
        if (_pendingHeader == null || _pendingCard == null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point point = e.GetPosition(TrackRegion);
        _lastPointer = point;

        if (!_isDragging)
        {
            Vector delta = point - _pointerDown;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            StartDrag(_pendingCard, point);
        }

        UpdateDrag(point);
        e.Handled = true;
    }

    private void Header_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_pendingHeader == null)
        {
            return;
        }

        if (_isDragging)
        {
            CommitDrag();
        }
        else if (_pendingCard != null)
        {
            CardItemModel clicked = _pendingCard;
            _pendingHeader.ReleaseMouseCapture();
            _pendingHeader = null;
            _pendingCard = null;
            ToggleExpanded(clicked);
        }

        e.Handled = true;
    }

    private void StartDrag(CardItemModel card, Point point)
    {
        _draggedCard = card;
        _originalIndex = _cards.IndexOf(card);
        _currentInsertIndex = _originalIndex;
        _isDragging = true;

        if (_cardContainers.TryGetValue(card.Id, out Border? source))
        {
            _draggedHeight = Math.Max(HeaderHeight, source.ActualHeight);
            source.Opacity = 0.10;
        }

        if (_placeholder != null)
        {
            _placeholder.Height = _draggedHeight;
            _placeholder.Visibility = Visibility.Visible;
        }

        PreviewSequenceText.Text = card.SequenceText;
        PreviewTitleText.Text = card.Title;
        DragPreview.Width = Math.Max(260, TrackRegion.ActualWidth - SideMargin * 2 - 18);
        ClearDragPreviewAnimations();
        DragPreview.Visibility = Visibility.Visible;
        UpdatePreviewPosition(point);
        LayoutForDrag(animated: false);
        _autoScrollTimer.Start();
        SetStatus($"Dragging {card.Title}. Release to place it at position {_currentInsertIndex + 1}.", true);
    }

    private void UpdateDrag(Point point)
    {
        if (!_isDragging || _draggedCard == null)
        {
            return;
        }

        _lastPointer = point;
        UpdatePreviewPosition(point);

        Point contentPoint = Mouse.GetPosition(TrackCanvas);
        int nextIndex = CalculateInsertIndex(contentPoint.Y);
        if (nextIndex != _currentInsertIndex)
        {
            _currentInsertIndex = nextIndex;
            LayoutForDrag(animated: true);
            SetStatus($"Dragging {_draggedCard.Title}. Release to place it at position {_currentInsertIndex + 1}.", true);
        }

        bool outside = point.X < 0 || point.X > TrackRegion.ActualWidth || point.Y < 0 || point.Y > TrackRegion.ActualHeight;
        OutsideWarning.Visibility = outside ? Visibility.Visible : Visibility.Collapsed;
    }

    private int CalculateInsertIndex(double pointerY)
    {
        if (_draggedCard == null)
        {
            return 0;
        }

        List<CardItemModel> remaining = _cards.Where(card => card != _draggedCard).ToList();
        double cursor = StartY;
        int index = 0;
        foreach (CardItemModel card in remaining)
        {
            double height = GetCardHeight(card);
            if (pointerY < cursor + height / 2)
            {
                break;
            }

            cursor += height + CardGap;
            index++;
        }

        return Math.Clamp(index, 0, _cards.Count - 1);
    }

    private void LayoutCards(bool animated)
    {
        if (_isDragging)
        {
            LayoutForDrag(animated);
            return;
        }

        double top = StartY;
        foreach (CardItemModel card in _cards)
        {
            if (_cardContainers.TryGetValue(card.Id, out Border? container))
            {
                MoveCanvasTop(container, top, animated);
                top += Math.Max(HeaderHeight, container.ActualHeight) + CardGap;
            }
        }

        TrackCanvas.Height = Math.Max(TrackRegion.ActualHeight, top + StartY);
    }

    private void LayoutForDrag(bool animated)
    {
        if (_draggedCard == null || _placeholder == null)
        {
            return;
        }

        List<CardItemModel> remaining = _cards.Where(card => card != _draggedCard).ToList();
        int remainingIndex = 0;
        double top = StartY;

        for (int slot = 0; slot < _cards.Count; slot++)
        {
            if (slot == _currentInsertIndex)
            {
                MoveCanvasTop(_placeholder, top, animated);
                top += _draggedHeight + CardGap;
                continue;
            }

            CardItemModel card = remaining[remainingIndex++];
            if (_cardContainers.TryGetValue(card.Id, out Border? container))
            {
                MoveCanvasTop(container, top, animated);
                top += Math.Max(HeaderHeight, container.ActualHeight) + CardGap;
            }
        }

        TrackCanvas.Height = Math.Max(TrackRegion.ActualHeight, top + StartY);
    }

    private void CommitDrag()
    {
        if (!_isDragging || _draggedCard == null || _placeholder == null)
        {
            return;
        }

        CardItemModel card = _draggedCard;
        int from = _originalIndex;
        int to = _currentInsertIndex;
        _isDragging = false;
        _autoScrollTimer.Stop();
        OutsideWarning.Visibility = Visibility.Collapsed;
        _pendingHeader?.ReleaseMouseCapture();
        _pendingHeader = null;
        _pendingCard = null;

        double targetTop = Canvas.GetTop(_placeholder);
        Point target = TrackCanvas.TranslatePoint(new Point(SideMargin, targetTop), DragOverlay);
        var xAnimation = new DoubleAnimation(target.X, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var yAnimation = new DoubleAnimation(target.Y, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        yAnimation.Completed += (_, _) =>
        {
            if (from != to)
            {
                _cards.Move(from, to);
            }

            _draggedCard = null;
            _originalIndex = -1;
            _currentInsertIndex = -1;
            ClearDragPreviewAnimations();
            DragPreview.Visibility = Visibility.Collapsed;
            RenderCards();
            SetStatus(from == to
                ? $"{card.Title} remains at position {to + 1}."
                : $"{card.Title} moved from position {from + 1} to position {to + 1}.", from != to);
        };
        DragPreview.BeginAnimation(Canvas.LeftProperty, xAnimation);
        DragPreview.BeginAnimation(Canvas.TopProperty, yAnimation);
    }

    private void ToggleExpanded(CardItemModel card)
    {
        if (card.IsExpanded)
        {
            CollapseCard(card, () => SetStatus($"{card.Title} parameters collapsed.", false));
            return;
        }

        if (_expandedCard != null && _expandedCard != card && _expandedCard.IsExpanded)
        {
            CardItemModel previous = _expandedCard;
            CollapseCard(previous, () => ExpandCard(card));
        }
        else
        {
            ExpandCard(card);
        }
    }

    private void ExpandCard(CardItemModel card)
    {
        if (!_parameterPanels.TryGetValue(card.Id, out Border? panel))
        {
            return;
        }

        card.IsExpanded = true;
        _expandedCard = card;
        AnimatePanel(panel, 0, ParameterHeight, 0, 1, null);
        AnimateArrow(card, 180);
        UpdateContainerAppearance(card, expanded: true);
        SetStatus($"{card.Title} parameters expanded.", true);
    }

    private void CollapseCard(CardItemModel card, Action? completed)
    {
        if (!_parameterPanels.TryGetValue(card.Id, out Border? panel))
        {
            completed?.Invoke();
            return;
        }

        double from = double.IsNaN(panel.Height) ? panel.ActualHeight : panel.Height;
        AnimatePanel(panel, from, 0, panel.Opacity, 0, () =>
        {
            card.IsExpanded = false;
            if (_expandedCard == card)
            {
                _expandedCard = null;
            }
            UpdateContainerAppearance(card, expanded: false);
            completed?.Invoke();
        });
        AnimateArrow(card, 0);
    }

    private void AnimatePanel(Border panel, double fromHeight, double toHeight, double fromOpacity, double toOpacity, Action? completed)
    {
        var duration = TimeSpan.FromMilliseconds(220);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var heightAnimation = new DoubleAnimation(fromHeight, toHeight, duration) { EasingFunction = ease };
        var opacityAnimation = new DoubleAnimation(fromOpacity, toOpacity, duration);
        if (completed != null)
        {
            heightAnimation.Completed += (_, _) => completed();
        }
        panel.BeginAnimation(HeightProperty, heightAnimation, HandoffBehavior.SnapshotAndReplace);
        panel.BeginAnimation(OpacityProperty, opacityAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    private void AnimateArrow(CardItemModel card, double angle)
    {
        if (_arrowTransforms.TryGetValue(card.Id, out RotateTransform? rotate))
        {
            rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(angle, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            });
        }
    }

    private void UpdateContainerAppearance(CardItemModel card, bool expanded)
    {
        if (_cardContainers.TryGetValue(card.Id, out Border? container))
        {
            container.BorderBrush = BrushFrom(expanded ? "#2563EB" : "#CBD5E1");
            container.BorderThickness = new Thickness(expanded ? 2 : 1);
        }
    }

    private void AutoScrollTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        double offset = CardsScrollViewer.VerticalOffset;
        double maxOffset = CardsScrollViewer.ScrollableHeight;
        double next = offset;

        if (_lastPointer.Y < AutoScrollEdge)
        {
            next = Math.Max(0, offset - 14);
        }
        else if (_lastPointer.Y > TrackRegion.ActualHeight - AutoScrollEdge)
        {
            next = Math.Min(maxOffset, offset + 14);
        }

        if (Math.Abs(next - offset) > 0.1)
        {
            CardsScrollViewer.ScrollToVerticalOffset(next);
            UpdateDrag(_lastPointer);
        }
    }

    private void TrackRegion_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateCardWidths();
        LayoutCards(animated: false);
    }

    private void UpdateCardWidths()
    {
        double width = Math.Max(300, TrackRegion.ActualWidth - SideMargin * 2 - 18);
        TrackCanvas.Width = Math.Max(320, TrackRegion.ActualWidth - 2);
        foreach (Border container in _cardContainers.Values)
        {
            container.Width = width;
            Canvas.SetLeft(container, SideMargin);
        }

        if (_placeholder != null)
        {
            _placeholder.Width = width;
            Canvas.SetLeft(_placeholder, SideMargin);
        }
    }

    private double GetCardHeight(CardItemModel card)
    {
        return _cardContainers.TryGetValue(card.Id, out Border? container)
            ? Math.Max(HeaderHeight, container.ActualHeight)
            : HeaderHeight;
    }

    private void UpdatePreviewPosition(Point point)
    {
        Canvas.SetLeft(DragPreview, SideMargin);
        Canvas.SetTop(DragPreview, point.Y - Math.Min(_grabOffset.Y, HeaderHeight - 6));
    }

    private void ClearDragPreviewAnimations()
    {
        DragPreview.BeginAnimation(Canvas.LeftProperty, null);
        DragPreview.BeginAnimation(Canvas.TopProperty, null);
    }

    private void UpdateSummary()
    {
        OrderSummaryText.Text = "Current order: " + string.Join(" → ", _cards.Select(card => card.Title));
    }

    private void SetStatus(string message, bool accent)
    {
        StatusText.Text = message;
        StatusText.Foreground = BrushFrom(accent ? "#1D4ED8" : "#64748B");
    }

    private static SolidColorBrush BrushFrom(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    private static void MoveCanvasTop(UIElement element, double target, bool animated)
    {
        double current = Canvas.GetTop(element);
        if (double.IsNaN(current) || !animated)
        {
            element.BeginAnimation(Canvas.TopProperty, null);
            Canvas.SetTop(element, target);
            return;
        }

        element.BeginAnimation(Canvas.TopProperty, null);
        Canvas.SetTop(element, target);
        element.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(current, target, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        });
    }
}
