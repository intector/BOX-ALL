using Android.App;
using Android.Content;
using Android.Provider;
using BOX_ALL.ViewModels;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using BOX_ALL.Services;
using System.Text.Json;
using System.Diagnostics;

namespace BOX_ALL.Platforms.Android
{
    public class FolderPickerService : IFolderPickerService
    {
        private TaskCompletionSource<string?>? _pickFolderTaskCompletionSource;
        private readonly ExportImportService _exportImportService;

        public FolderPickerService(ExportImportService exportImportService)
        {
            _exportImportService = exportImportService;
        }

        public Task<string?> PickExportFolderAsync()
        {
            _pickFolderTaskCompletionSource = new TaskCompletionSource<string?>();

            var currentActivity = Platform.CurrentActivity ?? throw new InvalidOperationException("Current Activity is null");

            // Create intent for folder selection
            var intent = new Intent(Intent.ActionOpenDocumentTree);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission);
            intent.AddFlags(ActivityFlags.GrantPersistableUriPermission);

            // Try to hint the system to start at Documents folder
            try
            {
                var documentsUri = DocumentsContract.BuildDocumentUri(
                    "com.android.externalstorage.documents",
                    "primary:Documents/BOX-ALL/exports"
                );
                intent.PutExtra(DocumentsContract.ExtraInitialUri, documentsUri);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not set initial URI: {ex.Message}");
            }

            // Start the activity
            currentActivity.StartActivityForResult(intent, 9999);

            // Set up result handling
            MainActivity.Instance.ActivityResult += OnActivityResult;

            return _pickFolderTaskCompletionSource.Task;
        }

        private void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            if (requestCode == 9999)
            {
                MainActivity.Instance.ActivityResult -= OnActivityResult;

                if (resultCode == Result.Ok && data?.Data != null)
                {
                    try
                    {
                        var uri = data.Data;
                        var currentActivity = Platform.CurrentActivity;

                        if (currentActivity != null && uri != null)
                        {
                            // Take persistent permission
                            var contentResolver = currentActivity.ContentResolver;
                            var takeFlags = ActivityFlags.GrantReadUriPermission;
                            contentResolver?.TakePersistableUriPermission(uri, takeFlags);

                            // Return the URI string
                            _pickFolderTaskCompletionSource?.TrySetResult(uri.ToString());
                        }
                        else
                        {
                            _pickFolderTaskCompletionSource?.TrySetResult(null);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing folder selection: {ex.Message}");
                        _pickFolderTaskCompletionSource?.TrySetResult(null);
                    }
                }
                else
                {
                    // User cancelled
                    _pickFolderTaskCompletionSource?.TrySetResult(null);
                }
            }
        }
    }
}