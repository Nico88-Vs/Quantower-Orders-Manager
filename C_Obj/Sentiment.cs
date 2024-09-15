using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DivergentStrV0_1.C_Obj
{
    public enum IchimokuCloudScenario
    {
        STRONG_BULLISH, // la prima nuvola è sopra la seconda e la terza nuvola, tutte e tre le nuvole sono verdi    0
        STRONG_BEARISH, // la prima nuvola è sotto la seconda e la terza nuvola, tutte e tre le nuvole sono rosse     1
        MODERATELY_BULLISH, // la prima e la seconda nuvola sono sopra la terza nuvola      2
        MODERATELY_BEARISH, // la prima e la seconda nuvola sono sotto la terza nuvola     3
        CONSOLIDATION_BULLISH, // la prima e la terza nuvola sono sopra la seconda nuvola    4
        CONSOLIDATION_BEARISH, // la prima e la terza nuvola sono sotto la seconda nuvola      5
        UNDEFINED
    }

    public enum Sentiment
    {
        Buy, Sell, Wait
    }


}
