
# Quantower Order Manager System README

## Overview
This project provides a comprehensive system for managing trading strategies using the Quantower platform. It focuses on implementing conditional trading logic and managing Stop-Loss (SL) and Take-Profit (TP) orders using delegates. The architecture is designed to be extensible, allowing for custom strategies to be developed and deployed easily.

## Features
- **Conditional Trading**: Implement trading strategies with flexible conditional logic using delegates.
- **Order Management**: Simplify the creation and management of SL and TP orders through a streamlined API.
- **Historical Data Integration**: Leverage Quantower's historical data (HD) for analyzing and calculating indicators.
- **Extensibility**: Easily extend the system by implementing and inheriting from base classes to create your own strategies.

## Project Structure
- **`CondiCtionableBase.cs`**: Abstract base class providing core functionality for conditional trading strategies. This serves as a template for creating new strategies with minimal configuration.
- **`IConditionable.cs`**: Interface defining the methods and properties required for a conditional trading strategy.
- **`SlTpCondictionHolder.cs`**: Manages the logic for setting up and computing SL and TP conditions using the delegates.
- **`SlTpItems.cs`**: Represents individual SL and TP items, storing essential information for tracking orders.
- **`TpSlComputator.cs`**: Contains the logic for computing SL and TP levels based on market data and indicator values.
- **`TpSlManager.cs`**: Static class responsible for managing SL and TP orders throughout the lifecycle of a strategy.

## Getting Started

### Prerequisites
- **Quantower Platform**: Ensure you have the Quantower trading platform installed and configured.
- **Development Environment**: Visual Studio or any compatible C# IDE.
- **API Access**: Valid API access to Quantower to integrate with live and historical market data.

### Installation
Clone the repository and open the project in your preferred C# IDE:
```bash
git clone https://github.com/Nico88-Vs/Quantower-Orders-Manager.git
```

## Usage
1. **Create a Strategy**:
   - Inherit from the `ConditionableBase` class.
   - Implement the required methods like `SetCondictionHolder`, `GetMetrics`, and `Update`.
2. **Add Conditional Logic**:
   - Use the `SlTpCondictionHolder` class to set up conditions for managing SL and TP orders dynamically.
   - Delegate the logic for TP and SL management using functions such as `GeTp` and `GetSl`.
3. **Run the Strategy**:
   - Load the built strategy into Quantower, configure the trading parameters, and start live or backtesting operations.

## Examples
Below is a simple example of implementing a strategy:
```csharp
public class CustomStrategy : ConditionableBase<Indicator>
{
    // Custom parameters
    private Indicator _smaIndicator;
    private double _fibLevel;

    // Constructor
    public CustomStrategy(Indicator smaIndicator, Account account, Symbol symbol, double quantity)
        : base(account, symbol, quantity)
    {
        _smaIndicator = smaIndicator; // Initialize your indicator
        SetCondictionHolder(); // Set up the SL/TP logic
        ManagerInit(); // Initialize the manager
    }

    public override void SetCondictionHolder()
    {
        // Define SL and TP delegates based on strategy logic
        SlTpCondictionHolder<Indicator>.DefineSl[] slDelegates = new SlTpCondictionHolder<Indicator>.DefineSl[]
        {
            this.CalculateSL
        };

        SlTpCondictionHolder<Indicator>.DefineTp[] tpDelegates = new SlTpCondictionHolder<Indicator>.DefineTp[]
        {
            this.CalculateTP
        };

        // Assign the holder
        CondictionHolder = new SlTpCondictionHolder<Indicator>(new Indicator[] { _smaIndicator }, slDelegates, tpDelegates);
    }

    private double CalculateSL(Indicator indicator, string guid)
    {
        // Custom logic for Stop Loss
    }

    private double CalculateTP(Indicator indicator, string guid)
    {
        // Custom logic for Take Profit
    }
}

```

## Support
If you encounter any issues or have any questions, please feel free to open an issue or contact us through the project's support channels.

## License
This project is licensed under the apache License - see the [LICENSE.md](LICENSE.md) file for details.
```

