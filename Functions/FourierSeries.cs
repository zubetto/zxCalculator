using System;
using System.Windows;
using zxCalculator;

namespace FourierSeries
{
    public class RectPulse : ICalculate
    {
        string[] argLabels = new string[5] { "t", "Tp", "Tw", "A", "n" };

        int ICalculate.argsNum { get { return 5; } }

        string ICalculate.Label { get { return "RectPulse"; } }
        string ICalculate.Description { get { return "Fourier Series for periodic rectangular pulse"; } }
        string[] ICalculate.ArgLabels { get { return argLabels; } }

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

            if (args[1] - args[2] > 1e-131)
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
