using System.Collections.Generic;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public interface ISlTpStrategy<T>
    {
        abstract List<double> CalculateSl(T marketData);
        abstract List<double> CalculateTp(T marketData);



        // Estensione opzionale per logiche avanzate
        IEnumerable<IDomainEvent> GenerateExitEvents(T marketData, string itemId);
    }

}
