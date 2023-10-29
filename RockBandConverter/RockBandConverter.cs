using NAudio.Midi;
using NAudio.Wave;
using SongFormat;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

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

            SongStructure songStructure = new SongStructure();
            SongKeyboardNotes keyboardNotes = new SongKeyboardNotes();
            List<SongVocal> vocals = new List<SongVocal>();
            SongSection lastSection = null;

            foreach (var track in midiFile.Events)
            {
                Dictionary<int, long> noteDict = new Dictionary<int, long>();

                bool isKeys = false;
                bool isVocals = false;
                bool isEvents = false;
                bool isBeats = false;

                bool tempoMapEnded = false;

                int currentMicrosecondsPerQuarterNote = 0;
                long currentMicrosecond = 0;
                long currentTick = 0;

                var tempoEnumerator = tempoMap.GetEnumerator();

                if (tempoMap.Count > 0)
                    haveTempoMap = true;

                if (haveTempoMap)
                {
                    tempoEnumerator.MoveNext();
                    currentMicrosecondsPerQuarterNote = tempoEnumerator.Current.Tempo;
                }

                foreach (MidiEvent midiEvent in track)
                {
                    if (haveTempoMap)
                    {
                        long ticksLeft = midiEvent.DeltaTime;

                        while (ticksLeft > 0)
                        {
                            if (!tempoMapEnded && tempoEnumerator.Current.Tick < (currentTick + ticksLeft))
                            {
                                // We're out of ticks at our current tempo, so do a delta and update the tempo
                                long delta = tempoEnumerator.Current.Tick - currentTick;

                                currentMicrosecond += ((long)delta * (long)currentMicrosecondsPerQuarterNote) / (long)ticksPerBeat;

                                currentTick += delta;
                                ticksLeft -= delta;

                                currentMicrosecondsPerQuarterNote = tempoEnumerator.Current.Tempo;

                                if (!tempoEnumerator.MoveNext())
                                {
                                    tempoMapEnded = true;
                                }

                                continue;
                            }
                            else
                            {
                                // The current event fits without updating tempo
                                currentMicrosecond += ((long)ticksLeft * (long)currentMicrosecondsPerQuarterNote) / (long)ticksPerBeat;

                                currentTick += ticksLeft;
                                ticksLeft = 0;
                            }
                        }
                    }

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
                                isBeats = true;
                            }
                            else if (lower.EndsWith("vocals"))
                            {
                                isVocals = true;

                                songData.InstrumentParts.Add(new SongInstrumentPart
                                {
                                    InstrumentName = "vocals",
                                    InstrumentType = ESongInstrumentType.Vocals
                                });
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
                                isEvents = true;
                            }
                        }
                        else if (metaEvent.MetaEventType == MetaEventType.SetTempo)
                        {
                            TempoEvent tempoEvent = metaEvent as TempoEvent;

                            tempoMap.Add((tempoEvent.AbsoluteTime, tempoEvent.MicrosecondsPerQuarterNote));
                        }
                        else if (midiEvent is TextEvent)
                        {
                            if (isEvents)
                            {
                                TextEvent textEvent = midiEvent as TextEvent;

                                string lower = textEvent.Text.ToLower();

                                Match match = Regex.Match(lower, @"\[section (\w+)\]");

                                if (match.Success)
                                {
                                    Capture c = match.Groups[1].Captures[0];

                                    float time = (float)((double)currentMicrosecond / 1000000.0);

                                    if (lastSection != null)
                                        lastSection.EndTime = time;

                                    songStructure.Sections.Add(new SongSection()
                                    {
                                        Name = c.Value,
                                        StartTime = time
                                    });
                                }
                            }
                            else if (isVocals)
                            {
                                string text = (midiEvent as TextEvent).Text;

                                if ((text == "+") || text.StartsWith("["))
                                {

                                }
                                else
                                {
                                    if (text.EndsWith("#") || text.EndsWith("^") || text.EndsWith("="))
                                    {
                                        text = text.Substring(0, text.Length - 1);
                                    }

                                    vocals.Add(new SongVocal()
                                    {
                                        TimeOffset = (float)((double)currentMicrosecond / 1000000.0),
                                        Vocal = text
                                    });
                                }
                            }
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

                                    int ticks = (int)(((currentMicrosecond - start) * 960) / currentMicrosecondsPerQuarterNote);

                                    SongKeyboardNote note = new SongKeyboardNote()
                                    {
                                        TimeOffset = (float)((double)start / 1000000.0),
                                        //TimeLength =  (ticks > 240) ? (float)((double)(currentMicrosecond - start) / 1000000.0) : 0,
                                        TimeLength = (float)((double)(currentMicrosecond - start) / 1000000.0),
                                        Note = noteEvent.NoteNumber,
                                        Velocity = noteEvent.Velocity
                                    };

                                    keyboardNotes.Notes.Add(note);
                                }

                                noteDict.Remove(noteEvent.NoteNumber);
                            }
                        }
                        else if (isBeats)
                        {
                            if (noteEvent.CommandCode == MidiCommandCode.NoteOn)
                            {
                                // 12 is downbeat, 13 is other beat
                                if ((noteEvent.NoteNumber == 12) || (noteEvent.NoteNumber == 13))
                                {
                                    songStructure.Beats.Add(new SongBeat()
                                    {
                                        TimeOffset = (float)((double)currentMicrosecond / 1000000.0),
                                        IsMeasure = (noteEvent.NoteNumber == 12)
                                    });
                                }
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
                else if (isVocals)
                {
                    if (vocals.Count > 0)
                    {
                        using (FileStream stream = File.Create(Path.Combine(songDir, "vocals.json")))
                        {
                            JsonSerializer.Serialize(stream, vocals, SerializationUtil.CondensedSerializerOptions);
                        }
                    }
                }
                else if (isEvents)
                {
                    if (lastSection != null)
                        lastSection.EndTime = (float)((double)currentMicrosecond / 1000000.0);
                }
            }

            using (FileStream stream = File.Create(Path.Combine(songDir, "song.json")))
            {
                JsonSerializer.Serialize(stream, songData, SerializationUtil.IndentedSerializerOptions);
            }

            using (FileStream stream = File.Create(Path.Combine(songDir, "arrangement.json")))
            {
                JsonSerializer.Serialize(stream, songStructure, SerializationUtil.CondensedSerializerOptions);
            }

            if (convertAudio)
            {
                foreach (string oggFile in Directory.GetFiles(songFolder, "*.ogg"))
                {
                    File.Copy(oggFile, Path.Combine(songDir, Path.GetFileName(oggFile)), overwrite: true);
                }
            }
        }

        public void ConvertAll(string path)
        {

        }
    }
}