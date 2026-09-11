using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Metroidvania.Save;

namespace Metroidvania.Shared;

// Folder+key convention instead of one string constant per file (same idea PlayVoice already used
// for locale lookup) — dropping a new file into a category folder needs zero code changes, and
// numbered variants ("Sword Attack 1.wav", "Sword Attack 2.wav", ...) are picked at random from a
// plain base key ("Sword Attack") instead of every caller having to alternate indices by hand.
public static class Sfx
{
	private const string BasePath = "res://assets/audio/Sonidos/";
	private const string VoiceBasePath = "res://assets/audio/Voces/";
	private const string VoiceFallbackLocale = "es";

	// Slight per-play pitch wobble so a sound repeated many times in a row (footsteps, sword
	// swings) doesn't read as a mechanical loop.
	private const float PitchVariance = 0.08f;

	private static readonly Random Rng = new();
	private static readonly Dictionary<string, string[]> FolderCache = new();

	// category: subfolder under Sonidos/, e.g. "Combat/Sword". key: base file name without the
	// trailing " N" variant number or extension, e.g. "Sword Attack" for "Sword Attack 1.wav".
	public static void Play(Node context, string category, string key)
	{
		PlayStream(context, LoadVariant(category, key));
	}

	// Positional variant for world-space sound sources (traps, enemies) that can be far from the
	// camera — Play() above is non-positional and was fine as long as every caller was the player
	// (always on/near camera), but that made hazards like ArrowTrap audible at full volume from
	// anywhere on the map.
	public static void PlayAt(Node2D context, string category, string key, float maxDistance = 900f)
	{
		PlayStreamAt(context, LoadVariant(category, key), maxDistance);
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

	// Resolves a category/key to a stream without playing it — for callers that need to own their
	// own AudioStreamPlayer (e.g. a looping ambience that must outlive a single one-shot playback,
	// unlike Play/PlayAt's fire-and-forget players which free themselves when finished).
	public static AudioStream Load(string category, string key) => LoadVariant(category, key);

	private static AudioStream LoadVariant(string category, string key)
	{
		string folder = BasePath + category;
		string fileName = PickVariantFile(folder, key);
		if (fileName is null)
		{
			GD.PushWarning($"Sfx: no file found for \"{category}/{key}\"");
			return null;
		}

		return GD.Load<AudioStream>(folder + "/" + fileName);
	}

	// Some packs number variants as a trailing suffix ("Sword Attack 1.wav"), others as a leading
	// prefix ("01. Damage Grunt (Male).wav") — strip a leading "N." / "N " prefix before comparing
	// so either convention resolves to the same plain key.
	private static readonly System.Text.RegularExpressions.Regex LeadingNumberPrefix =
		new(@"^\d+\.?\s*", System.Text.RegularExpressions.RegexOptions.Compiled);

	// Matches "<key>.wav", "<key> <N>.wav", and "<N>. <key>.wav" (any numbered variant, either
	// convention) — never a name that merely starts with key (e.g. key "Sword Attack" must not
	// pick up "Sword Attack Extra.wav").
	private static string PickVariantFile(string folder, string key)
	{
		string[] files = GetFolderFiles(folder);
		List<string> matches = new();
		foreach (string file in files)
		{
			string nameNoExt = Path.GetFileNameWithoutExtension(file);
			string withoutPrefix = LeadingNumberPrefix.Replace(nameNoExt, "");
			if (nameNoExt == key || withoutPrefix == key || IsNumberedVariant(nameNoExt, key))
				matches.Add(file);
		}

		return matches.Count == 0 ? null : matches[Rng.Next(matches.Count)];
	}

	private static bool IsNumberedVariant(string nameNoExt, string key)
	{
		string prefix = key + " ";
		return nameNoExt.StartsWith(prefix) && int.TryParse(nameNoExt[prefix.Length..], out _);
	}

	private static string[] GetFolderFiles(string folder)
	{
		if (FolderCache.TryGetValue(folder, out string[] cached))
			return cached;

		List<string> files = new();
		using DirAccess dir = DirAccess.Open(folder);
		if (dir is not null)
		{
			dir.ListDirBegin();
			for (string name = dir.GetNext(); name != ""; name = dir.GetNext())
			{
				if (!dir.CurrentIsDir() && !name.EndsWith(".import"))
					files.Add(name);
			}

			dir.ListDirEnd();
		}

		string[] result = files.ToArray();
		FolderCache[folder] = result;
		return result;
	}

	private static void PlayStream(Node context, AudioStream stream)
	{
		if (stream is null)
			return;

		var player = new AudioStreamPlayer
		{
			Stream = stream,
			PitchScale = RandomPitch(),
			ProcessMode = Node.ProcessModeEnum.Always,
		};
		context.GetTree().CurrentScene.AddChild(player);
		player.Finished += player.QueueFree;
		player.Play();
	}

	private static void PlayStreamAt(Node2D context, AudioStream stream, float maxDistance)
	{
		if (stream is null)
			return;

		var player = new AudioStreamPlayer2D
		{
			Stream = stream,
			PitchScale = RandomPitch(),
			ProcessMode = Node.ProcessModeEnum.Always,
			GlobalPosition = context.GlobalPosition,
			MaxDistance = maxDistance,
		};
		context.GetTree().CurrentScene.AddChild(player);
		player.Finished += player.QueueFree;
		player.Play();
	}

	private static float RandomPitch() => 1f + ((float)Rng.NextDouble() * 2f - 1f) * PitchVariance;
}
