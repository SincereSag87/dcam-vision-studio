using DcamVision.Core;

namespace DcamVision.Tests;

public sealed class CameraPropertyTests
{
    [Fact]
    public void CameraPropertyValidator_RejectsReadOnlyWrite()
    {
        var property = IntegerProperty() with { Access = CameraPropertyAccess.Read };

        var result = CameraPropertyValidator.ValidateWrite(property, 4);

        Assert.False(result.Success);
        Assert.Contains("read-only", result.ErrorMessage);
    }

    [Fact]
    public void CameraPropertyValidator_ValidatesIntegerRange()
    {
        var result = CameraPropertyValidator.ValidateWrite(IntegerProperty(), 11);

        Assert.False(result.Success);
        Assert.Contains("no more than 10", result.ErrorMessage);
    }

    [Fact]
    public void CameraPropertyValidator_ValidatesFloatingPointRange()
    {
        var property = GainProperty();

        var result = CameraPropertyValidator.ValidateWrite(property, "0.5");

        Assert.False(result.Success);
        Assert.Contains("at least 1", result.ErrorMessage);
    }

    [Fact]
    public void CameraPropertyValidator_ValidatesStep()
    {
        var result = CameraPropertyValidator.ValidateWrite(GainProperty(), "1.25");

        Assert.False(result.Success);
        Assert.Contains("increments", result.ErrorMessage);
    }

    [Fact]
    public void CameraPropertyValidator_AcceptsBoolean()
    {
        var property = new CameraProperty
        {
            Id = "sensor.coolingEnabled",
            DisplayName = "Cooling Enabled",
            PropertyType = CameraPropertyType.Boolean,
            Value = true
        };

        var result = CameraPropertyValidator.ValidateWrite(property, false);

        Assert.True(result.Success);
        Assert.False((bool)result.UpdatedProperty!.Value);
    }

    [Fact]
    public void CameraPropertyValidator_AcceptsText()
    {
        var property = new CameraProperty
        {
            Id = "advanced.label",
            DisplayName = "Label",
            PropertyType = CameraPropertyType.Text,
            Value = "A"
        };

        var result = CameraPropertyValidator.ValidateWrite(property, "B");

        Assert.True(result.Success);
        Assert.Equal("B", result.UpdatedProperty!.Value);
    }

    [Fact]
    public void CameraPropertyValidator_ValidatesEnumMembership()
    {
        var property = TriggerModeProperty();

        var result = CameraPropertyValidator.ValidateWrite(property, "Invalid");

        Assert.False(result.Success);
        Assert.Contains("Internal", result.ErrorMessage);
    }

    [Fact]
    public void CameraPropertyValidator_RejectsStreamingWriteWhenNotAllowed()
    {
        var property = IntegerProperty();

        var result = CameraPropertyValidator.ValidateWrite(property, 5, isStreaming: true);

        Assert.False(result.Success);
        Assert.Contains("live acquisition", result.ErrorMessage);
    }

    [Fact]
    public void CameraPropertyValidator_AllowsStreamingWriteWhenAllowed()
    {
        var property = GainProperty() with { Access = CameraPropertyAccess.Read | CameraPropertyAccess.Write | CameraPropertyAccess.WriteWhileStreaming };

        var result = CameraPropertyValidator.ValidateWrite(property, 2.5, isStreaming: true);

        Assert.True(result.Success);
    }

    [Fact]
    public void CameraPropertyValueConverter_ParsesEnumDisplayName()
    {
        var property = TriggerModeProperty();

        var success = CameraPropertyValueConverter.TryConvert(property, "External", out var value, out _);

        Assert.True(success);
        Assert.Equal("External", value);
    }

    [Fact]
    public async Task SimulatedCameraService_ReturnsExpandedCategorizedProperties()
    {
        var service = await CreateConnectedSimulatorAsync();

        var properties = await service.GetPropertiesAsync();

        Assert.True(properties.Count >= 17);
        Assert.Contains(properties, property => property.Id == "sensor.temperature" && property.Category == CameraPropertyCategories.Sensor);
        Assert.Contains(properties, property => property.Id == "device.firmwareVersion" && property.IsReadOnly);
    }

    [Fact]
    public async Task SimulatedCameraService_GenericGainUpdateChangesCurrentSettings()
    {
        var service = await CreateConnectedSimulatorAsync();

        var result = await service.SetPropertyAsync("sensor.gain", 2.5);

        Assert.True(result.Success);
        Assert.Equal(2.5, service.CurrentSettings.Gain);
        Assert.Equal(2.5, (await service.GetPropertyAsync("sensor.gain"))!.Value);
    }

    [Fact]
    public async Task SimulatedCameraService_GenericExposureUpdateSynchronizesSpecializedExposure()
    {
        var service = await CreateConnectedSimulatorAsync();

        var result = await service.SetPropertyAsync("exposure.time", 50.0);

        Assert.True(result.Success);
        Assert.Equal(TimeSpan.FromMilliseconds(50), service.CurrentSettings.Exposure);
    }

    [Fact]
    public async Task SimulatedCameraService_SetExposureUpdatesDynamicExposureProperty()
    {
        var service = await CreateConnectedSimulatorAsync();

        await service.SetExposureAsync(TimeSpan.FromMilliseconds(12.5));

        Assert.Equal(12.5, (await service.GetPropertyAsync("exposure.time"))!.Value);
    }

    [Fact]
    public async Task SimulatedCameraService_RejectsReadOnlySensorTemperatureWrite()
    {
        var service = await CreateConnectedSimulatorAsync();

        var result = await service.SetPropertyAsync("sensor.temperature", -20.0);

        Assert.False(result.Success);
        Assert.Contains("read-only", result.ErrorMessage);
    }

    [Fact]
    public async Task SimulatedCameraService_TriggerPolarityDependsOnExternalTrigger()
    {
        var service = await CreateConnectedSimulatorAsync();

        var unavailable = await service.SetPropertyAsync("trigger.polarity", "Falling Edge");
        await service.SetPropertyAsync("trigger.mode", "External");
        var available = await service.SetPropertyAsync("trigger.polarity", "Falling Edge");

        Assert.False(unavailable.Success);
        Assert.True(available.Success);
        Assert.Equal("Falling Edge", (await service.GetPropertyAsync("trigger.polarity"))!.Value);
    }

    [Fact]
    public async Task SimulatedCameraService_FanModeDependsOnCooling()
    {
        var service = await CreateConnectedSimulatorAsync();

        await service.SetPropertyAsync("sensor.coolingEnabled", false);
        var result = await service.SetPropertyAsync("device.fanMode", "High");

        Assert.False(result.Success);
        Assert.Equal("Off", (await service.GetPropertyAsync("device.fanMode"))!.Value);
    }

    [Fact]
    public async Task SimulatedCameraService_RejectsStoppedOnlyPropertyDuringStreaming()
    {
        var service = await CreateConnectedSimulatorAsync(new SimulatedCameraOptions { FramesPerSecond = 60 });
        await using var enumerator = service.StreamFramesAsync().GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());

        var result = await service.SetPropertyAsync("image.width", 1024);

        Assert.False(result.Success);
        Assert.Contains("live acquisition", result.ErrorMessage);
    }

    [Fact]
    public async Task CameraPropertyFilterEngine_SearchesMetadataLocally()
    {
        var service = await CreateConnectedSimulatorAsync();
        var properties = await service.GetPropertiesAsync();

        var filtered = CameraPropertyFilterEngine.Apply(properties, new CameraPropertyFilter(SearchText: "trigger"));

        Assert.True(filtered.Count >= 2);
        Assert.All(filtered, property => Assert.Contains("trigger", $"{property.DisplayName} {property.Id} {property.Category} {property.Description}".ToLowerInvariant()));
    }

    [Fact]
    public async Task CameraPropertyFilterEngine_FiltersByCategory()
    {
        var service = await CreateConnectedSimulatorAsync();
        var properties = await service.GetPropertiesAsync();

        var filtered = CameraPropertyFilterEngine.Apply(properties, new CameraPropertyFilter(Category: CameraPropertyCategories.Sensor));

        Assert.All(filtered, property => Assert.Equal(CameraPropertyCategories.Sensor, property.Category));
    }

    [Fact]
    public async Task CameraPropertyFilterEngine_FiltersWritableOnly()
    {
        var service = await CreateConnectedSimulatorAsync();
        var properties = await service.GetPropertiesAsync();

        var filtered = CameraPropertyFilterEngine.Apply(properties, new CameraPropertyFilter(WritableOnly: true));

        Assert.All(filtered, property => Assert.False(property.IsReadOnly));
    }

    private static CameraProperty IntegerProperty()
    {
        return new CameraProperty
        {
            Id = "image.width",
            DisplayName = "Width",
            PropertyType = CameraPropertyType.Integer,
            Value = 5,
            Minimum = 1,
            Maximum = 10,
            Step = 1
        };
    }

    private static CameraProperty GainProperty()
    {
        return new CameraProperty
        {
            Id = "sensor.gain",
            DisplayName = "Gain",
            PropertyType = CameraPropertyType.FloatingPoint,
            Value = 1.0,
            Minimum = 1.0,
            Maximum = 8.0,
            Step = 0.1,
            Unit = "x"
        };
    }

    private static CameraProperty TriggerModeProperty()
    {
        return new CameraProperty
        {
            Id = "trigger.mode",
            DisplayName = "Trigger Mode",
            PropertyType = CameraPropertyType.Enumeration,
            Value = "Internal",
            Options =
            [
                new CameraPropertyOption("Internal", "Internal"),
                new CameraPropertyOption("External", "External")
            ]
        };
    }

    private static async Task<SimulatedCameraService> CreateConnectedSimulatorAsync(SimulatedCameraOptions? options = null)
    {
        var service = new SimulatedCameraService(options ?? new SimulatedCameraOptions());
        var device = (await service.DiscoverAsync()).Single();
        await service.ConnectAsync(device.Id);
        return service;
    }
}
