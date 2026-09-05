using System.Collections.ObjectModel;
using DcamVision.Core;

namespace DcamVision.App.ViewModels;

public sealed class CameraPropertyExplorerViewModel : ObservableObject
{
    private readonly ICameraService _cameraService;
    private readonly Func<bool> _isStreaming;
    private string _searchText = string.Empty;
    private string _selectedCategory = "All";
    private bool _writableOnly;
    private string _message = "Connect to a camera to inspect properties.";

    public CameraPropertyExplorerViewModel(ICameraService cameraService, Func<bool> isStreaming)
    {
        _cameraService = cameraService;
        _isStreaming = isStreaming;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public ObservableCollection<CameraPropertyViewModel> Properties { get; } = [];

    public ObservableCollection<CameraPropertyViewModel> FilteredProperties { get; } = [];

    public ObservableCollection<string> CategoryOptions { get; } = ["All"];

    public AsyncRelayCommand RefreshCommand { get; }

    public event EventHandler<CameraProperty>? PropertyApplied;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                ApplyFilters();
            }
        }
    }

    public bool WritableOnly
    {
        get => _writableOnly;
        set
        {
            if (SetProperty(ref _writableOnly, value))
            {
                ApplyFilters();
            }
        }
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public int TotalCount => Properties.Count;

    public int WritableCount => Properties.Count(property => !property.Property.IsReadOnly);

    public int ReadOnlyCount => Properties.Count(property => property.Property.IsReadOnly);

    public int FilteredCount => FilteredProperties.Count;

    public string CountsText => TotalCount == 0
        ? "No properties loaded"
        : $"Showing {FilteredCount} of {TotalCount} properties | {WritableCount} writable | {ReadOnlyCount} read-only";

    public bool HasDirtyProperties => Properties.Any(property => property.IsDirty);

    public async Task RefreshAsync()
    {
        if (_cameraService.ConnectedDevice is null)
        {
            Properties.Clear();
            FilteredProperties.Clear();
            RefreshCategories([]);
            Message = "Connect to a camera to inspect properties.";
            RefreshCounts();
            return;
        }

        if (HasDirtyProperties)
        {
            Message = "Apply or revert modified properties before refreshing.";
            return;
        }

        var properties = await _cameraService.GetPropertiesAsync();
        LoadProperties(properties);
        Message = $"Loaded {Properties.Count} camera properties.";
    }

    public void LoadProperties(IReadOnlyList<CameraProperty> properties)
    {
        Properties.Clear();
        foreach (var property in SortProperties(properties))
        {
            var viewModel = new CameraPropertyViewModel(property, ApplyPropertyAsync);
            viewModel.SetStreamingState(_isStreaming());
            Properties.Add(viewModel);
        }

        RefreshCategories(properties);
        ApplyFilters();
        RefreshCounts();
    }

    public void SetStreamingStateChanged()
    {
        foreach (var property in Properties)
        {
            property.SetStreamingState(_isStreaming());
        }

        ApplyFilters();
    }

    private async Task<CameraPropertyUpdateResult> ApplyPropertyAsync(CameraPropertyViewModel propertyViewModel)
    {
        var validation = CameraPropertyValidator.ValidateWrite(
            propertyViewModel.Property,
            propertyViewModel.GetCandidateValue(),
            _isStreaming());
        if (!validation.Success)
        {
            Message = validation.ErrorMessage ?? "Property validation failed.";
            return validation;
        }

        var result = await _cameraService.SetPropertyAsync(propertyViewModel.Id, validation.UpdatedProperty!.Value);
        if (!result.Success || result.UpdatedProperty is null)
        {
            Message = result.ErrorMessage ?? "Property update failed.";
            return result;
        }

        propertyViewModel.UpdateAuthoritativeProperty(result.UpdatedProperty);
        PropertyApplied?.Invoke(this, result.UpdatedProperty);
        await RefreshAsync();
        Message = $"{result.UpdatedProperty.DisplayName} updated.";
        return result;
    }

    private void ApplyFilters()
    {
        var filtered = CameraPropertyFilterEngine.Apply(
                Properties.Select(property => property.Property),
                new CameraPropertyFilter(SearchText, SelectedCategory, WritableOnly))
            .Select(property => Properties.First(viewModel => viewModel.Id == property.Id));

        FilteredProperties.Clear();
        foreach (var property in filtered)
        {
            FilteredProperties.Add(property);
        }

        RefreshCounts();
    }

    private void RefreshCategories(IReadOnlyList<CameraProperty> properties)
    {
        var current = SelectedCategory;
        CategoryOptions.Clear();
        CategoryOptions.Add("All");

        var categories = properties
            .Select(property => property.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(CategorySortKey)
            .ThenBy(category => category, StringComparer.OrdinalIgnoreCase);

        foreach (var category in categories)
        {
            CategoryOptions.Add(category);
        }

        SelectedCategory = CategoryOptions.Contains(current) ? current : "All";
    }

    private void RefreshCounts()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(WritableCount));
        OnPropertyChanged(nameof(ReadOnlyCount));
        OnPropertyChanged(nameof(FilteredCount));
        OnPropertyChanged(nameof(CountsText));
        OnPropertyChanged(nameof(HasDirtyProperties));
    }

    private static IEnumerable<CameraProperty> SortProperties(IEnumerable<CameraProperty> properties)
    {
        return properties
            .OrderBy(property => CategorySortKey(property.Category))
            .ThenBy(property => property.DisplayOrder)
            .ThenBy(property => property.DisplayName, StringComparer.OrdinalIgnoreCase);
    }

    private static int CategorySortKey(string category)
    {
        var index = CameraPropertyCategories.DefaultOrder
            .Select((name, order) => new { name, order })
            .FirstOrDefault(candidate => string.Equals(candidate.name, category, StringComparison.OrdinalIgnoreCase));
        return index?.order ?? int.MaxValue;
    }
}
