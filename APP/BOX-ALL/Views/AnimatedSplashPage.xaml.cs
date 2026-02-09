using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace BOX_ALL.Views
{
    public partial class AnimatedSplashPage : ContentPage
    {
        public AnimatedSplashPage()
        {
            InitializeComponent();

            // Initial values will be set in OnAppearing
            VersionLabel.Opacity = 0;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Set initial state for the animation - use ScaleX and ScaleY instead of Scale
            SplashLogo.ScaleX = 1.5;
            SplashLogo.ScaleY = 1.5;
            SplashLogo.TranslationX = -250;
            SplashLogo.TranslationY = -350;
            SplashLogo.Rotation = -15;
            SplashLogo.Opacity = 0;
            SplashLogo.IsVisible = true;

            // Wait a moment for the layout to complete
            await Task.Delay(100);

            Debug.WriteLine($"Initial state - ScaleX: {SplashLogo.ScaleX}, ScaleY: {SplashLogo.ScaleY}, X: {SplashLogo.TranslationX}, Y: {SplashLogo.TranslationY}");

            // Start the animation
            await AnimateSplash();
        }

        private async Task AnimateSplash()
        {
            try
            {
                Debug.WriteLine($"Starting stamp animation - Initial ScaleX: {SplashLogo.ScaleX}, ScaleY: {SplashLogo.ScaleY}");

                // Start fade in (don't await - let it run in parallel)
                // _ = SplashLogo.FadeTo(1, 300);

                var aniFade = SplashLogo.FadeTo(1, 300);
                var aniScale = SplashLogo.ScaleTo(1.0, 400, Easing.CubicIn);
                var aniTranslate = SplashLogo.TranslateTo(0, 0, 400, Easing.CubicIn);
                var aniRotate = SplashLogo.RotateTo(-3, 400, Easing.CubicIn);

                // Run animations with ScaleX and ScaleY separately
                await Task.WhenAll(aniFade, aniScale, aniTranslate, aniRotate);
                /*
                await Task.WhenAll(
                    SplashLogo.ScaleXTo(1.0, 400, Easing.CubicIn),
                    SplashLogo.ScaleYTo(1.0, 400, Easing.CubicIn),
                    SplashLogo.TranslateTo(0, 0, 400, Easing.CubicIn),
                    SplashLogo.RotateTo(-3, 400, Easing.CubicIn)
                );
                */

                Debug.WriteLine($"After main animation - ScaleX: {SplashLogo.ScaleX}, ScaleY: {SplashLogo.ScaleY}");

                // Phase 2: Impact vibration
                await ImpactVibration();

                // Phase 3: Settle (tiny adjustment to make it feel real)
                // await SplashLogo.RotateTo(-2, 100, Easing.CubicOut);

                // Phase 4: Fade in version label
                await VersionLabel.FadeTo(1, 300);

                // Hold for a moment
                await Task.Delay(1000);

                // Navigate to main app
                await NavigateToMainApp();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Animation error: {ex.Message}");
                // If animation fails, still navigate to main app
                await NavigateToMainApp();
            }
        }

        private async Task ImpactVibration()
        {
            // Quick, sharp vibration at impact
            double originalX = SplashLogo.TranslationX;
            double originalY = SplashLogo.TranslationY;

            // Vibration sequence - very quick and diminishing
            var shakes = new[]
            {
                (x: 6.0, y: -3.0, duration: 30),
                (x: -4.0, y: 2.0, duration: 30),
                (x: 2.0, y: -1.0, duration: 30),
                (x: 0.0, y: 0.0, duration: 30)
            };

            foreach (var shake in shakes)
            {
                await SplashLogo.TranslateTo(
                    originalX + shake.x,
                    originalY + shake.y,
                    (uint)shake.duration,
                    Easing.Linear);
            }

            // Try haptic feedback for physical impact feel
            try
            {
                var vibration = Microsoft.Maui.Devices.Vibration.Default;
                vibration.Vibrate(TimeSpan.FromMilliseconds(100));
            }
            catch
            {
                // Vibration not available
            }
        }

        private async Task NavigateToMainApp()
        {
            try
            {
                Debug.WriteLine("Starting navigation to main app");

                // Fade out everything
                var fadeOut = new Animation();
                fadeOut.Add(0, 1, new Animation(v => SplashLogo.Opacity = v, 1, 0));
                fadeOut.Add(0, 1, new Animation(v => VersionLabel.Opacity = v, 1, 0));
                fadeOut.Add(0.3, 1, new Animation(v => SplashLogo.Scale = v, 1, 0.8));

                // fadeOut.Commit(this, "FadeOut", 16, 300);
                await Task.Delay(300);

                Debug.WriteLine("Fade out complete, navigating to AppShell");

                // Navigate to the main shell - ensure we're on main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (Application.Current != null)
                    {
                        Debug.WriteLine("Setting MainPage to AppShell");
                        Application.Current.MainPage = new AppShell();
                        Debug.WriteLine("Navigation complete");
                    }
                    else
                    {
                        Debug.WriteLine("ERROR: Application.Current is null!");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Navigation error: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}