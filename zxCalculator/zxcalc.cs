using System;
using System.Windows;

namespace zxCalculator
{
    public interface ISignal
    {
        bool IsAborted { get; }
        bool IsPaused { get; }
    }

    public class Interrupter : ISignal
    {
        private bool abortFlag;
        private bool pauseFlag;

        bool ISignal.IsAborted { get { return abortFlag; } }
        bool ISignal.IsPaused { get { return pauseFlag; } }

        public void Abort() { abortFlag = true; }

        public void Pause() { pauseFlag = true; }

        public void Resume() { pauseFlag = false; }

        public void ResetFlags()
        {
            abortFlag = false;
            pauseFlag = false;
        }
    }

    /// <summary>
    /// Contains methods and properties suitable for on-the-fly analysis and setting
    /// </summary>
    public interface IAnalyzer
    {
        double[] ArgumentSet { get; }

        int SegmentLength { get; }
        double SegmentLimA { get; }
        double SegmentLimB { get; }
        double SegmentStep { get; }

        /// <summary>
        /// Sets point coordinates for plotting. X,Y values are checked for NaN and +-Infinity
        /// </summary>
        /// <param name="i">point index</param>
        /// <param name="X">Point.X</param>
        /// <param name="Y">Point.Y</param>
        void SetPoint(int i, double X, double Y);

        /// <summary>
        /// Is used to fit in plotter output area
        /// </summary>
        /// <param name="f">The current function value to compare</param>
        void SetMinMax(double f);
    }

    public class AnalysisData : IAnalyzer
    {
        private bool completed = false;
        public bool IsComplete { get { return completed; } }

        // inputs
        private double[] argSet;
        private Point[] PointsArr;

        private int argInd, Num;
        private double Xmin, Xmax, Xstep;

        public int ArgumentIndex { get { return argInd; } }
        public int SegmentLength { get { return Num; } }
        public double SegmentLimA { get { return Xmin; } }
        public double SegmentLimB { get { return Xmax; } }
        public double SegmentStep { get { return Xstep; } }

        // outputs
        private double min = double.PositiveInfinity;
        private double max = double.NegativeInfinity;

        public double Minimum { get { return min; } }
        public double Maximum { get { return max; } }

        private string exceptionsStr = "";
        public string ExceptionsStrig { get { return exceptionsStr; } }

        void IAnalyzer.SetMinMax(double f)
        {
            if (f < min) min = f;
            if (f > max) max = f;
        }

        void IAnalyzer.SetPoint(int i, double X, double Y)
        {
            if (PointsArr == null) return; // >>>>> Do not collect points >>>>>

            // checking of X
            if (double.IsNaN(X))
            {
                if (i == 0) PointsArr[i].X = 0;
                else PointsArr[i].X = PointsArr[i - 1].X;
            }
            if (double.IsNegativeInfinity(X)) PointsArr[i].X = double.MinValue;
            if (double.IsPositiveInfinity(X)) PointsArr[i].X = double.MaxValue;

            // checking of Y
            if (double.IsNaN(Y))
            {
                if (i == 0) PointsArr[i].Y = 0;
                else PointsArr[i].Y = PointsArr[i - 1].Y;
            } 
            if (double.IsNegativeInfinity(Y)) PointsArr[i].Y = double.MinValue;
            if (double.IsPositiveInfinity(Y)) PointsArr[i].Y = double.MaxValue;
        }

        public double[] ArgumentSet { get { return argSet; } }
        public Point[] PointsArray { get { return PointsArr; } }

        public void WriteException(string e, int i)
        {
            exceptionsStr += string.Format("Segment {0}:\n\r{1}\n\r---------\n\n\r", i, e);
        }

        public void Complete()
        {
            completed = true;
        }

        public void ResetData()
        {
            completed = false;

            min = double.PositiveInfinity;
            max = double.NegativeInfinity;
            
            if (PointsArr != null)
            {
                PointsArr[0].X = double.PositiveInfinity; // unreliable but simple way to know whether the array has been set
                PointsArr[PointsArr.GetLength(0) - 1].X = double.NegativeInfinity;
            }

            exceptionsStr = "";
        }

        public AnalysisData(double[] args, int byteSize, int argIndex, int num, double limA, double limB, double step, Point[] ptArr = null)
        {
            argSet = new double[args.GetLength(0)];

            // BlockCopy is needed due to each thread will be change the argument with index argIndex
            // in the set of the arguments during array calculation
            Buffer.BlockCopy(args, 0, argSet, 0, byteSize);

            argInd = argIndex;

            Num = num;
            Xmin = limA;
            Xmax = limB;
            Xstep = step;

            if (double.IsNaN(Xmax) || Xmax < Xmin) Xmax = Xmin + (Num - 1) * Xstep;

            PointsArr = ptArr;
        }
    }

    /// <summary>
    /// Calculates custom-function values on a given arguments set.
    /// Array of any argument could be specified as double[] array or as range with limits and step 
    /// </summary>
    /// <param name="args">Arguments set {x, y, z ... }</param>
    /// <param name="argsArr">Array of values of one of the function argument</param>
    /// <param name="limA">Lower range limit</param>
    /// <param name="limB">Upper range limit</param>
    /// <param name="step">The range sampling value</param>
    /// <param name="argInd">Index of the argument in the args for which argArr or range are given and the function will be caclculated</param>
    /// <param name="signal">Could be used to stop or pause calculation</param>
    /// <param name="Analyze">Could be used for lightweight on-the-fly analysis</param>
    /// <returns></returns>
    public delegate double[] Function(double[] args, double[] argArr = null, 
                                      double limA = double.NaN, double limB = double.NaN, double step = double.NaN, int argInd = 0,
                                      ISignal signal = null, IAnalyzer Analyze = null);
    /*// Method implementation template: 
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
    //*/

    /// <summary>
    /// Intended for custom function calculations
    /// </summary>
    public interface ICalculate
    {
        /// <summary>
        /// Number of function arguments
        /// </summary>
        int argsNum { get; }

        /// <summary>
        /// Only first ten characters will be used
        /// </summary>
        string Label { get; }

        /// <summary>
        /// It can be empty string
        /// </summary>
        string UnitName { get; }

        /// <summary>
        /// Good place for the arguments description
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Up to seven characters should fit in place well
        /// </summary>
        string[] ArgLabels { get; }

        /// <summary>
        /// It can be null or contains empty strings; in the latter case the array length should be equal argsNum
        /// </summary>
        string[] ArgUnitsNames { get; }

        /// <summary>
        /// If true then array of an argument values will not be divided into segments for parallel calculations
        /// </summary>
        bool ForceSerialCalc { get; }

        /// <summary>
        /// Calculates custom-function values on a given arguments set.
        /// Array of any argument could be specified as double[] array or as range with limits and step 
        /// </summary>
        /// <param name="args">Arguments set {x, y, z ... }</param>
        /// <param name="argsArr">Array of values of one of the function argument</param>
        /// <param name="limA">Lower range limit</param>
        /// <param name="limB">Upper range limit</param>
        /// <param name="step">The range sampling value</param>
        /// <param name="argInd">Index of the argument in the args for which argArr or range are given and the function will be caclculated</param>
        /// <param name="signal">Could be used to stop or pause calculation</param>
        /// <param name="Analyze">Could be used for lightweight on-the-fly analysis</param>
        /// <returns></returns>
        double[] Calculate(double[] args, double[] argArr = null, 
                           double limA = double.NaN, double limB = double.NaN, double step = double.NaN, int argInd = 0,
                           ISignal signal = null, IAnalyzer Analyze = null);
    }
}
