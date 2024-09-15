using System;
using System.Collections.Generic;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.C_Obj
{
    public enum TypeOfMin_Max
    {
        MaximaFast,
        MinimaFast,
        MaximaSlow,
        MinimaSlow,
        bases
    }

    public enum CloudColor
    {
        green = 1,
        red = -1,
        white = 0
    }

    public class Cloud
    {
        public int Id { get; set; }
        public int Buffer { get; set; }
        public TF Time_F { get; set; }
        public bool IsOpen { get; set; }
        public int Length { get; set; }
        public double EndPrice { get; private set; }
        public List<double> LenghtList { get; private set; }
        public double OriginPrice { get; set; }
        public List<double> AverageList { get; set; }
        public List<double> ThickList { get; set; }
        public List<double> MomentumList { get; set; }
        public double Thickness { get; private set; }
        public CloudColor Color { get; private set; }
        public List<Bases> RoofList { get; set; }
        public List<Bases> BasesList { get; set; }
        public List<Min_Max> MaximaFast { get; set; }
        public List<Min_Max> MinimaFast { get; set; }
        public List<Min_Max> MaximaSlow { get; set; }
        public List<Min_Max> MinimaSlow { get; set; }

        public int FastSeries { get; set; }
        public int SlowSeries { get; set; }
        public List<double> FastValue { get; private set; }
        public List<double> SlowValue { get; private set; }

        private Indicator Indicator;

        public Cloud(int id, int buffer, TF tF, double originPrice)
        {
            Id = id;
            Time_F = tF;
            FastSeries = Time_F.FastSeries;
            SlowSeries = Time_F.SlowSeries;
            Indicator = Time_F.Indicatore;
            Buffer = buffer;
            IsOpen = true;
            Length = 1;
            OriginPrice = originPrice;

            LenghtList = new List<double>();
            LenghtList.Add(Length);

            AverageList = new List<double>();
            AverageList.Add(0);

            ThickList = new List<double>();
            ThickList.Add(0);

            MomentumList = new List<double>();
            MomentumList.Add(0);


            RoofList = new List<Bases>();
            BasesList = new List<Bases>();

            MinimaFast = new List<Min_Max>();
            MinimaSlow = new List<Min_Max>();
            MaximaFast = new List<Min_Max>();
            MaximaSlow = new List<Min_Max>();


            SlowValue = new List<double>();
            FastValue = new List<double>();
            FastValue.Add(0);
            SlowValue.Add(0);
        }

        public void UpdateCloud(int ofset)
        {
            if (IsOpen)
            {

                double fastserie = Indicator.GetValue(lineIndex: FastSeries, offset: ofset);
                FastValue.Add(fastserie);

                double slowserie = Indicator.GetValue(lineIndex: SlowSeries, offset: ofset);
                SlowValue.Add(slowserie);

                if (Length == 1)
                {
                    switch (fastserie - slowserie)
                    {
                        case > 0:
                            Color = CloudColor.green;
                            break;

                        case < 0:
                            Color = CloudColor.red;
                            break;

                        default:
                            Color = CloudColor.white;
                            break;
                    }
                }

                Length++;
                LenghtList.Add(Length);

                Thickness = Math.Abs(fastserie - slowserie);
                ThickList.Add(Thickness);

                AverageList.Add(GetAverage(ThickList));

                MomentumList.Add(Math.Atan2(Length, Thickness));

                EndPrice = (fastserie + slowserie) / 2;

                BasesUpdate(RoofList, ofset, FastSeries);
                BasesUpdate(BasesList, ofset, SlowSeries);

                UpdateMinMax(FastSeries, ofset);
                UpdateMinMax(SlowSeries, ofset);

            }
        }

        private void UpdateMinMax(int lineseries, int offset)
        {
            double line = Indicator.GetValue(lineIndex: lineseries, offset: offset);
            int b = Buffer + Length;

            // Fast
            if (lineseries == FastSeries)
            {
                Min_Max min = new Min_Max(MinimaFast.Count, line, Length, b);
                Min_Max max = new Min_Max(MaximaFast.Count, line, Length, b);

                // minimi
                if (MinimaFast.Count < 1)
                {
                    if (min.Value < OriginPrice)
                        MinimaFast.Add(min);
                }
                else if (MinimaFast.Count >= 1)
                {
                    if (min.Value < MinimaFast[MinimaFast.Count - 1].Value)
                        MinimaFast.Add(min);
                }

                //massimi
                if (MaximaFast.Count < 1)
                {
                    if (max.Value > OriginPrice)
                        MaximaFast.Add(max);
                }
                else if (MaximaFast.Count >= 1)
                {
                    if (max.Value > MaximaFast[MaximaFast.Count - 1].Value)
                        MaximaFast.Add(min);
                }
            }

            // Slow
            if (lineseries == SlowSeries)
            {
                Min_Max min = new Min_Max(MinimaSlow.Count, line, Length, b);
                Min_Max max = new Min_Max(MaximaSlow.Count, line, Length, b);

                // minimi
                if (MinimaSlow.Count < 1)
                {
                    if (min.Value < OriginPrice)
                        MinimaSlow.Add(min);
                }
                else if (MinimaSlow.Count >= 1)
                {
                    if (min.Value < MinimaSlow[MinimaSlow.Count - 1].Value)
                        MinimaSlow.Add(min);
                }

                //massimi
                if (MaximaSlow.Count < 1)
                {
                    if (max.Value > OriginPrice)
                        MaximaSlow.Add(max);
                }
                else if (MaximaSlow.Count >= 1)
                {
                    if (max.Value > MaximaSlow[MaximaSlow.Count - 1].Value)
                        MaximaSlow.Add(min);
                }
            }

            else return;
        }

        public void CloudIsClosed(double endPrice, double endbarIndex)
        {
            IsOpen = false;
            EndPrice = endPrice;
        }

        public double GetAverage(List<double> array)
        {
            double sum = 0;

            foreach (double item in array)
            {
                sum += item;
            }

            return sum / array.Count;
        }

        private void BasesUpdate(List<Bases> list, int ofset, int serie)
        {
            double now = Indicator.GetValue(lineIndex: serie, offset: ofset);
            double before = Indicator.GetValue(lineIndex: serie, offset: ofset + 1);

            if (!list.Any() || list.Last().BStatus == Bases.Status.Waiting)
            {
                if (now == before)
                {
                    int id = 0;
                    if (list.Any())
                        id = list.Last().Id;
                    Bases bases = new Bases(id, serie, now, Buffer + Length - 1);
                    bases.Lenght = 1;
                    list.Add(bases);
                }
            }
            else if (list.Any() && list.Last().BStatus == Bases.Status.Running)
            {
                if (now == list.Last().Value)
                {
                    Bases b = list.Last();
                    b.Lenght += 1;
                    list[list.Count - 1] = b;
                }
                else if (now != list.Last().Value)
                {
                    Bases b = list.Last();
                    b.Close();
                    list[list.Count - 1] = b;
                }

            }
        }

        public double? GetArea()
        {
            double? output = null;

            if (ThickList.Any())
                output = ThickList.Sum() / Length;

            return output;
        }

        public double GetPosition()
        {
            return (FastValue.Last() + FastValue.Last()) / 2;
        }


    }
}
