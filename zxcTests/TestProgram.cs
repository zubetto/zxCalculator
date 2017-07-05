using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace zxcTests
{
    public class testMinMaxParallel
    {
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
            for (int i = 1; i < segNum; i++)
            {
                // ### TEST ###
                Console.WriteLine("START - {0}", i);

                ThreadPool.QueueUserWorkItem(new WaitCallback(SegmentBound), i);
            }

            // ### TEST ###
            Console.WriteLine("START - 0");

            // if there is only one segment provided then it is processed in main thread
            SegmentBound(0);

            // define overall bound for all segment
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

                            if (tmpBounds[i].Top < bottom) bottom = tmpBounds[i].Top;
                            if (tmpBounds[i].Bottom > top) top = tmpBounds[i].Bottom;

                            // ### TEST ###
                            string state = "";
                            foreach (bool bit in readyFlags)
                            {
                                if (bit) state += "1";
                                else state += "0";
                            }

                            Rect Bounds = tmpBounds[i];
                            string intmBounds = String.Format("bounds: \t Left:{0:f2} \t Top:{1:f2} \t Right:{2:f2} \t Bottom:{3:f2}",
                                                               Bounds.Left, Bounds.Bottom, Bounds.Right, Bounds.Top);
                            Console.WriteLine("END - {0}-{1} ; Status: {2} ; {3}", i, tmpPoints[i].GetLength(0), state, intmBounds);
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

                //Thread.Sleep(1); // ### TEST ###
            }
            
            tmpBounds[index] = new Rect(left, bottom, right - left, top - bottom);
            readyFlags[index] = true;
        }
    } // end of public class testMinMaxParallel //////////////////////////////////////////////////////////////////////////

    class TestProgram
    {
        static Point[][] generatePoints(int num, int length, bool variLength = false)
        {
            Point[][] ptArrs = new Point[num][];
            Point[] pts;
            int rLen;
            Random rnd = new Random();

            for (int i = 0; i < num; i++)
            {
                rLen = length;

                if (variLength) rLen *= rnd.Next(1, 100);

                pts = new Point[rLen];

                for (int j = 0; j < rLen; j++)
                {
                    pts[j] = new Point(1000 * rnd.NextDouble() - 1000 * rnd.NextDouble(), 
                                       1000 * rnd.NextDouble() - 1000 * rnd.NextDouble());
                }

                ptArrs[i] = pts;
            }

            return ptArrs;
        }

        static void Main(string[] args)
        {
            ConsoleKeyInfo ki = new ConsoleKeyInfo();

            testMinMaxParallel Test001 = new testMinMaxParallel();
            Point[][] ptArrs;
            Rect Bounds;
            
            while (ki.Key != ConsoleKey.Escape)
            {
                ptArrs = generatePoints(10, 100, variLength: true);
                Console.WriteLine("points are ready >>> go >>>\n\r");
                Console.ReadKey();

                Bounds = Test001.DefineBounds(ptArrs);

                Console.WriteLine("\n\r------- Overall -------\n\r");
                Console.WriteLine("Left:{0:f2} ; Top:{1:f2} ; Right:{2:f2} ; Bottom:{3:f2}",
                               Bounds.Left, Bounds.Bottom, Bounds.Right, Bounds.Top);
                Console.WriteLine("\n\r------- Ecsape to exit -------\n\r");

                ki = Console.ReadKey();
            }
        }
    }
}
