using LinkedLamp.Models;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace LinkedLamp.Services;

public sealed class LinkedLampBLEService
{
    private enum EspToAppMessageType : byte
    {
        SSID,
        SSID_END,
        WIFI_OK,
        WIFI_FAIL
    }
    private enum AppToEspMessageType : byte
    {
        SSID_LIST_REQUEST,
        SSID_ACK,
        CONFIGURATION,
    }
    private static readonly Guid SERVICE_UUID = Guid.Parse("6E400001-B5A3-F393-E0A9-E50E24DCCA9E");
    private static readonly Guid APP_TO_ESP_UUID = Guid.Parse("6E400002-B5A3-F393-E0A9-E50E24DCCA9E");
    private static readonly Guid ESP_TO_APP_UUID = Guid.Parse("6E400003-B5A3-F393-E0A9-E50E24DCCA9E");

    private readonly IAdapter _adapter;
    private readonly ConcurrentDictionary<Guid, IDevice?> _foundDevices = [];
    private readonly List<string> _ssidList = [];
    private bool _isReceivingSsidList;
    private IDevice? _connectedDevice;
    private IService? _connectedService;
    private ICharacteristic? _appToEspChar;
    private ICharacteristic? _espToAppChar;
    private bool _isConnected;
    private bool _isConnecting;
    private Action? _onDeviceDisconnected;
    public bool IsScanning => _adapter.IsScanning;
    public bool IsConnected => _isConnected;
    public bool IsConnecting => _isConnecting;

    public Action? OnDeviceDisconnected { get => _onDeviceDisconnected; set => _onDeviceDisconnected = value; }

    public LinkedLampBLEService()
    {
        _adapter = CrossBluetoothLE.Current.Adapter;
    }
    public async Task<List<string>> ScanUntilFindAndConnectToBestDeviceThenRequestSsidsListAsync(string? deviceNameStartsWithFilter = null, int delayBetweenScansMs = 500, CancellationToken cancellationToken = default)
    {
        await ScanUntilFindAndConnectToBestDeviceAsync(deviceNameStartsWithFilter, delayBetweenScansMs, cancellationToken);
        return await RequestSsidList();
    }
    public async Task ScanUntilFindAndConnectToBestDeviceAsync(string? deviceNameStartsWithFilter = null, int delayBetweenScansMs = 500, CancellationToken cancellationToken = default)
    {
        bool connected = false;
        while (!connected)
        {
            connected = await ScanFindAndConnectToBestDeviceAsync(deviceNameStartsWithFilter, cancellationToken);
            if (!connected)
            {
                await Task.Delay(delayBetweenScansMs, cancellationToken);
            }
        }
    }
    public async Task<bool> ScanFindAndConnectToBestDeviceAsync(string? deviceNameStartsWithFilter = null, CancellationToken cancellationToken = default)
    {
        IDevice? bestDevice = (await ScanAndFindDevicesAsync(deviceNameStartsWithFilter, cancellationToken)).FirstOrDefault();
        if (bestDevice == null)
        {
            return false;
        }
        await ConnectAsync(bestDevice, cancellationToken);
        return true;
    }
    public async Task<IDevice?> ScanAndFindBestDeviceAsync(string? deviceNameStartsWithFilter = null, CancellationToken cancellationToken = default)
    {
        return (await ScanAndFindDevicesAsync(deviceNameStartsWithFilter, cancellationToken)).FirstOrDefault();
    }
    public async Task<List<IDevice?>> ScanAndFindDevicesAsync(string? deviceNameStartsWithFilter = null, CancellationToken cancellationToken = default)
    {
        if (_adapter.IsScanning)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [ScanAndFindDevicesAsync] <Exception> Adapter is already scanning.");
            throw new InvalidOperationException("Adapter is already scanning.");
        }
        if (_isConnecting)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [ScanAndFindDevicesAsync] <Exception> Adapter is already connecting.");
            throw new InvalidOperationException("Adapter is already connecting.");
        }
        if (_isConnected)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [ScanAndFindDevicesAsync] <Exception> Adapter is already connected.");
            throw new InvalidOperationException("Adapter is already connected.");
        }
        _foundDevices.Clear();
        _adapter.DeviceDiscovered += OnBLEDeviceDiscovered;
        using var reg = cancellationToken.Register(() =>
        {
            Debug.WriteLine($"[LinkedLampBLEService] [ScanAndFindDevicesAsync] Scan cancelled (register).");
            _ = _adapter.StopScanningForDevicesAsync();
        });
        Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [ScanAndFindDevicesAsync] Scan started.");
        try
        {
            await _adapter.StartScanningForDevicesAsync(new ScanFilterOptions { ServiceUuids = [SERVICE_UUID] }, (IDevice device) => device != null && (string.IsNullOrEmpty(deviceNameStartsWithFilter) || (device.Name != null && device.Name.StartsWith(deviceNameStartsWithFilter))), false, cancellationToken);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[LinkedLampBLEService] [ScanAndFindDevicesAsync] <Native Exception> {e.Message}");
            throw;
        }
        finally
        {
            _adapter.DeviceDiscovered -= OnBLEDeviceDiscovered;
        }
        if (cancellationToken.IsCancellationRequested)
        {
            Debug.WriteLine($"[LinkedLampBLEService] [ScanAndFindDevicesAsync] Scan cancelled (normal flow).");
        }
        cancellationToken.ThrowIfCancellationRequested();
        Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [ScanAndFindDevicesAsync] Scan complete.");
        if (_foundDevices.Count == 0)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [ScanAndFindDevicesAsync] No device found.");
            return new();
        }
        List<IDevice?> devices = _foundDevices.Where(x => x.Value != null).Select(x => x.Value).OrderByDescending(d => d?.Rssi).ToList();
        Debug.WriteLine($"[LinkedLampBLEService] [ScanAndFindDevicesAsync] Devices found ({_foundDevices.Count}).");
        return devices;
    }
    public async Task ConnectAsync(IDevice device, CancellationToken cancellationToken = default)
    {
        if (_adapter.IsScanning)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [ConnectAsync] <Exception> Adapter is already scanning.");
            throw new InvalidOperationException("Adapter is already scanning.");
        }
        if (_isConnecting)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [ConnectAsync] <Exception> Adapter is already connecting.");
            throw new InvalidOperationException("Adapter is already connecting.");
        }
        if (_isConnected)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [ConnectAsync] <Exception> Adapter is already connected.");
            throw new InvalidOperationException("Adapter is already connected.");
        }
        _connectedDevice = null;
        _isConnecting = true;
        Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [ConnectAsync] Connection started.");
        try
        {
            await _adapter.ConnectToDeviceAsync(device, default, cancellationToken);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[LinkedLampBLEService] [ConnectAsync] <Native Exception> {e.Message}");
            throw;
        }
        finally
        {
            _isConnecting = false;
        }
        Debug.WriteLine($"[LinkedLampBLEService] [OnDeviceDisconnected] Device disconnected Id:{device.Id} Name:{device.Name} RSSI:{device.Rssi} .");
        _adapter.DeviceDisconnected += OnBLEDeviceDisconnected;
        _isConnected = true;
        _connectedDevice = device;
        Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [ConnectAsync] BLE service setup started.");
        try
        {
            await SetupBLEService(cancellationToken);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[LinkedLamp] [LinkedLampBLEService] [ConnectAsync] <Native Exception> {e.Message}");
            await DisconnectAsync(cancellationToken);
            throw;
        }
        Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [ConnectAsync] BLE service setup success.");
    }
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_adapter.IsScanning)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [DisconnectAsync] <Exception> Adapter is already scanning.");
            throw new InvalidOperationException("Adapter is already scanning.");
        }
        if (_isConnecting)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [DisconnectAsync] <Exception> Adapter is already connecting.");
            throw new InvalidOperationException("Adapter is already connecting.");
        }
        if (!_isConnected)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [DisconnectAsync] <Exception> Adapter is not connected.");
            throw new InvalidOperationException("Adapter is not connected.");
        }
        Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [DisconnectAsync] Disconnection started");
        try
        {
            await _adapter.DisconnectDeviceAsync(_connectedDevice, cancellationToken);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[LinkedLampBLEService] [DisconnectAsync] <Native Exception> {e.Message}");
            throw;
        }
        finally
        {
            _adapter.DeviceDisconnected -= OnBLEDeviceDisconnected;
            _isConnected = false;
            _connectedDevice = null;
        }
        Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [DisconnectAsync] Disconnection sucess");
    }
    private async Task SetupBLEService(CancellationToken cancellationToken)
    {
        if (_connectedDevice == null)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [SetupBLEService] <Exception> No connected device.");
            throw new InvalidOperationException("No connected device.");
        }
        IReadOnlyList<IService> services;
        Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [SetupBLEService] Getting services started.");
        try
        {
            services = await _connectedDevice.GetServicesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[LinkedLampBLEService] [DisconnectAsync] <Native Exception> {e.Message}");
            throw new InvalidOperationException("Cannot get services.");
        }
        Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [SetupBLEService] Getting services success.");
        Debug.WriteLine($"[LinkedLamp] [LinkedLampBLEService] [SetupBLEService] Getting main service (UUID:{SERVICE_UUID}) started.");
        _connectedService = services.FirstOrDefault(s => s.Id == SERVICE_UUID);
        if (_connectedService == null)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [SetupBLEService] <Exception> Service not found.");
            throw new InvalidOperationException("Service not found.");
        }
        Debug.WriteLine($"[LinkedLamp] [LinkedLampBLEService] [SetupBLEService] Getting main service (UUID:{SERVICE_UUID}) success.");
        Debug.WriteLine($"[LinkedLamp] [LinkedLampBLEService] [SetupBLEService] Getting appToEsp characteristic (UUID:{APP_TO_ESP_UUID}) started.");
        _appToEspChar = await _connectedService.GetCharacteristicAsync(APP_TO_ESP_UUID, cancellationToken);
        if (_appToEspChar == null)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [SetupBLEService] <Exception> AppToEsp Characteristic not found.");
            throw new InvalidOperationException("AppToEsp Characteristic not found.");
        }
        Debug.WriteLine($"[LinkedLamp] [LinkedLampBLEService] [SetupBLEService] Getting appToEsp characteristic (UUID:{APP_TO_ESP_UUID}) success.");
        Debug.WriteLine($"[LinkedLamp] [LinkedLampBLEService] [SetupBLEService] Getting espToApp characteristic (UUID:{ESP_TO_APP_UUID}) started.");
        _espToAppChar = await _connectedService.GetCharacteristicAsync(ESP_TO_APP_UUID, cancellationToken);
        _espToAppChar.ValueUpdated += OnMessageReceived;
        await _espToAppChar.StartUpdatesAsync();
        if (_espToAppChar == null)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [SetupBLEService] <Exception> EspToApp Characteristic not found.");
            throw new InvalidOperationException("EspToApp Characteristic not found.");
        }
        Debug.WriteLine($"[LinkedLamp] [LinkedLampBLEService] [SetupBLEService] Getting espToApp characteristic (UUID:{ESP_TO_APP_UUID}) success.");
    }
    private void OnBLEDeviceDiscovered(object? sender, DeviceEventArgs e)
    {
        Debug.WriteLine($"[LinkedLampBLEService] [OnDeviceDiscovered] Device discovered Id:{e.Device.Id} Name:{e.Device.Name} RSSI:{e.Device.Rssi} .");
        _foundDevices[e.Device.Id] = e.Device;
    }
    private void OnBLEDeviceDisconnected(object? sender, DeviceEventArgs e)
    {
        Debug.WriteLine($"[LinkedLampBLEService] [OnDeviceDisconnected] Device disconnected Id:{e.Device.Id} Name:{e.Device.Name} RSSI:{e.Device.Rssi} .");
        _isConnected = false;
        _connectedDevice = null;
        _onDeviceDisconnected?.Invoke();
    }
    public async Task<List<string>> RequestSsidList()
    {
        if (_adapter.IsScanning)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [ConnectAsync] <Exception> Adapter is already scanning.");
            throw new InvalidOperationException("Adapter is already scanning.");
        }
        if (_isConnecting)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [ConnectAsync] <Exception> Adapter is already connecting.");
            throw new InvalidOperationException("Adapter is already connecting.");
        }
        if (!_isConnected)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [ConnectAsync] <Exception> Adapter is not connected.");
            throw new InvalidOperationException("Adapter is already connected.");
        }
        if (_espToAppChar == null)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [ConnectAsync] <Exception> EspToApp characteristic is null.");
            throw new InvalidOperationException("EspToApp characteristic is null.");
        }
        if (_appToEspChar == null)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [ConnectAsync] <Exception> AppToEsp characteristic is null.");
            throw new InvalidOperationException("AppToEsp characteristic is null.");
        }
        lock (_ssidList)
        {
            _ssidList.Clear();
        }
        _isReceivingSsidList = true;
        await _appToEspChar.WriteAsync([(byte)AppToEspMessageType.SSID_LIST_REQUEST]);
        while (_isReceivingSsidList)
        {
            await Task.Delay(100);
        }
        List<string> ssidList;
        lock (_ssidList)
        {
            ssidList = [.. _ssidList];
        }
        return ssidList;
    }
    private void OnMessageReceived(object? sender, CharacteristicUpdatedEventArgs e)
    {
        byte[] byteMsg = e.Characteristic.Value;
        if (byteMsg.Length == 0)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [ConnectAsync] <Exception> Empty message received from Esp.");
            throw new InvalidOperationException("Empty message received from Esp.");
        }
        EspToAppMessageType msgType = (EspToAppMessageType)byteMsg[0];
        Debug.WriteLine($"[LinkedLamp] [LinkedLampBLEService] [OnSsidReceived] Message received from esp (type : {msgType}).");
        byte[] msg = [.. byteMsg.Skip(1)];
        switch (msgType)
        {
            case EspToAppMessageType.SSID:
                OnSsidReceived(msg);
                break;
            case EspToAppMessageType.SSID_END:
                OnSsidEndReceived();
                break;
            case EspToAppMessageType.WIFI_OK:
                break;
            case EspToAppMessageType.WIFI_FAIL:
                break;
            default:
                break;
        }
    }
    private async void OnSsidReceived(byte[] ssidMsg)
    {
        if (_appToEspChar == null)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [OnSsidReceived] <Exception> AppToEsp characteristic is null.");
            throw new InvalidOperationException("AppToEsp characteristic is null.");
        }
        if (!_isReceivingSsidList)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [OnSsidReceived] <Exception> Received a ssid message but was not expecting one.");
            throw new InvalidOperationException("Received a ssid message but was not expecting one.");
        }
        if (ssidMsg.Length < 5)
        {
            Debug.WriteLine("[LinkedLamp] [LinkedLampBLEService] [OnSsidReceived] <Exception> Empty ssid message received from Esp.");
            throw new InvalidOperationException("Empty ssid message received from Esp.");
        }
        byte ssidIndex = ssidMsg[0];
        uint len = BitConverter.ToUInt32(ssidMsg, 1);
        Debug.WriteLine($"[LinkedLamp] [LinkedLampBLEService] [OnSsidReceived] Received ssid (length : {len}).");
        string ssid = Encoding.UTF8.GetString(ssidMsg, 5, (int)len);
        Debug.WriteLine($"[LinkedLamp] [LinkedLampBLEService] [OnSsidReceived] Received ssid {ssidIndex} : {ssid}).");
        lock (_ssidList)
        {
            _ssidList.Add(ssid);
        }
        Debug.WriteLine($"[LinkedLamp] [LinkedLampBLEService] [OnSsidReceived] Sending ACK for ssid {ssidIndex} : {ssid} started).");
        await _appToEspChar.WriteAsync([(byte)AppToEspMessageType.SSID_ACK, ssidIndex]);
        Debug.WriteLine($"[LinkedLamp] [LinkedLampBLEService] [OnSsidReceived] Sending ACK for ssid {ssidIndex} : {ssid} done).");
    }
    private void OnSsidEndReceived()
    {
        Debug.WriteLine($"[LinkedLamp] [LinkedLampBLEService] [OnSsidEndReceived] Received ssid end).");
        _isReceivingSsidList = false;
    }

    //public async Task ProvisionAsync(IDevice device, string groupName, string ssid, string pass, CancellationToken cancellationToken)
    //{
    //    return;
    //    await _gate.WaitAsync(cancellationToken);
    //    try
    //    {
    //        await CancelAndDisconnectInternalAsync();

    //        _activeOpCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    //        var token = _activeOpCts.Token;

    //        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    //        _discoHandler = (s, e) =>
    //        {
    //            if (_connectedDevice != null && e.Device.Id == _connectedDevice.Id)
    //                tcs.TrySetException(new InvalidOperationException("BLE device disconnected."));
    //        };

    //        _adapter.DeviceDisconnected += _discoHandler;

    //        try
    //        {
    //            await _adapter.ConnectToDeviceAsync(device, default, token);

    //            _connectedDevice = device;

    //            var services = await device.GetServicesAsync(token);
    //            var provService = services.FirstOrDefault(s => s.Id == SERVICE_UUID)
    //                ?? throw new InvalidOperationException("Service not found.");

    //            _wifiProvChar = await provService.GetCharacteristicAsync(WIFIPROV_UUID, token)
    //                ?? throw new InvalidOperationException("Characteristic not found.");

    //            _wifiConfChar = await provService.GetCharacteristicAsync(WIFICONF_UUID, token)
    //                ?? throw new InvalidOperationException("Characteristic not found.");

    //            _confHandler = (sender, e) =>
    //            {
    //                try
    //                {
    //                    var val = e.Characteristic.Value;
    //                    if (val == null || val.Length == 0)
    //                    {
    //                        tcs.TrySetException(new InvalidOperationException("Empty BLE response"));
    //                        return;
    //                    }

    //                    var ok = val[0] == 1;
    //                    tcs.TrySetResult(ok);
    //                }
    //                catch (Exception ex)
    //                {
    //                    tcs.TrySetException(ex);
    //                }
    //            };

    //            _wifiConfChar.ValueUpdated += _confHandler;
    //            await _wifiConfChar.StartUpdatesAsync(token);

    //            var payload = SerializeCredentials(groupName.Trim(), ssid.Trim(), pass.Trim());
    //            await _wifiProvChar.WriteAsync(payload, token);

    //            var okResult = await tcs.Task.WaitAsync(token);
    //            if (!okResult)
    //                throw new InvalidOperationException("ESP Wifi connection failed");
    //        }
    //        finally
    //        {
    //            await StopUpdatesAndUnsubscribeAsync();
    //            await DisconnectInternalAsync();

    //            if (_discoHandler != null)
    //            {
    //                try { _adapter.DeviceDisconnected -= _discoHandler; } catch { }
    //                _discoHandler = null;
    //            }

    //            ClearActiveOpCts();
    //        }
    //    }
    //    finally
    //    {
    //        _gate.Release();
    //    }
    //}

    //public async Task CancelAndDisconnectAsync()
    //{
    //    await _gate.WaitAsync();
    //    try
    //    {
    //        await CancelAndDisconnectInternalAsync();
    //    }
    //    finally
    //    {
    //        _gate.Release();
    //    }
    //}

    //private async Task CancelAndDisconnectInternalAsync()
    //{
    //try { _activeOpCts?.Cancel(); } catch { }
    //await StopUpdatesAndUnsubscribeAsync();
    //await DisconnectInternalAsync();

    //if (_adapter.IsScanning)
    //{
    //    try { await _adapter.StopScanningForDevicesAsync(); }
    //    catch { }
    //}

    //if (_discoHandler != null)
    //{
    //    try { _adapter.DeviceDisconnected -= _discoHandler; } catch { }
    //    _discoHandler = null;
    //}

    //ClearActiveOpCts();
    //}

    //private async Task StopUpdatesAndUnsubscribeAsync()
    //{
    //    //if (_wifiConfChar != null && _confHandler != null)
    //    //{
    //    //    try { _wifiConfChar.ValueUpdated -= _confHandler; } catch { }
    //    //    _confHandler = null;

    //    //    try { await _wifiConfChar.StopUpdatesAsync(); }
    //    //    catch { }
    //    //}
    //}

    //private async Task DisconnectInternalAsync()
    //{
    //    var dev = _connectedDevice;
    //    if (dev == null)
    //    {
    //        _wifiProvChar = null;
    //        _wifiConfChar = null;
    //        return;
    //    }

    //    try
    //    {
    //        Debug.WriteLine(">>> DisconnectDeviceAsync.");
    //        await _adapter.DisconnectDeviceAsync(dev);
    //    }
    //    catch { }
    //    finally
    //    {
    //        _connectedDevice = null;
    //        _wifiProvChar = null;
    //        _wifiConfChar = null;
    //    }
    //}

    //private void ClearActiveOpCts()
    //{
    //    try { _activeOpCts?.Dispose(); } catch { }
    //    _activeOpCts = null;
    //}

    private static byte[] SerializeCredentials(string groupName, string ssid, string pass)
    {
        var data = new List<byte>();

        var groupNameBytes = Encoding.UTF8.GetBytes(groupName);
        var ssidBytes = Encoding.UTF8.GetBytes(ssid);
        var passBytes = Encoding.UTF8.GetBytes(pass);

        data.Add((byte)groupNameBytes.Length);
        data.AddRange(groupNameBytes);

        data.Add((byte)ssidBytes.Length);
        data.AddRange(ssidBytes);

        data.Add((byte)passBytes.Length);
        data.AddRange(passBytes);

        return data.ToArray();
    }
}
