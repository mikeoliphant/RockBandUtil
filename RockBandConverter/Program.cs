using System;
using System.IO;

namespace RockBandConverter
{
    class Program
    {
        public static void Main(string[] args)
        {
            RockBandConverter converter = new RockBandConverter(@"C:\Share\JamSongs", convertAudio: false);

            converter.ConvertSong(@"C:\Share\Drums\RockBand\RB3_MSL(V1)\John Lennon - Imagine");
        }
    }
}