using System.Collections.Generic;
using System;
using TradingPlatform.BusinessLayer;
using DivergentStrV0_1.OperationSystemAdv.DDDCore;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public interface ISlTpStrategy<T>
    {
        abstract List<double> CalculateSl(T marketData, Side side, double entry_price );
        abstract List<double> CalculateTp(T marketData, Side side, double entry_price);

        //🧠 HINT: [il double in ingresso viene ripetuto per  trigger price e order Price]
        //🧠 HINT: [Returning price avoid update]

        abstract Func<double, double> UpdateTp(T marketData, ITpSlItems item);
        abstract Func<double, double> UpdateSl(T marketData, ITpSlItems item);

        //TODO: [Bookmark] Implementare la logica per generare eventi di uscita
    }

}
