using Godot;

namespace Metroidvania.Save;

public partial class LocaleManager : Node
{
	public static readonly string[] AvailableLocales = { "es", "en", "pt" };
	private const string ConfigPath = "user://settings.cfg";

	public static LocaleManager Instance { get; private set; }

	[Signal] public delegate void LocaleChangedEventHandler(string locale);

	public string CurrentLocale { get; private set; } = "es";

	public override void _Ready()
	{
		Instance = this;

		ConfigFile config = new();
		if (config.Load(ConfigPath) == Error.Ok)
		{
			string saved = config.GetValue("settings", "locale", "es").AsString();
			if (System.Array.IndexOf(AvailableLocales, saved) >= 0)
				CurrentLocale = saved;
		}

		TranslationServer.SetLocale(CurrentLocale);
	}

	public void SetLocale(string locale)
	{
		if (System.Array.IndexOf(AvailableLocales, locale) < 0 || locale == CurrentLocale)
			return;

		CurrentLocale = locale;
		TranslationServer.SetLocale(locale);

		ConfigFile config = new();
		config.Load(ConfigPath);
		config.SetValue("settings", "locale", locale);
		config.Save(ConfigPath);

		EmitSignal(SignalName.LocaleChanged, locale);
	}
}
