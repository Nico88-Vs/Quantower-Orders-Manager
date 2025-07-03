using System.Collections.Generic;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public interface ISlTpStrategy<T>
    {
        abstract List<double> CalculateSl(T marketData);
        abstract List<double> CalculateTp(T marketData);
        abstract double UpdateTp(double currentPrice);
        abstract double UpdateSl(double currentPrice);

        //TODO: [Bookmark] Implementare la logica per generare eventi di uscita
    }

}
