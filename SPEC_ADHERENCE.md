# Analisi dei requisiti vs progetto

## Sintesi generale
Il repository `Quantower-Orders-Manager` fornisce una struttura di base per strategie di trading (`ConditionableBase`, `TpSlManager`, `FixedSlTpStrategy`) e una strategia di esempio (`StrategyTest`) che apre posizioni quando il delta di volume cambia segno.

Tuttavia, rispetto alla specifica fornita, molte funzionalità critiche risultano assenti o solo parzialmente implementate.

## Elementi presenti
- Parametri configurabili per quantità, esposizione massima e percentuali fisse di stop loss e take profit.
- Strategia di test che apre posizioni quando il delta del volume cambia segno.
- Strategia di stop loss e take profit basata su percentuali fisse rispetto al prezzo di ingresso.
- Gestore centralizzato degli ordini con stop loss e take profit (`TpSlManager`).

## Elementi mancanti o non aderenti
- Nessuna implementazione di Rvol, HMA, rapporti di volume delta o altri indicatori richiesti.
- Assenza di filtri temporali configurabili per fasce orarie di trading.
- Mancanza di calcolo dello stop loss basato su min/max della candela precedente più ATR e limiti min/max.
- Mancanza di gestione dei livelli chiave (high/low di sessioni precedenti) per take profit.
- Nessuna gestione di slippage, order stacking, massima perdita giornaliera o logica per conferme multiple degli ingressi.
- Logging solo parziale; non sono presenti le voci dettagliate indicate.

## Conclusione
Il progetto fornisce una base per la gestione di ordini e una strategia dimostrativa, ma richiede un'estesa integrazione per soddisfare tutti i requisiti critici elencati (indicatori specifici, gestione temporale, logica di stop/take profit avanzata, gestione del rischio e logging completo).
