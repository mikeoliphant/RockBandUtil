using NAudio.Midi;
using SongFormat;
using System.Collections;
using System.Text.Json;

namespace RockBandConverter
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
        string destPath;
        bool convertAudio;

        public RockBandConverter(string destPath, bool convertAudio)
        {
            this.destPath = destPath;
            this.convertAudio = convertAudio;
        }

        public static int GetNoteOctave(int midiNoteNum)
        {
            return (midiNoteNum / 12) - 1;
        }

        public RockBandIni LoadSongIni(string filename)
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

        public void ConvertSong(string songFolder)
        {
            RockBandIni ini = LoadSongIni(Path.Combine(songFolder, "song.ini"));

            SongData songData = new SongData()
            {
                SongName = ini.Song,
                ArtistName = ini.Artist,
                AlbumName = ini.Album
            };

            MidiFile midiFile = new MidiFile(Path.Combine(songFolder, "notes.mid"), strictChecking: false);

            string artistDir = Path.Combine(destPath, SerializationUtil.GetSafeFilename(songData.ArtistName));

            if (!Directory.Exists(artistDir))
            {
                Directory.CreateDirectory(artistDir);
            }

            string songDir = Path.Combine(artistDir, SerializationUtil.GetSafeFilename(songData.SongName));

            if (!Directory.Exists(songDir))
            {
                Directory.CreateDirectory(songDir);
            }

            int ticksPerBeat = midiFile.DeltaTicksPerQuarterNote;

            List<(long Tick, int Tempo)> tempoMap = new List<(long Tick, int Tempo)>();
            bool haveTempoMap = false;

            SongKeyboardNotes keyboardNotes = new SongKeyboardNotes();

            foreach (var track in midiFile.Events)
            {
                var tempoEnumerator = tempoMap.GetEnumerator();

                if (tempoMap.Count > 0)
                    haveTempoMap = true;

                Dictionary<int, long> noteDict = new Dictionary<int, long>();

                bool isKeys = false;

                long endTick = 0;
                int currentMicrosecondsPerQuarterNote = 0;
                long currentMicrosecond = 0;

                foreach (MidiEvent midiEvent in track)
                {
                    if (haveTempoMap)
                    {
                        while (tempoEnumerator.Current.Tick <= midiEvent.AbsoluteTime)
                        {
                            currentMicrosecondsPerQuarterNote = tempoEnumerator.Current.Tempo;

                            if (!tempoEnumerator.MoveNext())
                                break;
                        }

                        currentMicrosecond += ((long)midiEvent.DeltaTime * (long)currentMicrosecondsPerQuarterNote) / (long)ticksPerBeat;
                    }

                    endTick = (int)Math.Max(endTick, midiEvent.AbsoluteTime);

                    int offset = midiEvent.DeltaTime;

                    if (midiEvent is MetaEvent)
                    {
                        MetaEvent metaEvent = midiEvent as MetaEvent;

                        if (metaEvent.MetaEventType == MetaEventType.SequenceTrackName)
                        {
                            TextEvent textEvent = metaEvent as TextEvent;

                            string lower = textEvent.Text.ToLower();

                            if (lower.EndsWith("drums"))
                            {
                            }
                            else if (lower.EndsWith("beat"))
                            {
                            }
                            else if (lower.EndsWith("vocals"))
                            {
                            }
                            //else if (lower.Contains("harm"))
                            //{
                            //    isVocals = true;

                            //    vocalVoice = (lower[lower.Length - 1] - '1') + 1;
                            //}
                            else if (lower.EndsWith("real_bass"))
                            {
                            }
                            else if (lower.EndsWith("real_keys_x"))
                            {
                                isKeys = true;

                                songData.InstrumentParts.Add(new SongInstrumentPart
                                {
                                    InstrumentName = "keys",
                                    InstrumentType = ESongInstrumentType.Keys
                                });
                            }
                            else if (lower.EndsWith("events"))
                            {
                            }
                        }
                        else if (metaEvent.MetaEventType == MetaEventType.SetTempo)
                        {
                            TempoEvent tempoEvent = metaEvent as TempoEvent;

                            tempoMap.Add((tempoEvent.AbsoluteTime, tempoEvent.MicrosecondsPerQuarterNote));
                        }
                    }
                    else if (midiEvent is NoteEvent)
                    {
                        if (!haveTempoMap)
                            throw new InvalidOperationException("Can't have a midi note before tempo map is established");

                        NoteEvent noteEvent = midiEvent as NoteEvent;

                        if (isKeys)
                        {
                            if (noteEvent.CommandCode == MidiCommandCode.NoteOn)
                            {
                                noteDict[noteEvent.NoteNumber] = currentMicrosecond;
                            }
                            else if (noteEvent.CommandCode == MidiCommandCode.NoteOff)
                            {
                                if (!noteDict.ContainsKey(noteEvent.NoteNumber))
                                {
                                    throw new InvalidDataException("Note off without previous note on: " + noteEvent.ToString());
                                }

                                if (noteEvent.NoteNumber > 0)
                                {
                                    long start = noteDict[noteEvent.NoteNumber];

                                    SongKeyboardNote note = new SongKeyboardNote()
                                    {
                                        TimeOffset = (float)((double)start / 1000000.0),
                                        TimeLength = (float)((double)(currentMicrosecond - start) / 1000000.0),
                                        Note = noteEvent.NoteNumber,
                                        Velocity = noteEvent.Velocity
                                    };

                                    keyboardNotes.Notes.Add(note);
                                }

                                noteDict.Remove(noteEvent.NoteNumber);
                            }
                        }
                    }
                }

                if (isKeys)
                {
                    using (FileStream stream = File.Create(Path.Combine(songDir, "keys.json")))
                    {
                        JsonSerializer.Serialize(stream, keyboardNotes, SerializationUtil.CondensedSerializerOptions);
                    }
                }
            }
        }

        public void ConvertAll(string path)
        {

        }
    }
}