using System;

namespace zxCalculator
{
    public class InertialFilter : ICalculate
    {
        bool ICalculate.ForceSerialCalc { get { return true; } }

        string[] argLabels = new string[10] { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
        string[] argUnitsNames = new string[10] { "s", "m", "m", "m", "", "", "", "m", "", "" };

        int ICalculate.argsNum { get { return 10; } }

        string ICalculate.Label { get { return "InertialF"; } }
        string ICalculate.Description { get { return "Low-pass filter"; } }
        string[] ICalculate.ArgLabels { get { return argLabels; } }
        string[] ICalculate.ArgUnitsNames { get { return argUnitsNames; } }
        string ICalculate.UnitName { get { return "m"; } }
        
        private double inSignal(double t, double A0, double A1, double A2, double w0, double w1, double w2)
        {
            if (A0 < 0) A0 = -A0;
            if (A1 < 0) A1 = -A1;
            if (A2 < 0) A2 = -A2;

            if (w0 < 0) w0 = -w0;
            if (w1 < 0) w1 = -w1;
            if (w2 < 0) w2 = -w2;

            if (w1 < w0) w1 = w0;
            if (w2 < w1) w2 = w1;

            return A0 * Math.Sin(w0 * t) + A1 * Math.Sin(w1 * t) + A2 * Math.Sin(w2 * t);
        }

        // i-2  i-1  i
        // inS0 inS1 inS2
        // inV0 inV1
        private double inS0, inS1, inS2, inV0; // original signal values
        private double outS, outV; // filtered signal values
        
        private double FadeIn(double t, double W)
        {
            double k = 0.25 * (W + 1) * t;

            k *= k;

            return k / (k + 1);
        }

        private double outSignal(double dt, double inS2, double Af, double Wf, double n)
        {
            // to avoid NaN value of the K
            if (Af == 0 || Wf == 0) // then the limited acceleration is equal zero
            {
                // store original signal values
                inV0 = (inS2 - inS1) / dt;
                inS1 = inS2;

                outS += outV * dt;

                return outS; // >>>>>>> >>>>>>>
            }

            if (n < 0) n = -n;

            double inV1 = (inS2 - inS1) / dt;
            double Acc = (inV1 - inV0) / dt;

            // store original signal values
            inS1 = inS2;
            inV0 = inV1;
            
            // apply limiter for the acceleration Acc
            double Accu = Acc / (Af * Wf * Wf); // (Af * Wf * Wf) is the acceleration limit

            if (Accu < 0) Accu = -Accu;

            double Accn = Math.Pow(Accu, n);
            double K = (Accu * Accn + 1) / (Accn + 1);

            Acc /= K;

            // calculate output signal value based on limited acceleration
            outV += Acc * dt;
            outS += outV * dt;

            return outS;
        }

        public double[] Calculate(double[] args, double[] argArr = null,
                                 double limA = double.NaN, double limB = double.NaN, double step = double.NaN, int argInd = 0,
                                 ISignal signal = null, IAnalyzer Analyze = null)
        {
            double[] output;

            if (argArr != null)
            {
                int Num = argArr.Length;
                output = new double[Num];

                //    0     1     2     3     4     5     6     7     8    9
                // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };

                // i = 0
                args[argInd] = argArr[0];

                inS0 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                outS = inS0;
                output[0] = outS;

                Analyze.SetMinMax(outS);
                Analyze.SetPoint(0, args[argInd], outS);

                // i = 1
                args[argInd] = argArr[1];

                inS1 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                outS = inS1;
                output[0] = outS;
                inV0 = (inS1 - inS0) / (argArr[1] - argArr[0]);
                outV = inV0;

                Analyze.SetMinMax(outS);
                Analyze.SetPoint(1, args[argInd], outS);

                for (int i = 2; i < Num; i++)
                {
                    args[argInd] = argArr[i];

                    //    0     1     2     3     4     5     6     7     8    9
                    // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
                    inS2 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                    outSignal(argArr[i] - argArr[i - 1], inS2, args[7], args[8], args[9]);
                    output[i] = outS;

                    Analyze.SetMinMax(outS);
                    Analyze.SetPoint(i, args[argInd], outS);
                }
            }
            else if (!(double.IsNaN(limA) || double.IsNaN(limB) || double.IsNaN(step))) // using the given range limits
            {
                int stepNum = Analyze.SegmentLength;
                double currX = limA;

                output = new double[stepNum];
                stepNum--;

                //    0     1     2     3     4     5     6     7     8    9
                // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };

                // i = 0
                args[argInd] = currX;

                inS0 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                outS = inS0;
                output[0] = outS;

                Analyze.SetMinMax(outS);
                Analyze.SetPoint(0, currX, outS);

                // i = 1
                currX += step;
                args[argInd] = currX;
                
                inS1 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                outS = inS1;
                output[1] = outS;
                inV0 = (inS1 - inS0) / step;
                outV = inV0;

                Analyze.SetMinMax(outS);
                Analyze.SetPoint(1, currX, outS);

                currX += step;
                args[argInd] = currX;

                for (int i = 2; i < stepNum; i++)
                {
                    //    0     1     2     3     4     5     6     7     8    9
                    // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
                    inS2 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                    outSignal(step, inS2, args[7], args[8], args[9]);
                    output[i] = outS;

                    Analyze.SetMinMax(outS);
                    Analyze.SetPoint(i, currX, outS);

                    currX += step;
                    args[argInd] = currX;
                }
                
                args[argInd] = limB;

                //    0     1     2     3     4     5     6     7     8    9
                // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
                inS2 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                outSignal(limB - currX + step, inS2, args[7], args[8], args[9]);
                output[stepNum] = outS;

                Analyze.SetMinMax(outS);
                Analyze.SetPoint(stepNum, limB, outS);
            }
            else
            {
                output = new double[1];
                //    0     1     2     3     4     5     6     7     8    9
                // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
                output[0] = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]);
            }

            return output;
        }

        public InertialFilter() { }
    } // end of public class InertialFilter : ICalculate /////////////////////////////////////////////////////////////////////////////////////

    public class InertialFilter_in : ICalculate
    {
        bool ICalculate.ForceSerialCalc { get { return true; } }

        string[] argLabels = new string[10] { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
        string[] argUnitsNames = new string[10] { "s", "m", "m", "m", "", "", "", "m", "", "" };

        int ICalculate.argsNum { get { return 10; } }

        string ICalculate.Label { get { return "IF input"; } }
        string ICalculate.Description { get { return "Original signal"; } }
        string[] ICalculate.ArgLabels { get { return argLabels; } }
        string[] ICalculate.ArgUnitsNames { get { return argUnitsNames; } }
        string ICalculate.UnitName { get { return "m"; } }

        private double inSignal(double t, double A0, double A1, double A2, double w0, double w1, double w2)
        {
            if (A0 < 0) A0 = -A0;
            if (A1 < 0) A1 = -A1;
            if (A2 < 0) A2 = -A2;

            if (w0 < 0) w0 = -w0;
            if (w1 < 0) w1 = -w1;
            if (w2 < 0) w2 = -w2;

            if (w1 < w0) w1 = w0;
            if (w2 < w1) w2 = w1;

            return A0 * Math.Sin(w0 * t) + A1 * Math.Sin(w1 * t) + A2 * Math.Sin(w2 * t);
        }

        // i-2  i-1  i
        // inS0 inS1 inS2
        // inV0 inV1
        private double inS1, inV0; // original signal values
        private double outS, outV; // filtered signal values

        private double FadeIn(double t, double W)
        {
            double k = 0.25 * (W + 1) * t;

            k *= k;

            return k / (k + 1);
        }

        private double outSignal(double dt, double inS2, double Af, double Wf, double n)
        {
            // to avoid NaN value of the K
            if (Af == 0 || Wf == 0) // then the limited acceleration is equal zero
            {
                // store original signal values
                inV0 = (inS2 - inS1) / dt;
                inS1 = inS2;

                outS += outV * dt;

                return outS; // >>>>>>> >>>>>>>
            }

            if (n < 0) n = -n;

            double inV1 = (inS2 - inS1) / dt;
            double Acc = (inV1 - inV0) / dt;

            // store original signal values
            inS1 = inS2;
            inV0 = inV1;

            // apply limiter for the acceleration Acc
            double Accu = Acc / (Af * Wf * Wf); // (Af * Wf * Wf) is the acceleration limit

            if (Accu < 0) Accu = -Accu;

            double Accn = Math.Pow(Accu, n);
            double K = (Accu * Accn + 1) / (Accn + 1);

            Acc /= K;

            // calculate output signal value based on limited acceleration
            outV += Acc * dt;
            outS += outV * dt;

            return outS;
        }

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

                    //    0     1     2     3     4     5     6     7     8    9
                    // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
                    outS = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                    output[i] = outS;

                    Analyze.SetMinMax(outS);
                    Analyze.SetPoint(i, args[argInd], outS);
                }
            }
            else if (!(double.IsNaN(limA) || double.IsNaN(limB) || double.IsNaN(step))) // using the given range limits
            {
                int stepNum = Analyze.SegmentLength;
                double currX = limA;

                output = new double[stepNum];
                stepNum--;

                args[argInd] = currX;
                
                for (int i = 0; i < stepNum; i++)
                {
                    //    0     1     2     3     4     5     6     7     8    9
                    // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
                    outS = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                    output[i] = outS;

                    Analyze.SetMinMax(outS);
                    Analyze.SetPoint(i, currX, outS);

                    currX += step;
                    args[argInd] = currX;
                }

                args[argInd] = limB;

                //    0     1     2     3     4     5     6     7     8    9
                // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
                outS = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                output[stepNum] = outS;

                Analyze.SetMinMax(outS);
                Analyze.SetPoint(stepNum, limB, outS);
            }
            else
            {
                output = new double[1];
                //    0     1     2     3     4     5     6     7     8    9
                // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
                output[0] = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]);
            }

            return output;
        }

        public InertialFilter_in() { }
    } // end of public class InertialFilter_in : ICalculate ///////////////////////////////////////////////////////////////////////////////

    public class InertialFilter_Acc : ICalculate
    {
        bool ICalculate.ForceSerialCalc { get { return true; } }

        string[] argLabels = new string[10] { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
        string[] argUnitsNames = new string[10] { "s", "m", "m", "m", "", "", "", "m", "", "" };

        int ICalculate.argsNum { get { return 10; } }

        string ICalculate.Label { get { return "IF Acc"; } }
        string ICalculate.Description { get { return "Second derivative of the original signal"; } }
        string[] ICalculate.ArgLabels { get { return argLabels; } }
        string[] ICalculate.ArgUnitsNames { get { return argUnitsNames; } }
        string ICalculate.UnitName { get { return "m/s^2"; } }

        private double inSignal(double t, double A0, double A1, double A2, double w0, double w1, double w2)
        {
            if (A0 < 0) A0 = -A0;
            if (A1 < 0) A1 = -A1;
            if (A2 < 0) A2 = -A2;

            if (w0 < 0) w0 = -w0;
            if (w1 < 0) w1 = -w1;
            if (w2 < 0) w2 = -w2;

            if (w1 < w0) w1 = w0;
            if (w2 < w1) w2 = w1;

            return A0 * Math.Sin(w0 * t) + A1 * Math.Sin(w1 * t) + A2 * Math.Sin(w2 * t);
        }

        // i-2  i-1  i
        // inS0 inS1 inS2
        // inV0 inV1
        private double inS0, inS1, inS2, inV0; // original signal values
        //private double outS, outV; // filtered signal values

        private double FadeIn(double t, double W)
        {
            double k = 0.25 * (W + 1) * t;

            k *= k;

            return k / (k + 1);
        }

        private double outSignalAcc(double dt, double inS2, double Af, double Wf, double n)
        {
            // to avoid NaN value of the K
            if (Af == 0 || Wf == 0) // then the limited acceleration is equal zero
            {
                // store original signal values
                inV0 = (inS2 - inS1) / dt;
                inS1 = inS2;

                //outS += outV * dt;

                return 0; // >>>>>>> >>>>>>>
            }

            if (n < 0) n = -n;

            double inV1 = (inS2 - inS1) / dt;
            double Acc = (inV1 - inV0) / dt;

            // store original signal values
            inS1 = inS2;
            inV0 = inV1;

            // apply limiter for the acceleration Acc
            double Accu = Acc / (Af * Wf * Wf); // (Af * Wf * Wf) is the acceleration limit

            if (Accu < 0) Accu = -Accu;

            double Accn = Math.Pow(Accu, n);
            double K = (Accu * Accn + 1) / (Accn + 1);

            Acc /= K;

            // calculate output signal value based on limited acceleration
            //outV += Acc * dt;
            //outS += outV * dt;

            return Acc;
        }

        public double[] Calculate(double[] args, double[] argArr = null,
                                 double limA = double.NaN, double limB = double.NaN, double step = double.NaN, int argInd = 0,
                                 ISignal signal = null, IAnalyzer Analyze = null)
        {
            double[] output;

            if (argArr != null)
            {
                int Num = argArr.Length;
                output = new double[Num];

                double outAcc = 0;

                //    0     1     2     3     4     5     6     7     8    9
                // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };

                // i = 0
                args[argInd] = argArr[0];

                inS0 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);

                // i = 1
                args[argInd] = argArr[1];

                inS1 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                inV0 = (inS1 - inS0) / (argArr[1] - argArr[0]);

                int j = 0;

                for (int i = 2; i < Num; i++)
                {
                    args[argInd] = argArr[i];

                    //    0     1     2     3     4     5     6     7     8    9
                    // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
                    inS2 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                    outAcc = outSignalAcc(step, inS2, args[7], args[8], args[9]); // acceleration in point i-2
                    output[j] = outAcc;

                    Analyze.SetMinMax(outAcc);
                    Analyze.SetPoint(j, argArr[j++], outAcc);
                }

                // Filling in the remaining elements of the output array
                output[j] = outAcc;
                Analyze.SetPoint(j, argArr[j++], outAcc); // i-1

                output[j] = outAcc;
                Analyze.SetPoint(j, argArr[j++], outAcc); // i
            }
            else if (!(double.IsNaN(limA) || double.IsNaN(limB) || double.IsNaN(step))) // using the given range limits
            {
                int stepNum = Analyze.SegmentLength;
                double currX = limA, outAcc;

                output = new double[stepNum];
                stepNum--;

                //    0     1     2     3     4     5     6     7     8    9
                // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };

                // i = 0
                args[argInd] = currX;

                inS0 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);

                // i = 1
                currX += step;
                args[argInd] = currX;

                inS1 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                inV0 = (inS1 - inS0) / step;

                currX += step;
                args[argInd] = currX;

                int j = 0;
                double Xj = limA;

                for (int i = 2; i < stepNum; i++)
                {
                    //    0     1     2     3     4     5     6     7     8    9
                    // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
                    inS2 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                    outAcc = outSignalAcc(step, inS2, args[7], args[8], args[9]); // acceleration in point i-2
                    output[j] = outAcc;

                    Analyze.SetMinMax(outAcc);
                    Analyze.SetPoint(j++, Xj, outAcc);

                    currX += step;
                    args[argInd] = currX;

                    Xj += step;
                }

                args[argInd] = limB;

                //    0     1     2     3     4     5     6     7     8    9
                // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
                inS2 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                outAcc = outSignalAcc(limB - currX + step, inS2, args[7], args[8], args[9]);
                output[j] = outAcc;

                Analyze.SetMinMax(outAcc);
                Analyze.SetPoint(j++, Xj, outAcc);

                // Filling in the remaining elements of the output array
                Xj += step;

                output[j] = outAcc;
                Analyze.SetPoint(j++, Xj, outAcc); // i-1 

                output[j] = outAcc;
                Analyze.SetPoint(stepNum, limB, outAcc); // i
            }
            else
            {
                output = new double[1];
                //    0     1     2     3     4     5     6     7     8    9
                // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
                output[0] = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]);
            }

            return output;
        }

        public InertialFilter_Acc() { }
    } // end of public class InertialFilter_Acc : ICalculate /////////////////////////////////////////////////////////////////////////////

    public class InertialFilter_V : ICalculate
    {
        bool ICalculate.ForceSerialCalc { get { return true; } }

        string[] argLabels = new string[10] { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
        string[] argUnitsNames = new string[10] { "s", "m", "m", "m", "", "", "", "m", "", "" };


        int ICalculate.argsNum { get { return 10; } }

        string ICalculate.Label { get { return "IF V"; } }
        string ICalculate.Description { get { return "First derivative of the original signal"; } }
        string[] ICalculate.ArgLabels { get { return argLabels; } }
        string[] ICalculate.ArgUnitsNames { get { return argUnitsNames; } }
        string ICalculate.UnitName { get { return "m/s"; } }

        private double inSignal(double t, double A0, double A1, double A2, double w0, double w1, double w2)
        {
            if (A0 < 0) A0 = -A0;
            if (A1 < 0) A1 = -A1;
            if (A2 < 0) A2 = -A2;

            if (w0 < 0) w0 = -w0;
            if (w1 < 0) w1 = -w1;
            if (w2 < 0) w2 = -w2;

            if (w1 < w0) w1 = w0;
            if (w2 < w1) w2 = w1;

            return A0 * Math.Sin(w0 * t) + A1 * Math.Sin(w1 * t) + A2 * Math.Sin(w2 * t);
        }

        // i-2  i-1  i
        // inS0 inS1 inS2
        // inV0 inV1 
        private double inS0, inS1, inS2, inV0; // original signal values
        private double outV; // filtered signal values

        private double FadeIn(double t, double W)
        {
            double k = 0.25 * (W + 1) * t;

            k *= k;

            return k / (k + 1);
        }

        private double outSignalV(double dt, double inS2, double Af, double Wf, double n)
        {
            // to avoid NaN value of the K
            if (Af == 0 || Wf == 0) // then the limited acceleration is equal zero
            {
                // store original signal values
                inV0 = (inS2 - inS1) / dt;
                inS1 = inS2;

                //outS += outV * dt;

                return outV; // >>>>>>> >>>>>>>
            }

            if (n < 0) n = -n;

            double inV1 = (inS2 - inS1) / dt;
            double Acc = (inV1 - inV0) / dt;

            // store original signal values
            inS1 = inS2;
            inV0 = inV1;

            // apply limiter for the acceleration Acc
            double Accu = Acc / (Af * Wf * Wf); // (Af * Wf * Wf) is the acceleration limit

            if (Accu < 0) Accu = -Accu;

            double Accn = Math.Pow(Accu, n);
            double K = (Accu * Accn + 1) / (Accn + 1);

            Acc /= K;

            // calculate output signal value based on limited acceleration
            outV += Acc * dt;
            //outS += outV * dt;

            return outV;
        }

        public double[] Calculate(double[] args, double[] argArr = null,
                                 double limA = double.NaN, double limB = double.NaN, double step = double.NaN, int argInd = 0,
                                 ISignal signal = null, IAnalyzer Analyze = null)
        {
            double[] output;

            if (argArr != null)
            {
                int Num = argArr.Length;
                output = new double[Num];

                //    0     1     2     3     4     5     6     7     8    9
                // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };

                // i = 0
                args[argInd] = argArr[0];

                inS0 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);

                // i = 1
                args[argInd] = argArr[1];

                inS1 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                inV0 = (inS1 - inS0) / (argArr[1] - argArr[0]);
                outV = inV0;
                output[0] = outV;

                Analyze.SetMinMax(outV);
                Analyze.SetPoint(0, argArr[0], outV);

                int j = 1;

                for (int i = 2; i < Num; i++)
                {
                    args[argInd] = argArr[i];

                    //    0     1     2     3     4     5     6     7     8    9
                    // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
                    inS2 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                    outSignalV(argArr[i] - argArr[j], inS2, args[7], args[8], args[9]); // velocity in point i-1
                    output[j] = outV;

                    Analyze.SetMinMax(outV);
                    Analyze.SetPoint(j, argArr[j++], outV);
                }

                // Filling in the remaining elements of the output array
                output[j] = outV;
                Analyze.SetPoint(j, argArr[j], outV); // i
            }
            else if (!(double.IsNaN(limA) || double.IsNaN(limB) || double.IsNaN(step))) // using the given range limits
            {
                int stepNum = Analyze.SegmentLength;

                double currX = limA;
                double Xj = limA;

                output = new double[stepNum];
                stepNum--;

                //    0     1     2     3     4     5     6     7     8    9
                // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };

                // i = 0
                args[argInd] = currX;

                inS0 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);

                // i = 1
                currX += step;
                args[argInd] = currX;
                
                inS1 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                inV0 = (inS1 - inS0) / step;
                outV = inV0;
                output[0] = outV;

                Analyze.SetMinMax(outV);
                Analyze.SetPoint(0, limA, outV);

                int j = 1;

                Xj += step;
                currX += step;
                args[argInd] = currX;

                for (int i = 2; i < stepNum; i++)
                {
                    //    0     1     2     3     4     5     6     7     8    9
                    // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
                    inS2 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                    outSignalV(step, inS2, args[7], args[8], args[9]); // velocity in point i-1
                    output[j] = outV;

                    Analyze.SetMinMax(outV);
                    Analyze.SetPoint(j++, Xj, outV);

                    currX += step;
                    args[argInd] = currX;

                    Xj += step;
                }

                args[argInd] = limB;

                //    0     1     2     3     4     5     6     7     8    9
                // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
                inS2 = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]) * FadeIn(args[0], args[4]);
                outSignalV(limB - currX + step, inS2, args[7], args[8], args[9]);
                output[j] = outV;

                Analyze.SetMinMax(outV);
                Analyze.SetPoint(j++, Xj, outV);

                // Filling in the remaining elements of the output array
                output[j] = outV;
                Analyze.SetPoint(stepNum, limB, outV); // i
            }
            else
            {
                output = new double[1];
                //    0     1     2     3     4     5     6     7     8    9
                // { "t", "A0", "A1", "A2", "w0", "w1", "w2", "Af", "Wf", "n" };
                output[0] = inSignal(args[0], args[1], args[2], args[3], args[4], args[5], args[6]);
            }

            return output;
        }

        public InertialFilter_V() { }
    } // end of public class InertialFilter_Acc : ICalculate ///////////////////////////////////////////////////////////////////////////////
}
