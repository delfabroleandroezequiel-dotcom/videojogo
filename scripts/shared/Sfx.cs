using Godot;
using Metroidvania.Save;

namespace Metroidvania.Shared;

public static class Sfx
{
	public const string HitDado = "HitDado.wav";
	public const string HitRecibido = "HitRecibido.wav";
	public const string HitRecibidoFlecha = "HitRecibidoFlecha.wav";
	public const string FalloGolpe = "FalloGolpe.wav";
	public const string EstusFlask = "EstusFlask.wav";
	public const string Paso1 = "Paso1.wav";
	public const string Paso2 = "Paso2.wav";

	private const string BasePath = "res://assets/audio/Sonidos/";
	private const string VoiceBasePath = "res://assets/audio/Voces/";
	private const string VoiceFallbackLocale = "es";

	public static void Play(Node context, string fileName)
	{
		PlayStream(context, GD.Load<AudioStream>(BasePath + fileName));
	}

	// Drop files under res://assets/audio/Voces/<locale>/<key>.(wav|mp3|ogg) (locale = "es"/"en"/"pt")
	// and this picks the current language, falling back to Spanish if that locale's line is missing.
	public static void PlayVoice(Node context, string key)
	{
		string locale = LocaleManager.Instance?.CurrentLocale ?? VoiceFallbackLocale;
		AudioStream stream = LoadVoiceStream(locale, key);
		if (stream is null && locale != VoiceFallbackLocale)
			stream = LoadVoiceStream(VoiceFallbackLocale, key);

		PlayStream(context, stream);
	}

	private static readonly string[] VoiceExtensions = { ".wav", ".mp3", ".ogg" };

	private static AudioStream LoadVoiceStream(string locale, string key)
	{
		string basePath = $"{VoiceBasePath}{locale}/{key}";
		foreach (string extension in VoiceExtensions)
		{
			string path = basePath + extension;
			if (ResourceLoader.Exists(path))
				return GD.Load<AudioStream>(path);
		}

		return null;
	}

	private static void PlayStream(Node context, AudioStream stream)
	{
		if (stream is null)
			return;

		var player = new AudioStreamPlayer
		{
			Stream = stream,
			ProcessMode = Node.ProcessModeEnum.Always,
		};
		context.GetTree().CurrentScene.AddChild(player);
		player.Finished += player.QueueFree;
		player.Play();
	}
}
