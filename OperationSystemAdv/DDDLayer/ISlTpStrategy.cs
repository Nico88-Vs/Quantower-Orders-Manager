using System.Collections.Generic;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public interface ISlTpStrategy<T>
    {
        double CalculateSl(T marketData, string itemId);
        double CalculateTp(T marketData, string itemId);

        // Estensione opzionale per logiche avanzate
        IEnumerable<IDomainEvent> GenerateExitEvents(T marketData, string itemId);
    }

}
