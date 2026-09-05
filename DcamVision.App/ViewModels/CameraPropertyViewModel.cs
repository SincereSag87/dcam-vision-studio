using DcamVision.Core;

namespace DcamVision.App.ViewModels;

public sealed class CameraPropertyViewModel : ObservableObject
{
    private readonly Func<CameraPropertyViewModel, Task<CameraPropertyUpdateResult>> _applyAsync;
    private CameraProperty _property;
    private string _editableValue;
    private bool _editableBoolean;
    private CameraPropertyOption? _selectedOption;
    private bool _isDirty;
    private string _errorMessage = string.Empty;
    private bool _isStreaming;

    public CameraPropertyViewModel(
        CameraProperty property,
        Func<CameraPropertyViewModel, Task<CameraPropertyUpdateResult>> applyAsync)
    {
        _property = property;
        _applyAsync = applyAsync;
        _editableValue = CameraPropertyValueConverter.Format(property.Value, property.PropertyType);
        _editableBoolean = property.Value is bool boolean && boolean;
        _selectedOption = property.Options.FirstOrDefault(option => Equals(option.Value, property.Value));
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, () => CanApply);
        RevertCommand = new RelayCommand(Revert, () => IsDirty);
    }

    public CameraProperty Property => _property;

    public string Id => _property.Id;

    public string DisplayName => _property.DisplayName;

    public string Description => _property.Description ?? string.Empty;

    public string Category => _property.Category;

    public int DisplayOrder => _property.DisplayOrder;

    public string Unit => _property.Unit ?? string.Empty;

    public string CurrentValueText => _property.FormatValue();

    public string MetadataText
    {
        get
        {
            var parts = new List<string>();
            if (_property.Minimum is not null || _property.Maximum is not null)
            {
                parts.Add($"Range: {_property.Minimum ?? "-"} - {_property.Maximum ?? "-"}{(_property.Unit is null ? string.Empty : $" {_property.Unit}")}");
            }

            if (_property.Step is not null)
            {
                parts.Add($"Step: {_property.Step}");
            }

            parts.Add(_property.IsReadOnly ? "Read-only" : "Writable");
            if (_property.IsWritableWhileStreaming)
            {
                parts.Add("Live writable");
            }

            if (!_property.IsAvailable && !string.IsNullOrWhiteSpace(_property.AvailabilityReason))
            {
                parts.Add(_property.AvailabilityReason);
            }

            return string.Join(" | ", parts);
        }
    }

    public string AccessText => _property.IsReadOnly
        ? "READ ONLY"
        : _property.IsWritableWhileStreaming
            ? "WRITABLE LIVE"
            : "WRITABLE STOPPED";

    public IReadOnlyList<CameraPropertyOption> Options => _property.Options;

    public bool IsInteger => _property.PropertyType is CameraPropertyType.Integer;

    public bool IsFloatingPoint => _property.PropertyType is CameraPropertyType.FloatingPoint;

    public bool IsBoolean => _property.PropertyType is CameraPropertyType.Boolean;

    public bool IsEnumeration => _property.PropertyType is CameraPropertyType.Enumeration;

    public bool IsText => _property.PropertyType is CameraPropertyType.Text;

    public bool IsNumericOrText => IsInteger || IsFloatingPoint || IsText;

    public bool CanEdit => !_property.IsReadOnly && _property.IsAvailable && (!_isStreaming || _property.IsWritableWhileStreaming);

    public bool CanApply => CanEdit && IsDirty && !HasError;

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(CanApply));
                ApplyCommand.RaiseCanExecuteChanged();
                RevertCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string EditableValue
    {
        get => _editableValue;
        set
        {
            if (SetProperty(ref _editableValue, value))
            {
                ValidateCandidate(value);
            }
        }
    }

    public bool EditableBoolean
    {
        get => _editableBoolean;
        set
        {
            if (SetProperty(ref _editableBoolean, value))
            {
                ValidateCandidate(value);
            }
        }
    }

    public CameraPropertyOption? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (SetProperty(ref _selectedOption, value))
            {
                ValidateCandidate(value?.Value);
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(CanApply));
                ApplyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public AsyncRelayCommand ApplyCommand { get; }

    public RelayCommand RevertCommand { get; }

    public object? GetCandidateValue()
    {
        if (IsBoolean)
        {
            return EditableBoolean;
        }

        if (IsEnumeration)
        {
            return SelectedOption?.Value;
        }

        return EditableValue;
    }

    public void SetStreamingState(bool isStreaming)
    {
        if (_isStreaming == isStreaming)
        {
            return;
        }

        _isStreaming = isStreaming;
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanApply));
        ApplyCommand.RaiseCanExecuteChanged();
    }

    public void UpdateAuthoritativeProperty(CameraProperty property)
    {
        _property = property;
        Revert();
        OnPropertyChanged(nameof(Property));
        OnPropertyChanged(nameof(CurrentValueText));
        OnPropertyChanged(nameof(MetadataText));
        OnPropertyChanged(nameof(AccessText));
        OnPropertyChanged(nameof(CanEdit));
    }

    private async Task ApplyAsync()
    {
        if (!CanApply)
        {
            return;
        }

        var result = await _applyAsync(this);
        if (result.Success && result.UpdatedProperty is not null)
        {
            _property = result.UpdatedProperty;
            Revert();
            ErrorMessage = string.Empty;
            OnPropertyChanged(nameof(CurrentValueText));
            OnPropertyChanged(nameof(MetadataText));
            OnPropertyChanged(nameof(AccessText));
            OnPropertyChanged(nameof(CanEdit));
            return;
        }

        ErrorMessage = result.ErrorMessage ?? "Property update failed.";
    }

    private void Revert()
    {
        ErrorMessage = string.Empty;
        _editableValue = CameraPropertyValueConverter.Format(_property.Value, _property.PropertyType);
        _editableBoolean = _property.Value is bool boolean && boolean;
        _selectedOption = _property.Options.FirstOrDefault(option => Equals(option.Value, _property.Value));
        IsDirty = false;
        OnPropertyChanged(nameof(EditableValue));
        OnPropertyChanged(nameof(EditableBoolean));
        OnPropertyChanged(nameof(SelectedOption));
        OnPropertyChanged(nameof(CurrentValueText));
    }

    private void ValidateCandidate(object? candidate)
    {
        if (!CanEdit)
        {
            ErrorMessage = string.Empty;
            IsDirty = false;
            return;
        }

        var validation = CameraPropertyValidator.ValidateWrite(_property, candidate, _isStreaming);
        ErrorMessage = validation.Success ? string.Empty : validation.ErrorMessage ?? "Invalid value.";
        IsDirty = !ValuesEqual(candidate);
    }

    private bool ValuesEqual(object? candidate)
    {
        if (!CameraPropertyValueConverter.TryConvert(_property, candidate, out var converted, out _))
        {
            return false;
        }

        return Equals(converted, _property.Value);
    }
}
