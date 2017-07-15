using System;

namespace zxCalculator
{
    public class zCos : ICalculate
    {
        string[] argLabels = new string[2] { "t", "w" };

        int ICalculate.argsNum { get { return 2; } }

        string ICalculate.Label { get { return "cos(wt)"; } }
        string ICalculate.Description { get { return "Just simple cos(w*t)"; } }
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
                    output[i] = Math.Cos(args[0] * args[1]);

                    Analyze.SetMinMax(output[i]);
                    Analyze.SetPoint(i, args[argInd], output[i]);
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
                    Analyze.SetPoint(i, currX, currY);

                    currX += step;
                    args[argInd] = currX;
                }

                args[argInd] = limB;

                currY = Math.Cos(args[0] * args[1]);
                output[stepNum] = currY;

                Analyze.SetMinMax(currY);
                Analyze.SetPoint(stepNum, limB, currY);
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
        string[] argLabels = new string[3] { "t", "w", "k" };

        int ICalculate.argsNum { get { return 3; } }

        string ICalculate.Label { get { return "cos(kw*t)+"; } }
        string ICalculate.Description { get { return "0.5*[cos(w*t) + cos(k*w*t)]"; } }
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
                    output[i] = 0.5 * (Math.Cos(args[0] * args[1]) + Math.Cos(args[2] * args[0] * args[1]));

                    Analyze.SetMinMax(output[i]);
                    Analyze.SetPoint(i, args[argInd], output[i]);
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
                    Analyze.SetPoint(i, currX, currY);

                    currX += step;
                    args[argInd] = currX;
                }

                args[argInd] = limB;

                currY = 0.5 * (Math.Cos(args[0] * args[1]) + Math.Cos(args[2] * args[0] * args[1]));
                output[stepNum] = currY;

                Analyze.SetMinMax(currY);
                Analyze.SetPoint(stepNum, limB, currY);
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
}


