#if ANDROID
using Android.App;
using Android.Bluetooth;
using Android.Content;
namespace LinkedLamp.Platforms.Android;

public static class AndroidBluetoothEnabler
{
    const int RequestEnableBtCode = 4242;
    static TaskCompletionSource<bool>? _tcs;

    public static Task<bool> RequestEnableAsync()
    {
        BluetoothAdapter? adapter = null;
#if ANDROID31_0_OR_GREATER
        var bluetoothManager = (BluetoothManager?)Platform.CurrentActivity?.GetSystemService(Context.BluetoothService);
        adapter = bluetoothManager?.Adapter;
#else
        adapter = BluetoothAdapter.DefaultAdapter;
#endif
        if (adapter == null)
            return Task.FromResult(false); // device has no Bluetooth

        if (adapter.IsEnabled)
            return Task.FromResult(true);

        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException("No current Activity.");

        _tcs = new TaskCompletionSource<bool>();

        var intent = new Intent(BluetoothAdapter.ActionRequestEnable);
        activity.StartActivityForResult(intent, RequestEnableBtCode);

        return _tcs.Task;
    }

    public static void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != RequestEnableBtCode)
            return;

        _tcs?.TrySetResult(resultCode == Result.Ok);
        _tcs = null;
    }
}
#endif