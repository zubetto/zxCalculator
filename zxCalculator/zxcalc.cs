namespace zxCalculator
{
    public delegate double[] Function(double[] args, double[] argArr = null, int argInd = 0);
    /*// Method implementation example: 
    { 
        double[] output;

        if (argArr != null)
        {
            uint Num = argArr.GetLength(0);
            output = new double[Num];

            for (uint i = 0; i < Num; i++)
            {
                args[argInd] = argArr[i];
                output[i] = Math.Sin(args[1] + 2*args[2] + ... + 10*args[10] + ...);
            }
        }
        else
        {
            output = new double[1];
            output[0] = Math.Sin(args[1] + 2*args[2] + ... + 10*args[10] + ...);
        }

        return output;
    }
    //*/

    /// <summary>
    /// Intended for custom function calculations
    /// </summary>
    public interface ICalculate
    {
        int argsNum { get; }

        /// <summary>
        /// Only first ten chars will be used
        /// </summary>
        string Label { get; }
        string Description { get; }
        string[] ArgLabels { get; }

        /// <summary>
        /// Calculates custom-function values on given arguments array 
        /// </summary>
        /// <param name="args">Arguments array {x, y, z ... }</param>
        /// <param name="argsArr">Array of values of one of the function argument</param>
        /// <param name="argInd">Index of the argument in the args for which argArr is given and the function will be caclculated</param>
        /// <returns></returns>
        double[] Calculate(double[] args, double[] argArr = null, int argInd = 0);
    }
}
