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


namespace zxCalculator
{
    /// <summary>
    /// (x,y) is used for the real grid coordinates
    /// (u,v) is used for the WPF canvas coordinates
    /// </summary>
    public class CoordinateGrid
    {
        private MainWindow windowMain;
        private Canvas outputCanvas;

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

        private bool AutoFit = true;

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

        // Graphs
        private SWShapes.Path[] GraphPaths;
        public SWShapes.Path[] FunctionGraphs { get { return GraphPaths; } }

        private SWShapes.Path UgridLines = new SWShapes.Path();
        private SWShapes.Path VgridLines = new SWShapes.Path();
        private PathGeometry UgridLinesGeometry;
        private PathGeometry VgridLinesGeometry;
        public SolidColorBrush GridBrush = new SolidColorBrush(Color.FromArgb(170, 150, 233, 233));

        private SWShapes.Path GridMat = new SWShapes.Path();
        private CombinedGeometry GridMatFrame;
        public SolidColorBrush GridMatBrush = new SolidColorBrush(Color.FromArgb(170, 50, 50, 50));
        public SolidColorBrush GridBorderBrush = new SolidColorBrush(Color.FromArgb(255, 150, 233, 233));

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
        public SolidColorBrush GridLabelsBrush = new SolidColorBrush(Color.FromArgb(255, 150, 233, 233));

        private string UlabelsFormatSpec = "";
        private string VlabelsFormatSpec = "";

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

        private bool MLBflag = false;
        private Point MouseIniPoint;

        public CoordinateGrid(MainWindow mainwin, Canvas canvas, int slots, Thickness padding = new Thickness())
        {
            windowMain = mainwin;
            outputCanvas = canvas;
            outputCanvas.Cursor = Cursors.Cross;

            // --- graphs num ----------------------------------
            GraphPaths = new SWShapes.Path[slots];
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

            // --- subscribe on events -------------------------
            outputCanvas.SizeChanged += CanvasSizeChanged;
            outputCanvas.MouseWheel += MouseWheel;
            outputCanvas.MouseLeftButtonDown += MLBdown;
            outputCanvas.MouseMove += ON_MouseOver;

            windowMain.PreviewMouseMove += MLBholdMove;
            windowMain.PreviewMouseLeftButtonUp += MLBup;

            // --- ini fiealds ---------------------------------
            zoomBase = UStepMax / UStepMin;
            zoomFactor = Math.Pow(zoomBase, 1.0 / zoomDetents);
        }

        public void AddGraph(int index, int segmentsNum, Rect Bounds, Point[][] SegmentsPoints = null)
        {
            if (BoundsAdded == GraphPaths.GetLength(0)) return; // >>>>> GraphPaths is already full >>>>>
            
            SWShapes.Path newGraph;
            PathGeometry pGeom;
            PathFigure pFigure;
            PathSegment[] Segments;

            if (GraphPaths[index] == null)
            {
                newGraph = new SWShapes.Path();
                GraphPaths[index] = newGraph;
                SetGraph(index);

                pGeom = new PathGeometry();
                pFigure = new PathFigure();

                newGraph.Data = pGeom;
                pGeom.Transform = GraphMxTr;
                pFigure.IsFilled = false;
                pGeom.Figures = new PathFigureCollection(1);
                pGeom.Figures.Add(pFigure);

                outputCanvas.Children.Add(newGraph);
            }
            else
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
                segmentsNum = SegmentsPoints.GetLength(0);
                Segments = new PathSegment[segmentsNum];

                for (int i = 0; i < segmentsNum; i++)
                {
                    Segments[i] = new PolyLineSegment(SegmentsPoints[i], true);
                }

                pFigure.StartPoint = SegmentsPoints[0][0];
                pFigure.Segments = new PathSegmentCollection(Segments);

                if (Bounds.IsEmpty) Bounds = DefineBounds(SegmentsPoints);

                AddBounds(index, Bounds);

                if (AutoFit) FitIn();
            } // end if (SegmentsPoints == null)
        }

        public void AddSegment(int indGraph, int indSegment, Rect Bounds, Point[] pointsArr)
        {
            if (GraphPaths[indGraph] == null) return; // >>>>> >>>>>

            PathGeometry pGeom = GraphPaths[indGraph].Data as PathGeometry;
            PathSegmentCollection pSegs = pGeom.Figures[0].Segments;

            pSegs[indSegment] = new PolyLineSegment(pointsArr, true);

            // adjusting previous invisible segment
            if (indSegment == 0)
            {
                pGeom.Figures[0].StartPoint = pointsArr[0];
            }
            else if (!pSegs[--indSegment].IsStroked)
            {
                LineSegment LSeg = pSegs[indSegment] as LineSegment; // the AddGraph method initializes path with invisible LineSegments
                LSeg.Point = pointsArr[0];
            }

            if (Bounds.IsEmpty) Bounds = DefineBounds(new Point[1][] { pointsArr });

            AddBounds(indGraph, Bounds);

            if (AutoFit) FitIn();
        }

        public void HideGraph(int index)
        {
            //GraphsAdded--;
            //isDrawnFlags[index] = false;
        }

        public void UnhideGraph(int index)
        {
            //GraphsAdded++;
            //isDrawnFlags[index] = true;
        }

        public void SetGraph(int index) // set stroke, brush
        {
            GraphPaths[index].Stroke = Brushes.BlanchedAlmond;
            GraphPaths[index].StrokeThickness = 2;
        }

        public void RemoveGraph(int index)
        {
            if (BoundsAdded == 0) return; // >>>>> GraphPaths is already empty >>>>>

            outputCanvas.Children.Remove(GraphPaths[index]);
            GraphPaths[index] = null;
            RemoveBounds(index);
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

                UnitsLabel = String.Format("x {0} {1}{2}", mStr, SIprefixes[ind], unitName);

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

                if (mStr != "" || eStr != "" || unitName != "") UnitsLabel = String.Format("x {0}{1} {2}", mStr, eStr, unitName);

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
        }

        private void MLBdown(object sender, MouseButtonEventArgs e)
        {
            MLBflag = true;
            MouseIniPoint = e.GetPosition(outputCanvas);
            windowMain.CaptureMouse();
            windowMain.Cursor = Cursors.ScrollAll;
        }

        private void MLBup(object sender, MouseButtonEventArgs e)
        {
            MLBflag = false;
            windowMain.ReleaseMouseCapture();
            windowMain.Cursor = Cursors.Arrow;
        }

        private void ON_MouseOver(object sender, MouseEventArgs e)
        {
            Point currPoint = e.GetPosition(outputCanvas);

            // TESTs ------------------------------------
            App myApp = Application.Current as App;
            myApp.myLabelUV.Content = String.Format("UV: {0,5:n1} | {1,5:n1}", currPoint.X, currPoint.Y);

            double x = (currPoint.X - OffsetU) / ratioXtoU;
            double y = (currPoint.Y - OffsetV) / ratioYtoV;

            myApp.myLabelXY.Content = String.Format("XY: {0,5:n1} | {1,5:n1}", x, y);
        }

        private void MLBholdMove(object sender, MouseEventArgs e)
        {
            Point currPoint = e.GetPosition(outputCanvas);

            if (MLBflag)
            {
                double du = currPoint.X - MouseIniPoint.X;
                double dv = currPoint.Y - MouseIniPoint.Y;

                OffsetU += du;
                OffsetV += dv;
                GraphMatrix.OffsetX = OffsetU;
                GraphMatrix.OffsetY = OffsetV;
                GraphMxTr.Matrix = GraphMatrix;

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
                    int pendex = UlabelsArr.GetLength(0) - 1;

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
                    int pendex = VlabelsArr.GetLength(0) - 1;

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
                int endX = XstepFactors.GetLength(0) - 1;
                int endY = YstepFactors.GetLength(0) - 1;

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
                int endX = XstepFactors.GetLength(0) - 1;
                int endY = YstepFactors.GetLength(0) - 1;

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

                double ordine = Calibrate(XStep, ref labelLeft, out unitsLabel, out UlabelsFormatSpec, XgridBase, unitName: "Zu");

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

                double ordine = Calibrate(YStep, ref labelTop, out unitsLabel, out VlabelsFormatSpec, YgridBase, unitName: "Zu");

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
        }

        private void AddBounds(int index, Rect Bounds)
        {
            if (Bounds.IsEmpty) return; // >>>>> nothing to add >>>>>

            if (BoundsArr[index].IsEmpty)
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

        private void RemoveBounds(int index)
        {
            if (BoundsAdded == 0 || BoundsArr[index].IsEmpty) return; // >>>>> nothing to remove >>>>>

            BoundsArr[index] = Rect.Empty;
            BoundsAdded--;

            if (BoundsAdded > 0) // shrinking;
            {
                bool iniFlag = true;

                foreach (Rect bound in BoundsArr)
                {
                    if (!bound.IsEmpty)
                    {
                        if (iniFlag)
                        {
                            BoundsXmin = bound.Left;
                            BoundsXmax = bound.Right;
                            BoundsYmin = bound.Top; // Y-axis inversion, as in System.Windows.Rect:
                            BoundsYmax = bound.Bottom; // Bottom = Top + Height

                            iniFlag = false;
                        }
                        else
                        {
                            if (bound.Left < BoundsXmin) BoundsXmin = bound.Left;
                            else if (bound.Right > BoundsXmax) BoundsXmax = bound.Right;

                            // keeping in mind that Bottom = Top + Height
                            if (bound.Top < BoundsYmin) BoundsYmin = bound.Top;
                            else if (bound.Bottom > BoundsYmax) BoundsYmax = bound.Bottom;
                        }
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

        private void SetBoundsRDNT(Rect Bounds, bool addition = true)
        {
            if (addition)
            {
                if (BoundsAdded == 0) // then set initial bounds
                {
                    BoundsAdded++;

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
                        BoundsXmin = 0;
                        BoundsXmax = 2 * Bounds.X;
                        BoundsYmin = 0;
                        BoundsYmax = 2 * Bounds.Y;
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
            else if (BoundsAdded > 1) // shrinking;
            {
                BoundsAdded--;

                bool iniFlag = true;

                foreach (Rect bound in BoundsArr)
                {
                    if (!bound.IsEmpty)
                    {
                        if (iniFlag)
                        {
                            BoundsXmin = bound.Left;
                            BoundsXmax = bound.Right;
                            BoundsYmin = bound.Top; // Y-axis inversion, as in System.Windows.Rect:
                            BoundsYmax = bound.Bottom; // Bottom = Top + Height

                            iniFlag = false;
                        }
                        else
                        {
                            if (bound.Left < BoundsXmin) BoundsXmin = bound.Left;
                            else if (bound.Right > BoundsXmax) BoundsXmax = bound.Right;

                            // keeping in mind that Bottom = Top + Height
                            if (bound.Top < BoundsYmin) BoundsYmin = bound.Top;
                            else if (bound.Bottom > BoundsYmax) BoundsYmax = bound.Bottom;
                        }
                    }
                } // end of foreach (Rect bound in BoundsArr)
            }
        }

        // used by DefineBounds and SegmentBound methods
        private Point[][] tmpPoints;
        private Rect[] tmpBounds;
        private bool[] readyFlags;

        public Rect DefineBounds(Point[][] SegmentsPoints)
        {
            int segNum = SegmentsPoints.GetLength(0);

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

        /// <summary>
        /// 
        /// </summary>
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

            // ------- Defining of the linear grid step for X-axis ------------------------------------------------------------------------
            UStep = 0.5 * (UStepMin + UStepMax); // define most preferable value for the grid representation in u,v space
            XStep = UStep / ratioXtoU; // conversion to x,y space
            double ordine = Math.Floor(Math.Log10(XStep / XgridBase)); // defining of the order of magnitude

            XStep = XgridBase * Math.Pow(10, ordine); // corrected step value in x,y space
            UStep = XStep * ratioXtoU; // backward conversion to u,v space

            // if UStep is beyond the limits then find suitable factor to place UStep into the range
            if (UStep < UStepMin)
            {
                int Num = XstepFactors.GetLength(0) - 1;
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
                int Num = XstepFactors.GetLength(0) - 1;
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
                int Num = YstepFactors.GetLength(0) - 1;
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
                int Num = YstepFactors.GetLength(0) - 1;
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

            ordine = Calibrate(XStep, ref labelLeft, out unitsLabel, out UlabelsFormatSpec, XgridBase, unitName: "Zu");

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

            ordine = Calibrate(YStep, ref labelTop, out unitsLabel, out VlabelsFormatSpec, YgridBase, unitName: "Zu");

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
