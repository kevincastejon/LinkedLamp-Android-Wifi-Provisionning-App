namespace LinkedRGB.Permissions;

public class BluetoothScanPermission : Microsoft.Maui.ApplicationModel.Permissions.BasePlatformPermission
{
#if ANDROID
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        new[]
        {
            ("android.permission.BLUETOOTH_SCAN", true),
            ("android.permission.BLUETOOTH_CONNECT", true),
            ("android.permission.ACCESS_FINE_LOCATION", true),
        };
#endif
}
