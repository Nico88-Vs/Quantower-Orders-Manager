using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.Utils
{
    /// <summary>
    /// Sessione semplice basata SOLO su UTC.
    /// - Days: giorni abilitati (in UTC).
    /// - Open/Close: orari UTC (TimeOnly).
    /// - IsOvernight gestisce finestre che attraversano la mezzanotte UTC.
    /// Tutti i metodi accettano/ritornano DateTime in UTC (Kind=Utc).
    /// </summary>
    /// 
    public enum Status
    {
        Active,
        Inactive
    }

    public sealed class SimpleSessionUtc
    {
        public string Name { get; }
        public HashSet<DayOfWeek> Days { get; }
        public TimeOnly Open { get; }
        public TimeOnly Close { get; }
        public Status Status => this._status;
        public bool IsOvernight => Close <= Open;
        private Status _status = Status.Inactive;
        public event EventHandler<Status> StatusChanged;

        public SimpleSessionUtc(string name,
                                IEnumerable<DayOfWeek> days,
                                TimeOnly openUtc,
                                TimeOnly closeUtc)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Session" : name;
            Days = new HashSet<DayOfWeek>(days ?? Enum.GetValues<DayOfWeek>());
            Open = openUtc;
            Close = closeUtc;
        }

        public void UpdateStatus(IHistoryItem item)
        {
            if (item == null)

                //📝 TODO: [Log]
                return;

            Status tempStatus = Contains(item) ? Status.Active : Status.Inactive;

            if (tempStatus != this._status)
                StatusChanged.Invoke(this, tempStatus);

            this._status = tempStatus;
        }


        /// <summary>
        /// Restituisce l'intervallo (StartUtc, EndUtc) della **sessione precedente**
        /// rispetto all'item più recente di hd. Se non trovata, ritorna null.
        /// Scorre gli item dal più recente verso il passato.
        /// </summary>
        public (DateTime StartUtc, DateTime EndUtc)? GetPreviousSessionRangeUtc(HistoricalData hd)
        {
            if (hd == null)
                return null;

            // 1) Prendi l'item più recente e stabilisci se siamo dentro alla sessione corrente
            int offset = 0;
            var last = SafeGet(hd, offset);
            if (last is null) return null;

            var t0 = ItemTimeUtc(last);
            bool inCurrent = ContainsUtc(t0);

            // 2) Se siamo dentro la sessione corrente, scorri indietro finché **esci** dalla sessione corrente
            if (inCurrent)
            {
                while (true)
                {
                    offset++;
                    var it = SafeGet(hd, offset);
                    if (it is null) return null; // finita la history

                    var t = ItemTimeUtc(it);
                    if (!ContainsUtc(t))
                        break; // siamo ufficialmente "fuori" dalla sessione corrente
                }
            }

            // 3) Ora, prosegui indietro finché **entri** nella prossima sessione trovata (che è la "precedente")
            while (true)
            {
                offset++;
                var it = SafeGet(hd, offset);
                if (it is null) return null;

                var t = ItemTimeUtc(it);
                if (ContainsUtc(t))
                {
                    // allinea ai confini ufficiali della sessione che contiene t
                    return WindowContainingUtc(t);
                }
            }
        }

        // -------- helpers --------
#nullable enable
        private static IHistoryItem? SafeGet(HistoricalData hd, int offset)
        {
            try
            {
                return hd[offset, SeekOriginHistory.End]; // dal più recente verso il passato
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Estrae un timestamp UTC rappresentativo dall'item.
        /// Per le barre usiamo il bordo sinistro (TimeLeft); per i tick/last usiamo Time.
        /// </summary>
        private static DateTime ItemTimeUtc(IHistoryItem it)
        {
            switch (it)
            {
                case HistoryItemBar b:
                    // TimeLeft è l'inizio barra (UTC sulla tua pipeline)
                    return EnsureUtc(b.TimeLeft);
                case HistoryItemLast l:
                    return EnsureUtc(l.TimeLeft);
                case HistoryItemTick tk:
                    return EnsureUtc(tk.TimeLeft);
                default:
                    // fallback generico (se disponibile)
                    return EnsureUtc(it.TimeLeft); // o it.Time se la tua interfaccia lo espone
            }
        }
        // ---------- CONTAINMENT (UTC) ----------

        /// <summary>
        /// Controlla appartenenza assumendo che 'utc' sia DateTimeKind.Utc.
        /// </summary>
        public bool ContainsUtc(DateTime utc)
        {
            if (utc.Kind != DateTimeKind.Utc)
                utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

            var tod = TimeOnly.FromDateTime(utc);

            if (!IsOvernight)
            {
                return Days.Contains(utc.DayOfWeek)
                    && tod >= Open && tod < Close;
            }

            // overnight: es. 22:00-02:00 UTC
            if (tod >= Open)
                return Days.Contains(utc.DayOfWeek);
            else
                return Days.Contains(utc.AddDays(-1).DayOfWeek) && tod < Close;
        }

        /// <summary>
        /// Controlla appartenenza di un item di storia (IHistoryItem) alla sessione.
        /// Usa item.Time come UTC (o in generale un timestamp UTC del tuo feed).
        /// </summary>
        public bool Contains(IHistoryItem item)
        {
            // Se il tuo item espone una proprietà diversa, sostituisci qui (es. item.TimeLeft / TicksRight già UTC).
            var t = EnsureUtc(item.TimeLeft);
            return ContainsUtc(t);
        }

        // ---------- BOUNDARIES (UTC) ----------

        /// <summary>
        /// Restituisce l'intervallo (StartUtc, EndUtc) della sessione che contiene 'utc'. Altrimenti null.
        /// </summary>
        public (DateTime StartUtc, DateTime EndUtc)? WindowContainingUtc(DateTime utc)
        {
            if (!ContainsUtc(utc))
                return null;

            var tod = TimeOnly.FromDateTime(utc);

            DateOnly sessionDay;
            if (!IsOvernight)
            {
                sessionDay = DateOnly.FromDateTime(utc);
            }
            else
            {
                sessionDay = (tod >= Open)
                    ? DateOnly.FromDateTime(utc)
                    : DateOnly.FromDateTime(utc.AddDays(-1));
            }

            var startUtc = sessionDay.ToDateTime(Open, DateTimeKind.Utc);
            var endUtc = !IsOvernight
                ? sessionDay.ToDateTime(Close, DateTimeKind.Utc)
                : sessionDay.AddDays(1).ToDateTime(Close, DateTimeKind.Utc);

            return (startUtc, endUtc);
        }

        /// <summary>
        /// Restituisce l'intervallo (StartUtc, EndUtc) per un giorno specifico (UTC).
        /// </summary>
        public (DateTime StartUtc, DateTime EndUtc)? WindowForDayUtc(DateOnly dayUtc)
        {
            if (!Days.Contains(dayUtc.DayOfWeek))
                return null;

            var startUtc = dayUtc.ToDateTime(Open, DateTimeKind.Utc);
            var endUtc = !IsOvernight
                ? dayUtc.ToDateTime(Close, DateTimeKind.Utc)
                : dayUtc.AddDays(1).ToDateTime(Close, DateTimeKind.Utc);

            return (startUtc, endUtc);
        }


        // ---------- helpers ----------

        private static DateTime EnsureUtc(DateTime dt) =>
            dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }
}
