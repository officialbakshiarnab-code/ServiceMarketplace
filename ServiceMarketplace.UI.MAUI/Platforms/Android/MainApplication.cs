#if ANDROID
using Android.App;
using Android.Runtime;

namespace ServiceMarketplace.UI.MAUI;

// Android entry point. This file must only be compiled for the Android target.
[global::Android.App.Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

#endif
