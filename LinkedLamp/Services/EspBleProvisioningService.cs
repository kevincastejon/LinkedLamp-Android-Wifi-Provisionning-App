using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Extensions;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace LinkedLamp.Services;

public class EspBleProvisioningService
{
    private static readonly Guid SERVICE_UUID = Guid.Parse("6E400001-B5A3-F393-E0A9-E50E24DCCA9E");
    private static readonly Guid WIFIPROV_UUID = Guid.Parse("6E400002-B5A3-F393-E0A9-E50E24DCCA9E");
    private static readonly Guid WIFICONF_UUID = Guid.Parse("6E400003-B5A3-F393-E0A9-E50E24DCCA9E");

    private readonly IAdapter _adapter;
    private IDevice? _connectedDevice;
    private ICharacteristic? _wifiProvChar;
    private ICharacteristic? _wifiConfChar;
    private string _groupName = "";
    private string _ssid = "";
    private string _pass = "";
    EventHandler<CharacteristicUpdatedEventArgs>? _handler;
    public EspBleProvisioningService()
    {
        _adapter = CrossBluetoothLE.Current.Adapter;
    }
    public async Task<HashSet<IDevice>> Scan(CancellationToken cancellationToken = default)
    {
        Dictionary<Guid, IDevice> found = new();

        void Handler(object? sender, DeviceEventArgs e)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var device = e.Device;
            var name = device?.Name;

            if (string.IsNullOrWhiteSpace(name))
                return;

            if (!name.StartsWith("LinkedLamp_Caskev_", StringComparison.Ordinal))
                return;

            lock (found)
            {
                if (device != null)
                {
                    found[device.Id] = device;
                }
            }
        }

        _adapter.DeviceDiscovered += Handler;

        try
        {
            if (_adapter.IsScanning)
                await _adapter.StopScanningForDevicesAsync();

            await _adapter.StartScanningForDevicesAsync(
                new Plugin.BLE.Abstractions.ScanFilterOptions
                {
                    ServiceUuids = [SERVICE_UUID]
                },
                deviceFilter: null,
                allowDuplicatesKey: false,
                cancellationToken: cancellationToken
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        finally
        {
            _adapter.DeviceDiscovered -= Handler;

            if (_adapter.IsScanning)
            {
                try { await _adapter.StopScanningForDevicesAsync(); }
                catch { }
            }
        }

        lock (found)
            return found.Values.ToHashSet();
    }

    public async Task ConnectAndSetup(IDevice device, string groupName, string ssid, string pass, CancellationToken cancellationToken = default)
    {
        _groupName = groupName.Trim();
        _ssid = ssid.Trim();
        _pass = pass.Trim();
        await _adapter.ConnectToDeviceAsync(device, default, cancellationToken);
        var services = await device.GetServicesAsync(cancellationToken);
        var provService = services.FirstOrDefault(s => s.Id == SERVICE_UUID) ?? throw new InvalidOperationException("Service not found.");
        ICharacteristic wifiProvChar = await provService.GetCharacteristicAsync(WIFIPROV_UUID, cancellationToken) ?? throw new InvalidOperationException("Characteristic not found.");
        ICharacteristic wifiConfChar = await provService.GetCharacteristicAsync(WIFICONF_UUID, cancellationToken) ?? throw new InvalidOperationException("Characteristic not found.");
        if (wifiProvChar is null || wifiConfChar is null)
            throw new InvalidOperationException("Required characteristics not found.");
        _connectedDevice = device;
        _wifiProvChar = wifiProvChar;
        _wifiConfChar = wifiConfChar;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        CancellationTokenRegistration ctr = cancellationToken.Register(() =>
        {
            try
            {
                _wifiConfChar.ValueUpdated -= _handler;
                _wifiConfChar.StopUpdatesAsync().ConfigureAwait(false);
            }
            catch { }

            tcs.TrySetCanceled(cancellationToken);
        });

        _handler = (sender, e) =>
        {
            _wifiConfChar.ValueUpdated -= _handler;

            if (e.Characteristic.Value?.Length > 0)
            {
                bool success = e.Characteristic.Value[0] == 1;
                tcs.TrySetResult(success);
            }
            else
            {
                tcs.TrySetException(new InvalidOperationException("Empty BLE response"));
            }
        };

        _wifiConfChar.ValueUpdated += _handler;
        await _wifiConfChar.StartUpdatesAsync(cancellationToken);
        await SendWifiCredentialsAndConnectAsync(cancellationToken);
        bool wifiSuccess;
        try
        {
            wifiSuccess = await tcs.Task;
        }
        finally
        {
            ctr.Dispose();
            if (_handler != null)
            {
                _wifiConfChar.ValueUpdated -= _handler;
            }
        }
        await DisconnectAsync();
        if (!wifiSuccess)
        {
            throw new Exception("ESP Wifi connection failed");
        }
    }

    private async Task DisconnectAsync()
    {
        var dev = _connectedDevice;
        if (dev is null)
            return;

        await _adapter.DisconnectDeviceAsync(dev);
        _connectedDevice = null;
        _wifiProvChar = null;
    }
    private async Task SendWifiCredentialsAndConnectAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_ssid))
            throw new ArgumentException("SSID empty.");

        await _wifiProvChar!.WriteAsync(SerializeCredentials(_groupName, _ssid, _pass), cancellationToken);
    }
    static private byte[] SerializeCredentials(string groupName, string ssid, string pass)
    {
        List<byte> data = new();
        byte[] groupNameBytes = Encoding.UTF8.GetBytes(groupName);
        byte[] ssidBytes = Encoding.UTF8.GetBytes(ssid);
        byte[] passBytes = Encoding.UTF8.GetBytes(pass);
        data.Add((byte)groupNameBytes.Length);
        data.AddRange(groupNameBytes);
        data.Add((byte)ssidBytes.Length);
        data.AddRange(ssidBytes);
        data.Add((byte)passBytes.Length);
        data.AddRange(passBytes);
        return [.. data];
    }
}
