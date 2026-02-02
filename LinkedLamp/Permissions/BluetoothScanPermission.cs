#if ANDROID
using Android.OS;
#endif

namespace LinkedLamp.Permissions;

public class BluetoothScanPermission : Microsoft.Maui.ApplicationModel.Permissions.BasePlatformPermission
{
#if ANDROID
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions
        => Build.VERSION.SdkInt >= BuildVersionCodes.S  // API 31 = Android 12
            ? new[]
            {
                ("android.permission.BLUETOOTH_SCAN", true),
                ("android.permission.BLUETOOTH_CONNECT", true),
            }
            : new[]
            {
                // Android 11 et moins : scan BLE => localisation (runtime)
                ("android.permission.ACCESS_FINE_LOCATION", true),
            };
#endif
}
