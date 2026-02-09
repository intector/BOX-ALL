using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using System;

namespace BOX_ALL
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
        ScreenOrientation = ScreenOrientation.Portrait)]  // Lock to portrait mode
    public class MainActivity : MauiAppCompatActivity
    {
        // Singleton instance for SAF callbacks
        public static MainActivity? Instance { get; private set; }

        // Event for activity result
        public event Action<int, Result, Intent?>? ActivityResult;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Instance = this;

            // Request permissions on first run
            RequestStoragePermissions();
        }

        protected override void OnDestroy()
        {
            Instance = null;
            base.OnDestroy();
        }

        protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent? data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            // Notify subscribers (folder picker service)
            ActivityResult?.Invoke(requestCode, resultCode, data);
        }

        private void RequestStoragePermissions()
        {
            // Check if we need to request permissions
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                var permissions = new[]
                {
                    Android.Manifest.Permission.WriteExternalStorage,
                    Android.Manifest.Permission.ReadExternalStorage
                };

                var permissionsToRequest = new System.Collections.Generic.List<string>();

                foreach (var permission in permissions)
                {
                    if (ContextCompat.CheckSelfPermission(this, permission) != Permission.Granted)
                    {
                        permissionsToRequest.Add(permission);
                    }
                }

                if (permissionsToRequest.Count > 0)
                {
                    ActivityCompat.RequestPermissions(this, permissionsToRequest.ToArray(), 1001);
                }
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            if (requestCode == 1001)
            {
                // Handle permission results if needed
                for (int i = 0; i < permissions.Length; i++)
                {
                    if (grantResults[i] == Permission.Granted)
                    {
                        System.Diagnostics.Debug.WriteLine($"Permission granted: {permissions[i]}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Permission denied: {permissions[i]}");
                    }
                }
            }
        }
    }
}