using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public class SlTpCondictionHolder<T>
    {
        public Func<T, string, double> DefineSl { get; set; }
        public Func<T, string, double> DefineTp { get; set; }

        public ISlTpStrategy<T> Strategy { get; set; }

        public Symbol Symbol { get; }

        public SlTpCondictionHolder(Symbol symbol)
        {
            this.Symbol = symbol;
        }

        public void UseStrategyMode()
        {
            if (Strategy != null)
            {
                DefineSl = (q, id) => Strategy.CalculateSl(q, id);
                DefineTp = (q, id) => Strategy.CalculateTp(q, id);
            }
        }
    }
}
