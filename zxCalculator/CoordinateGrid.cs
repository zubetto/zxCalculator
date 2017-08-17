using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SWShapes = System.Windows.Shapes;
using System.Windows.Controls.Primitives;
using ColorTools;


namespace zxCalculator
{
    /// <summary>
    /// Wrapping class for the simple filtering method that
    /// maintains the number of points for output geometry within specified limit
    /// and also performs transition of those points from x,y to u,v space;
    /// x,y is used for the space in which calculations are performed
    /// u,v is used for the output canvas space
    /// </summary>
    public class PointsSampler : CoordinateGrid.IPointsFilter
    {
        // OUTPUT
        private PathGeometry outputGeometry;
        private Point[] outputPoints;
        private bool outputIsSetted = false;
        
        private Canvas outputCanvas; // The canvas and transform matrix are used to define current field of view
        private MatrixTransform MxTransformXYtoUV; // Contains transition matrix from the x,y space to the u,v space

        // INPUT
        private double[][] inputYArr;
        private AnalysisData[] inputInfo;
        private double[] XRange; // [0] - limA, [1] - limB, [2] - step
        private bool inputIsSetted = false;

        // Maximum number of points that could be outputted
        private int PointsMaxNum = 1170; // setted equal to the width of the maximum stretched outputCanvas
        public int PointsNumber
        {
            get { return PointsMaxNum; }
            set
            {
                if (value > 0 && outputGeometry != null)
                {
                    outputPoints = new Point[value];

                    PolyLineSegment plSeg = outputGeometry.Figures[0].Segments[0] as PolyLineSegment;
                    plSeg.Points = new PointCollection(outputPoints);
                }
            }
        }

        public PointsSampler(double[] range, int maxPoints = 1170)
        {
            if (maxPoints > 0) PointsMaxNum = maxPoints;

            outputPoints = new Point[PointsMaxNum];

            XRange = range;
        }
        
        public void SetOutput(Canvas canvas, Geometry outGeom)
        {
            outputCanvas = canvas;
            outputGeometry = outGeom as PathGeometry;
            MxTransformXYtoUV = outputGeometry.Transform as MatrixTransform; // Obtainig reference to the XYtoUV transition matrix
            outputGeometry.Transform = new MatrixTransform(Matrix.Identity); // Points will be added already transformed, therefore WPF transform is not used
            
            PolyLineSegment plSeg = new PolyLineSegment(outputPoints, true);
            PathFigure pFigure = new PathFigure(new Point(), new PolyLineSegment[1] { plSeg }, false);
            outputGeometry.Figures = new PathFigureCollection(new PathFigure[1] { pFigure });

            pFigure.IsFilled = false;

            outputIsSetted = true;
        }

        public void ResetInputFlag() { inputIsSetted = false; }

        public void SetInput(double[][] Yarr, AnalysisData[] info)
        {
            inputYArr = Yarr;
            inputInfo = info;

            inputIsSetted = true;
        }

        public Point[] Filter()
        {
            try
            {
                if (!(inputIsSetted && outputIsSetted)) return null; // >>>>>>> To prevent race condition >>>>>>>

                Matrix Mx = MxTransformXYtoUV.Matrix;

                // u = x * ratioXtoU + offsetU;     
                // v = y * ratioYtoV + offsetV; 
                // x = (u - offsetU) / ratioXtoU;
                // y = (v - offsetV) / ratioYtoV;
                double viewLimA = -Mx.OffsetX / Mx.M11;
                double viewLimB = (outputCanvas.ActualWidth - Mx.OffsetX) / Mx.M11;

                // [0] - limA, [1] - limB, [2] - step
                double XlimA = XRange[0];
                double XlimB = XRange[1];
                double Xstep = XRange[2];

                PolyLineSegment plSegment = outputGeometry.Figures[0].Segments[0] as PolyLineSegment;

                if (viewLimA >= XlimB || viewLimB <= XlimA) // Entire graph is out-of-sight
                {
                    plSegment.IsStroked = false;
                    return null; // >>>>> >>>>>
                }
                else // Searching for completed segments in the field of view
                {
                    plSegment.IsStroked = true;

                    int SegNum = inputYArr.Length;
                    int firstViewInd = -1;
                    int lastViewInd = -1;

                    // Searching for first completed segment in the field of view
                    for (int i = 0; i < SegNum; i++)
                    {
                        if (inputInfo[i].IsComplete && inputInfo[i].SegmentLimB > viewLimA)
                        {
                            firstViewInd = i;
                            break;
                        }
                    }

                    if (firstViewInd < 0) // Neither segment is completed
                    {
                        plSegment.IsStroked = false;
                        return null; // >>>>> >>>>>
                    }

                    // Searching for last completed segment in the field of view 
                    for (int i = SegNum - 1; i >= 0; i--)
                    {
                        if (inputInfo[i].IsComplete && inputInfo[i].SegmentLimA < viewLimB)
                        {
                            lastViewInd = i;
                            break;
                        }
                    }

                    if (lastViewInd < firstViewInd) // All completed segments are out-of-sight
                    {
                        plSegment.IsStroked = false;
                        return null; // >>>>> >>>>>
                    }

                    // Defining number of all points in the field of view
                    double dA = viewLimA - inputInfo[firstViewInd].SegmentLimA;
                    double dB = inputInfo[lastViewInd].SegmentLimB - viewLimB;

                    int indA = 0;
                    int indB = inputInfo[lastViewInd].SegmentLength - 1;

                    if (dA > Xstep) indA = (int)Math.Floor(dA / Xstep);
                    if (dB > Xstep) indB -= (int)Math.Floor(dB / Xstep);

                    // Reducing the number of output points by using index increment
                    int incr = indB - indA + 1; // Is equal to ratio of number of all points in the field of view and the PointsMaxNum

                    // Adding length of each completed segment located between the first and the last
                    if (firstViewInd < lastViewInd)
                    {
                        incr += inputInfo[firstViewInd].SegmentLength;

                        for (int i = firstViewInd + 1; i < lastViewInd; i++)
                        {
                            if (inputInfo[i].IsComplete) incr += inputInfo[i].SegmentLength;
                        }
                    }

                    //incr /= PointsMaxNum;
                    incr = (int)Math.Ceiling(1.0 * incr / PointsMaxNum);
                    if (incr == 0) incr = 1;

                    // Init variables for the segments iteration
                    int SegLen;
                    int ptInd = 0;
                    double Xspan = incr * Xstep;
                    double X = inputInfo[firstViewInd].SegmentLimA + indA * Xstep;
                    double[] Segment;
                    double ku = Mx.M11, kv = Mx.M22;
                    double du = Mx.OffsetX, dv = Mx.OffsetY;
                    double currValue = 0, preValue = 0;

                    // *** Outputting of the points ***
                    for (int i = firstViewInd; i < lastViewInd; i++) // segments loop
                    {
                        if (inputInfo[i].IsComplete)
                        {
                            Segment = inputYArr[i];
                            SegLen = Segment.Length;

                            while (indA < SegLen) // points loop
                            {
                                currValue = kv * Segment[indA] + dv;

                                if (double.IsNegativeInfinity(currValue)) currValue = double.MinValue;
                                else if (double.IsPositiveInfinity(currValue)) currValue = double.MaxValue;

                                if (double.IsNaN(currValue)) currValue = preValue;
                                else preValue = currValue;

                                outputPoints[ptInd].X = ku * X + du;
                                outputPoints[ptInd].Y = currValue;

                                if (++ptInd == PointsMaxNum)
                                {
                                    plSegment.Points = new PointCollection(outputPoints);
                                    outputGeometry.Figures[0].StartPoint = outputPoints[0];
                                    return outputPoints; // >>>>> COMPLETED >>>>>
                                }

                                indA += incr;
                                X += Xspan;
                            }

                            indA -= SegLen;
                        }
                        else
                        {
                            indA = 0;
                        }
                    } // end of segments loop

                    if (inputInfo[lastViewInd].IsComplete)
                    {
                        // The last segment loop with i = lastViewInd
                        Segment = inputYArr[lastViewInd];
                        SegLen = Segment.Length - 1; // last index

                        while (indA < indB) // points loop
                        {
                            if (ptInd == PointsMaxNum)
                            {
                                plSegment.Points = new PointCollection(outputPoints);
                                outputGeometry.Figures[0].StartPoint = outputPoints[0];
                                return outputPoints; // >>>>> COMPLETED >>>>>
                            }

                            currValue = kv * Segment[indA] + dv;

                            if (double.IsNegativeInfinity(currValue)) currValue = double.MinValue;
                            else if (double.IsPositiveInfinity(currValue)) currValue = double.MaxValue;

                            if (double.IsNaN(currValue)) currValue = preValue;
                            else preValue = currValue;

                            outputPoints[ptInd].X = ku * X + du;
                            outputPoints[ptInd].Y = currValue;
                            
                            ptInd++;
                            indA += incr;
                            X += Xspan;
                        }

                        if (ptInd == PointsMaxNum)
                        {
                            plSegment.Points = new PointCollection(outputPoints);
                            outputGeometry.Figures[0].StartPoint = outputPoints[0];
                            return outputPoints; // >>>>> COMPLETED >>>>>
                        }

                        if (indA > SegLen)
                        {
                            currValue = kv * Segment[SegLen] + dv;

                            if (double.IsNegativeInfinity(currValue)) currValue = double.MinValue;
                            else if (double.IsPositiveInfinity(currValue)) currValue = double.MaxValue;

                            if (double.IsNaN(currValue)) currValue = preValue;
                            else preValue = currValue;

                            outputPoints[ptInd].X = ku * XlimB + du;
                            outputPoints[ptInd].Y = currValue;
                        }
                        else // indB < SegLen
                        {
                            currValue = kv * Segment[indA] + dv;

                            if (double.IsNegativeInfinity(currValue)) currValue = double.MinValue;
                            else if (double.IsPositiveInfinity(currValue)) currValue = double.MaxValue;

                            if (double.IsNaN(currValue)) currValue = preValue;
                            else preValue = currValue;

                            outputPoints[ptInd].X = ku * X + du;
                            outputPoints[ptInd].Y = currValue;
                        }
                    }

                    // Filling out remaining points
                    X = outputPoints[ptInd].X;

                    while (++ptInd < PointsMaxNum)
                    {
                        outputPoints[ptInd].X = X;
                        outputPoints[ptInd].Y = currValue;
                    }

                    plSegment.Points = new PointCollection(outputPoints);
                    outputGeometry.Figures[0].StartPoint = outputPoints[0];
                }

                return outputPoints;
            }
            catch (Exception e)
            {
                string err = e.ToString();
                return null;
            }
        }

        /// <summary>
        /// Multithreading version
        /// </summary>
        /// <param name="state"></param>
        public void Filter(object state) // #### CHANGE IS NEEDED ####
        {
            try
            {
                if (!(inputIsSetted && outputIsSetted)) return; // >>>>>>> To prevent race condition >>>>>>>

                Matrix Mx = MxTransformXYtoUV.Matrix;

                // u = x * ratioXtoU + offsetU;     
                // v = y * ratioYtoV + offsetV; 
                // x = (u - offsetU) / ratioXtoU;
                // y = (v - offsetV) / ratioYtoV;
                double viewLimA = -Mx.OffsetX / Mx.M11;
                double viewLimB = (outputCanvas.ActualWidth - Mx.OffsetX) / Mx.M11;

                // [0] - limA, [1] - limB, [2] - step
                double XlimA = XRange[0];
                double XlimB = XRange[1];
                double Xstep = XRange[2];

                PolyLineSegment plSegment = outputGeometry.Figures[0].Segments[0] as PolyLineSegment;

                if (viewLimA >= XlimB || viewLimB <= XlimA) // Entire graph is out-of-sight
                {
                    plSegment.IsStroked = false;
                    return; // >>>>> >>>>>
                }
                else // Searching for completed segments in the field of view
                {
                    plSegment.IsStroked = true;

                    int SegNum = inputYArr.Length;
                    int firstViewInd = -1;
                    int lastViewInd = -1;

                    // Searching for first completed segment in the field of view
                    for (int i = 0; i < SegNum; i++)
                    {
                        if (inputInfo[i].IsComplete && inputInfo[i].SegmentLimB > viewLimA)
                        {
                            firstViewInd = i;
                            break;
                        }
                    }

                    if (firstViewInd < 0) // Neither segment is completed
                    {
                        plSegment.IsStroked = false;
                        return; // >>>>> >>>>>
                    }

                    // Searching for last completed segment in the field of view 
                    for (int i = SegNum - 1; i >= 0; i--)
                    {
                        if (inputInfo[i].IsComplete && inputInfo[i].SegmentLimA < viewLimB)
                        {
                            lastViewInd = i;
                            break;
                        }
                    }

                    if (lastViewInd < firstViewInd) // All completed segments are out-of-sight
                    {
                        plSegment.IsStroked = false;
                        return; // >>>>> >>>>>
                    }

                    // Defining number of all points in the field of view
                    double dA = viewLimA - inputInfo[firstViewInd].SegmentLimA;
                    double dB = inputInfo[lastViewInd].SegmentLimB - viewLimB;

                    int indA = 0;
                    int indB = inputInfo[lastViewInd].SegmentLength - 1;

                    if (dA > Xstep) indA = (int)Math.Floor(dA / Xstep);
                    if (dB > Xstep) indB -= (int)Math.Floor(dB / Xstep);

                    // Reducing the number of output points by using index increment
                    int incr = indB - indA + 1; // Is equal to ratio of number of all points in the field of view and the PointsMaxNum

                    // Adding length of each completed segment located between the first and the last
                    if (firstViewInd < lastViewInd)
                    {
                        incr += inputInfo[firstViewInd].SegmentLength;

                        for (int i = firstViewInd + 1; i < lastViewInd; i++)
                        {
                            if (inputInfo[i].IsComplete) incr += inputInfo[i].SegmentLength;
                        }
                    }

                    //incr /= PointsMaxNum;
                    incr = (int)Math.Ceiling(1.0 * incr / PointsMaxNum);
                    if (incr == 0) incr = 1;

                    // Init variables for the segments iteration
                    int SegLen;
                    int ptInd = 0;
                    double Xspan = incr * Xstep;
                    double X = inputInfo[firstViewInd].SegmentLimA + indA * Xstep;
                    double[] Segment;
                    double ku = Mx.M11, kv = Mx.M22;
                    double du = Mx.OffsetX, dv = Mx.OffsetY;

                    // *** Outputting of the points ***
                    for (int i = firstViewInd; i <= lastViewInd; i++) // segments loop
                    {
                        if (inputInfo[i].IsComplete)
                        {
                            Segment = inputYArr[i];
                            SegLen = Segment.Length;

                            while (indA < SegLen) // points loop
                            {
                                outputPoints[ptInd].X = ku * X + du;
                                outputPoints[ptInd].Y = kv * Segment[indA] + dv;

                                if (++ptInd == PointsMaxNum)
                                {
                                    plSegment.Points = new PointCollection(outputPoints);
                                    outputGeometry.Figures[0].StartPoint = outputPoints[0];
                                    return; // >>>>> COMPLETED >>>>>
                                }

                                indA += incr;
                                X += Xspan;
                            }

                            indA -= SegLen;
                        }
                        else
                        {
                            indA = 0;
                        }
                    } // end of segments loop

                    // Finalize
                    Xspan = outputPoints[ptInd - 1].Y; // just fill out remainig points
                    X = ku * X + du;

                    while (ptInd < PointsMaxNum)
                    {
                        outputPoints[ptInd].X = X;
                        outputPoints[ptInd].Y = Xspan;
                        ptInd++;
                    }

                    plSegment.Points = new PointCollection(outputPoints);
                    outputGeometry.Figures[0].StartPoint = outputPoints[0];
                }

                return;
            }
            catch (Exception e)
            {
                string es = e.ToString();
                return;
            }


        }
    } // end of public class PointsSampler ///////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// (x,y) is used for the space in which calculations are performed, i.e. grid coordinates
    /// (u,v) is used for the WPF output canvas space, i.e. canvas coordinates
    /// </summary>
    public class CoordinateGrid
    {
        private Canvas outputCanvas;

        public delegate Point[] Filtering();

        public interface IPointsFilter
        {
            void SetOutput(Canvas outCanvas, Geometry outGeom);
            Point[] Filter();
            void Filter(object state); // For multi-thread processing
        }

        private double[] MarkersYvalues;
        private Visibility[] MarkersVisibility;

        /// <summary>
        /// u = x * XYtoUV.M11 + XYtoUV.OffsetX;     
        /// v = y * XYtoUV.M22 + XYtoUV.OffsetY;
        /// </summary>
        public class MouseMoveEventArgs : EventArgs
        {
            public readonly Matrix XYtoUV;
            public readonly Point MousePointUV;

            public readonly double[] FunctionsYvalues;
            public readonly Visibility[] MarkersVisibility;

            public bool ShowUline = true;
            public bool ShowVline = true;

            public Point MarkerPointXY = new Point(double.NegativeInfinity, double.NegativeInfinity);

            public MouseMoveEventArgs(Matrix transitionMatrix, Point mousePoint, double[] Yarr, Visibility[] Vis)
            {
                XYtoUV = transitionMatrix;
                MousePointUV = mousePoint;
                FunctionsYvalues = Yarr;
                MarkersVisibility = Vis;
            }
        }

        /// <summary>
        /// Occurs when mouse is over the output canvas
        /// Can be used to set marker position
        /// u = x * XYtoUV.M11 + XYtoUV.OffsetX;     
        /// v = y * XYtoUV.M22 + XYtoUV.OffsetY;
        /// </summary>
        public event EventHandler<MouseMoveEventArgs> MouseMove;
        
        public enum GraphEditorSettings { Color, Dashes, Thickness, IsActive }

        public class GraphEditorEventArgs : EventArgs
        {
            public readonly int EditedIndex;
            public readonly GraphEditorSettings ChangedValue;

            public readonly Color SettedColor;
            public readonly DoubleCollection SettedDashArray;
            public readonly double SettedThickness;
            public readonly bool IsActive;

            public GraphEditorEventArgs(int index, GraphEditorSettings setting, Color color, DoubleCollection dashes, double thick, bool active)
            {
                EditedIndex = index;
                ChangedValue = setting;

                SettedColor = color;
                SettedDashArray = dashes;
                SettedThickness = thick;
                IsActive = active;
            }
        }

        /// <summary>
        /// Occurs whenever any setting is changed through the Graph Editor window
        /// </summary>
        public event EventHandler<GraphEditorEventArgs> GraphSettingsChanged;

        private EditStrokeWin GraphEditorWin;
        private List<DoubleCollection> DashesList;
        public List<DoubleCollection> DashArrayList { get { return DashesList; } }

        public double MaxWidth = SystemParameters.PrimaryScreenWidth;
        public double MaxHeight = SystemParameters.PrimaryScreenHeight;
        public bool SquareGrid = false;

        private double leftIndent = 54; // for ordinate labels
        private double bottomIndent = 40; // for abscissa labels
        private double topIndent = 10;
        private double rightIndent = 10;

        private double XStep = 10;
        private double YStep = 10;
        private double UStep = 30;
        private double VStep = 30;
        private double UStepScaled = 30;
        private double VStepScaled = 30;

        // SquareGrid will not work properly if corresponding U and V min/max values are not the same
        private double UStepMin = 40;
        private double UStepMax = 120;
        private double VStepMin = 40;
        private double VStepMax = 120;

        public enum FitinModes { WH, W, H, Off, Update }
        public FitinModes AutoFit = FitinModes.Off;
        
        // when the shift goes beyond [-MaxShift, MaxShift] corresponding outside grid-line is transfered to the opposite side
        private double UShift = 0, VShift = 0;
        private double ULeft, URight, VTop, VBottom;
        private int indLeft, indRight, indTop, indBottom;

        // while StepScaled lies within [StepMin, StepMax] the grid layout is the same and is scaled by means of WPF transform
        private int zoomDetents = 16;
        private double zoomBase = 2; // UStepMax/UStepMin
        private double zoomFactor = Math.Pow(2, 1.0 / 8); // Math.Pow( UzoomBase, 1.0/UzoomDetents ) > 1 REQUIRED CONDITION!;

        private double XgridBase = 1;
        private double YgridBase = 1;

        // when zoomCurrent becomes less than 0, then step in x,y space is multiplied by stepFactors[factorInd++] - Zomm Out
        // when zoomCurrent becomes more than zoomDetents, then step in x,y space is devided by stepFactors[factorInd--] - Zoom In
        // and then a new grid layout is produced based on that step
        private double[] XstepFactors = new double[3] { 2, 2.5, 2 };
        private double[] YstepFactors = new double[3] { 2, 2.5, 2 };
        private int XfactorInd = 0;
        private int YfactorInd = 0;
        private bool grewXStep = false;
        private bool grewYStep = false;

        // following variables are used for units labels content
        private double GaugeX;
        private double GaugeY;
        private double labelLeft, labelRight, labelTop, labelBottom;

        // Graphs and filters
        private SWShapes.Path[] GraphPaths;
        public System.Collections.IEnumerable FunctionGraphs { get { return GraphPaths; } }
        private Filtering[] OutFilters;

        // Grid frame, lines, labels
        private SWShapes.Path UgridLines = new SWShapes.Path();
        private SWShapes.Path VgridLines = new SWShapes.Path();
        private PathGeometry UgridLinesGeometry;
        private PathGeometry VgridLinesGeometry;
        public SolidColorBrush GridBrush = new SolidColorBrush(Color.FromArgb(100, 150, 150, 150));

        private SWShapes.Path GridMat = new SWShapes.Path();
        private CombinedGeometry GridMatFrame;
        public SolidColorBrush GridMatBrush = new SolidColorBrush(Color.FromArgb(170, 50, 50, 50));
        public SolidColorBrush GridBorderBrush = new SolidColorBrush(Color.FromArgb(255, 150, 150, 150));

        private SWShapes.Path MarkerUline = new SWShapes.Path();
        private SWShapes.Path MarkerVline = new SWShapes.Path();
        private SWShapes.Ellipse[] Markers;
        public SolidColorBrush MarkerLinesBrush = new SolidColorBrush(Color.FromArgb(170, 170, 170, 170));

        private Canvas UlabelsArea = new Canvas();
        private Canvas VlabelsArea = new Canvas();
        private UniformGrid UlabelsCells = new UniformGrid();
        private UniformGrid VlabelsCells = new UniformGrid();
        private Label[] UlabelsArr;
        private Label[] VlabelsArr;
        private Label unitsUlabel = new Label();
        private Label unitsVlabel = new Label();
        private double UlabelsOffsetX = 0;
        private double VlabelsOffsetY = 0;
        public FontFamily LabelsFont = new FontFamily("Cambria Math");
        public FontFamily UnitsFont = new FontFamily("Cambria Math");
        private double LabelsFontSize = 12;
        private double UnitsFontSize = 14;
        public SolidColorBrush GridLabelsBrush = new SolidColorBrush(Color.FromArgb(255, 233, 233, 233));

        private string UlabelsFormatSpec = "";
        private string VlabelsFormatSpec = "";

        private string UnitsNameU = "";
        private string UnitsNameV = "";
        private bool UsePrefixU = false;
        private bool UsePrefixV = false;

        public string XunitsName
        {
            get { return UnitsNameU; }

            set
            {
                UnitsNameU = value;

                if (String.IsNullOrWhiteSpace(value)) UsePrefixU = false;
                else UsePrefixU = true;
            }
        }

        public string YunitsName
        {
            get { return UnitsNameV; }

            set
            {
                UnitsNameV = value;

                if (String.IsNullOrWhiteSpace(value)) UsePrefixV = false;
                else UsePrefixV = true;
            }
        }

        // overall bounds 
        private double BoundsXmin = 0;
        private double BoundsXmax = 10;
        private double BoundsYmin = 0;
        private double BoundsYmax = 10;
        private Rect[] BoundsArr;
        private int BoundsAdded = 0;

        // u = x * ratioXtoU + offsetU;     
        // v = y * ratioYtoV + offsetV;     
        // x = (u - leftIndent)/ratioXtoU + Xmin;
        // y = v/ratioYtoV + Ymax - topIndent/ratioYtoV;
        private double ratioXtoU = 1;   //  (CanvasWidth - leftIndent - rightIndent)/(Xmax - Xmin)
        private double ratioYtoV = -1;  // -(CanvasHeight - bottomIndent - topIndent)/(Ymax - Ymin)
        private double OffsetU = 0;     // -Xmin * ratioXtoU + leftIndent
        private double OffsetV = 0;     // (CanvasHeight - bottomIndent) - Ymin * ratioYtoV
        
        private Matrix GraphMatrix = new Matrix(1, 0, 0, -1, 0, 0);
        private Matrix UgridMatrix = new Matrix(1, 0, 0, 1, 0, 0);
        private Matrix VgridMatrix = new Matrix(1, 0, 0, 1, 0, 0);
        private Matrix OuterRectMatrix = new Matrix(1, 0, 0, 1, 0, 0); // frame mat
        private Matrix InnerRectMatrix = new Matrix(1, 0, 0, 1, 0, 0); // frame mat
        //private Matrix UlabelsMatrix = new Matrix(1, 0, 0, 1, 0, 0); // not used
        //private Matrix VlabelsMatrix = new Matrix(1, 0, 0, 1, 0, 0); // not used

        private MatrixTransform GraphMxTr = new MatrixTransform();
        private MatrixTransform UgridMxTr = new MatrixTransform();
        private MatrixTransform VgridMxTr = new MatrixTransform();
        private MatrixTransform OuterRectMxTr = new MatrixTransform();
        private MatrixTransform InnerRectMxTr = new MatrixTransform();
        private TranslateTransform UlabelsTT = new TranslateTransform();
        private TranslateTransform VlabelsTT = new TranslateTransform();
        private TranslateTransform MarkerUlineTT = new TranslateTransform();
        private TranslateTransform MarkerVlineTT = new TranslateTransform();
        
        private Point MouseIniPoint;

        private SWShapes.Path RectZoomPath;
        private RectangleGeometry RectZoomGeometry;
        public SolidColorBrush RectZoomBrush = new SolidColorBrush(Color.FromRgb(33, 150, 255));

        public CoordinateGrid(Canvas canvas, int slots, Thickness padding = new Thickness())
        {
            outputCanvas = canvas;
            outputCanvas.Cursor = Cursors.Cross;
            
            // --- graphs num ----------------------------------
            GraphPaths = new SWShapes.Path[slots];
            OutFilters = new Filtering[slots];
            BoundsArr = new Rect[slots];

            for (int i = 0; i < slots; i++) BoundsArr[i] = Rect.Empty;

            // --- U,V grid lines ------------------------------
            UgridLines.Stroke = GridBrush;
            UgridLines.StrokeThickness = 0.5;
            VgridLines.Stroke = GridBrush;
            VgridLines.StrokeThickness = 0.5;
            
            Canvas.SetZIndex(UgridLines, -100);
            Canvas.SetZIndex(VgridLines, -100);

            outputCanvas.Children.Add(UgridLines);
            outputCanvas.Children.Add(VgridLines);

            // --- frame mat -----------------------------------
            double actW = canvas.ActualWidth;
            double actH = canvas.ActualHeight;

            RectangleGeometry outerRect = new RectangleGeometry(new Rect(new Point(-2, -2), new Point(actW + 2, actH + 2)));
            RectangleGeometry innerRect = new RectangleGeometry(new Rect(new Point(leftIndent, topIndent),
                                                                         new Point(actW - rightIndent, actH - bottomIndent)));

            OuterRectMatrix = ((MatrixTransform)outerRect.Transform).Matrix;
            InnerRectMatrix = ((MatrixTransform)innerRect.Transform).Matrix;

            GridMatFrame = new CombinedGeometry(GeometryCombineMode.Exclude, outerRect, innerRect);
            GridMat.Data = GridMatFrame;
            GridMat.Fill = GridMatBrush;
            GridMat.Stroke = GridBorderBrush;
            Canvas.SetZIndex(GridMat, 10);
            outputCanvas.Children.Add(GridMat);

            OuterRectMxTr.Matrix = OuterRectMatrix;
            InnerRectMxTr.Matrix = InnerRectMatrix;
            GridMatFrame.Geometry1.Transform = OuterRectMxTr;
            GridMatFrame.Geometry2.Transform = InnerRectMxTr;

            // --- grid labels and units -----------------------
            // U,V Areas
            UlabelsArea.Width = canvas.ActualWidth - leftIndent - rightIndent + LabelsFontSize;
            UlabelsArea.Height = bottomIndent;

            VlabelsArea.Height = canvas.ActualHeight - topIndent - bottomIndent + LabelsFontSize;
            VlabelsArea.Width = leftIndent;

            UlabelsArea.ClipToBounds = true;
            VlabelsArea.ClipToBounds = true;

            UlabelsOffsetX = leftIndent - 0.5 * LabelsFontSize;
            Canvas.SetLeft(UlabelsArea, UlabelsOffsetX);
            Canvas.SetBottom(UlabelsArea, 0);
            Canvas.SetZIndex(UlabelsArea, 20);

            VlabelsOffsetY = topIndent - 0.5 * LabelsFontSize;
            Canvas.SetTop(VlabelsArea, VlabelsOffsetY);
            Canvas.SetLeft(VlabelsArea, 0);
            Canvas.SetZIndex(VlabelsArea, 20);

            outputCanvas.Children.Add(UlabelsArea);
            outputCanvas.Children.Add(VlabelsArea);

            // U,V cells
            UlabelsArea.Children.Add(UlabelsCells);
            UlabelsArea.Children.Add(unitsUlabel);

            VlabelsArea.Children.Add(VlabelsCells);
            VlabelsArea.Children.Add(unitsVlabel);

            UlabelsCells.Height = LabelsFontSize * 1.5;
            UlabelsCells.Rows = 1;
            Canvas.SetTop(UlabelsCells, 0);
            UlabelsCells.RenderTransform = UlabelsTT;

            VlabelsCells.Width = LabelsFontSize * 3.66;
            VlabelsCells.Columns = 1;
            Canvas.SetRight(VlabelsCells, 0);
            VlabelsCells.RenderTransform = VlabelsTT;

            // U,V units labels
            unitsUlabel.Height = UnitsFontSize * 2;
            unitsUlabel.Width = UlabelsArea.Width;
            unitsUlabel.Padding = new Thickness(2);
            unitsUlabel.HorizontalContentAlignment = HorizontalAlignment.Center;
            unitsUlabel.VerticalContentAlignment = VerticalAlignment.Top;
            unitsUlabel.FontSize = UnitsFontSize;
            unitsUlabel.Foreground = GridLabelsBrush;
            Canvas.SetTop(unitsUlabel, UlabelsCells.Height);
            Canvas.SetLeft(unitsUlabel, 0);

            unitsVlabel.Height = UnitsFontSize * 2;
            unitsVlabel.Width = VlabelsArea.Height;
            unitsVlabel.Padding = new Thickness(2);
            unitsVlabel.HorizontalContentAlignment = HorizontalAlignment.Center;
            unitsVlabel.VerticalContentAlignment = VerticalAlignment.Top;
            unitsVlabel.FontSize = UnitsFontSize;
            unitsVlabel.Foreground = GridLabelsBrush;
            unitsVlabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetRight(unitsVlabel, VlabelsCells.Width - 0.5 * unitsVlabel.DesiredSize.Width);
            Canvas.SetTop(unitsVlabel, 0.5 * (VlabelsArea.Height - unitsVlabel.Height));
            unitsVlabel.RenderTransform = new RotateTransform(-90, 0.5 * unitsVlabel.DesiredSize.Width, 0.5 * unitsVlabel.Height);
            unitsVlabel.Content = "Contents";

            // --- marker and marker lines ---------------------
            Markers = new SWShapes.Ellipse[slots];
            MarkersYvalues = new double[slots];
            MarkersVisibility = new Visibility[slots];

            for (int i = 0; i < slots; i++) MarkersVisibility[i] = Visibility.Collapsed;

            MarkerUline.Data = new PathGeometry(new PathFigure[1] 
                                              { new PathFigure(new Point(0, -MaxHeight), new PathSegment[1] 
                                              { new LineSegment(new Point(0, MaxHeight), true)}, false) });

            MarkerVline.Data = new PathGeometry(new PathFigure[1]
                                              { new PathFigure(new Point(-MaxWidth, 0), new PathSegment[1]
                                              { new LineSegment(new Point(MaxWidth, 0), true)}, false) });

            MarkerUline.Stroke = MarkerLinesBrush;
            MarkerVline.Stroke = MarkerLinesBrush;
            MarkerUline.StrokeThickness = 1;
            MarkerVline.StrokeThickness = 1;
            MarkerUline.Visibility = Visibility.Collapsed;
            MarkerVline.Visibility = Visibility.Collapsed;
            MarkerUline.Data.Transform = MarkerUlineTT;
            MarkerVline.Data.Transform = MarkerVlineTT;
            Canvas.SetZIndex(MarkerUline, -10);
            Canvas.SetZIndex(MarkerVline, -10);
            Canvas.SetLeft(MarkerUline, 0);
            Canvas.SetLeft(MarkerVline, 0);
            Canvas.SetTop(MarkerUline, 0);
            Canvas.SetTop(MarkerVline, 0);
            
            outputCanvas.Children.Add(MarkerUline);
            outputCanvas.Children.Add(MarkerVline);

            // --- Zooming rectangle ---------------------------
            RectZoomPath = new SWShapes.Path();
            RectZoomGeometry = new RectangleGeometry();
            RectZoomPath.Data = RectZoomGeometry;
            Color fillColor = (RectZoomBrush as SolidColorBrush).Color;
            fillColor.A = 33;
            RectZoomPath.Stroke = RectZoomBrush;
            RectZoomPath.Fill = new SolidColorBrush(fillColor);
            RectZoomPath.Visibility = Visibility.Collapsed;

            outputCanvas.Children.Add(RectZoomPath);

            // --- populate the dashes list --------------------
            DashesList = new List<DoubleCollection>(7);

            DashesList.Add(new DoubleCollection(new double[2] { 1, 0 }));
            DashesList.Add(new DoubleCollection(new double[2] { 1, 1 }));
            DashesList.Add(new DoubleCollection(new double[2] { 2, 1 }));
            DashesList.Add(new DoubleCollection(new double[2] { 4, 2 }));
            DashesList.Add(new DoubleCollection(new double[4] { 3, 1, 1, 1 }));
            DashesList.Add(new DoubleCollection(new double[4] { 4, 2, 1, 2 }));
            DashesList.Add(new DoubleCollection(new double[6] { 4, 1, 1, 1, 1, 1 }));

            // --- subscribe on events -------------------------
            // Such a way of subscribing is used due to unexpected behaviour of the mouse event handlers;
            // Literally, the MLBholdMove() should handles the MouseMove event after call of the MLBdown()
            // and until call of the MLBup(). But at very first press of the MLB the MLBholdMove() is called only once,
            // no matter how long the MLB remains pressed while mouse move. The next sequences of these events
            // are handled in right manner. Subscribing in that way prevents the first-click issue.
            outputCanvas.MouseEnter += SetHandlers;

            // --- ini fiealds ---------------------------------
            zoomBase = UStepMax / UStepMin;
            zoomFactor = Math.Pow(zoomBase, 1.0 / zoomDetents);
        }

        private void SetHandlers(object sender, RoutedEventArgs e)
        {
            outputCanvas.SizeChanged += CanvasSizeChanged;
            outputCanvas.MouseWheel += MouseWheel;
            outputCanvas.MouseLeftButtonDown += MLBdown;
            outputCanvas.MouseMove += ON_MouseOver;
            outputCanvas.MouseLeave += MouseLeave_Canvas;

            outputCanvas.MouseEnter -= SetHandlers;
        }

        private void AddMarker(int index)
        {
            if (GraphPaths[index] != null)
            {
                SWShapes.Ellipse newMarker = new SWShapes.Ellipse();
                Markers[index] = newMarker;

                newMarker.Fill = GraphPaths[index].Stroke;
                newMarker.Width = 2 * GraphPaths[index].StrokeThickness;
                newMarker.Height = 2 * GraphPaths[index].StrokeThickness;
                newMarker.Visibility = Visibility.Collapsed;

                outputCanvas.Children.Add(newMarker);
            }
        }

        private void RemoveMarker(int index)
        {
            if (Markers[index] != null)
            {
                outputCanvas.Children.Remove(Markers[index]);
            }
        }

        public void AddFilter(int index, IPointsFilter filterData)
        {
            if (GraphPaths[index] != null)
            {
                OutFilters[index] = filterData.Filter;
                filterData.SetOutput(outputCanvas, GraphPaths[index].Data);
            }
        }

        //public void RemoveFilter(int index)
        //{
        //    OutFilters[index] = null;
        //    RemoveGraph(index);
        //}
        
        public void AddGraph(int index, IPointsFilter outFilter)
        {
            GraphPaths[index] = new SWShapes.Path();
            GraphPaths[index].Data = new PathGeometry();
            GraphPaths[index].Data.Transform = GraphMxTr;
            SetGraph(index); // brushes, thickness, stroke
            AddMarker(index);

            outputCanvas.Children.Add(GraphPaths[index]);

            AddFilter(index, outFilter);
        }

        public void AddGraph(int index, int segmentsNum, Rect Bounds, Point[][] SegmentsPoints = null)
        {
            if (BoundsAdded == GraphPaths.Length) return; // >>>>> GraphPaths is already full >>>>>
            
            SWShapes.Path newGraph;
            PathGeometry pGeom;
            PathFigure pFigure;
            PathSegment[] Segments;

            if (GraphPaths[index] == null) // then add a new
            {
                newGraph = new SWShapes.Path();
                GraphPaths[index] = newGraph;
                SetGraph(index); // brushes, thickness, stroke
                AddMarker(index);

                pGeom = new PathGeometry();
                pFigure = new PathFigure();

                newGraph.Data = pGeom;
                pGeom.Transform = GraphMxTr; // transform matrix
                pFigure.IsFilled = false;
                pGeom.Figures = new PathFigureCollection(1);
                pGeom.Figures.Add(pFigure);

                outputCanvas.Children.Add(newGraph);
            }
            else // Update graph
            {
                newGraph = GraphPaths[index];
                pGeom = newGraph.Data as PathGeometry;
                pFigure = pGeom.Figures[0];

                RemoveBounds(index);
            }

            if (SegmentsPoints == null) // --- just initialize Path Data ---------------------------------
            {
                Segments = new PathSegment[segmentsNum];

                for (int i = 0; i < segmentsNum; i++)
                {
                    Segments[i] = new LineSegment(new Point(0, 0), false);
                }

                pFigure.StartPoint = new Point(0, 0);
                pFigure.Segments = new PathSegmentCollection(Segments);
            }
            else // --- plot a function graph by given points --------------------------------------------
            {
                segmentsNum = SegmentsPoints.Length;
                Segments = new PathSegment[segmentsNum];

                for (int i = 0; i < segmentsNum; i++)
                {
                    Segments[i] = new PolyLineSegment(SegmentsPoints[i], true);
                }

                pFigure.StartPoint = SegmentsPoints[0][0];
                pFigure.Segments = new PathSegmentCollection(Segments);

                if (Bounds.IsEmpty) Bounds = DefineBounds(SegmentsPoints);

                AddBounds(index, Bounds);

                FitIn(AutoFit);
            } // end if (SegmentsPoints == null)
        }

        public void AddSegment(int indGraph, int indSegment, Rect Bounds, Point[] pointsArr)
        {
            if (GraphPaths[indGraph] == null || OutFilters[indGraph] != null) return; // >>>>> null or maintained by a filter >>>>>

            PathGeometry pGeom = GraphPaths[indGraph].Data as PathGeometry;
            PathSegmentCollection pSegs = pGeom.Figures[0].Segments;
            LineSegment LSeg = pSegs[indSegment] as LineSegment;

            if (LSeg == null) return; // >>>>> GraphPaths[indGraph] was initialized by points array rather than segments number >>>>>

            pSegs[indSegment] = new PolyLineSegment(pointsArr, true);

            // adjusting previous invisible segment
            if (indSegment == 0)
            {
                pGeom.Figures[0].StartPoint = pointsArr[0];
            }
            else if (!pSegs[--indSegment].IsStroked)
            {
                LSeg = pSegs[indSegment] as LineSegment; // the AddGraph method initializes path with invisible LineSegments
                LSeg.Point = pointsArr[0];
            }

            if (Bounds.IsEmpty) Bounds = DefineBounds(new Point[1][] { pointsArr });
            
            AddBounds(indGraph, Bounds);

            FitIn(AutoFit);
        }

        public void AddSegment(int indGraph, Rect Bounds, bool first = false)
        {
            if (GraphPaths[indGraph] == null || OutFilters[indGraph] == null) return; // >>>>> null or is not maintained by a filter >>>>>

            if (first) RemoveBounds(indGraph);

            if (!Bounds.IsEmpty) AddBounds(indGraph, Bounds);

            if (!FitIn(AutoFit)) OutFilters[indGraph](); // if FitIn returns true then filters are called within FitIn method
        }

        public Color[] DefaultColors = new Color[7] { Colors.BlanchedAlmond, Colors.LightSteelBlue, Colors.LightPink, Colors.SteelBlue, Colors.LightCoral,
                                                      Colors.MediumAquamarine, Colors.PaleGoldenrod};
        private int defColorInd = 0;

        public Color CurrentColor { get { return DefaultColors[defColorInd]; } }

        public void SetGraph(int index) // set stroke, brush
        {
            SolidColorBrush graphBrush = new SolidColorBrush();
            graphBrush.Color = DefaultColors[defColorInd];

            if (++defColorInd >= DefaultColors.Length) defColorInd = 0;

            GraphPaths[index].Stroke = graphBrush;
            GraphPaths[index].StrokeThickness = 2;
        }

        public void RemoveGraph(int index)
        {
            outputCanvas.Children.Remove(GraphPaths[index]);
            GraphPaths[index] = null;
            OutFilters[index] = null;
            RemoveBounds(index);
            RemoveMarker(index);

            if (BoundsAdded > 0) FitIn(AutoFit);
        }
        
        private int EditedIndex;

        public void SwitchGraphEditor(bool switchON)
        {
            if (GraphEditorWin != null && GraphEditorWin.IsVisible) GraphEditorWin.RootGrid.IsEnabled = switchON;
        }

        private bool iniFlag = false;
        public void IniGraphEditor(int index, Color iniColor, DoubleCollection iniDashes, double iniThickness, EventHandler<GraphEditorEventArgs> settingsHandler = null)
        {
            EditedIndex = index;

            if (GraphEditorWin == null || !GraphEditorWin.IsVisible)
            {
                GraphEditorWin = new EditStrokeWin();
                GraphEditorWin.Show();

                GraphEditorWin.combobxDashes.ItemsSource = DashesList;

                GraphEditorWin.ColorControl.ColorChanged += SetGraphColor;
                GraphEditorWin.combobxDashes.SelectionChanged += SetGraphDashes;

                GraphEditorWin.txtBoxThickness.LostKeyboardFocus += SetGraphThickness_lostKeyFocus;
                GraphEditorWin.txtBoxThickness.KeyDown += SetGraphThickness_keyEnter;
                GraphEditorWin.txtBoxThickness.AddHandler(Button.ClickEvent, new RoutedEventHandler(SetGraphThickness_spinners));

                GraphEditorWin.chbxIsActive.Checked += SetGraphVisibility;
                GraphEditorWin.chbxIsActive.Unchecked += SetGraphVisibility;

                GraphSettingsChanged += settingsHandler;
            }
            else GraphEditorWin.Activate();

            iniFlag = true;

            GraphEditorWin.ColorControl.InitialColorBrush.Color = iniColor;
            GraphEditorWin.ColorControl.SelectedColorBrush.Color = iniColor;

            if (iniDashes == null || iniDashes.Count == 0) GraphEditorWin.combobxDashes.SelectedIndex = -1;
            else GraphEditorWin.combobxDashes.SelectedItem = iniDashes;

            GraphEditorWin.txtBoxThickness.Text = iniThickness.ToString("g2");
            GraphEditorWin.chbxIsActive.IsChecked = (iniColor.A != 0);

            iniFlag = false;
        }
        
        private bool isDrivenByColor = false;
        private void SetGraphColor(object sender, ColorControlPanel.ColorChangedEventArgs e)
        {
            if (iniFlag) return; // >>>>>>>>> >>>>>>>>>

            if (GraphPaths[EditedIndex] != null) (GraphPaths[EditedIndex].Stroke as SolidColorBrush).Color = e.CurrentColor;

            GraphEditorEventArgs GEe = new GraphEditorEventArgs(EditedIndex, GraphEditorSettings.Color, e.CurrentColor, GraphEditorWin.SelectedDashes,
                                                                GraphEditorWin.SettedThickness, GraphEditorWin.IsGraphActive);
            GraphSettingsChanged?.Invoke(this, GEe);

            if (e.PreviousColor.A == 0 && e.CurrentColor.A != 0) // then activate graph
            {
                // Allow only invert the state, to prevent possible eternal looping
                if (GraphEditorWin.chbxIsActive.IsChecked != true)
                {
                    isDrivenByColor = true;
                    GraphEditorWin.chbxIsActive.IsChecked = true;
                }
            }
            else if (e.PreviousColor.A != 0 && e.CurrentColor.A == 0) // then disactivate graph
            {
                // Allow only invert the state, to prevent possible eternal looping
                if (GraphEditorWin.chbxIsActive.IsChecked == true)
                {
                    isDrivenByColor = true;
                    GraphEditorWin.chbxIsActive.IsChecked = false;
                } 
            }
        }

        private void SetGraphDashes(object sender, SelectionChangedEventArgs e)
        {
            if (iniFlag) return; // >>>>>>>>> >>>>>>>>>

            if (GraphPaths[EditedIndex] != null && GraphEditorWin.combobxDashes.SelectedIndex > -1)
            {
                GraphPaths[EditedIndex].StrokeDashArray = DashesList[GraphEditorWin.combobxDashes.SelectedIndex];
            }

            GraphEditorEventArgs GEe = new GraphEditorEventArgs(EditedIndex, GraphEditorSettings.Dashes, GraphEditorWin.SelectedColor, GraphEditorWin.SelectedDashes,
                                                                GraphEditorWin.SettedThickness, GraphEditorWin.IsGraphActive);
            GraphSettingsChanged?.Invoke(this, GEe);
        }

        private void SetGraphThickness_spinners(object sender, RoutedEventArgs e)
        {
            RepeatButton rebtt = e.OriginalSource as RepeatButton;

            if (rebtt != null)
            {
                double preValue = GraphEditorWin.SettedThickness;

                if (rebtt.Name == "rebttIncrease" && preValue < 10) preValue += 0.1;
                else if (rebtt.Name == "rebttDecrease" && preValue > 0) preValue -= 0.1;

                preValue = GraphEditorWin.SetThickness(preValue);

                if (GraphPaths[EditedIndex] != null)
                {
                    GraphPaths[EditedIndex].StrokeThickness = preValue;

                    Markers[EditedIndex].Width = 2 * preValue;
                    Markers[EditedIndex].Height = Markers[EditedIndex].Width;
                } 

                GraphEditorEventArgs GEe = new GraphEditorEventArgs(EditedIndex, GraphEditorSettings.Thickness, GraphEditorWin.SelectedColor, GraphEditorWin.SelectedDashes,
                                                                    preValue, GraphEditorWin.IsGraphActive);
                GraphSettingsChanged?.Invoke(this, GEe);
            }
            
            e.Handled = true;
        }
        
        private void SetGraphThickness_keyEnter(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TextBox txtBox = e.OriginalSource as TextBox;

                if (txtBox != null)
                {
                    GraphEditorWin.SetThickness(txtBox.Text);

                    if (GraphPaths[EditedIndex] != null)
                    {
                        GraphPaths[EditedIndex].StrokeThickness = GraphEditorWin.SettedThickness;

                        Markers[EditedIndex].Width = 2 * GraphEditorWin.SettedThickness;
                        Markers[EditedIndex].Height = Markers[EditedIndex].Width;
                    } 

                    GraphEditorEventArgs GEe = new GraphEditorEventArgs(EditedIndex, GraphEditorSettings.Thickness, GraphEditorWin.SelectedColor, GraphEditorWin.SelectedDashes,
                                                                        GraphEditorWin.SettedThickness, GraphEditorWin.IsGraphActive);
                    GraphSettingsChanged?.Invoke(this, GEe);

                    e.Handled = true;
                }
            }
        }

        private void SetGraphThickness_lostKeyFocus(object sender, RoutedEventArgs e)
        {
            TextBox txtBox = e.OriginalSource as TextBox;

            if (txtBox != null)
            {
                GraphEditorWin.SetThickness(txtBox.Text);

                if (GraphPaths[EditedIndex] != null)
                {
                    GraphPaths[EditedIndex].StrokeThickness = GraphEditorWin.SettedThickness;

                    Markers[EditedIndex].Width = 2 * GraphEditorWin.SettedThickness;
                    Markers[EditedIndex].Height = Markers[EditedIndex].Width;
                }

                GraphEditorEventArgs GEe = new GraphEditorEventArgs(EditedIndex, GraphEditorSettings.Thickness, GraphEditorWin.SelectedColor, GraphEditorWin.SelectedDashes,
                                                                    GraphEditorWin.SettedThickness, GraphEditorWin.IsGraphActive);
                GraphSettingsChanged?.Invoke(this, GEe);
            }

            e.Handled = true;
        }

        private void SetGraphVisibility(object sender, RoutedEventArgs e)
        {
            if (iniFlag) return; // >>>>>>>>> >>>>>>>>>

            bool flag = GraphEditorWin.IsGraphActive;
            Color theColor = GraphEditorWin.SelectedColor;

            if (isDrivenByColor)
            {
                isDrivenByColor = false;

                if (!flag)
                {
                    RemoveBounds(EditedIndex);
                    FitIn(AutoFit);
                }
            } 
            else // visibility is driven by the check box
            {
                if (flag)
                {
                    theColor.A = 255;
                }
                else
                {
                    theColor.A = 0;

                    RemoveBounds(EditedIndex);
                    FitIn(AutoFit);
                }

                GraphEditorWin.ColorControl.SelectedColorBrush.Color = theColor;
            }
            
            GraphEditorEventArgs GEe = new GraphEditorEventArgs(EditedIndex, GraphEditorSettings.IsActive, GraphEditorWin.SelectedColor, GraphEditorWin.SelectedDashes,
                                                                GraphEditorWin.SettedThickness, flag);
            GraphSettingsChanged?.Invoke(this, GEe);
        }

        readonly string[] SIprefixes = new string[17] { "y", "z", "a", "f", "p", "n", '\u00B5'.ToString(), "m", "", "k", "M", "G", "T", "P", "E", "Z", "Y" };

        /// <summary>
        /// Returns gauge for the displayed step value: DisplayStep = gauge * Step
        /// </summary>
        /// <param name="Step"></param>
        /// <param name="Origin">if Origin/Step is greater than the MaxRatio, then Origin is setted to 0 and corresponding offset info is added to the UnitsLabel</param>
        /// <param name="UnitsLabel"></param>
        /// <param name="FormatSpec">standard numeric format string</param>
        /// <param name="multiplier"></param>
        /// <param name="unitName"></param>
        /// <param name="InKilos">if true then exponent is restricted to multiples of three</param>
        /// <param name="AddPrefix"></param>
        /// <param name="MaxRatio">the limit for the Origin to Step ratio to determine whether or not to use corresponding offset</param>
        /// <returns></returns>
        public double Calibrate(double Step, ref double Origin, out string UnitsLabel, out string FormatSpec,
                                double multiplier = 1, string unitName = "", bool InKilos = true, bool AddPrefix = false, double MaxRatio = 1000)
        {
            double Gauge; // DisplayStep = Gauge * Step
            double ordine;
            double multiStep = Step / multiplier;

            UnitsLabel = "";
            FormatSpec = "";
            string mStr = "";

            if (multiplier <= 0) multiplier = 1;
            else if (multiplier != 1)
            {
                if (multiplier == Math.PI) mStr = '\u03C0'.ToString();
                else mStr = multiplier.ToString();
            }

            bool addOffset = (Math.Abs(Origin) / Step >= MaxRatio);

            if (AddPrefix)
            {
                ordine = Math.Truncate(Math.Log10(multiStep) / 3);

                if (ordine > 8) ordine = 8;
                else if (ordine < -8) ordine = -8;

                Gauge = Math.Pow(1000, -ordine) / multiplier;

                int ind = (int)ordine + 8;

                UnitsLabel = String.Format("* {0} {1}{2}", mStr, SIprefixes[ind], unitName);

                // define required number of digits after the decimal point
                ordine *= 3;
                ordine -= Math.Floor(Math.Log10(multiStep));
                if (ordine < 0) ordine = 0;
                FormatSpec = String.Format("N{0}", ordine);

                if (addOffset)
                {
                    ordine = Math.Round(Math.Log10(Math.Abs(Origin)) / 3);

                    if (ordine > 8) ordine = 8;
                    else if (ordine < -8) ordine = -8;

                    ind = (int)ordine + 8;

                    UnitsLabel += String.Format(", Offset: {0} {1}{2}", Origin * Math.Pow(1000, -ordine), SIprefixes[ind], unitName);

                    Origin = 0;
                }
            }
            else
            {
                if (InKilos)
                {
                    ordine = 3 * Math.Truncate(Math.Log10(multiStep) / 3);
                }
                else
                {
                    ordine = Math.Truncate(Math.Log10(multiStep));
                }

                Gauge = Math.Pow(10, -ordine) / multiplier;

                string eStr = "";

                if (ordine > 0) eStr = "e+" + ordine;
                else if (ordine < 0) eStr = "e" + ordine;

                if (mStr != "" || eStr != "" || unitName != "") UnitsLabel = String.Format("* {0}{1} {2}", mStr, eStr, unitName);

                // define required number of digits after the decimal point
                ordine -= Math.Floor(Math.Log10(multiStep));
                if (ordine < 0) ordine = 0;
                FormatSpec = String.Format("N{0}", ordine);

                if (addOffset)
                {
                    if (InKilos) ordine = 3 * Math.Round(Math.Log10(Math.Abs(Origin)) / 3);
                    else ordine = Math.Round(Math.Log10(Math.Abs(Origin)));

                    if (ordine > 0) eStr = "e+" + ordine;
                    else if (ordine < 0) eStr = "e" + ordine;

                    UnitsLabel += String.Format(", Offset: {0}{1} {2}", Origin * Math.Pow(10, -ordine), eStr, unitName);

                    Origin = 0;
                }
            }

            return Gauge;
        }

        private void CanvasSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // --- change mat frame size ---------------------------------------------------------------------
            double ratioWidth = e.NewSize.Width / e.PreviousSize.Width;
            double ratioHeight = e.NewSize.Height / e.PreviousSize.Height;

            OuterRectMatrix.M11 *= ratioWidth;
            OuterRectMatrix.M22 *= ratioHeight;

            OuterRectMxTr.Matrix = OuterRectMatrix;
            //GridMatFrame.Geometry1.Transform = new MatrixTransform(OuterRectMatrix); // it litters

            ratioWidth = 1 + (--ratioWidth) * e.PreviousSize.Width / GridMatFrame.Geometry2.Bounds.Width;
            ratioHeight = 1 + (--ratioHeight) * e.PreviousSize.Height / GridMatFrame.Geometry2.Bounds.Height;

            InnerRectMatrix.M11 *= ratioWidth;
            InnerRectMatrix.M22 *= ratioHeight;
            InnerRectMatrix.OffsetX = ratioWidth * (InnerRectMatrix.OffsetX - leftIndent) + leftIndent;
            InnerRectMatrix.OffsetY = ratioHeight * (InnerRectMatrix.OffsetY - topIndent) + topIndent;

            InnerRectMxTr.Matrix = InnerRectMatrix;
            //GridMatFrame.Geometry2.Transform = new MatrixTransform(InnerRectMatrix); // it litters

            // --- change labels areas sizes -------------------------------------------------------------------
            UlabelsArea.Width = e.NewSize.Width - leftIndent - rightIndent + LabelsFontSize;
            unitsUlabel.Width = UlabelsArea.Width;

            VlabelsArea.Height = e.NewSize.Height - topIndent - bottomIndent + LabelsFontSize;
            Canvas.SetTop(unitsVlabel, 0.5 * (VlabelsArea.Height - unitsVlabel.Height));

            if (BoundsAdded > 0) FitIn(AutoFit);
        }
        
        public Rect ConvertRectUVtoXY(Rect rectUV)
        {
            double Xtl = (rectUV.X - GraphMatrix.OffsetX) / ratioXtoU;
            double Ytl = (rectUV.Y - GraphMatrix.OffsetY) / ratioYtoV;

            double Xwidth = rectUV.Width / ratioXtoU;
            double Yheight = rectUV.Height / ratioYtoV;

            return new Rect(new Point(Xtl, Ytl), new Vector(Xwidth, Yheight));
        }

        public event EventHandler RectZoomingComplete;
        private bool RectZoomFlag = false;

        public void RectZoomSwitch(bool turnON)
        {
            if (turnON)
            {
                outputCanvas.MouseLeftButtonDown -= MLBdown;
                outputCanvas.MouseMove -= ON_MouseOver;
                outputCanvas.MouseLeave -= MouseLeave_Canvas;

                outputCanvas.MouseLeftButtonDown += RectZoom_MLBdown;

                outputCanvas.Cursor = Cursors.SizeNWSE;
            }
            else // turnOFF
            {
                if (outputCanvas.IsMouseCaptured) outputCanvas.ReleaseMouseCapture();
                if (RectZoomPath.IsVisible) RectZoomPath.Visibility = Visibility.Collapsed;

                outputCanvas.MouseLeftButtonDown -= RectZoom_MLBdown;
                outputCanvas.MouseLeftButtonUp -= RectZoom_MLBup;
                outputCanvas.MouseMove -= RectZoom_MLBholdMove;

                outputCanvas.MouseLeftButtonDown += MLBdown;
                outputCanvas.MouseMove += ON_MouseOver;
                outputCanvas.MouseLeave += MouseLeave_Canvas;

                outputCanvas.Cursor = Cursors.Cross;

                RectZoomingComplete?.Invoke(this, EventArgs.Empty);
            }

            RectZoomFlag = turnON;
        }

        private void RectZoom_MLBdown(object sender, MouseButtonEventArgs e)
        {
            MouseIniPoint = e.GetPosition(outputCanvas);

            RectZoomGeometry.Rect = new Rect(MouseIniPoint, MouseIniPoint);
            RectZoomPath.Visibility = Visibility.Visible;

            outputCanvas.CaptureMouse();

            outputCanvas.MouseLeftButtonDown -= RectZoom_MLBdown;
            outputCanvas.MouseLeftButtonUp += RectZoom_MLBup;

            outputCanvas.MouseMove += RectZoom_MLBholdMove;
        }

        private void RectZoom_MLBup(object sender, MouseButtonEventArgs e)
        {
            outputCanvas.MouseMove -= RectZoom_MLBholdMove;
            outputCanvas.MouseLeftButtonUp -= RectZoom_MLBup;

            outputCanvas.ReleaseMouseCapture();

            RectZoomPath.Visibility = Visibility.Collapsed;

            if (RectZoomGeometry.Rect.Width > 10 && RectZoomGeometry.Rect.Height > 10) // then perform zooming
            {
                RectZoomSwitch(false);

                FitIn(Bounds: ConvertRectUVtoXY(RectZoomGeometry.Rect));
            }
            else // give user another chance
            {
                outputCanvas.MouseLeftButtonDown += RectZoom_MLBdown;
            }
        }

        private void RectZoom_MLBholdMove(object sender, MouseEventArgs e)
        {
            Point currPoint = e.GetPosition(outputCanvas);

            if (currPoint.X < 0) currPoint.X = 0;
            else if (currPoint.X > outputCanvas.ActualWidth) currPoint.X = outputCanvas.ActualWidth;

            if (currPoint.Y < 0) currPoint.Y = 0;
            else if (currPoint.Y > outputCanvas.ActualHeight) currPoint.Y = outputCanvas.ActualHeight;

            RectZoomGeometry.Rect = new Rect(MouseIniPoint, currPoint);
        }

        private void MLBdown(object sender, MouseButtonEventArgs e)
        {
            MouseIniPoint = e.GetPosition(outputCanvas);

            outputCanvas.Cursor = Cursors.ScrollAll;
            outputCanvas.CaptureMouse();

            outputCanvas.MouseLeftButtonDown -= MLBdown;
            outputCanvas.MouseLeftButtonUp += MLBup;

            outputCanvas.MouseMove -= ON_MouseOver;
            outputCanvas.MouseMove += MLBholdMove;
        }

        private void MLBup(object sender, MouseButtonEventArgs e)
        {
            outputCanvas.ReleaseMouseCapture();
            outputCanvas.Cursor = Cursors.Cross;

            outputCanvas.MouseLeftButtonDown += MLBdown;
            outputCanvas.MouseLeftButtonUp -= MLBup;

            outputCanvas.MouseMove -= MLBholdMove;
            outputCanvas.MouseMove += ON_MouseOver;
        }
        
        private void ON_MouseOver(object sender, MouseEventArgs e)
        {
            Point currPoint = e.GetPosition(outputCanvas);
            double pU = currPoint.X, pV;

            MouseMoveEventArgs eArgs = new MouseMoveEventArgs(GraphMatrix, currPoint, MarkersYvalues, MarkersVisibility);

            MouseMove?.Invoke(this, eArgs);

            // Obtain the Marker Point
            if (!double.IsNegativeInfinity(eArgs.MarkerPointXY.X))
            {
                pU = GraphMatrix.M11 * eArgs.MarkerPointXY.X + GraphMatrix.OffsetX;
            }

            SWShapes.Ellipse marker;

            for (int i = 0; i < Markers.Length; i++)
            {
                marker = Markers[i];

                if (marker != null)
                {
                    marker.Visibility = eArgs.MarkersVisibility[i];

                    if (marker.IsVisible)
                    {
                        pV = GraphMatrix.M22 * MarkersYvalues[i] + GraphMatrix.OffsetY;

                        pV -= 0.5 * marker.ActualHeight;

                        if (double.IsNegativeInfinity(pV)) pV = double.MinValue;
                        else if (double.IsPositiveInfinity(pV)) pV = double.MaxValue;
                        else if (double.IsNaN(pV)) pV = -100;

                        Canvas.SetLeft(marker, pU - 0.5 * marker.ActualWidth);
                        Canvas.SetTop(marker, pV);
                    }
                    
                }
            }

            if (!double.IsNegativeInfinity(eArgs.MarkerPointXY.Y))
            {
                pV = GraphMatrix.M22 * eArgs.MarkerPointXY.Y + GraphMatrix.OffsetY;
            }
            else pV = currPoint.Y;

            if (eArgs.ShowUline)
            {
                if (!MarkerUline.IsVisible) MarkerUline.Visibility = Visibility.Visible;
                
                MarkerUlineTT.X = pU;
                MarkerUlineTT.Y = pV;
            }
            else if (MarkerUline.IsVisible) MarkerUline.Visibility = Visibility.Collapsed;

            if (eArgs.ShowVline)
            {
                if (!MarkerVline.IsVisible) MarkerVline.Visibility = Visibility.Visible;
                
                MarkerVlineTT.X = pU;
                MarkerVlineTT.Y = pV;
            }
            else if (MarkerVline.IsVisible) MarkerVline.Visibility = Visibility.Collapsed;
        }

        private void ON_MouseOver(Point currPoint)
        {
            double pU = currPoint.X, pV;

            MouseMoveEventArgs eArgs = new MouseMoveEventArgs(GraphMatrix, currPoint, MarkersYvalues, MarkersVisibility);

            MouseMove?.Invoke(this, eArgs);

            // Obtain the Marker Point
            if (!double.IsNegativeInfinity(eArgs.MarkerPointXY.X))
            {
                pU = GraphMatrix.M11 * eArgs.MarkerPointXY.X + GraphMatrix.OffsetX;
            }

            SWShapes.Ellipse marker;

            for (int i = 0; i < Markers.Length; i++)
            {
                marker = Markers[i];

                if (marker != null)
                {
                    marker.Visibility = eArgs.MarkersVisibility[i];

                    if (marker.IsVisible)
                    {
                        pV = GraphMatrix.M22 * MarkersYvalues[i] + GraphMatrix.OffsetY;

                        pV -= 0.5 * marker.ActualHeight;

                        if (double.IsNegativeInfinity(pV)) pV = double.MinValue;
                        else if (double.IsPositiveInfinity(pV)) pV = double.MaxValue;
                        else if (double.IsNaN(pV)) pV = -100;

                        Canvas.SetLeft(marker, pU - 0.5 * marker.ActualWidth);
                        Canvas.SetTop(marker, pV);
                    }

                }
            }

            if (!double.IsNegativeInfinity(eArgs.MarkerPointXY.Y))
            {
                pV = GraphMatrix.M22 * eArgs.MarkerPointXY.Y + GraphMatrix.OffsetY;
            }
            else pV = currPoint.Y;

            if (eArgs.ShowUline)
            {
                if (!MarkerUline.IsVisible) MarkerUline.Visibility = Visibility.Visible;

                MarkerUlineTT.X = pU;
                MarkerUlineTT.Y = pV;
            }
            else if (MarkerUline.IsVisible) MarkerUline.Visibility = Visibility.Collapsed;

            if (eArgs.ShowVline)
            {
                if (!MarkerVline.IsVisible) MarkerVline.Visibility = Visibility.Visible;

                MarkerVlineTT.X = pU;
                MarkerVlineTT.Y = pV;
            }
            else if (MarkerVline.IsVisible) MarkerVline.Visibility = Visibility.Collapsed;
        }

        private void MouseLeave_Canvas(object sender, MouseEventArgs e)
        {
            foreach (SWShapes.Ellipse marker in Markers) if (marker != null) marker.Visibility = Visibility.Collapsed;
            MarkerUline.Visibility = Visibility.Collapsed;
            MarkerVline.Visibility = Visibility.Collapsed;
        }

        private void MLBholdMove(object sender, MouseEventArgs e)
        {
            Point currPoint = e.GetPosition(outputCanvas);

            double du = currPoint.X - MouseIniPoint.X;
            double dv = currPoint.Y - MouseIniPoint.Y;

            OffsetU += du;
            OffsetV += dv;
            GraphMatrix.OffsetX = OffsetU;
            GraphMatrix.OffsetY = OffsetV;
            GraphMxTr.Matrix = GraphMatrix;
            foreach (Filtering F in OutFilters) F?.Invoke();

            UgridMatrix.OffsetX += du;
            VgridMatrix.OffsetY += dv;

            UgridMxTr.Matrix = UgridMatrix;
            VgridMxTr.Matrix = VgridMatrix;
            //UgridLinesGeometry.Transform = new MatrixTransform(UgridMatrix); //it litters
            //VgridLinesGeometry.Transform = new MatrixTransform(VgridMatrix);

            UShift += du;
            VShift += dv;

            // ------- U-labels offset -----------------------------------------------------------------------------------------------------
            double shiftNum = Math.Truncate(UShift / UStepScaled);

            if (shiftNum != 0)
            {
                int pendex = UlabelsArr.Length - 1;

                labelLeft -= GaugeX * shiftNum;
                labelRight = labelLeft;

                for (int i = 0; i < pendex; i++)
                {
                    UlabelsArr[i].Content = labelRight.ToString(UlabelsFormatSpec);
                    labelRight += GaugeX;
                }

                UlabelsArr[pendex].Content = labelRight.ToString(UlabelsFormatSpec);
            }

            // ------- V-labels offset -----------------------------------------------------------------------------------------------------
            shiftNum = Math.Truncate(VShift / VStepScaled);

            if (shiftNum != 0)
            {
                int pendex = VlabelsArr.Length - 1;

                labelTop += GaugeY * shiftNum;
                labelBottom = labelTop;

                for (int i = 0; i < pendex; i++)
                {
                    VlabelsArr[i].Content = labelBottom.ToString(VlabelsFormatSpec);
                    labelBottom -= GaugeY;
                }

                VlabelsArr[pendex].Content = labelBottom.ToString(VlabelsFormatSpec);
            }

            // ------- permutation of the U-lines ------------------------------------------------------------------------------------------
            int endex = UgridLinesGeometry.Figures.Count - 1;

            while (UShift >= UStepScaled) // "move" U-lines from right to left side
            {
                UShift -= UStepScaled;
                ULeft -= UStep;
                URight -= UStep;

                UgridLinesGeometry.Figures[indRight] =
                    new PathFigure(new Point(ULeft, 0), new LineSegment[1] { new LineSegment(new Point(ULeft, MaxHeight), true) }, false);

                indLeft--;
                indRight--;

                if (indLeft < 0) indLeft = endex;
                else if (indRight < 0) indRight = endex;
            }

            while (UShift <= -UStepScaled) // "move" U-lines from left to right side
            {
                UShift += UStepScaled;
                ULeft += UStep;
                URight += UStep;

                UgridLinesGeometry.Figures[indLeft] =
                    new PathFigure(new Point(URight, 0), new LineSegment[1] { new LineSegment(new Point(URight, MaxHeight), true) }, false);

                indLeft++;
                indRight++;

                if (indLeft > endex) indLeft = 0;
                else if (indRight > endex) indRight = 0;
            }

            //UlabelsCells.RenderTransform = new TranslateTransform(UShift, 0); // it litters
            UlabelsTT.X = UShift;

            // ------- permutation of the V-lines ------------------------------------------------------------------------------------------
            endex = VgridLinesGeometry.Figures.Count - 1;

            while (VShift >= VStepScaled) // "move" V-lines from bottom to top side
            {
                VShift -= VStepScaled;
                VTop -= VStep;
                VBottom -= VStep;

                VgridLinesGeometry.Figures[indBottom] =
                    new PathFigure(new Point(0, VTop), new LineSegment[1] { new LineSegment(new Point(MaxWidth, VTop), true) }, false);

                indTop--;
                indBottom--;

                if (indTop < 0) indTop = endex;
                else if (indBottom < 0) indBottom = endex;
            }

            while (VShift <= -VStepScaled) // "move" V-lines from top to bottom side
            {
                VShift += VStepScaled;
                VTop += VStep;
                VBottom += VStep;

                VgridLinesGeometry.Figures[indTop] =
                    new PathFigure(new Point(0, VBottom), new LineSegment[1] { new LineSegment(new Point(MaxWidth, VBottom), true) }, false);

                indTop++;
                indBottom++;

                if (indTop > endex) indTop = 0;
                else if (indBottom > endex) indBottom = 0;
            }

            //VlabelsCells.RenderTransform = new TranslateTransform(0, VShift); // it litters
            VlabelsTT.Y = VShift;

            // --- Marker and Marker lines update ------------------------------------------------
            double pU = currPoint.X;
            double pV = currPoint.Y;

            foreach (SWShapes.Ellipse marker in Markers)
            {
                if (marker != null && marker.IsVisible)
                {
                    Canvas.SetLeft(marker, Canvas.GetLeft(marker) + du);
                    Canvas.SetTop(marker, Canvas.GetTop(marker) + dv);
                }
            }

            if (MarkerUline.IsVisible)
            {
                MarkerUlineTT.X = pU;
                MarkerUlineTT.Y = pV;
            }

            if (MarkerVline.IsVisible)
            {
                MarkerVlineTT.X = pU;
                MarkerVlineTT.Y = pV;
            }

            MouseIniPoint = currPoint;
        }

        void MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point zoomOrigin = e.GetPosition(outputCanvas);
            int step = e.Delta / 120;

            double zooming = Math.Pow(zoomFactor, step);
            double zoomExcess = zooming;
            bool IsExceed = true;

            bool SetLimU = false;
            bool SetLimV = false;
            bool NewUgrid = false;
            bool NewVgrid = false;

            if (step > 0) // zooming in --------------------------------------------------------
            {
                double zoomUmax, zoomVmax, zoomTop;
                int endX = XstepFactors.Length - 1;
                int endY = YstepFactors.Length - 1;

                while (IsExceed) // zoom-in loop
                {
                    zoomUmax = UStepMax / UStepScaled;
                    zoomVmax = VStepMax / VStepScaled;

                    if (zoomUmax < zoomVmax)
                    {
                        zoomTop = zoomUmax;
                        SetLimU = true;
                    }
                    else if (zoomUmax > zoomVmax)
                    {
                        zoomTop = zoomVmax;
                        SetLimV = true;
                    }
                    else
                    {
                        zoomTop = zoomUmax;
                        SetLimU = true;
                        SetLimV = true;
                    }

                    if (zoomExcess >= zoomTop) // decrease X,Y-Steps
                    {
                        zoomExcess /= zoomTop;

                        UStepScaled *= zoomTop;
                        VStepScaled *= zoomTop;

                        if (SetLimU)
                        {
                            SetLimU = false;
                            NewUgrid = true;

                            if (grewXStep) grewXStep = false;
                            else if (--XfactorInd < 0) XfactorInd = endX;

                            XStep /= XstepFactors[XfactorInd];
                            UStepScaled /= XstepFactors[XfactorInd];
                        }

                        if (SetLimV)
                        {
                            SetLimV = false;
                            NewVgrid = true;

                            if (grewYStep) grewYStep = false;
                            else if (--YfactorInd < 0) YfactorInd = endY;

                            YStep /= YstepFactors[YfactorInd];
                            VStepScaled /= YstepFactors[YfactorInd];
                        }
                    }
                    else // finalize zooming
                    {
                        UStepScaled *= zoomExcess;
                        VStepScaled *= zoomExcess;

                        IsExceed = false; // !!! LOOP EXIT !!!
                        break;
                    }
                } // end zoom-in loop
            }
            else if (step < 0) // zooming out -------------------------------------------------------
            {
                double zoomUmin, zoomVmin, zoomLow;
                int endX = XstepFactors.Length - 1;
                int endY = YstepFactors.Length - 1;

                while (IsExceed) // zoom-out loop
                {
                    zoomUmin = UStepMin / UStepScaled;
                    zoomVmin = VStepMin / VStepScaled;

                    if (zoomUmin > zoomVmin)
                    {
                        zoomLow = zoomUmin;
                        SetLimU = true;
                    }
                    else if (zoomUmin < zoomVmin)
                    {
                        zoomLow = zoomVmin;
                        SetLimV = true;
                    }
                    else
                    {
                        zoomLow = zoomUmin;
                        SetLimU = true;
                        SetLimV = true;
                    }

                    if (zoomExcess <= zoomLow) // increase X,Y-Steps
                    {
                        zoomExcess /= zoomLow;

                        UStepScaled *= zoomLow;
                        VStepScaled *= zoomLow;

                        if (SetLimU)
                        {
                            SetLimU = false;
                            NewUgrid = true;

                            if (!grewXStep) grewXStep = true;
                            else if (++XfactorInd > endX) XfactorInd = 0;

                            XStep *= XstepFactors[XfactorInd];
                            UStepScaled *= XstepFactors[XfactorInd];
                        }

                        if (SetLimV)
                        {
                            SetLimV = false;
                            NewVgrid = true;

                            if (!grewYStep) grewYStep = true;
                            else if (++YfactorInd > endY) YfactorInd = 0;

                            YStep *= YstepFactors[YfactorInd];
                            VStepScaled *= YstepFactors[YfactorInd];
                        }
                    }
                    else // finalize zooming
                    {
                        UStepScaled *= zoomExcess;
                        VStepScaled *= zoomExcess;

                        IsExceed = false; // !!! LOOP EXIT !!!
                        break;
                    }
                } // end zoom-out loop
            } // end if (step > 0) ---------------------------------------------------------------------

            // scale the functions graphs
            ratioXtoU *= zooming;
            ratioYtoV *= zooming;
            GraphMatrix.M11 = ratioXtoU;
            GraphMatrix.M22 = ratioYtoV;

            OffsetU = zooming * (OffsetU - zoomOrigin.X) + zoomOrigin.X;
            OffsetV = zooming * (OffsetV - zoomOrigin.Y) + zoomOrigin.Y;
            GraphMatrix.OffsetX = OffsetU;
            GraphMatrix.OffsetY = OffsetV;
            GraphMxTr.Matrix = GraphMatrix;
            foreach (Filtering F in OutFilters) F?.Invoke();

            if (NewUgrid) // create new grid layout of the U-lines
            {
                UStep = ratioXtoU * XStep;

                int coverNumCurr = (int)Math.Ceiling(MaxWidth / UStep);
                int coverNumMax;
                int coverNumAdd;
                int coverNumTot;

                if (UStep > UStepMin)
                {
                    coverNumMax = (int)Math.Ceiling(coverNumCurr * UStep / UStepMin);
                    coverNumAdd = coverNumMax - coverNumCurr;
                    coverNumTot = coverNumMax + coverNumAdd;
                }
                else
                {
                    coverNumAdd = 0;
                    coverNumTot = coverNumCurr;
                }

                double Xclosest = (leftIndent - OffsetU) / ratioXtoU;
                double Uclosest = Math.Round(Xclosest / XStep) * XStep * ratioXtoU + OffsetU;
                double Ustart = Uclosest - coverNumAdd * UStep;
                double Ui = Ustart;

                PathFigure[] LinesU = new PathFigure[coverNumTot];
                UlabelsArr = new Label[coverNumTot];

                UlabelsCells.Children.Clear();
                UlabelsCells.Columns = coverNumTot;
                UlabelsCells.Width = UStep * coverNumTot;
                UlabelsTT.X = 0;
                Canvas.SetLeft(UlabelsCells, Ustart - 0.5 * UStep - UlabelsOffsetX);

                // U-grid calibration
                string unitsLabel;

                labelLeft = (Ustart - OffsetU) / ratioXtoU;

                double ordine = Calibrate(XStep, ref labelLeft, out unitsLabel, out UlabelsFormatSpec, XgridBase, unitName: UnitsNameU, AddPrefix: UsePrefixU);

                unitsUlabel.Content = unitsLabel;

                GaugeX = ordine * XStep;
                labelLeft *= ordine;
                double Xgi = labelLeft;

                for (int i = 0; i < coverNumTot; i++) // adding grid-lines and labels
                {
                    LinesU[i] = new PathFigure(new Point(Ui, 0), new LineSegment[1] { new LineSegment(new Point(Ui, MaxHeight), true) }, false);
                    Ui += UStep;

                    UlabelsArr[i] = new Label();
                    UlabelsArr[i].Content = Xgi.ToString(UlabelsFormatSpec);
                    UlabelsArr[i].Padding = new Thickness(2);
                    UlabelsArr[i].HorizontalAlignment = HorizontalAlignment.Center;
                    UlabelsArr[i].VerticalAlignment = VerticalAlignment.Center;
                    UlabelsArr[i].Foreground = GridLabelsBrush;
                    UlabelsCells.Children.Add(UlabelsArr[i]);
                    Xgi += GaugeX;
                }

                UgridLinesGeometry = new PathGeometry(LinesU);
                UgridLines.Data = UgridLinesGeometry;
                UgridMatrix = new Matrix(1, 0, 0, 1, 0, 0);
                UgridMxTr.Matrix = UgridMatrix;
                UgridLines.Data.Transform = UgridMxTr;

                UShift = 0;
                ULeft = Ustart;
                URight = Ui - UStep;
                labelRight = Xgi - GaugeX;
                indLeft = 0;
                indRight = coverNumTot - 1;
            }
            else // just scale the current grid layout of the U-lines
            {
                UgridMatrix.M11 *= zooming;
                UgridMatrix.OffsetX = zooming * (UgridMatrix.OffsetX - zoomOrigin.X) + zoomOrigin.X;

                UgridMxTr.Matrix = UgridMatrix;
                //UgridLinesGeometry.Transform = new MatrixTransform(UgridMatrix); // it litters

                // scale U-labels cells
                UlabelsCells.Width *= zooming;

                double labelsZo = zoomOrigin.X - UlabelsOffsetX;
                double Uo = Canvas.GetLeft(UlabelsCells) + UlabelsCells.RenderTransform.Value.OffsetX;

                UShift = 0;
                //UlabelsCells.RenderTransform = new TranslateTransform(0, 0); // it litters
                UlabelsTT.X = 0;
                Canvas.SetLeft(UlabelsCells, zooming * (Uo - labelsZo) + labelsZo);
            } // end if (NewUgrid) -------


            if (NewVgrid) // create new grid layout of the V-lines
            {
                VStep = -ratioYtoV * YStep;

                int coverNumCurr = (int)Math.Ceiling(MaxHeight / VStep);
                int coverNumMax;
                int coverNumAdd;
                int coverNumTot;

                if (VStep > VStepMin)
                {
                    coverNumMax = (int)Math.Ceiling(coverNumCurr * VStep / VStepMin);
                    coverNumAdd = coverNumMax - coverNumCurr;
                    coverNumTot = coverNumMax + coverNumAdd;
                }
                else
                {
                    coverNumAdd = 0;
                    coverNumTot = coverNumCurr;
                }

                double Yclosest = (topIndent - OffsetV) / ratioYtoV;
                double Vclosest = Math.Round(Yclosest / YStep) * YStep * ratioYtoV + OffsetV;
                double Vstart = Vclosest - coverNumAdd * VStep;
                double Vi = Vstart;

                PathFigure[] LinesV = new PathFigure[coverNumTot];
                VlabelsArr = new Label[coverNumTot];

                VlabelsCells.Children.Clear();
                VlabelsCells.Rows = coverNumTot;
                VlabelsCells.Height = VStep * coverNumTot;
                VlabelsTT.Y = 0;
                Canvas.SetTop(VlabelsCells, Vstart - 0.5 * VStep - VlabelsOffsetY);

                // V-grid calibration
                string unitsLabel;
                labelTop = (Vstart - OffsetV) / ratioYtoV;

                double ordine = Calibrate(YStep, ref labelTop, out unitsLabel, out VlabelsFormatSpec, YgridBase, unitName: UnitsNameV, AddPrefix: UsePrefixV);

                unitsVlabel.Content = unitsLabel;
                unitsVlabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetRight(unitsVlabel, VlabelsCells.Width - 0.5 * unitsVlabel.DesiredSize.Width);
                unitsVlabel.RenderTransform = new RotateTransform(-90, 0.5 * unitsVlabel.DesiredSize.Width, 0.5 * unitsVlabel.Height);

                GaugeY = ordine * YStep;
                labelTop *= ordine;
                double Ygi = labelTop;

                for (int i = 0; i < coverNumTot; i++) // adding grid-lines and labels
                {
                    LinesV[i] = new PathFigure(new Point(0, Vi), new LineSegment[1] { new LineSegment(new Point(MaxWidth, Vi), true) }, false);
                    Vi += VStep;

                    VlabelsArr[i] = new Label();
                    VlabelsArr[i].Content = Ygi.ToString(VlabelsFormatSpec);
                    VlabelsArr[i].Padding = new Thickness(2, 2, 3, 2);
                    VlabelsArr[i].HorizontalAlignment = HorizontalAlignment.Right;
                    VlabelsArr[i].VerticalAlignment = VerticalAlignment.Center;
                    VlabelsArr[i].Foreground = GridLabelsBrush;
                    VlabelsCells.Children.Add(VlabelsArr[i]);
                    Ygi -= GaugeY;
                }

                VgridLinesGeometry = new PathGeometry(LinesV);
                VgridLines.Data = VgridLinesGeometry;
                VgridMatrix = new Matrix(1, 0, 0, 1, 0, 0);
                VgridMxTr.Matrix = VgridMatrix;
                VgridLines.Data.Transform = VgridMxTr;

                VShift = 0;
                VTop = Vstart;
                VBottom = Vi - VStep;
                labelBottom = Ygi + GaugeY;
                indTop = 0;
                indBottom = coverNumTot - 1;
            }
            else // just scale the current grid layout of the V-lines
            {
                VgridMatrix.M22 *= zooming;
                VgridMatrix.OffsetY = zooming * (VgridMatrix.OffsetY - zoomOrigin.Y) + zoomOrigin.Y;

                VgridMxTr.Matrix = VgridMatrix;
                //VgridLinesGeometry.Transform = new MatrixTransform(VgridMatrix); // it litters

                // scale V-labels cells
                VlabelsCells.Height *= zooming;

                double labelsZo = zoomOrigin.Y - VlabelsOffsetY;
                double Vo = Canvas.GetTop(VlabelsCells) + VlabelsCells.RenderTransform.Value.OffsetY;

                VShift = 0;
                //VlabelsCells.RenderTransform = new TranslateTransform(0, 0); // it litters
                VlabelsTT.Y = 0;
                Canvas.SetTop(VlabelsCells, zooming * (Vo - labelsZo) + labelsZo);
            } // end if (NewVgrid) -------

            if (!RectZoomFlag) ON_MouseOver(zoomOrigin);
        }

        private void AddBounds(int index, Rect Bounds)
        {
            if (Bounds.IsEmpty) return; // >>>>> nothing to add >>>>>

            if (BoundsAdded == 0 || BoundsArr[index].IsEmpty)
            {
                BoundsArr[index] = Bounds;
                BoundsAdded++;
            }
            else
            {
                BoundsArr[index].Union(Bounds);
            }

            Bounds = BoundsArr[index];

            if (BoundsAdded == 1) // then set initial bounds
            {
                bool hasW = Bounds.Width > double.Epsilon;
                bool hasH = Bounds.Height > double.Epsilon;

                if (hasW && hasH)
                {
                    BoundsXmin = Bounds.Left;
                    BoundsXmax = Bounds.Right;
                    BoundsYmin = Bounds.Top; // Y-axis inversion, as in System.Windows.Rect:
                    BoundsYmax = Bounds.Bottom; // Bottom = Top + Height
                }
                else if (hasW && !hasH)
                {
                    BoundsXmin = Bounds.Left;
                    BoundsXmax = Bounds.Right;
                    BoundsYmin = Bounds.Y - 0.5 * Bounds.Width;
                    BoundsYmax = Bounds.Y + 0.5 * Bounds.Width;
                }
                else if (!hasW && hasH)
                {
                    BoundsXmin = Bounds.X - 0.5 * Bounds.Height;
                    BoundsXmax = Bounds.X + 0.5 * Bounds.Height;
                    BoundsYmin = Bounds.Top; // Y-axis inversion, as in System.Windows.Rect:
                    BoundsYmax = Bounds.Bottom; // Bottom = Top + Height
                }
                else // (!hasW && !hasH)
                {
                    double absXmin, absYmin;

                    if (Bounds.X >= 0) absXmin = Bounds.X;
                    else absXmin = -Bounds.X;

                    if (Bounds.Y >= 0) absYmin = Bounds.Y;
                    else absYmin = -Bounds.Y;

                    // X setting
                    if (absXmin > double.Epsilon)
                    {
                        BoundsXmin -= absXmin;
                        BoundsXmax += absXmin;
                    }
                    else
                    {
                        BoundsXmin = 0;
                        BoundsXmax = 10;
                    }

                    // Y setting
                    if (absYmin > double.Epsilon)
                    {
                        BoundsYmin -= absYmin;
                        BoundsYmax += absYmin;
                    }
                    else
                    {
                        BoundsYmin = 0;
                        BoundsYmax = 10;
                    }
                }
            }
            else // expand bounds as necessary
            {
                if (Bounds.Left < BoundsXmin) BoundsXmin = Bounds.Left;
                if (Bounds.Right > BoundsXmax) BoundsXmax = Bounds.Right;

                // keeping in mind that Bottom = Top + Height
                if (Bounds.Top < BoundsYmin) BoundsYmin = Bounds.Top;
                if (Bounds.Bottom > BoundsYmax) BoundsYmax = Bounds.Bottom;
            }
        }

        public void RemoveBounds(int index)
        {
            if (BoundsAdded == 0 || BoundsArr[index].IsEmpty) return; // >>>>> nothing to remove >>>>>

            BoundsArr[index] = Rect.Empty;
            BoundsAdded--;

            if (BoundsAdded > 0) // shrinking;
            {
                BoundsXmin = double.PositiveInfinity;
                BoundsXmax = double.NegativeInfinity;
                BoundsYmin = double.PositiveInfinity; 
                BoundsYmax = double.NegativeInfinity;

                foreach (Rect bound in BoundsArr)
                {
                    if (!bound.IsEmpty)
                    {
                        if (bound.Left < BoundsXmin) BoundsXmin = bound.Left;
                        if (bound.Right > BoundsXmax) BoundsXmax = bound.Right;

                        // Y-axis inversion, as in System.Windows.Rect:
                        // Bottom = Top + Height
                        if (bound.Top < BoundsYmin) BoundsYmin = bound.Top;
                        if (bound.Bottom > BoundsYmax) BoundsYmax = bound.Bottom;
                    }
                } // end of foreach (Rect bound in BoundsArr)

                // checking of that the remaining bounds are not empty
                double bW = BoundsXmax - BoundsXmin;
                double bH = BoundsYmax - BoundsYmin;
                bool hasW = bW > double.Epsilon;
                bool hasH = bH > double.Epsilon;
                
                if (hasW && !hasH)
                {
                    BoundsYmin -= 0.5 * bW;
                    BoundsYmax += 0.5 * bW;
                }
                else if (!hasW && hasH)
                {
                    BoundsXmin -= 0.5 * bH;
                    BoundsXmax += 0.5 * bH;
                }
                else if (!hasW && !hasH)
                {
                    double absXmin, absYmin;

                    if (BoundsXmin >= 0) absXmin = BoundsXmin;
                    else absXmin = -BoundsXmin;

                    if (BoundsYmin >= 0) absYmin = BoundsYmin;
                    else absYmin = -BoundsYmin;

                    // X setting
                    if (absXmin > double.Epsilon)
                    {
                        BoundsXmin -= absXmin;
                        BoundsXmax += absXmin;
                    }
                    else
                    {
                        BoundsXmin = 0;
                        BoundsXmax = 10;
                    }
                    
                    // Y setting
                    if (absYmin > double.Epsilon)
                    {
                        BoundsYmin -= absYmin;
                        BoundsYmax += absYmin;
                    }
                    else
                    {
                        BoundsYmin = 0;
                        BoundsYmax = 10;
                    } 
                }

            } // end of if (BoundsAdded > 0)
        }
        
        // used by DefineBounds and SegmentBound methods
        private Point[][] tmpPoints;
        private Rect[] tmpBounds;
        private bool[] readyFlags;

        public Rect DefineBounds(Point[][] SegmentsPoints)
        {
            int segNum = SegmentsPoints.Length;

            tmpPoints = SegmentsPoints;
            tmpBounds = new Rect[segNum];
            readyFlags = new bool[segNum];

            // ONE THREAD for each segment
            for (int i = 1; i < segNum; i++) ThreadPool.QueueUserWorkItem(new WaitCallback(SegmentBound), i);

            // if there is only one segment provided then it is processed in main thread
            SegmentBound(0);

            // define overall bound for all segment;
            // in System.Windows.Rect: Bottom = Top + Height;
            // the top is Rect.Bottom and the bottom is Rect.Top
            double left = double.PositiveInfinity;
            double right = double.NegativeInfinity;
            double top = double.NegativeInfinity;
            double bottom = double.PositiveInfinity;

            bool[] finishFlags = new bool[segNum];
            bool inProgress = true;

            while (inProgress)
            {
                inProgress = false;

                for (int i = 0; i < segNum; i++)
                {
                    if (readyFlags[i])
                    {
                        if (!finishFlags[i])
                        {
                            finishFlags[i] = true;

                            if (tmpBounds[i].Left < left) left = tmpBounds[i].Left;
                            if (tmpBounds[i].Right > right) right = tmpBounds[i].Right;

                            // in System.Windows.Rect: Bottom = Top + Height
                            if (tmpBounds[i].Top < bottom) bottom = tmpBounds[i].Top;
                            if (tmpBounds[i].Bottom > top) top = tmpBounds[i].Bottom;
                        }
                    }
                    else inProgress = true;
                } // end for (int i = 1; i < segNum; i++)
            }

            return new Rect(left, bottom, right - left, top - bottom);
        }

        private void SegmentBound(object dat)
        {
            int index = (int)dat;
            Point[] Segment = tmpPoints[index];
            
            double left = double.PositiveInfinity;
            double right = double.NegativeInfinity;
            double top = double.NegativeInfinity;
            double bottom = double.PositiveInfinity;

            foreach (Point pt in Segment)
            {
                if (pt.X < left) left = pt.X;
                if (pt.X > right) right = pt.X;

                if (pt.Y < bottom) bottom = pt.Y;
                if (pt.Y > top) top = pt.Y;
            }

            // in System.Windows.Rect: Bottom = Top + Height;
            // the top is Rect.Bottom and the bottom is Rect.Top
            tmpBounds[index] = new Rect(left, bottom, right - left, top - bottom);
            readyFlags[index] = true;
        }

        public bool FitIn(FitinModes mode)
        {
            switch (mode)
            {
                case FitinModes.WH:
                    FitIn();
                    return true;

                case FitinModes.W:
                    double V = outputCanvas.ActualHeight - bottomIndent;
                    double Y = (V - OffsetV) / ratioYtoV;
                    double H = (topIndent - V) / ratioYtoV; // ratioYtoV is less than zero

                    FitIn(Bounds: new Rect(0, Y, 0, H));
                    return true;

                case FitinModes.H:
                    double X = (leftIndent - OffsetU) / ratioXtoU;
                    double W = (outputCanvas.ActualWidth - leftIndent - rightIndent) / ratioXtoU;

                    FitIn(Bounds: new Rect(X, 0, W, 0));
                    return true;

                case FitinModes.Update:
                    double x = (leftIndent - OffsetU) / ratioXtoU;
                    double w = (outputCanvas.ActualWidth - leftIndent - rightIndent) / ratioXtoU;

                    double v = outputCanvas.ActualHeight - bottomIndent;
                    double y = (v - OffsetV) / ratioYtoV;
                    double h = (topIndent - v) / ratioYtoV;

                    FitIn(Bounds: new Rect(x, y, w, h));
                    return true;

                default: return false; // >>>>>>> OFF >>>>>>>
            }
        }

        /// <summary>
        /// Performs fit-in of a geometry specified by the Bounds to the output area specified by the fitSize
        /// </summary>
        /// <param name="Bounds">Geometry bounds defined in the x,y-space</param>
        /// <param name="fitSize">The frame into which you want to fit, defined in u,v-space</param>
        public void FitIn(Rect Bounds = new Rect(), Size fitSize = new Size())
        {
            // ------- Defining of the fit transform --------------------------------------------------------------------------------------
            double Xmin, Xmax, Ymin, Ymax;

            if (Bounds.Width == 0) // use stored horizontal bounds
            {
                Xmin = BoundsXmin;
                Xmax = BoundsXmax;
            }
            else
            {
                Xmin = Bounds.Left;
                Xmax = Bounds.Right;
            }

            if (Bounds.Height == 0)  // use stored vertical bounds
            {
                Ymin = BoundsYmin;
                Ymax = BoundsYmax;
            }
            else
            {
                // in System.Windows.Rect: Bottom = Top + Height
                Ymin = Bounds.Top;
                Ymax = Bounds.Bottom;
            }

            double FitWidth = fitSize.Width;
            double FitHeight = fitSize.Height;

            if (FitWidth == 0) FitWidth = outputCanvas.ActualWidth - leftIndent - rightIndent;
            if (FitHeight == 0) FitHeight = outputCanvas.ActualHeight - bottomIndent - topIndent;

            ratioXtoU = FitWidth / (Xmax - Xmin);
            ratioYtoV = FitHeight / (Ymax - Ymin);

            if (SquareGrid) // select the minimum transition ratio to completely fit the graphs
            {
                if (ratioXtoU < ratioYtoV) ratioYtoV = ratioXtoU;
                else ratioXtoU = ratioYtoV;
            }

            ratioYtoV = -ratioYtoV; // canvas vertical axis has direction from top to bottom

            OffsetU = -Xmin * ratioXtoU + leftIndent;
            OffsetV = outputCanvas.ActualHeight - bottomIndent - Ymin * ratioYtoV;

            GraphMatrix.M11 = ratioXtoU;
            GraphMatrix.M22 = ratioYtoV;
            GraphMatrix.OffsetX = OffsetU;
            GraphMatrix.OffsetY = OffsetV;
            GraphMxTr.Matrix = GraphMatrix;
            foreach (Filtering F in OutFilters) F?.Invoke();

            // ------- Defining of the linear grid step for X-axis ------------------------------------------------------------------------
            UStep = 0.5 * (UStepMin + UStepMax); // define most preferable value for the grid representation in u,v space
            XStep = UStep / ratioXtoU; // conversion to x,y space
            double ordine = Math.Floor(Math.Log10(XStep / XgridBase)); // defining of the order of magnitude

            XStep = XgridBase * Math.Pow(10, ordine); // corrected step value in x,y space
            UStep = XStep * ratioXtoU; // backward conversion to u,v space

            // if UStep is beyond the limits then find suitable factor to place UStep into the range
            if (UStep < UStepMin)
            {
                int Num = XstepFactors.Length - 1;
                XfactorInd = 0;
                double factor = 1;

                while (UStep < UStepMin)
                {
                    UStep *= XstepFactors[XfactorInd];
                    factor *= XstepFactors[XfactorInd];
                    XfactorInd++;

                    if (XfactorInd > Num) break;
                }

                XStep *= factor;

                XfactorInd--; // nullifying of the last increment
                grewXStep = true;
            }
            else if (UStep > UStepMax)
            {
                int Num = XstepFactors.Length - 1;
                XfactorInd = Num;
                double factor = 1;

                while (UStep > UStepMin)
                {
                    UStep /= XstepFactors[XfactorInd];
                    factor /= XstepFactors[XfactorInd];
                    XfactorInd--;

                    if (XfactorInd < 0) break;
                }

                XStep /= factor;

                XfactorInd++; // nullifying of the last increment
                grewXStep = false;
            }

            UStepScaled = UStep;

            // ------- Defining of the linear grid step for Y-axis ------------------------------------------------------------------------
            VStep = 0.5 * (VStepMin + VStepMax); // define most preferable value for the grid representation in u,v space
            YStep = -VStep / ratioYtoV; // conversion to x,y space
            ordine = Math.Floor(Math.Log10(YStep / YgridBase)); // defining of the order of magnitude

            YStep = YgridBase * Math.Pow(10, ordine); // corrected step value in x,y space
            VStep = -YStep * ratioYtoV; // backward conversion to u,v space

            // if UStep is beyond the limits then find suitable factor to place UStep into the range
            if (VStep < VStepMin)
            {
                int Num = YstepFactors.Length - 1;
                YfactorInd = 0;
                double factor = 1;

                while (VStep < VStepMin)
                {
                    VStep *= YstepFactors[YfactorInd];
                    factor *= YstepFactors[YfactorInd];
                    YfactorInd++;

                    if (YfactorInd > Num) break;
                }

                YStep *= factor;

                YfactorInd--; // nullifying of the last increment
                grewYStep = true;
            }
            else if (VStep > VStepMax)
            {
                int Num = YstepFactors.Length - 1;
                YfactorInd = Num;
                double factor = 1;

                while (VStep > VStepMin)
                {
                    VStep /= YstepFactors[YfactorInd];
                    factor /= YstepFactors[YfactorInd];
                    YfactorInd--;

                    if (YfactorInd < 0) break;
                }

                YStep /= factor;

                YfactorInd++; // nullifying of the last increment
                grewYStep = false;
            }

            VStepScaled = VStep;

            // ------- Creating the grid layout -------------------------------------------------------
            // while StepScaled lies within [StepMin, StepMax] 
            // the grid layout is the same and is scaled by means of WPF transform;
            // defining of the required quantity of the grid lines for full covering of the Canvas area

            // ------- U-lines and U-labels -------------------------------------------------------------------------------------------
            int coverNumCurr = (int)Math.Ceiling(MaxWidth / UStep);
            int coverNumMax = (int)Math.Ceiling(coverNumCurr * UStep / UStepMin);
            int coverNumAdd = coverNumMax - coverNumCurr;
            int coverNumTot = coverNumMax + coverNumAdd;

            double Uclosest = Math.Round(Xmin / XStep) * XStep * ratioXtoU + OffsetU;
            double Ustart = Uclosest - coverNumAdd * UStep;
            double Ui = Ustart;

            PathFigure[] LinesU = new PathFigure[coverNumTot];
            UlabelsArr = new Label[coverNumTot];

            UlabelsCells.Children.Clear();
            UlabelsCells.Columns = coverNumTot;
            UlabelsCells.Width = UStep * coverNumTot;
            UlabelsTT.X = 0;
            Canvas.SetLeft(UlabelsCells, Ustart - 0.5 * UStep - UlabelsOffsetX);

            // U-grid calibration
            string unitsLabel;

            labelLeft = (Ustart - OffsetU) / ratioXtoU;

            ordine = Calibrate(XStep, ref labelLeft, out unitsLabel, out UlabelsFormatSpec, XgridBase, unitName: UnitsNameU, AddPrefix: UsePrefixU);

            unitsUlabel.Content = unitsLabel;

            GaugeX = ordine * XStep;
            labelLeft *= ordine;
            double Xgi = labelLeft;

            for (int i = 0; i < coverNumTot; i++) // adding U-grid lines and labels
            {
                LinesU[i] = new PathFigure(new Point(Ui, 0), new LineSegment[1] { new LineSegment(new Point(Ui, MaxHeight), true) }, false);
                Ui += UStep;

                UlabelsArr[i] = new Label();
                UlabelsArr[i].Content = Xgi.ToString(UlabelsFormatSpec);
                UlabelsArr[i].Padding = new Thickness(2);
                UlabelsArr[i].HorizontalAlignment = HorizontalAlignment.Center;
                UlabelsArr[i].VerticalAlignment = VerticalAlignment.Center;
                UlabelsArr[i].Foreground = GridLabelsBrush;
                UlabelsCells.Children.Add(UlabelsArr[i]);
                Xgi += GaugeX;
            }

            UgridLinesGeometry = new PathGeometry(LinesU);
            UgridLines.Data = UgridLinesGeometry;
            UgridMatrix = new Matrix(1, 0, 0, 1, 0, 0);
            UgridMxTr.Matrix = UgridMatrix;
            UgridLines.Data.Transform = UgridMxTr;

            UShift = 0;
            ULeft = Ustart;
            URight = Ui - UStep;
            labelRight = Xgi - GaugeX;
            indLeft = 0;
            indRight = coverNumTot - 1;

            // ------- V-lines and V-labels -------------------------------------------------------------------------------------------
            coverNumCurr = (int)Math.Ceiling(MaxHeight / VStep);
            coverNumMax = (int)Math.Ceiling(coverNumCurr * VStep / VStepMin);
            coverNumAdd = coverNumMax - coverNumCurr;
            coverNumTot = coverNumMax + coverNumAdd;

            double Vclosest = Math.Round(Ymax / YStep) * YStep * ratioYtoV + OffsetV;
            double Vstart = Vclosest - coverNumAdd * VStep;
            double Vi = Vstart;

            PathFigure[] LinesV = new PathFigure[coverNumTot];
            VlabelsArr = new Label[coverNumTot];

            VlabelsCells.Children.Clear();
            VlabelsCells.Rows = coverNumTot;
            VlabelsCells.Height = VStep * coverNumTot;
            VlabelsTT.Y = 0;
            Canvas.SetTop(VlabelsCells, Vstart - 0.5 * VStep - VlabelsOffsetY);

            // V-grid calibration
            labelTop = (Vstart - OffsetV) / ratioYtoV;

            ordine = Calibrate(YStep, ref labelTop, out unitsLabel, out VlabelsFormatSpec, YgridBase, unitName: UnitsNameV, AddPrefix: UsePrefixV);

            unitsVlabel.Content = unitsLabel;
            unitsVlabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetRight(unitsVlabel, VlabelsCells.Width - 0.5 * unitsVlabel.DesiredSize.Width);
            unitsVlabel.RenderTransform = new RotateTransform(-90, 0.5 * unitsVlabel.DesiredSize.Width, 0.5 * unitsVlabel.Height);

            GaugeY = ordine * YStep;
            labelTop *= ordine;
            double Ygi = labelTop;

            for (int i = 0; i < coverNumTot; i++) // adding V-grid lines and labels
            {
                LinesV[i] = new PathFigure(new Point(0, Vi), new LineSegment[1] { new LineSegment(new Point(MaxWidth, Vi), true) }, false);
                Vi += VStep;

                VlabelsArr[i] = new Label();
                VlabelsArr[i].Content = Ygi.ToString(VlabelsFormatSpec);
                VlabelsArr[i].Padding = new Thickness(2, 2, 3, 2);
                VlabelsArr[i].HorizontalAlignment = HorizontalAlignment.Right;
                VlabelsArr[i].VerticalAlignment = VerticalAlignment.Center;
                VlabelsArr[i].Foreground = GridLabelsBrush;
                VlabelsCells.Children.Add(VlabelsArr[i]);
                Ygi -= GaugeY;
            }

            VgridLinesGeometry = new PathGeometry(LinesV);
            VgridLines.Data = VgridLinesGeometry;
            VgridMatrix = new Matrix(1, 0, 0, 1, 0, 0);
            VgridMxTr.Matrix = VgridMatrix;
            VgridLines.Data.Transform = VgridMxTr;

            VShift = 0;
            VTop = Vstart;
            VBottom = Vi - VStep;
            labelBottom = Ygi + GaugeY;
            indTop = 0;
            indBottom = coverNumTot - 1;
        }
    } // end of public class CoordinateGrid /////////////////////////////////////////////////////////////////////////////////
}
