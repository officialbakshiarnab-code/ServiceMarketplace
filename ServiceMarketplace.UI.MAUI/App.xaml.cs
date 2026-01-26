namespace ServiceMarketplace.UI.MAUI;

public partial class App : Microsoft.Maui.Controls.Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new MainPage()) { Title = "ServiceMarketplace.UI.MAUI" };
	}
}
