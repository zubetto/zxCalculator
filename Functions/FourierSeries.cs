using System;
using System.Windows;
using zxCalculator;

namespace FourierSeries
{
    public class RectPulseBasic : ICalculate
    {
        bool ICalculate.ForceSerialCalc { get { return false; } }

        string[] argLabels = new string[4] { "t", "Tp", "Tw", "A" };
        string[] argUnitsNames = new string[4] { "s", "s", "s", "V" };

        int ICalculate.argsNum { get { return 4; } }

        string ICalculate.Label { get { return "RectPulse"; } }
        string ICalculate.Description { get { return "Rectangular pulse specified by the period Tp, pulse width Tw and amplitude A"; } }
        string[] ICalculate.ArgLabels { get { return argLabels; } }
        string[] ICalculate.ArgUnitsNames { get { return argUnitsNames; } }
        string ICalculate.UnitName { get { return "V"; } }

        public double[] Calculate(double[] args, double[] argArr = null,
                                  double limA = double.NaN, double limB = double.NaN, double step = double.NaN, int argInd = 0,
                                  ISignal signal = null, IAnalyzer Analyze = null)
        {
            double[] output;

            if (argArr != null)
            {
                int Num = argArr.GetLength(0);
                output = new double[Num];

                for (int i = 0; i < Num; i++)
                {
                    args[argInd] = argArr[i];
                    output[i] = FourierSSum(args);

                    Analyze.SetMinMax(output[i]);
                    Analyze.SetPoint(i, args[argInd], output[i]);
                }
            }
            else if (!(double.IsNaN(limA) || double.IsNaN(limB) || double.IsNaN(step))) // using the given range limits
            {
                int stepNum = Analyze.SegmentLength;
                double currX = limA;
                double currY;

                output = new double[stepNum];
                stepNum--;

                args[argInd] = currX;

                for (int i = 0; i < stepNum; i++)
                {
                    currY = FourierSSum(args);
                    output[i] = currY;

                    Analyze.SetMinMax(currY);
                    Analyze.SetPoint(i, currX, currY);

                    currX += step;
                    args[argInd] = currX;
                }

                args[argInd] = limB;

                currY = FourierSSum(args);
                output[stepNum] = currY;

                Analyze.SetMinMax(currY);
                Analyze.SetPoint(stepNum, limB, currY);
            }
            else
            {
                output = new double[1] { FourierSSum(args) };
            }

            return output;
        }

        private double FourierSSum(double[] args)
        {
            // { "t", "Tp", "Tw", "A" };
            // {  0,   1,    2,    3  };

            if (args[1] < 1e-100) args[1] = 1e-100;
            if (args[2] < 1e-30 * args[1]) args[2] = 1e-30 * args[1];
            if (args[1] < args[2]) args[2] = args[1];
            if (args[3] < 0) args[3] = -args[3];

            double f = 0;

            if (args[1] - args[2] > 1e-133)
            {
                double t = args[0];
                double half = 0.5 * args[2];

                if (t < 0) t = -t;
                t -= (int)Math.Floor((t + half) / args[1]) * args[1];

                if (t == 0 || (t > 0 && t <= half) || (t < 0 && t >= -half)) f = args[3];
            }

            return f;
        }

        public RectPulseBasic() { }
    }

    public class RectPulse : ICalculate
    {
        bool ICalculate.ForceSerialCalc { get { return false; } }

        string[] argLabels = new string[5] { "t", "Tp", "Tw", "A", "n" };
        string[] argUnitsNames = new string[5] { "s", "s", "s", "V", "" };

        int ICalculate.argsNum { get { return 5; } }

        string ICalculate.Label { get { return "Fourier RP"; } }
        string ICalculate.Description { get { return "Fourier Series for periodic rectangular pulse"; } }
        string[] ICalculate.ArgLabels { get { return argLabels; } }
        string[] ICalculate.ArgUnitsNames { get { return argUnitsNames; } }
        string ICalculate.UnitName { get { return "V"; } }

        public double[] Calculate(double[] args, double[] argArr = null,
                                  double limA = double.NaN, double limB = double.NaN, double step = double.NaN, int argInd = 0,
                                  ISignal signal = null, IAnalyzer Analyze = null)
        {
            double[] output;

            if (argArr != null)
            {
                int Num = argArr.GetLength(0);
                output = new double[Num];

                for (int i = 0; i < Num; i++)
                {
                    args[argInd] = argArr[i];
                    output[i] = FourierSSum(args);

                    Analyze.SetMinMax(output[i]);
                    Analyze.SetPoint(i, args[argInd], output[i]);
                }
            }
            else if (!(double.IsNaN(limA) || double.IsNaN(limB) || double.IsNaN(step))) // using the given range limits
            {
                int stepNum = Analyze.SegmentLength;
                double currX = limA; 
                double currY;

                output = new double[stepNum];
                stepNum--;

                args[argInd] = currX;

                for (int i = 0; i < stepNum; i++)
                {
                    currY = FourierSSum(args);
                    output[i] = currY;

                    Analyze.SetMinMax(currY);
                    Analyze.SetPoint(i, currX, currY);

                    currX += step;
                    args[argInd] = currX;
                }

                args[argInd] = limB;

                currY = FourierSSum(args);
                output[stepNum] = currY;

                Analyze.SetMinMax(currY);
                Analyze.SetPoint(stepNum, limB, currY);
            }
            else
            {
                output = new double[1] { FourierSSum(args)};
            }

            return output;
        }

        private double FourierSSum(double[] args)
        {
            // { "t", "Tp", "Tw", "A", "n" };
            // {  0,   1,    2,    3,   4  };

            if (args[1] < 1e-100) args[1] = 1e-100;
            if (args[2] < 1e-30 * args[1]) args[2] = 1e-30 * args[1];
            if (args[1] < args[2]) args[2] = args[1];
            if (args[3] < 0) args[3] = -args[3];
            if (args[4] < 0) args[4] = -args[4];

            double pi = Math.PI;
            double t = args[0];
            double wo = 2 * pi / args[1];
            double k = 2 * args[3];
            double Tratio = args[2] / args[1];
            double a0 = args[3] * Tratio;
            double ai;
            double FSSum = a0;

            int FSnum = (int)Math.Round(args[4]);

            if (args[1] - args[2] > 1e-133)
            {
                // Fourier Series Sum
                for (int i = 1; i <= FSnum; i++)
                {
                    ai = k * Math.Sin(i * pi * Tratio) / (i * pi);
                    FSSum += ai * Math.Cos(i * wo * t);
                }
            }
            
            return FSSum;
        }

        public RectPulse() { }
    }
}
