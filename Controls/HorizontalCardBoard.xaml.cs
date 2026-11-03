using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WpfSortableCardLayoutsDemo.Models;

namespace WpfSortableCardLayoutsDemo.Controls;

public partial class HorizontalCardBoard : UserControl
{
    private const double CardWidth = 190;
    private const double CardHeight = 160;
    private const double CardGap = 24;
    private const double StartX = 24;
    private const double TrackTop = 34;
    private const double AutoScrollEdge = 64;
    private const double InsertHysteresis = 16;

    private readonly Dictionary<string, Border> _cardContainers = new();
    private readonly DispatcherTimer _autoScrollTimer;
    private ObservableCollection<CardItemModel> _cards = new();
    private Border? _placeholder;
    private Border? _pendingHeader;
    private CardItemModel? _pendingCard;
    private CardItemModel? _draggedCard;
    private Point _pointerDown;
    private Point _grabOffset;
    private Point _lastPointer;
    private int _originalIndex = -1;
    private int _currentInsertIndex = -1;
    private bool _isDragging;
    private bool _isInitialized;

    public HorizontalCardBoard()
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
        RenderCards();
        SetStatus("Ready. Drag any card header to begin.", false);
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

        for (int i = 0; i < _cards.Count; i++)
        {
            CardItemModel card = _cards[i];
            card.SequenceNumber = i + 1;
            Border container = BuildCard(card);
            Canvas.SetLeft(container, GetSlotX(i));
            Canvas.SetTop(container, TrackTop);
            TrackCanvas.Children.Add(container);
            _cardContainers[card.Id] = container;
        }

        _placeholder = CreatePlaceholder();
        _placeholder.Visibility = Visibility.Collapsed;
        TrackCanvas.Children.Add(_placeholder);
        TrackCanvas.Width = StartX * 2 + _cards.Count * (CardWidth + CardGap) - CardGap;
        UpdateSummary();
    }

    private Border BuildCard(CardItemModel card)
    {
        var border = new Border
        {
            Width = CardWidth,
            Height = CardHeight,
            Background = Brushes.White,
            BorderBrush = card.IsSelected ? BrushFrom("#2563EB") : BrushFrom("#CBD5E1"),
            BorderThickness = new Thickness(card.IsSelected ? 2 : 1),
            CornerRadius = new CornerRadius(10),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(30, 41, 59),
                BlurRadius = 12,
                ShadowDepth = 2,
                Opacity = 0.10
            }
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(31) });

        var header = new Border
        {
            Background = card.IsSelected ? BrushFrom("#EFF6FF") : BrushFrom("#F8FAFC"),
            CornerRadius = new CornerRadius(9, 9, 0, 0),
            Cursor = Cursors.SizeAll,
            Tag = card
        };
        header.PreviewMouseLeftButtonDown += Header_MouseLeftButtonDown;
        header.PreviewMouseMove += Header_MouseMove;
        header.PreviewMouseLeftButtonUp += Header_MouseLeftButtonUp;

        var headerGrid = new Grid { Margin = new Thickness(12, 0, 10, 0) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) });

        var badge = new Border
        {
            Width = 30,
            Height = 30,
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

        var title = new TextBlock
        {
            Text = card.Title,
            Foreground = BrushFrom("#0F172A"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(title, 1);

        var handle = new TextBlock
        {
            Text = "⋮⋮",
            Foreground = BrushFrom("#64748B"),
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(handle, 2);

        headerGrid.Children.Add(badge);
        headerGrid.Children.Add(title);
        headerGrid.Children.Add(handle);
        header.Child = headerGrid;
        grid.Children.Add(header);

        var body = new StackPanel { Margin = new Thickness(14, 14, 14, 8) };
        Grid.SetRow(body, 1);
        body.Children.Add(new TextBlock
        {
            Text = "Compact sortable item",
            Foreground = BrushFrom("#475569"),
            FontSize = 12.5
        });
        body.Children.Add(new TextBlock
        {
            Text = "Click to select · Drag to reorder",
            Foreground = BrushFrom("#64748B"),
            FontSize = 11,
            Margin = new Thickness(0, 8, 0, 0)
        });
        grid.Children.Add(body);

        var footer = new Border
        {
            Background = BrushFrom("#F8FAFC"),
            BorderBrush = BrushFrom("#E2E8F0"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            CornerRadius = new CornerRadius(0, 0, 9, 9)
        };
        Grid.SetRow(footer, 2);
        footer.Child = new TextBlock
        {
            Text = $"Position {card.SequenceNumber}",
            Foreground = BrushFrom("#64748B"),
            FontSize = 10.5,
            Margin = new Thickness(12, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        grid.Children.Add(footer);

        border.Child = grid;
        return border;
    }

    private Border CreatePlaceholder()
    {
        return new Border
        {
            Width = CardWidth,
            Height = CardHeight,
            Background = BrushFrom("#EAF2FF"),
            BorderBrush = BrushFrom("#2563EB"),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Opacity = 0.78,
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
            SelectCard(_pendingCard);
            _pendingHeader.ReleaseMouseCapture();
            _pendingHeader = null;
            _pendingCard = null;
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
            source.Opacity = 0.10;
        }

        if (_placeholder != null)
        {
            Canvas.SetLeft(_placeholder, GetSlotX(_currentInsertIndex));
            Canvas.SetTop(_placeholder, TrackTop);
            _placeholder.Visibility = Visibility.Visible;
        }

        PreviewSequenceText.Text = card.SequenceText;
        PreviewTitleText.Text = card.Title;
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

        // Translate the exact sampled pointer position into the scrolled content space.
        // Using one coordinate sample for both the preview and insertion logic prevents
        // the target slot from lagging or jumping while the ScrollViewer is moving.
        Point contentPoint = TrackRegion.TranslatePoint(point, TrackCanvas);
        int nextIndex = ComputeInsertIndexWithHysteresis(contentPoint.X, _currentInsertIndex);

        if (nextIndex != _currentInsertIndex)
        {
            _currentInsertIndex = nextIndex;
            LayoutForDrag(animated: true);
            SetStatus($"Dragging {_draggedCard.Title}. Release to place it at position {_currentInsertIndex + 1}.", true);
        }

        bool outside = point.X < 0 || point.X > TrackRegion.ActualWidth || point.Y < 0 || point.Y > TrackRegion.ActualHeight;
        OutsideWarning.Visibility = outside ? Visibility.Visible : Visibility.Collapsed;
    }

    private int ComputeInsertIndexWithHysteresis(double pointerX, int currentIndex)
    {
        if (_cards.Count <= 1)
        {
            return 0;
        }

        int candidate = Math.Clamp(currentIndex, 0, _cards.Count - 1);

        while (candidate < _cards.Count - 1)
        {
            double rightBoundary = GetBoundaryAfterSlot(candidate) + InsertHysteresis;
            if (pointerX <= rightBoundary)
            {
                break;
            }

            candidate++;
        }

        while (candidate > 0)
        {
            double leftBoundary = GetBoundaryAfterSlot(candidate - 1) - InsertHysteresis;
            if (pointerX >= leftBoundary)
            {
                break;
            }

            candidate--;
        }

        return candidate;
    }

    private void LayoutForDrag(bool animated)
    {
        if (_draggedCard == null || _placeholder == null)
        {
            return;
        }

        List<CardItemModel> remaining = _cards.Where(card => card != _draggedCard).ToList();
        int remainingIndex = 0;

        for (int slot = 0; slot < _cards.Count; slot++)
        {
            double x = GetSlotX(slot);
            if (slot == _currentInsertIndex)
            {
                MoveCanvasLeft(_placeholder, x, animated);
                continue;
            }

            CardItemModel card = remaining[remainingIndex++];
            if (_cardContainers.TryGetValue(card.Id, out Border? container))
            {
                MoveCanvasLeft(container, x, animated);
            }
        }
    }

    private void CommitDrag()
    {
        if (!_isDragging || _draggedCard == null)
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

        Point target = TrackCanvas.TranslatePoint(new Point(GetSlotX(to), TrackTop), DragOverlay);
        var xAnimation = new DoubleAnimation(target.X, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var yAnimation = new DoubleAnimation(target.Y, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        xAnimation.Completed += (_, _) =>
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

    private void SelectCard(CardItemModel selected)
    {
        foreach (CardItemModel card in _cards)
        {
            card.IsSelected = card == selected;
        }

        RenderCards();
        SetStatus($"{selected.Title} selected. Horizontal cards stay compact and never expand.", false);
    }

    private void AutoScrollTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        double offset = CardsScrollViewer.HorizontalOffset;
        double maxOffset = CardsScrollViewer.ScrollableWidth;
        double next = offset;

        if (_lastPointer.X < AutoScrollEdge)
        {
            next = Math.Max(0, offset - 16);
        }
        else if (_lastPointer.X > TrackRegion.ActualWidth - AutoScrollEdge)
        {
            next = Math.Min(maxOffset, offset + 16);
        }

        if (Math.Abs(next - offset) > 0.1)
        {
            CardsScrollViewer.ScrollToHorizontalOffset(next);
            UpdateDrag(_lastPointer);
        }
    }

    private void CardsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (CardsScrollViewer.ScrollableWidth <= 0)
        {
            return;
        }

        const double pixelsPerWheelNotch = 72;
        double wheelNotches = e.Delta / 120.0;
        double targetOffset = Math.Clamp(
            CardsScrollViewer.HorizontalOffset - wheelNotches * pixelsPerWheelNotch,
            0,
            CardsScrollViewer.ScrollableWidth);

        CardsScrollViewer.ScrollToHorizontalOffset(targetOffset);
        e.Handled = true;

        if (_isDragging)
        {
            UpdateDrag(_lastPointer);
        }
    }

    private void UpdatePreviewPosition(Point point)
    {
        Canvas.SetLeft(DragPreview, point.X - _grabOffset.X);
        Canvas.SetTop(DragPreview, point.Y - _grabOffset.Y);
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

    private static double GetSlotX(int index) => StartX + index * (CardWidth + CardGap);

    private static double GetBoundaryAfterSlot(int index) =>
        GetSlotX(index) + CardWidth + CardGap / 2;

    private static SolidColorBrush BrushFrom(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    private static void MoveCanvasLeft(UIElement element, double target, bool animated)
    {
        double current = Canvas.GetLeft(element);
        if (double.IsNaN(current) || !animated)
        {
            element.BeginAnimation(Canvas.LeftProperty, null);
            Canvas.SetLeft(element, target);
            return;
        }

        element.BeginAnimation(Canvas.LeftProperty, null);
        Canvas.SetLeft(element, target);
        element.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(current, target, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        });
    }
}
