using Godot;

public partial class Main : Control
{
	private void OnStartPressed()
	{
		GD.Print("Menu start button pressed.");
	}

	private void OnOptionsPressed()
	{
		GD.Print("Menu options button pressed.");
	}

	private void OnQuitPressed()
	{
		GetTree().Quit();
	}
}
