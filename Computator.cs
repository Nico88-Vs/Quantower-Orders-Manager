using System;
using System.Collections.Generic;
using DivergentStrV0_1.C_Obj;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1
{
    #region enums
    public enum IchiLineIndex
        {
            //moltiplicatore
            Tenkan_Sen = 0,
            Kijun_Sen = 1,
            Chikou_Span = 2,
            Senkou_SpanA = 3,
            Senkou_SpanB = 4,

            //moltiplicatore secondo
            Tenkan_Sen2 = 5,
            Kijun_Sen2 = 6,
            Chikou_Span2 = 7,
            Senkou_SpanA2 = 8,
            Senkou_SpanB2 = 9,

            //senza moltiplicatore
            Tenkan_Sen0 = 10,
            Kijun_Sen0 = 11,
            Chikou_Span0 = 12,
            Senkou_SpanA0 = 13,
            Senkou_SpanB0 = 14,

            LonGap = 16,
            ShortGap = 17,
            LonGap_Bigger = 18,
            ShortGap_Bigger = 19,
        }
    public enum VolumeLineIndex
        {
            Volume = 0,
            Avarage = 1,
        }
    public enum CumulativeAbsorbtionIndex
    {
        Absorbtion = 0,
        TopStd = 2,
        BottomStd = 3,
    }
    public enum Absorbed
    {
        NotWaiting,
        WaitingIn,
        WaitingOut,
    }
    public enum Signed
    {
        None,
        Signed,
        SignedBig,
    }
    public enum InTrade
    {
        None,
        Waiting,
        In,
        FullyIn
    }
    #endregion
    public static class Computator
    {
        //TODO: Ricordiamoci di resettare i contatori
        //TODO: Notifichiamo gli eventi
        public static event EventHandler<NewTradEventArg> TradeDetected;
        public static void Init()
        {

        }

        public static Absorbed? ComputeAbsorbtion(List<LineSeries> absorbtion_lineseries, ref int absorbedCount)
        {
            Absorbed? resoult = null;
            int _tempout = 0;
            try
            {
                //HINT:questo lo verifichiamo in anticipo
                double abs = absorbtion_lineseries[Convert.ToInt32(CumulativeAbsorbtionIndex.Absorbtion)].GetValue();
                double topStd = absorbtion_lineseries[Convert.ToInt32(CumulativeAbsorbtionIndex.TopStd)].GetValue();
                double bottomStd = absorbtion_lineseries[Convert.ToInt32(CumulativeAbsorbtionIndex.BottomStd)].GetValue();

                double ratio = abs / topStd;
                int sum = 0;

                if (ratio >= 2)
                    sum = ratio >= 3 ? 2 : 1;
                else if (ratio <= -2)
                    sum = ratio >= 3 ? 2 : 1;

                _tempout = absorbedCount + sum;

                _tempout = _tempout > 3 ? 3 : _tempout;
                _tempout = _tempout < -3 ? -3 : _tempout;

                if (_tempout > 0)
                    resoult = Absorbed.NotWaiting;
                if (_tempout <= 1)
                    resoult = abs < bottomStd ? Absorbed.WaitingIn : Absorbed.WaitingOut;
            }
            catch (Exception ex)
            {
                Core.Instance.Loggers.Log(ex, message: "Failed to Compute absorbtion in Static Class");
            }

            if (resoult != null)
                absorbedCount = _tempout;

            return resoult;
        }
        public static Signed? ComputeSignon(List<LineSeries> signon_lineseries, ref int signCount, Side side)
        {
            Signed? resoult = null;
            int _tempout = 0;
            double normal = 0;
            double big = 100000;
            bool detected = false;
            try
            {
                //TODO: Attenzione al limite di 100000 per i big , valore attuale per questioni grafiche
                switch (side)
                {
                    case Side.Buy:
                        normal = signon_lineseries[Convert.ToInt32(IchiLineIndex.LonGap)].GetValue();
                        big = signon_lineseries[Convert.ToInt32(IchiLineIndex.LonGap_Bigger)].GetValue();
                        break;

                    case Side.Sell:
                        normal = signon_lineseries[Convert.ToInt32(IchiLineIndex.ShortGap)].GetValue();
                        big = signon_lineseries[Convert.ToInt32(IchiLineIndex.ShortGap_Bigger)].GetValue();
                        break;
                }

                if (normal > 0)
                {
                    detected = true;
                    _tempout += 1;
                }
                if (big < 100000)
                {
                    detected = true;
                    _tempout += 2;
                }

                _tempout += signCount;

                _tempout = Math.Max(_tempout, signCount);

                _tempout = _tempout > 3 ? 3 : _tempout;


                if (_tempout == 0)
                    resoult = Signed.None;
                else if (_tempout > 0)
                    resoult = _tempout > 1 ? Signed.SignedBig : Signed.Signed;

                if (resoult != null)
                    signCount = _tempout;

                return resoult;

            }
            catch (Exception ex)
            {
                Core.Instance.Loggers.Log(ex, message: "Failed to Compute signon in Static Class");
                return null;
            }
            finally
            {
                if (resoult != null)
                    if (detected)
                    {
                        NewTradEventArg arg = new NewTradEventArg(NewTradeSenderType.IchimokuGap, side);
                        TradeDetected?.Invoke(signon_lineseries, arg);
                    }
            }
        }
        public static bool VolumeDetect(List<LineSeries> volume_lineseries)
        {
            //TODO: Lascio il delta alla startegia , qui nn entro nemmeno se non noto una divergenza o viceversa
            //HINT: pro di entrare prima qui meno calcoli, livello facile alla chiusura , contro nn raiso l evento
            //HINT: pro di entrare dopo raiso levento e potrei avere i  livelli volumetrici

            double Vol = volume_lineseries[Convert.ToInt32(VolumeLineIndex.Volume)].GetValue(1);
            double PastVol = volume_lineseries[Convert.ToInt32(VolumeLineIndex.Volume)].GetValue(2);
            double PastAvg = volume_lineseries[Convert.ToInt32(VolumeLineIndex.Avarage)].GetValue(2);
            double Avg = volume_lineseries[Convert.ToInt32(VolumeLineIndex.Avarage)].GetValue(1);

            if (Vol>=2*Avg || PastVol >= 2 * PastAvg)
                return true;
            else 
                return false;
        }
        public static int DivergenceDetect(List<IHistoryItem> items)
        {

            double barDirection = items[0][PriceType.Close] - items[0][PriceType.Open];
            double barDirectionDue = items[1][PriceType.Close] - items[1][PriceType.Open];
            //HINT:sto selezionando tutte le doji o a delta nullo 
            bool divergent = items[0].VolumeAnalysisData.Total.Delta * barDirection >= 0;
            bool divergentDue = items[1].VolumeAnalysisData.Total.Delta * barDirectionDue >= 0;

            bool deltaDoji = DojiDetect(items[0]);
            bool deltaDojiDue = DojiDetect(items[1]);

            if (divergent || deltaDoji)
                return 0;

            else if (divergentDue || deltaDojiDue)
                return 1;
            else
                return -1;
        }

        private static bool DojiDetect(IHistoryItem item)
        {
            double deltaMax = Math.Abs(item.VolumeAnalysisData.Total.MaxDelta - item.VolumeAnalysisData.Total.MinDelta);
            return 100 * (deltaMax / Math.Abs(item.VolumeAnalysisData.Total.Delta)) < 10;
        }

        private static List<double> ComputateClouds(List<LineSeries> lines, int multiplaier = 1, bool init = false)
        {
            List<double> resoutl = new List<double>();

            if (init)
            {
                bool cros = lines[0].GetValue() < lines[1].GetValue(multiplaier) & lines[0].GetValue() > lines[1].GetValue(multiplaier);
                bool crosdue = lines[0].GetValue() < lines[1].GetValue(multiplaier) & lines[0].GetValue() > lines[1].GetValue(multiplaier);
            }

            return resoutl;
        }

        private static IchimokuCloudScenario GetScenario(Cloud fast, Cloud Mid, Cloud slow)
        {

            double Fast_midpoint = fast.GetPosition();
            double Mid_midpoint = Mid.GetPosition();
            double Slow_midpoint = slow.GetPosition();

            IchimokuCloudScenario newScenario = IchimokuCloudScenario.UNDEFINED;

            if (Fast_midpoint >= Mid_midpoint && Mid_midpoint >= Slow_midpoint)
            {
                newScenario = IchimokuCloudScenario.STRONG_BULLISH; // tutto sopra
            }
            else if (Fast_midpoint <= Mid_midpoint && Mid_midpoint >= Slow_midpoint)
            {
                newScenario = IchimokuCloudScenario.CONSOLIDATION_BULLISH; // fast al centro rialzista
            }
            else if (Mid_midpoint <= Fast_midpoint && Fast_midpoint <= Slow_midpoint)
            {
                newScenario = IchimokuCloudScenario.CONSOLIDATION_BEARISH; // fast al centro ribassista
            }
            else if (Fast_midpoint >= Slow_midpoint && Slow_midpoint >= Mid_midpoint)
            {
                newScenario = IchimokuCloudScenario.MODERATELY_BULLISH; // slow al centro rialzista
            }
            else if (Fast_midpoint <= Slow_midpoint && Mid_midpoint >= Slow_midpoint)
            {
                newScenario = IchimokuCloudScenario.MODERATELY_BEARISH; // slow al centro ribassista
            }
            else if (Slow_midpoint >= Mid_midpoint && Mid_midpoint >= Fast_midpoint)
            {
                newScenario = IchimokuCloudScenario.STRONG_BEARISH; // tutto sotto
            }
            else
            {
                newScenario = IchimokuCloudScenario.UNDEFINED;
            }

            return newScenario;
        }
    }



    
}

