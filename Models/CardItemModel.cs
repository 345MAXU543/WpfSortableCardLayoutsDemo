using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfSortableCardLayoutsDemo.Models;

public sealed class CardItemModel : INotifyPropertyChanged
{
    private int _sequenceNumber;
    private bool _enabled = true;
    private int _value;
    private string _mode = "Standard";
    private string _description = string.Empty;
    private bool _isExpanded;
    private bool _isSelected;

    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;

    public int SequenceNumber
    {
        get => _sequenceNumber;
        set
        {
            if (SetField(ref _sequenceNumber, value))
            {
                OnPropertyChanged(nameof(SequenceText));
            }
        }
    }

    public string SequenceText => _sequenceNumber.ToString("00");

    public bool Enabled
    {
        get => _enabled;
        set => SetField(ref _enabled, value);
    }

    public int Value
    {
        get => _value;
        set => SetField(ref _value, Math.Clamp(value, 0, 999));
    }

    public string Mode
    {
        get => _mode;
        set => SetField(ref _mode, value);
    }

    public string Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public static ObservableCollection<CardItemModel> CreateDefaults()
    {
        var cards = new ObservableCollection<CardItemModel>();
        for (int i = 1; i <= 8; i++)
        {
            cards.Add(new CardItemModel
            {
                Id = $"card-{i:00}",
                Title = $"Card {i}",
                SequenceNumber = i,
                Enabled = i != 6,
                Value = i * 10,
                Mode = (i % 3) switch
                {
                    1 => "Standard",
                    2 => "Advanced",
                    _ => "Custom"
                },
                Description = $"Editable parameters for Card {i}."
            });
        }

        return cards;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
