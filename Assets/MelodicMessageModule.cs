using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;

public class MelodicMessageModule : MonoBehaviour
{
	private KMBombModule _module;
	private KMAudio _audio;
	private KMSelectable[] _keys;
	private TextMesh _screenText;

	private String _word;
	private String[] _submissionNotes = new String[3];
	private Key[] _keyInfo = new Key[12];

	private static int _moduleIdCounter = 1;
	private int _moduleId;

	private List<String> input = new List<String>();

	private bool _init = false;
	private bool _submitting = false;
	private bool _solved = false;
	private bool _busy = false;
	private bool _tpCycle = false;
	private bool _animation = false;

	private static float _tpCycleTime = 1.5f;
	
	private Coroutine _displayCoroutine;

	private void Awake()
	{
		_moduleId = _moduleIdCounter++;
		_module = GetComponent<KMBombModule>();
		_audio = GetComponent<KMAudio>();
		_keys = GetComponent<KMSelectable>().Children;
		_screenText = transform.Find("Rotate and Scale/Display/ScreenText").GetComponent<TextMesh>();

		Init();

		for (var i = 0; i < _keys.Length; i++)
		{
			int j = i;
			_keys[j].OnInteract += delegate { ButtonPressed(j); return false; };
		}
	}

	private void Init()
	{
		// Generate the notes used to activate submitting mode
		_submissionNotes = new[]
		{
			ConvertNoteToUnfriendly(((KeyNames) Random.Range(0, 12)).ToString()),
			ConvertNoteToUnfriendly(((KeyNames) Random.Range(0, 12)).ToString()),
			ConvertNoteToUnfriendly(((KeyNames) Random.Range(0, 12)).ToString())
		};

		_screenText.text = _submissionNotes.Join("  ");
		
		// Pick a random word
		_word = WordList.GetRandomWord();
		var correctLetters = _word.OrderBy(c => Random.Range(0f, 1f)).Join("");
		var incorrectLetters = "";

		// Generate the incorrect letters
		for (var i = 0; i < 6; i++)
		{
			var randomLetter = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
				.Where(c => !_word.Contains(c) && !incorrectLetters.Contains(c))
				.OrderBy(c => Random.Range(0f, 1f))
				.ToArray()[0];
			
			incorrectLetters += randomLetter;
		}
		
		// Gather the unassigned letters and shuffle them. The first letter of correctLetters and the last letter of incorrect letter are automatically assigned.
		char lastAssignedLetter = ' ';
		var unassignedClues = correctLetters.Substring(1) + incorrectLetters.Substring(0, 5);
		unassignedClues = unassignedClues.OrderBy(c => Random.Range(0f, 1f)).Join("");

		// Generate the data for each key (Each key needs a unique set of data, and needs to be generated in the specific way to always create valid puzzles.)
		for (var i = 0; i < _keyInfo.Length; i++)
		{
			switch (i)
			{
				// First key is always an incorrect key, with a random clue.
				case 0:
					_keyInfo[i] = new Key(incorrectLetters[5].ToString(), unassignedClues[0].ToString(), incorrectLetters.Contains(unassignedClues[0]));
					lastAssignedLetter = unassignedClues[0];
					unassignedClues = unassignedClues.Substring(1);
					break;
				// Last key is always a correct key with no clue
				case 11:
					_keyInfo[i] = new Key(correctLetters[0].ToString(), "", false);
					break;
				// Second to last key always has last key as a clue
				case 10:
					_keyInfo[i] = new Key(lastAssignedLetter.ToString(), correctLetters[0].ToString(), false);
					break;
				// Every other key is random with a random clue that has not been selected.
				default:
					_keyInfo[i] = new Key(lastAssignedLetter.ToString(), unassignedClues[0].ToString(), incorrectLetters.Contains(unassignedClues[0]));
					lastAssignedLetter = unassignedClues[0];
					unassignedClues = unassignedClues.Substring(1);
					break;
			}
		}

		_keyInfo = _keyInfo.OrderBy(k => Random.Range(0f, 1f)).ToArray();
	}

	private void Start()
	{
		DebugLog("Notes are: {0}", _submissionNotes.Join());   
		DebugLog("The chosen word is: {0}", _word);
		for (var i = 0; i < _keyInfo.Length; i++)
		{
			DebugLog("{0}: Letter is {1}, Clue is {2} {3}", ((KeyNames)i).ToString().Replace("s", "#"),  _keyInfo[i].Letter, _keyInfo[i].Clue.Equals("") ?  "NONE" : _keyInfo[i].Clue, _keyInfo[i].IsLying ? "(Incorrect)" : "");
		}

		// Complex thing to get solution XD
		DebugLog("Solution: {0}", _word.Select(c => ((KeyNames)Array.IndexOf(_keyInfo,_keyInfo.First(key => key.Letter == c.ToString()))).ToString().Replace("s", "#")).Join(" "));
		_init = true;
	}

	private void ButtonPressed(int buttonNumber)
	{
		StartCoroutine(DoKeyAnimation(buttonNumber));
		
		if (!_keyInfo[buttonNumber].IsLying || !_init || _submitting || _solved)
			_audio.PlaySoundAtTransform(((KeyNames)buttonNumber).ToString().Replace('s', '#'), transform);
		else if (_keyInfo[buttonNumber].IsLying && !_submitting && !_solved)
		{
			_audio.PlaySoundAtTransform("Ping", transform);
		}

		if (!_submitting && _init && !_solved)
		{
			// Set up display info for the key
			if (_displayCoroutine != null)
				StopCoroutine(_displayCoroutine);
			
			var message = Random.Range(0, 2) == 0
				? _keyInfo[buttonNumber].Clue + " " + _keyInfo[buttonNumber].Letter
				: _keyInfo[buttonNumber].Letter + " " + _keyInfo[buttonNumber].Clue;

			// For the one key without a clue, make sure the space isn't displayed.
			if (message.Length == 2)
				message = message.Replace(" ", "");
			
			// Display the info for 1 second. If Twitch Plays then a little longer.
			_displayCoroutine = StartCoroutine(DisplayMessageFor(message, _tpCycle ? _tpCycleTime : 1f));
			
			if (_tpCycle)
				return;
			
			// If the inputs match the submission notes, start submitting mode.
			input.Add(((KeyNames)buttonNumber).ToString());
			if (input.Count == 4)
			{
				input.RemoveAt(0);
			}

			if (input.ToArray().SequenceEqual(_submissionNotes.Select(ConvertNoteToFriendly)))
			{
				_submitting = true;
				_screenText.text = "";
				input = new List<string>();
				StopCoroutine(_displayCoroutine);
			}

			return;
		}

		if (_submitting && !_solved && !_busy)
		{
			input.Add(_keyInfo[buttonNumber].Letter);

			if (input.Count == 6)
			{
				var word = "incorrect...";

				if (input.Join("").Equals(_word))
				{
					word = "correct!";
					_solved = true;
				}

				DebugLog("You submitted {0}, that is {1}", input.Join(""), word);
				StartCoroutine(DoSolveOrStrike());
			}
		}
	}

	string ConvertNoteToFriendly(string noteName)
	{
		if (noteName.Contains("#"))
		{
			switch (noteName)
			{
				case "B#":
					return "C";
				case "E#":
					return "F";
				default:
					return noteName[0] + "s";
			}
		}
		
		// ToUpper needed because of TP
		if (noteName.ToUpperInvariant().Contains("B") && noteName.Length == 2)
		{
			switch (noteName.ToUpperInvariant())
			{
				case "CB":
					return "B";
				case "FB":
					return "E";
				default:
					var notes = "ABCDEFG".ToArray();
					return notes[(Array.IndexOf(notes, noteName[0]) + 6) % 7] + "s";
			}
		}

		return noteName;
	}

	string ConvertNoteToUnfriendly(string noteName)
	{
		var notes = "ABCDEFG".ToArray();
		if (noteName.Contains("s"))
		{
			return Random.Range(0,2) == 0 ? notes[(Array.IndexOf(notes, noteName[0]) + 1) % 7] + "b" : noteName[0] + "#";
		}
		
		if (new [] {"B", "C", "E", "F"}.Contains(noteName))
		{
			if (Random.Range(0, 2) == 0)
			{
				return new[] {"C", "F"}.Contains(noteName)
					? notes[(Array.IndexOf(notes, noteName[0]) + 6) % 7] + "#"
					: notes[(Array.IndexOf(notes, noteName[0]) + 1) % 7] + "b";
			}
		}

		return noteName;
	}

	private void DebugLog(string log, params object[] args)
	{
		var logData = string.Format(log, args);
		Debug.LogFormat("[Melodic Message #{0}] {1}", _moduleId, logData);
	}

	private IEnumerator DoKeyAnimation(int keyNumber)
	{
		yield return new WaitUntil(() => !_animation);

		_animation = true;
		
		// Black key and White key use different positions / angles
		var dy = new[] {1, 3, 6, 8, 10}.Contains(keyNumber) ? -0.03f : -0.07f; 
		var dax = new[] {1, 3, 6, 8, 10}.Contains(keyNumber) ? 5f : 10f;

		// Both of the following variables are per-direction.
		var frames = 5;
		var duration = .05f;

		for (var i = 0; i < frames * 2; i++)
		{
			var x = _keys[keyNumber].transform.localPosition.x;
			var y = _keys[keyNumber].transform.localPosition.y;
			var z = _keys[keyNumber].transform.localPosition.z;
			_keys[keyNumber].transform.localPosition = new Vector3(x, y + (dy / frames) * (i >= frames ? -1 : 1), z);

			var ax = _keys[keyNumber].transform.localEulerAngles.x;
			var ay = _keys[keyNumber].transform.localEulerAngles.y;
			var az = _keys[keyNumber].transform.localEulerAngles.z;
			// Due to the way I modeled the keys, the 3rd, 5th, 10th, and 12th, key need to be rotated the other way.
			_keys[keyNumber].transform.localEulerAngles = new Vector3(ax + (dax / frames) * (i >= frames ? -1 : 1) * (new[] {2, 4, 9, 11}.Contains(keyNumber) ? -1 : 1), ay, az);
			
			yield return new WaitForSeconds(duration/frames);
		}

		_animation = false;
	}

	private IEnumerator DoSolveOrStrike()
	{
		_busy = true;
		yield return new WaitForSeconds(.5f);
		_audio.PlaySoundAtTransform("Submitting", transform);
		
		for (var i = 0; i < input.Count; i++)
		{
			_screenText.text += input[i];
			yield return new WaitForSeconds(.4f);
		}

		if (_solved)
		{
			_module.HandlePass();
			_audio.PlaySoundAtTransform("Solve", transform);
			_screenText.text = _word;

			for (var i = 0; i < 8; i++)
			{
				_screenText.color = Color.green;
				yield return new WaitForSeconds(.3f / 2f);            
				_screenText.color = Color.black;
				yield return new WaitForSeconds(.3f / 2f);
			}
			_screenText.color = Color.green;
			yield return new WaitForSeconds(1.2f);
			_screenText.text = "";
		}
		else
		{
			_module.HandleStrike();
			_audio.PlaySoundAtTransform("Strike", transform);
			_screenText.text = input.Join("");
			_screenText.color = Color.red;
			yield return new WaitForSeconds(1.2f);
			_screenText.text = _submissionNotes.Join("  ").Replace("s", "#");
			_submitting = false;
			input = new List<string>();
		}

		_busy = false;
		_screenText.color = Color.white;
	}
	private IEnumerator DisplayMessageFor(String message, float seconds)
	{
		_screenText.text = message;
		yield return new WaitForSeconds(seconds);
		_screenText.text = _submissionNotes.Join("  ").Replace("s", "#");
	}

	string TwitchHelpMessage = "Do '!{0} cycle' to cycle through all the keys from left to right. The module will not enter submit mode when cycling. Do '!{0} press A Bb C#' to press the keys.";
	IEnumerator ProcessTwitchCommand(string command)
	{
		if (Regex.IsMatch(command, "^(?:(?:cycle)|(?:press)(?: [a-g][#b]?)*)$",
			RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
		{
			yield return null;

			if (command.ToLowerInvariant().Equals("cycle"))
			{
				_tpCycle = true;
				for (var i = 0; i < _keys.Length; i++)
				{
					ButtonPressed(i);
					yield return new WaitForSeconds(_tpCycleTime);
				}

				_tpCycle = false;
			}
			else
			{
				var split = command.Split(' ');
				
				foreach (var item in split)
				{
					if (item.ToLowerInvariant().Equals("press")) continue;

					ButtonPressed((int) Enum.Parse(typeof(KeyNames), ConvertNoteToFriendly(item.ToUpperInvariant())));
					yield return "trycancel";
					yield return new WaitForSeconds(.2f);
				}

				if (_solved) yield return "solve";
			}
		}
	}
}
internal class Key
{
	public readonly string Letter;
	public readonly string Clue;
	public readonly bool IsLying;

	public Key(string letter, string clue, bool lying)
	{
		Letter = letter;
		Clue = clue;
		IsLying = lying;
	}
}

internal class WordList
{
	private static readonly string[] Words =
	{
		"ADJUST", "ADMIRE", "ABSENT", "ASYLUM", "ABSURD", "AUDITS",
		"BISHOP", "BUCKET", "BANISH", "BRIGHT", "BOXCAR",
		"CHANGE", "CLOSED", "CHORUS", "COMEDY", "CLIMAX", "CONVEY", "CAWING",
		"DOUBLE", "DILUTE", "DUPLET", "DEPUTY", "DONKEY",
		"EARWAX", "EMPLOY", "EXOTIC", "EMBARK", "EXPAND", "EMBRYO",
		"FROZEN", "FLAVOR", "FORGET", "FIGURE", "FORMAT", "FAMILY", "FLIGHT",
		"GROWTH", "GRAVEL", "GLANCE", "GARLIC", "GUITAR", "GENTLY",
		"HUNTER", "HACKER", "HYDRAS", "HIJACK", "HUMBLE", "HOAXES", "HOWLED",
		"INJURY", "IMPORT", "ISLAND", "INJECT", "IMPACT", "INFECT",
		"JOCKEY", "JUNGLE", "JACKET", "JAILOR", "JUICED",
		"KANJIS", "KEYPAD", "KELVIN", "KOSHER", "KAIROS", "KIDNAP",
		"LAYOUT", "LENGTH", "LAWYER", "LOCATE", "LINGER",
		"MOSQUE", "MATRIX", "MATURE", "MISERY", "METHOD", "MARKET", "MODULE", "MYSTIC",
		"NATURE", "NUMBER", "NORMAL", "NATIVE", "NICELY",
		"OBJECT", "OUNCES", "OXYGEN", "OCTANE", "OINKED", "OUTAGE", "OSPREY",
		"POETRY", "PERSON", "PHLEGM", "PACKET", "PHOBIA",
		"QUILTS", "QUARTZ", "QUICHE", "QUENCH", "QUIVER", "QINTAR",
		"RADIUS", "RADIUM", "RANCID", "RAVENS", "REFLUX",
		"SAFETY", "SAVING", "SCRIPT", "SCRIBE", "SHRIMP",
		"TACKLE", "TALKIE", "TAVERN", "TUXEDO", "TOUCAN",
		"UPLIFT", "UTOPIA", "URCHIN", "UNFOLD", "UGLIFY",
		"VISAGE", "VANITY", "VISUAL", "VERMIN", "VOYAGE", "VOLTED",
		"WYVERN", "WRENCH", "WRAITH", "WICKED", "WHIMSY", "WARPED",
		"XYLOSE", "XENIUM", "XIPHOS", "XYSTER", "XORING",
		"YACHTS", "YOWLED", "YEOMAN", "YAWNED", "YONDER",
		"ZEBRAS", "ZOMBIE", "ZENITH", "ZEALOT", "ZODIAC"
	};

	public static string GetRandomWord()
	{
		return Words[Random.Range(0, Words.Length)];
	}
}

enum KeyNames
{
	C,
	Cs,
	D,
	Ds,
	E,
	F,
	Fs,
	G,
	Gs,
	A,
	As,
	B,
}

