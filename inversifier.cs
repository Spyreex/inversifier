using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class Program
{
	public static List<BpmLine> bpmList;
	public static List<Column> lanes;
	public static int keyCount;
	public static int minimumLnLength;
	public static float lnGapSnap;
	public static int lnGapMs;

    public static void Main()
    {
		string[] filePaths = Directory.GetFiles(@".\input\", "*.osu");
		string userInput;

		// user settings input
		Console.WriteLine("Settings:" + Environment.NewLine + "=========");

		// minimum LN length, smaller will be rice
		Console.WriteLine("Minimum LN Length (in ms) (leave empty for default of 20ms):");
		userInput = Console.ReadLine();
		while (!(Int32.TryParse(userInput, out minimumLnLength)) && userInput != "")
		{
			Console.WriteLine("Please use a whole number");
			Console.WriteLine("Minimum LN Length (in ms) (leave empty for default of 20ms):");
			userInput = Console.ReadLine();
		}
		if (userInput == "")
			minimumLnLength = 20;

		Console.WriteLine("LN Gap (in ms) (at least 1) (if left empty, asks for snap instead):");
		userInput = Console.ReadLine();
		while (!(Int32.TryParse(userInput, out lnGapMs)) && userInput != "")
		{
			Console.WriteLine("Please use a whole number");
			Console.WriteLine("LN Gap (in ms) (at least 1) (if left empty, asks for snap instead):");
			userInput = Console.ReadLine();
		}
		if (userInput == "" || lnGapMs == 0)
		{
			Console.WriteLine("LN Gap (in snap [x/y]) (leave empty for default of 1/4):");
			userInput = Console.ReadLine();
			while (userInput != "")
			{
				if (System.Text.RegularExpressions.Regex.IsMatch(userInput, @"^\d*\.?\d*/\d*\.?\d*$"))
				{
					string[] userInputSnap = userInput.Split('/');
					float original;
					float divider;
					if (float.TryParse(userInputSnap[0], out original) && float.TryParse(userInputSnap[1], out divider))
					{
						if (original > 0 && divider > 0)
						{
							lnGapSnap = original / divider;
							break;
						}
					}
				}
				Console.WriteLine("Invalid format, make sure you use [x/y] and don't use 0");
				Console.WriteLine("LN Gap (in snap [x/y]) (leave empty for default of 1/4):");
				userInput = Console.ReadLine();
			}
			lnGapMs = 0;
			if (userInput == "")
				lnGapSnap = 0.25f;
		}

		foreach (string filePath in filePaths)
		{
			Console.WriteLine("Parsing " + filePath);
			Initialize(filePath);
		}
    }

	private static void Initialize(string filePath)
	{
		bpmList = new List<BpmLine>();
		lanes = new List<Column>();

		/* full text read from input folder converted into string */
 	    string file = File.ReadAllText(filePath);

		/* path to output the output file to */
		string outputPath = filePath.Replace(".\\input\\",".\\output\\");
		outputPath = outputPath.Replace(".osu"," [Inversified].osu");

		/* current string until (and including) the Version: */
		string textBase = CutStr(file,Environment.NewLine + "Source:",false);

		// Console.WriteLine(textBase);

		// get keycount
		keyCount = Int32.Parse(CutStr(file.Substring(file.IndexOf("CircleSize:")),Environment.NewLine,false).Substring("CircleSize:".Length));

		/* gets every text between the Source: (previous newline included) line and [HitObjects] line including [HitObjects] (newline included) */
		int indexOfSource = file.IndexOf("Source:") - 2;
		int indexOfTiming = file.IndexOf("[TimingPoints]");
		int indexOfObjects = file.IndexOf("[HitObjects]");
		string textMetadata = file.Substring(indexOfSource, indexOfObjects - indexOfSource + "[HitObjects]".Length + 2);
		
		/* textOutput is eventual write for output file */
		string textOutput = textBase + " [Inversified]" + textMetadata;

		// finds and fills bpmList with bpm values
		FindBpms(file.Substring(indexOfTiming + "[TimingPoints]".Length + 2, indexOfObjects - indexOfTiming - "[TimingPoints]".Length - 2));

		// initialize columns according to keycount
		for (int x = 0; x < keyCount; x++)
		{
			lanes.Add(new Column(x + 1));
		}

		// turns entire chart into rice for easier conversion, then puts all objects inside Column classes
		FindObjects(Ricefy(file.Substring(indexOfObjects + "[HitObjects]".Length + 2)));

		// goes through all columns
		foreach (Column lane in lanes)
		{
			// goes through all objects in each column individually and makes an LN out of them
			for (int i = 0; i + 1 < lane.notes.Count; i++)
			{
				// * lnGapSnap (for instance 1/4 = 0.25)
				int lnEnd = lnGapMs != 0 ? lane.notes[i + 1].time - lnGapMs : (int)Math.Round(lane.notes[i + 1].time - CompareBpm(lane.notes[i + 1].time) * lnGapSnap);
			
				// if LN is smaller than minimumLnLength skip object
				if (lnEnd - lane.notes[i].time >= minimumLnLength)
				{
					lane.notes[i].objectParams = lnEnd;
					lane.notes[i].type = 128;
				}
			}
		}

		string textObjects = "";

		foreach (Column lane in lanes)
		{
			foreach (Note note in lane.notes)
			{
				textObjects += note.toString() + Environment.NewLine;
			}
		}
		File.WriteAllText(outputPath, textOutput + textObjects);
		Console.WriteLine("Finished Converting " + filePath + " to " + outputPath);
	}

	private static float CompareBpm(int time)
	{
		BpmLine prevTime = bpmList[0];

		foreach (BpmLine bpmClass in bpmList)
		{
			if (time > prevTime.time)
			{
				if (time < bpmClass.time)
					return prevTime.bpm;
			}
			prevTime = bpmClass;
		}
		return (time > prevTime.time ? bpmList.Last().bpm : bpmList[0].bpm);
	}

	// finds and fills bpmList with bpm values
	private static void FindBpms(string timingLines)
	{
		int indexOfComma;

		// splits the string by newline and removes empty ones
		string[] lines = timingLines.Split(new [] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);
		foreach (string line in lines) {
			indexOfComma = line.IndexOf(',');

			// check if value is a bpm line or sv line, if bpm, add it to list
			float bpmValue = float.Parse(line.Substring(indexOfComma + 1, line.IndexOf(',', indexOfComma + 1) - indexOfComma - 1));
			if (!(bpmValue < 0))
			{
				// adds the bpm (class) to the list of bpms
				bpmList.Add(new BpmLine(float.Parse(line.Substring(0, indexOfComma)), bpmValue));
			}
		}
	}

	// function for adding objects to their respective column class
	private static void FindObjects(string objectLines)
	{
		// splits the string by newline and removes empty ones
		string[] lines = objectLines.Split(new [] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);
		foreach (string line in lines) {
			string[] lineValues = line.Split(',');
			lanes[FindColumn(Int32.Parse(lineValues[0])) - 1].addNote(new Note(Int32.Parse(lineValues[0]),Int32.Parse(lineValues[1]),Int32.Parse(lineValues[2]),Int32.Parse(lineValues[3]),Int32.Parse(lineValues[4]),0,lineValues[5]));
		}
	}
	
	// returns column
	private static int FindColumn(int value)
	{
		// go through all keys' ranges and check if value is in that range 
		for (int x = 0; x < keyCount; x++)
		{
			if (value >= 512/keyCount*x && value < 512/keyCount*(x+1))
			{
				return (x + 1);
			}
		}
		if (value == 512)
			return (keyCount);
		// error return, value is outside valid range ()
		return (-1);
	}
	
	// removes all LN and make them rice
	private static string Ricefy(string objectLines)
	{
		string newString = "";

		// splits the string by newline and removes empty ones
		string[] lines = objectLines.Split(new [] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);
		foreach (string line in lines) {
			// splits the line by "," and the first ":", this is so we can easily alter the values
			string[] lineValues = line.Split(',');
			string[] lnEndTime = lineValues[5].Split(new[] {':'},2);
			
			// if LN, 128 = LN
			if (lineValues[3] == "128") {
				lineValues[3] = "1";
				lnEndTime[0] = "";
			} else {
				lnEndTime[0] = "0:";
			}

			// concat all values back into 1 being
			newString += lineValues[0] + "," + lineValues[1] + "," + lineValues[2] + "," + lineValues[3] + "," + lineValues[4] + "," + lnEndTime[0] + lnEndTime[1] + Environment.NewLine;
		}
		return newString;
	}

	// returns string containing everything until the needle, bool include determines whether needle is included
	private static string CutStr(string haystack, string needle, bool include)
	{
		int extraLength = include ? needle.Length : 0;
		return (haystack.Substring(0,haystack.IndexOf(needle) + extraLength));
	}
}

public class BpmLine
{
	public float time;
	public float bpm;

	public BpmLine(float time, float bpm) {
		this.time = time;
		this.bpm = bpm;
	}

	public string toString() {
		return ("At: " + time + " ms is value: " + bpm + " ms/beat or " + 60000/bpm + " BPM");
	}
}

public class Column
{
	public int lane;
	public List<Note> notes = new List<Note>();

	public Column(int lane)
	{
		this.lane = lane;
	}

	public void addNote(Note note)
	{
		notes.Add(note);
	}

	public string toString()
	{
		return "Column " + lane + " has " + notes.Count + " notes";
	}
}

public class Note
{
	private int x;
	private int y;
	public int time;
	public int type;
	private int hitSound;

	// this one will be the LN end
	public int objectParams;
	private string hitSample;

	public Note(int x, int y, int time, int type, int hitSound, int objectParams, string hitSample)
	{
		this.x = x;
		this.y = y;
		this.time = time;
		this.type = type;
		this.hitSound = hitSound;
		this.objectParams = objectParams;
		this.hitSample = hitSample;
	}

	public string toString()
	{
		return (objectParams != 0 ? (x + "," + y  + "," + time  + "," + type + "," + hitSound  + "," + objectParams  + ":" + hitSample) : (x + "," + y  + "," + time  + "," + type + "," + hitSound  + "," + hitSample));
	}
}