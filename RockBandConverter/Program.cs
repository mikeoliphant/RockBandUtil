using System;
using System.IO;

namespace RockBandConverter
{
    class Program
    {
        public static void Main(string[] args)
        {
            RockBandConverter converter = new RockBandConverter(@"C:\Share\RBSongs", convertAudio: true);

            converter.ConvertSong(@"C:\Share\Drums\RockBand\RB3_MSL(V1)\John Lennon - Imagine");
            //converter.ConvertSong(@"C:\Share\Drums\RockBand\RB3_MSL(V1)\Queen - Bohemian Rhapsody");
            //converter.ConvertSong(@"C:\Share\Drums\RockBand\RB_DLC\Blind Melon - No Rain");
        }
    }
}