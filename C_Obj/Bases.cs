using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DivergentStrV0_1.C_Obj
{
    public struct Bases
    {
        public enum Status
        {
            Waiting,
            Running
        }
        public int Lenght { get; set; }
        public Status BStatus { get; private set; }
        public int Id { get; set; }
        public double Value { get; set; }
        public int LineSeries { get; private set; }
        public int Buffer { get; set; }

        public Bases(int id, int lineseries, double value, int buffer)
        {
            Lenght = 0;
            BStatus = Status.Running;
            LineSeries = lineseries;
            Id = id;
            Value = value;
            Buffer = buffer;
        }

        public void Close()
        {
            BStatus = Status.Waiting;
        }
    }
}
