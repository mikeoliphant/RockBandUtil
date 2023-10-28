using NAudio.Midi;
using System.Text.RegularExpressions;

namespace RockBandUtil
{
    public class RockBandIni
    {
        public string Song { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public int MidiDelay { get; set; } = 0;
        public bool IsRockBand4 { get; set; } = false;
    }

    public class RockBandConverter
    {
        public static int GetNoteOctave(int midiNoteNum)
        {
            return (midiNoteNum / 12) - 1;
        }

        public RockBandIni LoadFretsOnFireSongIni(string filename)
        {
            if (File.Exists(filename))
            {
                RockBandIni ini = new RockBandIni();

                using (StreamReader reader = new StreamReader(filename))
                {
                    string line = null;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.StartsWith("["))
                            continue;

                        string[] values = line.Split('=');

                        if (values.Length < 2)
                            continue;

                        string key = values[0].Trim().ToLower();

                        string value = string.Join("=", values, 1, values.Length - 1).Trim();

                        if (key == "name")
                        {
                            ini.Song = value;
                        }
                        else if (key == "artist")
                        {
                            ini.Artist = value;
                        }
                        else if (key == "album")
                        {
                            ini.Album = value;
                        }
                        else if (key == "delay")
                        {
                            int delay = 0;

                            if (int.TryParse(value, out delay))
                            {
                                ini.MidiDelay = delay;
                            }
                        }
                        else if (key == "icon")
                        {
                            if (value.ToLower() == "rb4")
                            {
                                ini.IsRockBand4 = true;
                            }
                        }
                    }
                }

                return ini;
            }

            return null;
        }
    }
}