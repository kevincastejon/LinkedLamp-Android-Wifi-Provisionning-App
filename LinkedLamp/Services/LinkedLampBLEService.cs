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
        CONFIGURATION_ACK,
        WIFI_OK,
        WIFI_FAIL
    }
    private enum AppToEspMessageType : byte
    {
        SSID_LIST_REQUEST,
        SSID_ACK,
        CONFIGURATION,
        CONFIGURATION_END,
    }
    private static readonly Guid SERVICE_UUID = Guid.Parse("6E400001-B5A3-F393-E0A9-E50E24DCCA9E");
    private static readonly Guid APP_TO_ESP_UUID = Guid.Parse("6E400002-B5A3-F393-E0A9-E50E24DCCA9E");
    private static readonly Guid ESP_TO_APP_UUID = Guid.Parse("6E400003-B5A3-F393-E0A9-E50E24DCCA9E");

    private readonly IAdapter _adapter;
    private readonly ConcurrentDictionary<Guid, IDevice?> _foundDevices = [];
    private readonly ConcurrentDictionary<int, string> _ssidList = [];
    private bool _isReceivingSsidList;
    private IDevice? _connectedDevice;
    private IService? _connectedService;
    private ICharacteristic? _appToEspChar;
    private ICharacteristic? _espToAppChar;
    private bool _isConnected;
    private bool _isConnecting;
    private Action? _onDeviceDisconnected;
    private bool _verbose;
    private TaskCompletionSource? _ssidListTcs;
    private TaskCompletionSource<bool>? _provisionTcs;
    public bool IsScanning => _adapter.IsScanning;
    public bool IsConnected => _isConnected;
    public bool IsConnecting => _isConnecting;

    public Action? OnDeviceDisconnected { get => _onDeviceDisconnected; set => _onDeviceDisconnected = value; }
    public bool Verbose { get => _verbose; set => _verbose = value; }

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
            Log($"[ScanAndFindDevicesAsync] <Exception> Adapter is already scanning.");
            throw new InvalidOperationException("Adapter is already scanning.");
        }
        if (_isConnecting)
        {
            Log($"[ScanAndFindDevicesAsync] <Exception> Adapter is already connecting.");
            throw new InvalidOperationException("Adapter is already connecting.");
        }
        if (_isConnected)
        {
            Log($"[ScanAndFindDevicesAsync] <Exception> Adapter is already connected.");
            throw new InvalidOperationException("Adapter is already connected.");
        }
        _foundDevices.Clear();
        _adapter.DeviceDiscovered += OnBLEDeviceDiscovered;
        using var reg = cancellationToken.Register(() =>
        {
            Log($"[ScanAndFindDevicesAsync] Scan cancelled (register).");
            _ = _adapter.StopScanningForDevicesAsync();
        });
        Log($"[ScanAndFindDevicesAsync] Scan started.");
        try
        {
            await _adapter.StartScanningForDevicesAsync(new ScanFilterOptions { ServiceUuids = [SERVICE_UUID] }, (IDevice device) => device != null && (string.IsNullOrEmpty(deviceNameStartsWithFilter) || (device.Name != null && device.Name.StartsWith(deviceNameStartsWithFilter))), false, cancellationToken);
        }
        catch (Exception e)
        {
            Log($"[ScanAndFindDevicesAsync] <Native Exception> {e.Message}");
            throw;
        }
        finally
        {
            _adapter.DeviceDiscovered -= OnBLEDeviceDiscovered;
        }
        if (cancellationToken.IsCancellationRequested)
        {
            Log($"[ScanAndFindDevicesAsync] Scan cancelled (normal flow).");
        }
        cancellationToken.ThrowIfCancellationRequested();
        Log($"[ScanAndFindDevicesAsync] Scan complete.");
        if (_foundDevices.Count == 0)
        {
            Log($"[ScanAndFindDevicesAsync] No device found.");
            return new();
        }
        List<IDevice?> devices = _foundDevices.Where(x => x.Value != null).Select(x => x.Value).OrderByDescending(d => d?.Rssi).ToList();
        Log($"[ScanAndFindDevicesAsync] Devices found ({_foundDevices.Count}).");
        return devices;
    }
    public async Task ConnectAsync(IDevice device, CancellationToken cancellationToken = default)
    {
        if (_adapter.IsScanning)
        {
            Log($"[ConnectAsync] <Exception> Adapter is already scanning.");
            throw new InvalidOperationException("Adapter is already scanning.");
        }
        if (_isConnecting)
        {
            Log($"[ConnectAsync] <Exception> Adapter is already connecting.");
            throw new InvalidOperationException("Adapter is already connecting.");
        }
        if (_isConnected)
        {
            Log($"[ConnectAsync] <Exception> Adapter is already connected.");
            throw new InvalidOperationException("Adapter is already connected.");
        }
        _connectedDevice = null;
        _isConnecting = true;
        Log($"[ConnectAsync] Connection started.");
        try
        {
            await _adapter.ConnectToDeviceAsync(device, default, cancellationToken);
        }
        catch (Exception e)
        {
            Log($"[ConnectAsync] <Native Exception> {e.Message}");
            throw;
        }
        finally
        {
            _isConnecting = false;
        }
        Log($"[ConnectAsync] Device connected Id:{device.Id} Name:{device.Name} RSSI:{device.Rssi} .");
        _adapter.DeviceDisconnected += OnBLEDeviceDisconnected;
        _adapter.DeviceConnectionLost += OnBLEDeviceDisconnected;
        _isConnected = true;
        _connectedDevice = device;
        Log($"[ConnectAsync] BLE service setup started.");
        try
        {
            await SetupBLEService(cancellationToken);
        }
        catch (Exception e)
        {
            Log($"[ConnectAsync] <Native Exception> {e.Message}");
            await DisconnectAsync(cancellationToken);
            throw;
        }
        Log($"[ConnectAsync] BLE service setup success.");
    }
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_adapter.IsScanning)
        {
            Log($"[DisconnectAsync] <Exception> Adapter is already scanning.");
            throw new InvalidOperationException("Adapter is already scanning.");
        }
        if (_isConnecting)
        {
            Log($"[DisconnectAsync] <Exception> Adapter is already connecting.");
        }
        if (!_isConnected)
        {
            Log($"[DisconnectAsync] <Exception> Adapter is not connected.");
        }
        Log($"[DisconnectAsync] Disconnection started");
        _appToEspChar = null;
        if (_espToAppChar != null)
        {
            _espToAppChar.ValueUpdated -= OnMessageReceived;
            try
            {
                await _espToAppChar.StopUpdatesAsync();
            }
            catch (Exception e)
            {
                Log($"[DisconnectAsync] <Native Exception> {e.Message}");
            }
            finally
            {
                _espToAppChar = null;
            }
        }
        try
        {
            await _adapter.DisconnectDeviceAsync(_connectedDevice, cancellationToken);
        }
        catch (Exception e)
        {
            Log($"[DisconnectAsync] <Native Exception> {e.Message}");
        }
        finally
        {
            _adapter.DeviceDisconnected -= OnBLEDeviceDisconnected;
            _adapter.DeviceConnectionLost -= OnBLEDeviceDisconnected;
            _isConnected = false;
            _connectedDevice = null;
        }
        Log($"[DisconnectAsync] Disconnection success");
    }
    public async Task<List<string>> RequestSsidList()
    {
        if (_adapter.IsScanning)
        {
            Log($"[ConnectAsync] <Exception> Adapter is already scanning.");
            throw new InvalidOperationException("Adapter is already scanning.");
        }
        if (_isConnecting)
        {
            Log($"[ConnectAsync] <Exception> Adapter is already connecting.");
            throw new InvalidOperationException("Adapter is already connecting.");
        }
        if (!_isConnected)
        {
            Log($"[ConnectAsync] <Exception> Adapter is not connected.");
            throw new InvalidOperationException("Adapter is already connected.");
        }
        if (_espToAppChar == null)
        {
            Log($"[ConnectAsync] <Exception> EspToApp characteristic is null.");
            throw new InvalidOperationException("EspToApp characteristic is null.");
        }
        if (_appToEspChar == null)
        {
            Log($"[ConnectAsync] <Exception> AppToEsp characteristic is null.");
            throw new InvalidOperationException("AppToEsp characteristic is null.");
        }
        _ssidListTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ssidList.Clear();
        _isReceivingSsidList = true;
        Log($"[ConnectAsync] Sending ssids list request started.");
        try
        {
            await _appToEspChar.WriteAsync([(byte)AppToEspMessageType.SSID_LIST_REQUEST]);
        }
        catch (Exception e)
        {
            _ssidListTcs = null;
            Log($"[ConnectAsync] <Native Exception> " + e);
            throw;
        }
        Log($"[ConnectAsync] Sending ssids list request done.");
        Log($"[ConnectAsync] Awaiting ssid list to be received.");
        await _ssidListTcs.Task;
        _ssidListTcs = null;
        Log($"[ConnectAsync] Ssids list received with {_ssidList.Count} results.");
        List<string> ssidList = new();
        for (int i = 0; i < _ssidList.Count; i++)
        {
            ssidList.Add(_ssidList[i]);
        }
        return ssidList;
    }
    public async Task<bool> ProvisionAsync(string ssid, string password, string groupName, CancellationToken cancellationToken = default)
    {
        if (_appToEspChar == null)
        {
            Log($"[ProvisionAsync] <Exception> AppToEsp characteristic is null.");
            throw new InvalidOperationException("AppToEsp characteristic is null.");
        }
        var payload = SerializeConfiguration(groupName.Trim(), ssid.Trim(), password.Trim());
        _provisionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await _appToEspChar.WriteAsync(payload, cancellationToken);
        }
        catch (Exception e)
        {
            _provisionTcs = null;
            Log($"[DisconnectAsync] <Native Exception> {e.Message}");
            throw;
        }
        bool espWifiConnected = await _provisionTcs.Task;
        _provisionTcs = null;
        return espWifiConnected;
    }
    private async Task SetupBLEService(CancellationToken cancellationToken)
    {
        if (_connectedDevice == null)
        {
            Log($"[SetupBLEService] <Exception> No connected device.");
            throw new InvalidOperationException("No connected device.");
        }
        IReadOnlyList<IService> services;
        Log($"[SetupBLEService] Getting services started.");
        try
        {
            services = await _connectedDevice.GetServicesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            Log($"[DisconnectAsync] <Native Exception> {e.Message}");
            throw;
        }
        Log($"[SetupBLEService] Getting services success.");
        Log($"[SetupBLEService] Getting main service (UUID:{SERVICE_UUID}) started.");
        _connectedService = services.FirstOrDefault(s => s.Id == SERVICE_UUID);
        if (_connectedService == null)
        {
            Log($"[SetupBLEService] <Exception> Service not found.");
            throw new InvalidOperationException("Service not found.");
        }
        Log($"[SetupBLEService] Getting main service (UUID:{SERVICE_UUID}) success.");
        Log($"[SetupBLEService] Getting appToEsp characteristic (UUID:{APP_TO_ESP_UUID}) started.");
        _appToEspChar = await _connectedService.GetCharacteristicAsync(APP_TO_ESP_UUID, cancellationToken);
        if (_appToEspChar == null)
        {
            Log($"[SetupBLEService] <Exception> AppToEsp Characteristic not found.");
            throw new InvalidOperationException("AppToEsp Characteristic not found.");
        }
        Log($"[SetupBLEService] Getting appToEsp characteristic (UUID:{APP_TO_ESP_UUID}) success.");
        Log($"[SetupBLEService] Getting espToApp characteristic (UUID:{ESP_TO_APP_UUID}) started.");
        _espToAppChar = await _connectedService.GetCharacteristicAsync(ESP_TO_APP_UUID, cancellationToken);
        _espToAppChar.ValueUpdated += OnMessageReceived;
        await _espToAppChar.StartUpdatesAsync();
        if (_espToAppChar == null)
        {
            Log($"[SetupBLEService] <Exception> EspToApp Characteristic not found.");
            throw new InvalidOperationException("EspToApp Characteristic not found.");
        }
        Log($"[SetupBLEService] Getting espToApp characteristic (UUID:{ESP_TO_APP_UUID}) success.");
    }
    private void OnBLEDeviceDiscovered(object? sender, DeviceEventArgs e)
    {
        Log($"[OnDeviceDiscovered] Device discovered Id:{e.Device.Id} Name:{e.Device.Name} RSSI:{e.Device.Rssi} .");
        _foundDevices[e.Device.Id] = e.Device;
    }
    private void OnBLEDeviceDisconnected(object? sender, DeviceEventArgs e)
    {
        Log($"[OnDeviceDisconnected] Device disconnected Id:{e.Device.Id} Name:{e.Device.Name} RSSI:{e.Device.Rssi} .");
        _isConnected = false;
        _connectedDevice = null;
        _onDeviceDisconnected?.Invoke();
    }
    private void OnMessageReceived(object? sender, CharacteristicUpdatedEventArgs e)
    {
        if (!_isConnected)
        {
            Log($"[OnMessageReceived] <Warning> Received message from Esp after disconnection.");
            return;
        }
        byte[] byteMsg = e.Characteristic.Value;
        if (byteMsg.Length == 0)
        {
            Log($"[OnMessageReceived] <Exception> Empty message received from Esp.");
            return;
        }
        EspToAppMessageType msgType = (EspToAppMessageType)byteMsg[0];
        Log($"[OnMessageReceived] Message received from esp (type : {msgType}).");
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
                OnEspWifiConnected();
                break;
            case EspToAppMessageType.WIFI_FAIL:
                OnEspWifiFailed();
                break;
            default:
                break;
        }
    }
    private void OnEspWifiConnected()
    {
        _provisionTcs?.TrySetResult(true);
    }
    private void OnEspWifiFailed()
    {
        _provisionTcs?.TrySetResult(false);        
    }
    private async void OnSsidReceived(byte[] ssidMsg)
    {
        if (_appToEspChar == null)
        {
            Log($"[OnSsidReceived] <Exception> AppToEsp characteristic is null.");
            _ssidListTcs?.TrySetException(new InvalidOperationException("AppToEsp characteristic is null."));
            return;
        }
        if (!_isReceivingSsidList)
        {
            Log($"[OnSsidReceived] <Exception> Received a ssid message but was not expecting one.");
            _ssidListTcs?.TrySetException(new InvalidOperationException("Received a ssid message but was not expecting one."));
            return;
        }
        if (ssidMsg.Length < 4)
        {
            Log($"[OnSsidReceived] <Exception> Ssid message received with wrong format.");
            _ssidListTcs?.TrySetException(new InvalidOperationException("Empty ssid message received from Esp."));
            return;
        }
        byte ssidIndex = ssidMsg[0];
        byte chunkCount = ssidMsg[1];
        byte chunkIndex = ssidMsg[2];
        byte len = ssidMsg[3];
        string ssidChunk;
        try
        {
            ssidChunk = Encoding.UTF8.GetString(ssidMsg, 4, len);
        }
        catch (Exception e)
        {
            Log($"[OnSsidReceived] <Native Exception> " + e.Message);
            _ssidListTcs?.TrySetException(e);
            return;
        }
        Log($"[OnSsidReceived] Received ssid {ssidIndex} chunk {chunkIndex}/{chunkCount} : \"{ssidChunk}\".");
        if (!_ssidList.ContainsKey(ssidIndex))
        {
            _ssidList[ssidIndex] = "";
        }
        _ssidList[ssidIndex] += ssidChunk;
        Log($"[OnSsidReceived] Sending ACK for ssid {ssidIndex} chunk {chunkIndex}/{chunkCount} : \"{ssidChunk}\" started.");
        try
        {
            await _appToEspChar.WriteAsync([(byte)AppToEspMessageType.SSID_ACK, ssidIndex, chunkIndex]);
        }
        catch (Exception e)
        {
            Log($"[OnSsidReceived] <Native Exception> " + e.Message);
            _ssidListTcs?.TrySetException(e);
            return;
        }
        Log($"[OnSsidReceived] Sending ACK for ssid {ssidIndex} chunk {chunkIndex}/{chunkCount} : \"{ssidChunk}\" done.");
    }
    private void OnSsidEndReceived()
    {
        Log($"[OnSsidEndReceived] Received ssid end.");
        _isReceivingSsidList = false;
        _ssidListTcs?.SetResult();
    }
    private void Log(string message)
    {
        if (_verbose)
        {
            Debug.WriteLine($"[LinkedLamp] [LinkedLampBLEService] {message}");
        }
    }
    private static byte[] SerializeConfiguration(string groupName, string ssid, string pass)
    {
        var data = new List<byte>
        {
            (byte)AppToEspMessageType.CONFIGURATION
        };
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
