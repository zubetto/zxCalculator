using System;

namespace zxCalculator
{
    public class zCos : ICalculate
    {
        bool ICalculate.ForceSerialCalc { get { return false; } }

        string[] argLabels = new string[2] { "t", "w" };
        string[] argUnitsNames = new string[2] { "s", "" };

        int ICalculate.argsNum { get { return 2; } }

        string ICalculate.Label { get { return "cos(wt)"; } }
        string ICalculate.Description { get { return "Just simple cos(w*t)"; } }
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

                for (int i = 0; i < Num; i++)
                {
                    args[argInd] = argArr[i];
                    output[i] = Math.Cos(args[0] * args[1]);

                    Analyze.SetMinMax(output[i]);
                }
            }
            else if ( !( double.IsNaN(limA) || double.IsNaN(limB) || double.IsNaN(step) )) // using the given range limits
            {
                int stepNum = Analyze.SegmentLength;
                double currX = limA, currY;

                output = new double[stepNum];
                stepNum--;
                
                args[argInd] = currX;
                
                for (int i = 0; i < stepNum; i++)
                {
                    currY = Math.Cos(args[0] * args[1]);
                    output[i] = currY;
                    
                    Analyze.SetMinMax(currY);

                    currX += step;
                    args[argInd] = currX;
                }

                args[argInd] = limB;

                currY = Math.Cos(args[0] * args[1]);
                output[stepNum] = currY;

                Analyze.SetMinMax(currY);
            }
            else
            {
                output = new double[1];
                output[0] = Math.Cos(args[0] * args[1]);
            }

            return output;
        }

        public zCos() { }
    }

    class zCosPlus : ICalculate
    {
        bool ICalculate.ForceSerialCalc { get { return false; } }

        string[] argLabels = new string[3] { "t", "w", "k" };
        string[] argUnitsNames = new string[3] { "s", "", "" };

        int ICalculate.argsNum { get { return 3; } }

        string ICalculate.Label { get { return "cos(kw*t)+"; } }
        string ICalculate.Description { get { return "0.5*[cos(w*t) + cos(k*w*t)]"; } }
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

                for (int i = 0; i < Num; i++)
                {
                    args[argInd] = argArr[i];
                    output[i] = 0.5 * (Math.Cos(args[0] * args[1]) + Math.Cos(args[2] * args[0] * args[1]));

                    Analyze.SetMinMax(output[i]);
                }
            }
            else if (!(double.IsNaN(limA) || double.IsNaN(limB) || double.IsNaN(step))) // using the given range limits
            {
                int stepNum = Analyze.SegmentLength;
                double currX = limA, currY;

                output = new double[stepNum];
                stepNum--;

                args[argInd] = currX;

                for (int i = 0; i < stepNum; i++)
                {
                    currY = 0.5 * (Math.Cos(args[0] * args[1]) + Math.Cos(args[2] * args[0] * args[1]));
                    output[i] = currY;
                    
                    Analyze.SetMinMax(currY);

                    currX += step;
                    args[argInd] = currX;
                }

                args[argInd] = limB;

                currY = 0.5 * (Math.Cos(args[0] * args[1]) + Math.Cos(args[2] * args[0] * args[1]));
                output[stepNum] = currY;

                Analyze.SetMinMax(currY);
            }
            else
            {
                output = new double[1];
                output[0] = 0.5 * (Math.Cos(args[0] * args[1]) + Math.Cos(args[2] * args[0] * args[1]));
            }

            return output;
        }

        public zCosPlus() { }
    }

    class Sinc : ICalculate
    {
        bool ICalculate.ForceSerialCalc { get { return false; } }

        string[] argLabels = new string[3] { "t", "w", "k" };
        string[] argUnitsNames = new string[3] { "s", "", "" };

        int ICalculate.argsNum { get { return 3; } }

        string ICalculate.Label { get { return "sinc"; } }
        string ICalculate.Description { get { return "sin(kwt)/t"; } }
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

                for (int i = 0; i < Num; i++)
                {
                    args[argInd] = argArr[i];
                    output[i] = Math.Sin(args[2] * args[1] * args[0]) / args[0];

                    Analyze.SetMinMax(output[i]);
                }
            }
            else if (!(double.IsNaN(limA) || double.IsNaN(limB) || double.IsNaN(step))) // using the given range limits
            {
                int stepNum = Analyze.SegmentLength;
                double currX = limA, currY;

                output = new double[stepNum];
                stepNum--;

                args[argInd] = currX;

                for (int i = 0; i < stepNum; i++)
                {
                    currY = Math.Sin(args[2] * args[1] * args[0]) / args[0];
                    output[i] = currY;

                    Analyze.SetMinMax(currY);

                    currX += step;
                    args[argInd] = currX;
                }

                args[argInd] = limB;

                currY = Math.Sin(args[2] * args[1] * args[0]) / args[0];
                output[stepNum] = currY;

                Analyze.SetMinMax(currY);
            }
            else
            {
                output = new double[1];
                output[0] = Math.Sin(args[2] * args[1] * args[0]) / args[0];
            }

            return output;
        }

        public Sinc() { }
    }
}


