using DivergentStrV0_1.OperationSystemAdv;
using DivergentStrV0_1.OperationSystemAdv.DDDCore;
using DivergentStrV0_1.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.Strategies
{
    public struct SlTpData
    {
        public Symbol Symbol { get; set; }

        public double SlTriggerPrice { get; set; }
        public double currentPrice { get; set; }
        public double AtrInTicks { get; set; }
    }

    internal class RowanSlTpStrategy : ISlTpStrategy<SlTpData>
    {
        //📝 TODO: [CRITICAL] Aggiungere parametri per ATR multiplier (0.000 - 2.000)
        //📝 TODO: [HIGH] Aggiungere parametri per alternative TP distance
        //📝 TODO: [MEDIUM] Aggiungere validazione range per tutti i parametri
        
        public int max_slInTicks { get; set; }
        public int min_slInTicks { get; set; }

        public int MinTpInTicks { get; set; }
        public double AtrSlippageMultiplier { get; set; } = 0.0; // 0.0 - 2.0
        private int delta_InTicks;
        
        //📝 TODO: [CRITICAL] Aggiungere: public double AtrMultiplier { get; set; }
        //📝 TODO: [CRITICAL] Aggiungere: public int AlternativeTpTicks { get; set; }

        public RowanSlTpStrategy(int min_Tick, int max_Tick)
        {
            if (min_Tick <= 0)
                throw new ArgumentException("Min ticks must be positive", nameof(min_Tick));
            if (max_Tick <= 0)
                throw new ArgumentException("Max ticks must be positive", nameof(max_Tick));
            if (min_Tick > max_Tick)
                throw new ArgumentException("Min ticks cannot be greater than max ticks");

            this.max_slInTicks = max_Tick;
            this.min_slInTicks = min_Tick;
            delta_InTicks = Math.Abs(min_Tick - max_Tick);
        }

        public List<double> CalculateSl(SlTpData marketData, Side side, double entry_price)
        {
            //📝 TODO: [CRITICAL] IMPLEMENTAZIONE ERRATA - Deve usare previous candle high/low + ATR
            //📝 TODO: [CRITICAL] Sostituire SlTriggerPrice con previous candle data
            //📝 TODO: [CRITICAL] Aggiungere calcolo: previous_candle_low - (ATR * multiplier) per BUY
            //📝 TODO: [CRITICAL] Aggiungere calcolo: previous_candle_high + (ATR * multiplier) per SELL
            //📝 TODO: [CRITICAL] Implementare validazione min/max distance da current price
            //📝 TODO: [HIGH] Se fuori range, usare price +/- min/max come fallback
            
            var sl_temp = marketData.Symbol.CalculateTicks(entry_price, marketData.SlTriggerPrice);

            // ATR-based slippage in ticks, clamped multiplier
            var extraTicks = Math.Max(0, (int)Math.Round(Math.Abs(marketData.AtrInTicks) * Math.Max(0.0, Math.Min(2.0, this.AtrSlippageMultiplier))));
            var desiredAbsTicks = Math.Abs(sl_temp) + extraTicks;
            var sl = (int)Math.Clamp(Math.Ceiling(desiredAbsTicks), this.min_slInTicks, this.max_slInTicks);

            double sl_price  = side == Side.Buy ? marketData.Symbol.CalculatePrice(entry_price , -sl) :
                marketData.Symbol.CalculatePrice(entry_price, + sl);

            return new List<double> { sl_price };

        }

        public List<double> CalculateTp(SlTpData marketData, Side side, double entry_price)
        {
            //📝 TODO: [HIGH] Aggiungere validazione che i livelli siano aggiornati per il timeframe corrente
            //📝 TODO: [MEDIUM] Ottimizzare la ricerca dei livelli più vicini
            
            if (!StaticSessionManager.TpLevels.Levels.Any())
                StaticSessionManager.CalculateTPLevels();

            var tpItem_InTIcks = marketData.Symbol.CalculatePrice(marketData.currentPrice, side == Side.Buy ? this.MinTpInTicks : -this.MinTpInTicks);

            //📝 TODO: [CRITICAL] Implementare validazione minimum distance
            //📝 TODO: [CRITICAL] Se closest level < MinDistance, usare AlternativeTP
            //📝 TODO: [HIGH] Gestire caso quando siamo sopra/sotto tutti i livelli
            
            List<double> validTpItems = new List<double>();
            double selectedTpItem = 0;
            List<double> selectedTpItemList = new List<double>();

            switch (side)
            {
                case Side.Buy:
                    //📝 TODO: [MEDIUM] Ottimizzare: evitare doppio loop, usare LINQ più efficiente
                    foreach (var item in StaticSessionManager.TpLevels.Levels)
                    {
                        if (item.High > tpItem_InTIcks)
                            validTpItems.Add(item.High);

                        if (item.Low > tpItem_InTIcks)
                            validTpItems.Add(item.Low);
                    }
                    selectedTpItemList = validTpItems.Where(x => x > tpItem_InTIcks).OrderBy(x => x - tpItem_InTIcks).ToList();
                    if (selectedTpItemList.Count > 0)
                        selectedTpItem = selectedTpItemList.First();
                    else
                        selectedTpItem = tpItem_InTIcks; //📝 TODO: [CRITICAL] Usare AlternativeTP invece di MinTp
                    break;

                case Side.Sell:
                    //📝 TODO: [MEDIUM] Rimuovere commento e ottimizzare logica
                    foreach (var item in StaticSessionManager.TpLevels.Levels)
                    {
                        if (item.High < tpItem_InTIcks)
                            validTpItems.Add(item.High);

                        if (item.Low < tpItem_InTIcks)
                            validTpItems.Add(item.Low);
                    }

                    selectedTpItemList = validTpItems.Where(x => x < tpItem_InTIcks).OrderBy(x => tpItem_InTIcks - x).ToList();
                    if (selectedTpItemList.Count > 0)
                        selectedTpItem = selectedTpItemList.First();
                    else
                        selectedTpItem = tpItem_InTIcks; //📝 TODO: [CRITICAL] Usare AlternativeTP invece di MinTp
                    break;
            }
            
            return new List<double> { selectedTpItem };
        }
        public Func<double, double> UpdateSl(SlTpData marketData, ITpSlItems item)
        {
            // TODO: [CRITICAL] LOGICA ERRATA - Deve aggiornare SL basato su previous candle + ATR
            // TODO: [CRITICAL] Implementare: previous_candle_low - (ATR * multiplier) per BUY
            // TODO: [CRITICAL] Implementare: previous_candle_high + (ATR * multiplier) per SELL
            // TODO: [CRITICAL] Aggiornare SL ogni candela, non solo quando "isOut"
            // TODO: [HIGH] Validare che nuovo SL rispetti min/max distance
            // TODO: [HIGH] Cancellare vecchio SL order prima di piazzare nuovo
            // TODO: [MEDIUM] Aggiungere logging per ogni SL update
            
            try
            {
                return current_sl =>
                {
                    // TODO: [CRITICAL] Sostituire questa logica con calcolo basato su previous candle
                    var delta = marketData.Symbol.CalculateTicks(current_sl, marketData.currentPrice);
                    bool isOut = delta > this.delta_InTicks;

                    if (!isOut)
                        return current_sl;

                    // ramo "isOut": gestisci tutti i casi
                    return item.Side switch
                    {
                        Side.Buy => marketData.Symbol.CalculatePrice(marketData.currentPrice, -max_slInTicks),
                        Side.Sell => marketData.Symbol.CalculatePrice(marketData.currentPrice, +max_slInTicks),
                        _ => current_sl // default: evita il buco di ritorno
                    };
                };
            }
            catch (Exception)
            {
                // TODO: [HIGH] Implementare logging specifico per errori SL update
                // TODO: [MEDIUM] Decidere strategia: return current o throw exception
                return current_sl => current_sl;
            }
        }
        public Func<double, double> UpdateTp(SlTpData marketData, ITpSlItems item)
        {
            // TODO: [CRITICAL] IMPLEMENTARE - Attualmente lancia NotImplementedException
            // TODO: [HIGH] TP dovrebbe essere fisso al momento dell'entry (non si muove)
            // TODO: [MEDIUM] Considerare se implementare trailing TP per versioni future
            // TODO: [LOW] Per ora, restituire TP originale senza modifiche
            //📝 TODO: [HIGH] TP dovrebbe essere fisso al momento dell'entry (non si muove)
            //📝 TODO: [MEDIUM] Considerare se implementare trailing TP per versioni future
            //📝 TODO: [LOW] Per ora, restituire TP originale senza modifiche
            
            throw new NotImplementedException();
            //📝 TODO: [CRITICAL] Sostituire con:
            // return current_tp => current_tp; // TP fisso, non si muove
        }
    }
}
