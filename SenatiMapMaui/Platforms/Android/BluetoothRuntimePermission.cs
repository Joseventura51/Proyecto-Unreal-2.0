using Android;
using Android.OS;
using Microsoft.Maui.ApplicationModel;

namespace SenatiMapMaui.Platforms.Android;

public sealed class BluetoothRuntimePermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions
    {
        get
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            {
                return
                [
                    (Manifest.Permission.BluetoothScan, true),
                    (Manifest.Permission.BluetoothConnect, true)
                ];
            }

            return
            [
                (Manifest.Permission.AccessFineLocation, true)
            ];
        }
    }
}
