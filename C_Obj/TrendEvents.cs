using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DivergentStrV0_1.C_Obj
{
    //public enum TrendCros
    //{
    //    STRONG_BEARISH_TO_CONSOLIDATION_BEARISH, // la prima nuvola passa da sotto alla seconda e terza nuvola ad essere tra le due nuvole
    //    STRONG_BEARISH_TO_MODERATELY_BEARISH, // la terza nuvola passa da sopra a sotto la seconda nuvola
    //    STRONG_BEARISH_TO_STRONG_BULLISH, // la prima nuvola passa da sotto alla seconda e terza nuvola a sopra alle due nuvole

    //    MODERATELY_BEARISH_TO_CONSOLIDATION_BEARISH, // la prima nuvola passa da sotto alla seconda e terza nuvola ad essere sotto la seconda e sopra la terza nuvola
    //    MODERATELY_BEARISH_TO_MODERATELY_BULLISH, // la prima nuvola passa da sotto alla seconda e terza nuvola a sopra alle due nuvole
    //    MODERATELY_BEARISH_TO_STRONG_BEARISH, // la terza nuvola passa da sotto a sopra la prima e seconda nuvola

    //    CONSOLIDATION_BEARISH_TO_MODERATELY_BEARISH, // la prima nuvola passa da essere tra le due nuvole ad essere sotto la seconda e sopra la terza nuvola
    //    CONSOLIDATION_BEARISH_TO_MODERATELY_BULLISH, // la terza nuvola passa da essere sotto la seconda e sopra la prima nuvola ad essere sotto la prima e sopra la seconda nuvola
    //    CONSOLIDATION_BEARISH_TO_STRONG_BEARISH, // la prima nuvola passa da essere tra le due nuvole a sotto alla seconda e terza nuvola

    //    STRONG_BULLISH_TO_CONSOLIDATION_BULLISH, // la prima nuvola passa da sopra alla seconda e terza nuvola ad essere tra le due nuvole
    //    STRONG_BULLISH_TO_MODERATELY_BULLISH, // la terza nuvola passa da sotto a sopra la seconda nuvola
    //    STRONG_BULLISH_TO_STRONG_BEARISH, // la prima nuvola passa da sopra alla seconda e terza nuvola a sotto alle due nuvole

    //    MODERATELY_BULLISH_TO_CONSOLIDATION_BULLISH, // la prima nuvola passa da sopra alla seconda e terza nuvola ad essere sopra la seconda e sotto la terza nuvola
    //    MODERATELY_BULLISH_TO_MODERATELY_BEARISH, // la terza nuvola passa da sopra a sotto la prima e seconda nuvola
    //    MODERATELY_BULLISH_TO_STRONG_BEARISH, // la prima nuvola passa da sopra alla seconda e terza nuvola a sotto alle due nuvole

    //    CONSOLIDATION_BULLISH_TO_MODERATELY_BULLISH, // la prima nuvola passa da essere tra le due nuvole ad essere sopra la seconda e sotto la terza nuvola
    //    CONSOLIDATION_BULLISH_TO_MODERATELY_BEARISH, // la terza nuvola
    //    UNDEFINED
    //}
    public class TrendEvents : EventArgs
    {
        public int Buffer { get; private set; }
        public TrendCros TrendCross_Event_Arg { get; private set; }

        public TrendEvents(int buffer, TrendCros arg)
        {
            Buffer = buffer;
            TrendCross_Event_Arg = arg;
        }
    }
}
