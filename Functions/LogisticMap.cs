using System;

namespace zxCalculator
{
    public class LogisticMap : ICalculate
    {
        bool ICalculate.ForceSerialCalc { get { return false; } }

        string[] argLabels = new string[3] { "t", "r", "xo" };
        string[] argUnitsNames = new string[3] { "s", "", "" };

        int ICalculate.argsNum { get { return 3; } }

        string ICalculate.Label { get { return "LMap xo"; } }
        string ICalculate.Description { get { return "x[i+1] = rx[i](1-x[i])"; } }
        string[] ICalculate.ArgLabels { get { return argLabels; } }
        string[] ICalculate.ArgUnitsNames { get { return argUnitsNames; } }
        string ICalculate.UnitName { get { return ""; } }

        public double[] Calculate(double[] args, double[] argArr = null,
                                  double limA = double.NaN, double limB = double.NaN, double step = double.NaN, int argInd = 0,
                                  ISignal signal = null, IAnalyzer Analyze = null)
        {
            double[] output;

            if (argArr != null)
            {
                int Num = argArr.Length;
                output = new double[Num];

                double r = args[1];
                double outputPrev = args[2];

                output[0] = outputPrev;
                Analyze.SetMinMax(output[0]);

                for (int i = 1; i < Num; i++)
                {
                    outputPrev = r * outputPrev * (1 - outputPrev);
                    output[i] = outputPrev;

                    Analyze.SetMinMax(output[i]);
                }
            }
            else if (!(double.IsNaN(limA) || double.IsNaN(limB) || double.IsNaN(step))) // using the given range limits
            {
                int stepNum = Analyze.SegmentLength;

                output = new double[stepNum];

                double currY = args[2];
                double r = args[1];
                
                output[0] = currY;
                Analyze.SetMinMax(output[0]);

                for (int i = 1; i < stepNum; i++)
                {
                    currY = r * currY * (1 - currY);
                    output[i] = currY;

                    Analyze.SetMinMax(currY);
                }
            }
            else
            {
                output = new double[1];
                output[0] = args[2];
            }

            return output;
        }

        public LogisticMap() { }
    }

    public class LogisticMapDelta : ICalculate
    {
        bool ICalculate.ForceSerialCalc { get { return false; } }

        string[] argLabels = new string[4] { "t", "r", "xo", "dx" };
        string[] argUnitsNames = new string[4] { "s", "", "", "" };

        int ICalculate.argsNum { get { return 4; } }

        string ICalculate.Label { get { return "LMap xo+dx"; } }
        string ICalculate.Description { get { return "x[i+1] = rx[i](1-x[i])"; } }
        string[] ICalculate.ArgLabels { get { return argLabels; } }
        string[] ICalculate.ArgUnitsNames { get { return argUnitsNames; } }
        string ICalculate.UnitName { get { return ""; } }

        public double[] Calculate(double[] args, double[] argArr = null,
                                  double limA = double.NaN, double limB = double.NaN, double step = double.NaN, int argInd = 0,
                                  ISignal signal = null, IAnalyzer Analyze = null)
        {
            double[] output;

            if (argArr != null)
            {
                int Num = argArr.Length;
                output = new double[Num];

                double r = args[1];
                double outputPrev = args[2] + args[3];

                output[0] = outputPrev;
                Analyze.SetMinMax(output[0]);

                for (int i = 1; i < Num; i++)
                {
                    outputPrev = r * outputPrev * (1 - outputPrev);
                    output[i] = outputPrev;

                    Analyze.SetMinMax(output[i]);
                }
            }
            else if (!(double.IsNaN(limA) || double.IsNaN(limB) || double.IsNaN(step))) // using the given range limits
            {
                int stepNum = Analyze.SegmentLength;

                output = new double[stepNum];

                double currY = args[2] + args[3];
                double r = args[1];

                output[0] = currY;
                Analyze.SetMinMax(output[0]);

                for (int i = 1; i < stepNum; i++)
                {
                    currY = r * currY * (1 - currY);
                    output[i] = currY;

                    Analyze.SetMinMax(currY);
                }
            }
            else
            {
                output = new double[1];
                output[0] = args[2] + args[3];
            }

            return output;
        }

        public LogisticMapDelta() { }
    }
}
