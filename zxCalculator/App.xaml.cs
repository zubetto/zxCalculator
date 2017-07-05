﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SWShapes = System.Windows.Shapes;
using System.Windows.Controls.Primitives;
using System.IO;
using System.Security;
using System.Security.Permissions;

namespace zxCalculator
{
    public static class AppStuff
    {
        private static string DLLsPath = "";
        private static string SavesPath = "";
        private static string IOexceptionStr = "";
        private static bool IOexceptionFlag = false;
        public static bool FirstOpening = true;

        public static string OpenPath { get { return DLLsPath; } }
        public static string SavePath { get { return SavesPath; } }
        public static string IOexception
        {
            get
            {
                if (IOexceptionFlag) return IOexceptionStr;
                else return "";
            }
        }

        public static void PasteIOdata()
        {
            App AppCurr = (App)Application.Current;

            DLLsPath = AppCurr.DLLsPath;
            SavesPath = AppCurr.SavesPath;
            IOexceptionStr = AppCurr.IOexceptionStr;
            IOexceptionFlag = AppCurr.IOexceptionFlag;
        }

        public static readonly int FunctionsNumber = 20; // number of available slots for functions items
        public static readonly int ArgumentsNumber = 10; // number of available slots for arguments items
        public static readonly int BYTE_SIZE = ArgumentsNumber * sizeof(double);
        public static readonly long ArgArr_LIMIT = 1 + 1000 * 100; // don't set less than two
        public static int ArgArrDosage = 1000; // each dose is processed in separate thread

        private static int funcItemsAdded = 0;
        private static int argsItemsAdded = 0;
        private static FunctionStuff[] funcItems = new FunctionStuff[FunctionsNumber];
        private static ArgumentStuff[] argsItems = new ArgumentStuff[ArgumentsNumber];
        public static FunctionStuff[] FuncItems { get { return funcItems; } }
        public static ArgumentStuff[] ArgsItems { get { return argsItems; } }

        public static CoordinateGrid Plotter;

        public static StackPanel stackFunctions;
        public static StackPanel stackArguments;
        public static Button bttAddFunction;
        public static Button bttAddArgument;
        public static Button bttRemoveArgument;

        public static Style txtboxStyleOutput;
        public static Style txtboxStyleInput;
        public static Style bttActive;
        public static Style bttDefault;
        public static Style txtboxActive;

        public static SolidColorBrush ActiveCyan;
        public static SolidColorBrush ForeGrey;

        private static int SNum = 0;
        public static int SerialNumber { get { return SNum++; } } // is used to prevent repeated changing of the arguments labels by the same function

        private static double[] args = new double[ArgumentsNumber];
        public static double[] Arguments { get { return args; } }
        
        private static int argInd; // index of argument for which the range of values is defined
        public static int ArgIndex { get { return argInd; } }

        // variables storing the values inputted by user 
        private static double argArrLimA = 0;
        private static double argArrLimB = 10;
        private static double argArrStep = 0.1;
        private static long argArrLength = 101;

        public static long ArgArrayLength { get { return argArrLength; } }

        public static double ArgArrayLimA
        {
            get { return argArrLimA; }

            set
            {
                double band = argArrLimB - value;

                if (band >= argArrStep)
                {
                    argArrLength = (long)Math.Ceiling(1 + band / argArrStep);

                    if (argArrLength > ArgArr_LIMIT)
                    {
                        argArrLimA = argArrLimB - (ArgArr_LIMIT - 1) * argArrStep;
                        argArrLength = ArgArr_LIMIT;
                    } 
                    else argArrLimA = value;
                }
                else
                {
                    argArrLimA = argArrLimB - argArrStep;
                    argArrLength = 2;
                } 
            }
        }

        public static double ArgArrayLimB
        {
            get { return argArrLimB; }

            set
            {
                double band = value - argArrLimA;

                if (band >= argArrStep)
                {
                    argArrLength = (long)Math.Ceiling(1 + band / argArrStep);

                    if (argArrLength > ArgArr_LIMIT)
                    {
                        argArrLimB = argArrLimA + (ArgArr_LIMIT - 1) * argArrStep;
                        argArrLength = ArgArr_LIMIT;
                    } 
                    else argArrLimB = value;
                }
                else
                {
                    argArrLimB = argArrLimA + argArrStep;
                    argArrLength = 2;
                } 
            }
        }

        public static double ArgArrayStep
        {
            get { return argArrStep; }

            set
            {
                if (value > 0)
                {
                    double band = argArrLimB - argArrLimA;

                    if (value <= band)
                    {
                        argArrLength = (long)Math.Ceiling(1 + band / value);

                        if (argArrLength > ArgArr_LIMIT)
                        {
                            argArrStep = band / (ArgArr_LIMIT - 1);
                            argArrLength = ArgArr_LIMIT;
                        } 
                        else argArrStep = value;
                    }
                    else
                    {
                        argArrStep = band;
                        argArrLength = 2;
                    }
                }
            }
        }

        private static bool isRangeAct = false;
        public static bool IsRangeActive { get { return isRangeAct; } }

        private static string[] argLabels = new string[ArgumentsNumber];
        public static string[] ArgsLabels { get { return argLabels; } }

        private static int OngoingWorksNum = 0;
        //private static bool WorkIsDone = true; // redundant
        //private static object _locker_ = new Object(); // lock is not needed due to MarkWorkStart()/...End() can be performed in the same thread

        public static void SetArrIndex(int ind)
        {
            if (argsItems[ind].tgbttAddRange.IsChecked == true)
            {
                isRangeAct = true;
                argInd = ind;

                for (int i = 0; i < ind; i++) // remove range items before ...
                {
                    if (argsItems[i].tgbttAddRange.IsChecked == true)
                    {
                        argsItems[i].tgbttAddRange.IsChecked = false;
                        argsItems[i].AddRangeClick(new object(), new RoutedEventArgs());
                    }
                }

                for (int i = ++ind; i < argsItemsAdded; i++) // ... and after the ind
                {
                    if (argsItems[i].tgbttAddRange.IsChecked == true)
                    {
                        argsItems[i].tgbttAddRange.IsChecked = false;
                        argsItems[i].AddRangeClick(new object(), new RoutedEventArgs());
                    }
                }
            }
            else
            {
                isRangeAct = false;
            }
        }
        
        // redundant - the range limits and step are specified only by spinners at current stage
        public static void SetArgArray(double limA, double limB, double step)
        {
            double band = limB - limA;
            
            // as result of the following correction no big array will be created
            if (band <= 0 || band < step)
            {
                if (step > 0)
                {
                    if (limA != argArrLimA) limA = limB - step;
                    else if (limB != argArrLimB) limB = limA + step;
                    else step = band;
                }
                else return; // >>>>> band and step values are not correct; thus, ignore input >>>>>
            }
            else if (step <= 0)
            {
                step = band;
            }
            else // checking of the arg array capacity limits 
            {
                long num = (long)Math.Ceiling(band / step);

                if (num > ArgArr_LIMIT)
                {
                    num = ArgArr_LIMIT;

                    if (limA != argArrLimA) limA = limB - num * step;
                    else if (limB != argArrLimB) limB = limA + num * step;
                    else step = band / num;
                }
            }

            argArrLimA = limA;
            argArrLimB = limB;
            argArrStep = step;
        }

        /// <summary>
        /// Disables the stackArguments
        /// </summary>
        public static void MarkWorkStart()
        {
            //lock(_locker_) 
            {
                if (OngoingWorksNum++ == 0)
                {
                    //WorkIsDone = false;
                    stackArguments.IsEnabled = false;
                }
            }
        }

        /// <summary>
        /// Enables the stackArguments if all works are done
        /// </summary>
        public static void MarkWorkEnd()
        {
            //lock (_locker_)
            {
                if (--OngoingWorksNum == 0)
                {
                    //WorkIsDone = true;
                    stackArguments.IsEnabled = true;
                }
            }
        }

        private static int argsLabelsSerial = -1;

        public static void ChangeArgsLabels(string[] newLabels, int serial)
        {
            if (serial == argsLabelsSerial) return; // >>>>>>> LABELS ARE ACTUAL >>>>>>>

            argsLabelsSerial = serial;

            int newNum = newLabels.GetLength(0);
            int addNum = 0;
            bool addEmpty = false;

            if (newNum >= argsItemsAdded)
            {
                addNum = argsItemsAdded;
                addEmpty = false;
            }
            else
            {
                addNum = newNum;
                addEmpty = true;
            }

            for (int i = 0; i < addNum; i++)
            {
                argLabels[i] = newLabels[i];
                argsItems[i].argLabel.Content = argLabels[i];
            } 

            if (addEmpty)
            {
                for (int i = addNum; i < argsItemsAdded; i++)
                {
                    argLabels[i] = "_";
                    argsItems[i].argLabel.Content = "_";
                }
            } 
        }

        public static void ArgumentChanged(double Val, int index, bool increment = true)
        {
            if (index > -1 && index < ArgumentsNumber)
            {
                if (increment) args[index] += Val;
                else if (Math.Abs(Val - args[index]) > 1e-100) args[index] = Val;
                else return; // >>>>> To prevent repeated calculation >>>>>

                bool arrRequired = index != argInd;

                foreach (FunctionStuff fitem in funcItems)
                {
                    if (fitem != null && fitem.AutoUpdate)
                    {
                        fitem.ONclick_bttFunc(rangeChanged: arrRequired && argInd < fitem.argsNum);
                    }
                }

                argsLabelsSerial = -1; // for updating args labels on click of any function button
            } 
        }

        public static void RangeChanged()
        {
            foreach (FunctionStuff fitem in funcItems)
            {
                if (fitem != null && fitem.AutoUpdate && fitem.PlottingActive && argInd < fitem.argsNum)
                {
                    fitem.ONclick_bttFunc(argChanged: false);
                }
            }
        }

        public static void AddFunctionItem(FunctionStuff item)
        {
            for (int i = 0; i < FunctionsNumber; i++)
            {
                if (funcItems[i] == null)
                {
                    item.Index = i;
                    funcItems[i] = item;
                    stackFunctions.Children.Insert(funcItemsAdded, item.funcPanel);
                    funcItemsAdded++;

                    if (funcItemsAdded >= FunctionsNumber) bttAddFunction.IsEnabled = false;

                    break;
                }
            }
        }

        public static void RemoveFunctionItem(FunctionStuff item)
        {
            stackFunctions.Children.Remove(item.funcPanel);
            funcItems[item.Index] = null;

            if (funcItemsAdded-- == FunctionsNumber)
            {
                bttAddFunction.IsEnabled = true;
            }
        }

        public static void AddArgumentItem()
        {
            ArgumentStuff item = new ArgumentStuff(argsItemsAdded);
            stackArguments.Children.Insert(argsItemsAdded, item.argPanel);
            argsItems[argsItemsAdded] = item;
            argsItemsAdded++;

            if (argsItemsAdded == 2) bttRemoveArgument.IsEnabled = true;
            if (argsItemsAdded >= ArgumentsNumber) bttAddArgument.IsEnabled = false;

            argsLabelsSerial = -1; // for updating args labels on click of any function button
        }

        /// <summary>
        /// Removes ArgumentStuff at specified index
        /// </summary>
        /// <param name="index">Removes the last ArgumentStuff if index value is negative or exceeds max number of available slots</param>
        public static void RemoveArgumentItem(int index = -1)
        {
            if (argsItemsAdded == ArgumentsNumber) bttAddArgument.IsEnabled = true;
            if (argsItemsAdded == 2) bttRemoveArgument.IsEnabled = false;

            argsItemsAdded--;

            if (index < 0 || index >= ArgumentsNumber) index = argsItemsAdded;

            ArgumentStuff item = argsItems[index];

            stackArguments.Children.Remove(item.argPanel);
            argsItems[index] = null;
            args[index] = 0;

            ArgumentChanged(0, index, false);
        }

        // --- test stuff ----------------------------------------------------
        public static int testidx = 0;
        public static int Idx { get { return testidx++; } }

        public static TextBox testBoxArgs;
        public static TextBox testBoxFunc;
    } // end of public static class AppStuff /////////////////////////////////////////////////////////////////////////////////

    public class ArgumentStuff
    {
        public readonly DockPanel argPanel;
        public readonly TextBox inputTextBox;
        private RepeatButton rebttIncrease;
        private RepeatButton rebttDecrease;
        public readonly ToggleButton tgbttAddRange;
        public readonly Label argLabel;
        private Label rangeLabel;
        private UniformGrid rangeSpinners;

        private int itemIndex = -1;
        public int Index
        {
            get { return itemIndex; }
            set { if (itemIndex < 0) itemIndex = value; }
        }

        private double incSpinnerValue = 0.125; // for argument value spinner
        private double incSpinnerA = 1; 
        private double incSpinnerB = 1;
        private double incSpinnerStep = 0.005;
        
        /// <summary>
        /// Tries to obtain the value and increment from the input string.
        /// </summary>
        /// <param name="input">If it has form of "123.4:0.5", then the value will be asighed to 123.4 and the increment to 0.5</param>
        /// <param name="value">If parsing of the before colon part is failed, then NaN</param>
        /// <param name="increment">If parsing of the after colon part is failed, then NaN</param>
        /// <returns></returns>
        private void InputParser(string input, out double value, out double increment)
        {
            //value = double.NaN;
            increment = double.NaN;

            string strValue = "";
            int splitInd = input.IndexOf(':');

            if (splitInd > -1)
            {
                strValue = input.Substring(0, splitInd);
                string strStep = input.Substring(++splitInd);

                if (!Double.TryParse(strStep, out increment)) increment = double.NaN;
            }
            else
            {
                strValue = input;
            }

            if (!Double.TryParse(strValue, out value)) value = double.NaN;
        }

        private void LostKeyFocus_inputTextBox(object sender, RoutedEventArgs e) // handler for argument value
        {
            double val, incr;

            InputParser(inputTextBox.Text, out val, out incr);

            if (!double.IsNaN(val)) AppStuff.ArgumentChanged(val, itemIndex, increment: false);
            if (!double.IsNaN(incr)) incSpinnerValue = incr;

            inputTextBox.Text = AppStuff.Arguments[itemIndex].ToString();

            e.Handled = true;
        }

        private void KeyDown_inputTextBox(object sender, KeyEventArgs e) // handler for an argument value
        {
            if (e.Key == Key.Enter)
            {
                double val, incr;

                InputParser(inputTextBox.Text, out val, out incr);

                if (!double.IsNaN(val)) AppStuff.ArgumentChanged(val, itemIndex, increment: false);
                if (!double.IsNaN(incr)) incSpinnerValue = incr;

                inputTextBox.Text = AppStuff.Arguments[itemIndex].ToString();

                e.Handled = true;
            }
        }

        private void InputRangeSpinners(TextBox txtBox) // argument values range definition
        {
            double val, incr, buff;

            InputParser(txtBox.Text, out val, out incr);

            switch (txtBox.Name)
            {
                case "SpinnerA":
                    if (!double.IsNaN(incr)) incSpinnerA = incr;

                    if (!double.IsNaN(val))
                    {
                        buff = AppStuff.ArgArrayLimA;
                        AppStuff.ArgArrayLimA = val;

                        if (Math.Abs(buff - AppStuff.ArgArrayLimA) > 1e-100) AppStuff.RangeChanged();
                    }

                    txtBox.Text = AppStuff.ArgArrayLimA.ToString();
                    break;

                case "SpinnerB":
                    if (!double.IsNaN(incr)) incSpinnerB = incr;

                    if (!double.IsNaN(val))
                    {
                        buff = AppStuff.ArgArrayLimB;
                        AppStuff.ArgArrayLimB = val;

                        if (Math.Abs(buff - AppStuff.ArgArrayLimB) > 1e-100) AppStuff.RangeChanged();
                    }

                    txtBox.Text = AppStuff.ArgArrayLimB.ToString();
                    break;

                case "SpinnerStep":
                    if (!double.IsNaN(incr)) incSpinnerStep = incr;

                    if (!double.IsNaN(val))
                    {
                        buff = AppStuff.ArgArrayStep;
                        AppStuff.ArgArrayStep = val;

                        if (Math.Abs(buff - AppStuff.ArgArrayStep) > 1e-100) AppStuff.RangeChanged();
                    }

                    txtBox.Text = AppStuff.ArgArrayStep.ToString();
                    break;
            }
        }

        private void LostKeyFocus_RangeBox(object sender, RoutedEventArgs e) // handler for an argument values range definition
        {
            object o = e.Source;
            TextBox txtBox = e.OriginalSource as TextBox;

            if (txtBox != null)
            {
                InputRangeSpinners(txtBox);
                e.Handled = true; 
            }
        }

        private void KeyDown_RangeBox(object sender, KeyEventArgs e) // handler for an argument values range definition
        {
            if (e.Key == Key.Enter)
            {
                TextBox txtBox = e.Source as TextBox;

                if (txtBox != null)
                {
                    InputRangeSpinners(txtBox);
                    e.Handled = true;
                }
            }
        }

        private void SuppressLostKeyFocus_Spinner(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void SpinnerIncrease(object sender, RoutedEventArgs e) // handler for an argument value spinner
        {
            AppStuff.ArgumentChanged(incSpinnerValue, itemIndex);
            inputTextBox.Text = String.Format("{0}", AppStuff.Arguments[itemIndex]);

            e.Handled = true;
        }

        private void SpinnerDecrease(object sender, RoutedEventArgs e) // handler for an argument value spinner
        {
            AppStuff.ArgumentChanged(-incSpinnerValue, itemIndex);
            inputTextBox.Text = String.Format("{0}", AppStuff.Arguments[itemIndex]);

            e.Handled = true;
        }

        private void UpDownRangeSpinners(object sender, RoutedEventArgs e)
        {
            RepeatButton spinner = e.OriginalSource as RepeatButton;

            if (spinner != null)
            {
                TextBox txtBox = e.Source as TextBox;

                if (txtBox != null)
                {
                    switch (txtBox.Name)
                    {
                        case "SpinnerA":
                            if (spinner.Name == "rebttIncrease") AppStuff.ArgArrayLimA += incSpinnerA;
                            else if (spinner.Name == "rebttDecrease") AppStuff.ArgArrayLimA -= incSpinnerA;

                            txtBox.Text = AppStuff.ArgArrayLimA.ToString();
                            AppStuff.RangeChanged();
                            break;

                        case "SpinnerB":
                            if (spinner.Name == "rebttIncrease") AppStuff.ArgArrayLimB += incSpinnerB;
                            else if (spinner.Name == "rebttDecrease") AppStuff.ArgArrayLimB -= incSpinnerB;

                            txtBox.Text = AppStuff.ArgArrayLimB.ToString();
                            AppStuff.RangeChanged();
                            break;

                        case "SpinnerStep":
                            if (spinner.Name == "rebttIncrease") AppStuff.ArgArrayStep += incSpinnerStep;
                            else if (spinner.Name == "rebttDecrease") AppStuff.ArgArrayStep -= incSpinnerStep;

                            txtBox.Text = AppStuff.ArgArrayStep.ToString();
                            AppStuff.RangeChanged();
                            break;
                    }
                }
            }
        }

        public void Loaded_inputTextBox(object sender, RoutedEventArgs e)
        {
            ControlTemplate argInputTmpl = inputTextBox.Template;

            rebttIncrease = argInputTmpl.FindName("rebttIncrease", inputTextBox) as RepeatButton;
            if (rebttIncrease != null)
            {
                rebttIncrease.LostKeyboardFocus += SuppressLostKeyFocus_Spinner;
                rebttIncrease.Click += SpinnerIncrease; // Event handler for rebttIncrease
            } 

            rebttDecrease = argInputTmpl.FindName("rebttDecrease", inputTextBox) as RepeatButton;
            if (rebttDecrease != null)
            {
                rebttDecrease.LostKeyboardFocus += SuppressLostKeyFocus_Spinner;
                rebttDecrease.Click += SpinnerDecrease; // Event handler for rebttDecrease
            } 
        }

        public void AddRangeClick(object sender, RoutedEventArgs e)
        {
            if (tgbttAddRange.IsChecked == true) // ADD range-items
            {
                AppStuff.SetArrIndex(itemIndex);

                if (rangeSpinners == null) // create new items
                {
                    // <<<<<<< rangeSpinners = new UniformGrid(); >>>>>>>
                    rangeSpinners = new UniformGrid();
                    rangeSpinners.Rows = 1;
                    rangeSpinners.Columns = 3;
                    DockPanel.SetDock(rangeSpinners, Dock.Right);
                    rangeSpinners.Focusable = false;
                    rangeSpinners.KeyDown += KeyDown_RangeBox;
                    rangeSpinners.AddHandler(Button.ClickEvent, new RoutedEventHandler(UpDownRangeSpinners));

                    TextBox spinnerA = new TextBox();
                    spinnerA.Width = 133;
                    spinnerA.Margin = new Thickness(2, 0, 0, 0);
                    spinnerA.Name = "SpinnerA";
                    spinnerA.Text = AppStuff.ArgArrayLimA.ToString();
                    spinnerA.Style = AppStuff.txtboxStyleInput;
                    spinnerA.LostKeyboardFocus += LostKeyFocus_RangeBox;

                    TextBox spinnerStep = new TextBox();
                    spinnerStep.Width = 133;
                    spinnerStep.Margin = new Thickness(2, 0, 0, 0);
                    spinnerStep.Name = "SpinnerStep";
                    spinnerStep.Text = AppStuff.ArgArrayStep.ToString();
                    spinnerStep.Style = AppStuff.txtboxStyleInput;
                    spinnerStep.LostKeyboardFocus += LostKeyFocus_RangeBox;

                    TextBox spinnerB = new TextBox();
                    spinnerB.Width = 133;
                    spinnerB.Margin = new Thickness(2, 0, 0, 0);
                    spinnerB.Name = "SpinnerB";
                    spinnerB.Text = AppStuff.ArgArrayLimB.ToString();
                    spinnerB.Style = AppStuff.txtboxStyleInput;
                    spinnerB.LostKeyboardFocus += LostKeyFocus_RangeBox;

                    rangeSpinners.Children.Add(spinnerA);
                    rangeSpinners.Children.Add(spinnerStep);
                    rangeSpinners.Children.Add(spinnerB);

                    // <<<<<<< new Label(); >>>>>>>
                    rangeLabel = new Label();
                    rangeLabel.Content = "[a, step, b]:";
                    rangeLabel.Foreground = new SolidColorBrush(Color.FromArgb(255, 150, 150, 150)); // ##### CHANGE TO BRUSH! ##### 
                    rangeLabel.HorizontalContentAlignment = HorizontalAlignment.Right;
                    rangeLabel.VerticalContentAlignment = VerticalAlignment.Center;
                    rangeLabel.Padding = new Thickness(2, 0, 0, 2);
                    rangeLabel.Margin = new Thickness(2, 0, 0, 0);
                    DockPanel.SetDock(rangeLabel, Dock.Right);
                }

                argPanel.Children.Insert(2, rangeSpinners); // isert after tgbttAddRange
                argPanel.Children.Insert(3, rangeLabel);

                AppStuff.RangeChanged();
            }
            else // REMOVE range-items
            {
                AppStuff.SetArrIndex(itemIndex); // reset flag for array calcs

                argPanel.Children.Remove(rangeSpinners);
                argPanel.Children.Remove(rangeLabel);
            }
        }

        public ArgumentStuff(int index = -1, double val = 0)
        {
            itemIndex = index;

            // <<<<<<< argPanel = new DockPanel(); >>>>>>>
            argPanel = new DockPanel();
            argPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            argPanel.Height = 25;
            argPanel.LastChildFill = true;
            argPanel.Margin = new Thickness(0, 2, 0, 0);

            // <<<<<<< inputTextBox = new TextBox(); >>>>>>>
            inputTextBox = new TextBox();
            inputTextBox.LostKeyboardFocus += LostKeyFocus_inputTextBox;
            inputTextBox.KeyDown += KeyDown_inputTextBox;
            inputTextBox.Text = val.ToString();

            // <<<<<<< Style and Spinner RepeatButtons >>>>>>>
            if (AppStuff.txtboxStyleInput != null)
            {
                inputTextBox.Style = AppStuff.txtboxStyleInput;

                inputTextBox.Loaded += Loaded_inputTextBox; // adds handlers for the repeat buttons after the inputTextBox loading
            }

            // <<<<<<< argLabel = new Label(); >>>>>>>
            argLabel = new Label();
            argLabel.Width = 50;
            argLabel.HorizontalContentAlignment = HorizontalAlignment.Center;
            argLabel.Foreground = new SolidColorBrush(Color.FromArgb(255, 150, 150, 150)); // ##### CHANGE TO BRUSH! ##### 
            if (!String.IsNullOrWhiteSpace(AppStuff.ArgsLabels[itemIndex]))
            {
                argLabel.Content = AppStuff.ArgsLabels[itemIndex];
            }
            else
            {
                argLabel.Content = String.Format("x{0}", itemIndex);
            }

            // <<<<<<< tgbttAddRange = new ToggleButton(); >>>>>>>
            tgbttAddRange = new ToggleButton();
            tgbttAddRange.Width = 54;
            tgbttAddRange.Margin = new Thickness(2, 0, 0, 0);
            tgbttAddRange.Content = "Range";
            tgbttAddRange.Focusable = false;
            tgbttAddRange.Click += AddRangeClick;

            // Adding control items to the argPanel
            argPanel.Children.Add(argLabel);
            argPanel.Children.Add(tgbttAddRange);
            argPanel.Children.Add(inputTextBox);

            DockPanel.SetDock(argLabel, Dock.Left);
            DockPanel.SetDock(tgbttAddRange, Dock.Right);
        }
    }

    public class FunctionStuff
    {
        public readonly int SerialNumber = 0;
        private int itemIndex = -1;
        public int Index
        {
            get { return itemIndex; }
            set { if (itemIndex < 0) itemIndex = value; }
        }

        public readonly Function Cfunc; // custom function loaded from dll
        public readonly TextBox outputTextBox;
        public readonly Button funcBtt;
        public readonly Button Xbtt;
        public readonly Button strokeBtt;
        public readonly DockPanel funcPanel;

        private delegate void UIupdater(object o);
        public readonly Interrupter InterruptControl = new Interrupter();

        private AnalysisData[] SegmentsData;
        private int segmentsNum = 0;
        private int segmentsCompleted = 0;
        private SWShapes.Rectangle progressBar;
        private LinearGradientBrush progressBarBrush;
        
        public readonly int argsNum = 1;
        public readonly int BYTE_SIZE;

        private string label;
        private string description;
        private string[] argsLabels;

        private double[] args = null;
        private double[] argArr = null;
        private int argInd = 0;
        private double outputValue = double.NaN;
        private double[][] outputSegments = null;
        private double[] outputStitchedArray = null;
        private string exceptionStr = "";
        private bool exceptionFlag = false;
        private bool ArrInProgress = false;

        private double funcMin = double.PositiveInfinity;
        private double funcMax = double.NegativeInfinity;
        public double GlobalMin { get { return funcMin; } }
        public double GlobalMax { get { return funcMax; } }

        public bool AutoUpdate = false;
        public bool PlottingActive = true;

        private void Loaded_outputTextBox(object state, RoutedEventArgs e)
        {
            ControlTemplate outputBoxTmpl = outputTextBox.Template;

            progressBar = outputBoxTmpl.FindName("progressBar", outputTextBox) as SWShapes.Rectangle;

            progressBarBrush = new LinearGradientBrush(new GradientStopCollection(2), new Point(0, 0.5), new Point(1, 0.5));
            progressBarBrush.GradientStops.Add(new GradientStop(Color.FromArgb(127, 0, 160, 255), 0));
            progressBarBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 160, 255), 0));
            //progressBarBrush.GradientStops[0].Offset = 0;
            //progressBarBrush.GradientStops[0].Color = Color.FromArgb(127, 0, 160, 255);
            //progressBarBrush.GradientStops[1].Offset = 0;
            //progressBarBrush.GradientStops[1].Color = Color.FromArgb(0, 0, 160, 255);

            progressBar.Fill = progressBarBrush;
        }

        public FunctionStuff(ICalculate funcData)
        {
            SerialNumber = AppStuff.SerialNumber;

            Cfunc = funcData.Calculate;

            if (funcData.argsNum > 1) argsNum = funcData.argsNum;
            args = new double[argsNum];
            BYTE_SIZE = sizeof(double) * argsNum;

            label = funcData.Label;
            description = funcData.Description;
            argsLabels = funcData.ArgLabels;

            int num = argsLabels.GetLength(0);
            for (int i = 0; i < num; i++)
            {
                if (argsLabels[i].Length > 7) argsLabels[i] = argsLabels[i].Substring(0, 7);
                else if (String.IsNullOrWhiteSpace(argsLabels[i])) argsLabels[i] = String.Format("x{0}", i);
            }

            // <<<<<<< funcPanel = new DockPanel(); >>>>>>>
            funcPanel = new DockPanel();
            funcPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            funcPanel.Height = 25;
            funcPanel.Margin = new Thickness(0, 0, 0, 2);
            funcPanel.LastChildFill = true;

            // <<<<<<< outputTextBox >>>>>>>
            outputTextBox = new TextBox();
            outputTextBox.Style = AppStuff.txtboxStyleOutput;
            outputTextBox.Loaded += Loaded_outputTextBox;
            outputTextBox.MouseDoubleClick += ONmouseDoubleClick_TextBox;

            // <<<<<<< funcBtt >>>>>>>
            funcBtt = new Button();
            funcBtt.Width = 75;
            funcBtt.Height = 25;
            funcBtt.Margin = new Thickness(2, 0, 0, 0);
            funcBtt.FontFamily = new FontFamily("Cambria Math");
            funcBtt.FontSize = 12;
            if (label.Length > 10) funcBtt.Content = label.Substring(0, 10);
            else funcBtt.Content = label;
            funcBtt.Click += ONclick_bttFunc;

            // <<<<<<< Xbtt >>>>>>>
            Xbtt = new Button();
            Xbtt.Width = 25;
            Xbtt.Height = 25;
            Xbtt.Margin = new Thickness(2, 0, 0, 0);
            Xbtt.FontFamily = new FontFamily("Marlett");
            Xbtt.FontSize = 14;
            Xbtt.Content = "r";
            Xbtt.Click += ONclick_bttX;

            // <<<<<<< strokeBtt >>>>>>>
            strokeBtt = new Button();
            strokeBtt.Width = 25;
            strokeBtt.Height = 25;
            strokeBtt.Margin = new Thickness(0, 0, 2, 0);
            strokeBtt.Content = "--";

            // <<<<<<< Adding controls to the funcPanel >>>>>>>
            funcPanel.Children.Add(strokeBtt);
            funcPanel.Children.Add(Xbtt);
            funcPanel.Children.Add(funcBtt);
            funcPanel.Children.Add(outputTextBox);

            DockPanel.SetDock(strokeBtt, Dock.Left);
            DockPanel.SetDock(Xbtt, Dock.Right);
            DockPanel.SetDock(funcBtt, Dock.Right);
        }

        private void ExceptionDuringSegment(object info)
        {
            AppStuff.MarkWorkEnd();

            if (++segmentsCompleted == segmentsNum)
            {
                ArrInProgress = false;

                funcBtt.IsEnabled = true;
                outputTextBox.IsEnabled = true;

                int index = (int)info;
                AnalysisData segData = SegmentsData[index];

                string mbTitle = String.Format("{0}: Errors during the array calcs!", label);

                MessageBox.Show(segData.ExceptionsStrig, mbTitle, MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void MarkSegmentCompletion(object info)
        {
            int index = (int)info;

            AnalysisData segData = SegmentsData[index];
            double segmentMin = segData.Minimum;
            double segmentMax = segData.Maximum;

            if (segmentMin > segmentMax) // then the segmentMin/Max have not been set
            {
                segmentMin = double.PositiveInfinity;
                segmentMax = double.NegativeInfinity;

                foreach (double Y in outputSegments[index])
                {
                    if (Y < segmentMin) segmentMin = Y;
                    if (Y > segmentMax) segmentMax = Y;
                }
            }
            
            if (segmentMin < funcMin) funcMin = segmentMin;
            if (segmentMax > funcMax) funcMax = segmentMax;

            // --- Pass the points to the Plotter ------------------------------------------------------------------------
            Point[] ptArr = segData.PointsArray;
            int num = segData.SegmentLength;

            // checking of whether the PointsArray has been set on-the-fly
            if (ptArr[0].X >= ptArr[num - 1].X) // then NO, it is needed to convert outputSegments[index] to points array
            {
                double[] Yarr = outputSegments[index];
                double x = segData.SegmentLimA;
                double dx = segData.SegmentStep;

                for (int i = 0; i < num; i++)
                {
                    ptArr[i] = new Point(x, Yarr[i]);
                    x += dx;
                }
            }

            // in System.Windows.Rect: Bottom = Top + Height;
            // segmentMin is Rect.Top
            Rect bounds = new Rect(segData.SegmentLimA, segmentMin, segData.SegmentLimB - segData.SegmentLimA, segmentMax - segmentMin);
            AppStuff.Plotter.AddSegment(itemIndex, index, bounds, ptArr);

            // --- Checking of the entire work completion -------------------------------------------------------------------------
            if (++segmentsCompleted < segmentsNum) // IN PROGRESS
            {
                double progress = 1.0 * segmentsCompleted / segmentsNum;

                progressBarBrush.GradientStops[0].Offset = progress;
                progressBarBrush.GradientStops[1].Offset = progress;

                AppStuff.MarkWorkEnd();
            }
            else if (segData.ExceptionsStrig != "") // DONE WITH ERRORS
            {
                ExceptionDuringSegment(index);

                progressBarBrush.GradientStops[0].Offset = 0;
                progressBarBrush.GradientStops[1].Offset = 0;
            }
            else // DONE
            {
                ArrInProgress = false;

                progressBarBrush.GradientStops[0].Offset = 1;
                progressBarBrush.GradientStops[1].Offset = 1;

                progressBar.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UIupdater(UIresetProgessBar), new object()); // animation could be here
                
                // enabling UI items
                AppStuff.MarkWorkEnd();
                funcBtt.IsEnabled = true;
                outputTextBox.IsEnabled = true;
            }
        }

        private void UIresetProgessBar(object o)
        {
            progressBarBrush.GradientStops[0].Offset = 0;
            progressBarBrush.GradientStops[1].Offset = 0;
        }

        private void UIupdateTextBox(object o)
        {
            // Updating UI content
            if (exceptionFlag)
            {
                outputTextBox.Text = String.Format("{0}", exceptionStr);
            }
            else
            {
                outputTextBox.Text = String.Format("{0}", outputValue);
            }

            // Enabling UI items
            if (!ArrInProgress)
            {
                funcBtt.IsEnabled = true;
                outputTextBox.IsEnabled = true;
            }

            AppStuff.MarkWorkEnd();
        }

        /// <summary>
        /// Calculates custom function value at single point
        /// </summary>
        /// <param name="state">not used</param>
        private void CalcSingle(object state)
        {
            try
            {
                outputValue = Cfunc(args)[0];
            }
            catch (Exception e)
            {
                exceptionFlag = true;
                exceptionStr = e.Message;
            }

            outputTextBox.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UIupdater(UIupdateTextBox), new object());
        }

        private void CalcArray(object state)
        {
            int index = (int)state;

            AnalysisData Dat = SegmentsData[index];
            Dat.SetRange();
            
            // calculate custom function on the given range
            try
            {
                outputSegments[index] = Cfunc(Dat.ArgumentSet, 
                                              limA: Dat.SegmentLimA, limB: Dat.SegmentLimB, step: Dat.SegmentStep, argInd: Dat.ArgumentIndex,
                                              signal: InterruptControl, Analyze: Dat);
            }
            catch (Exception e)
            {
                Dat.WriteException(e.ToString(), index);
                progressBar.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UIupdater(ExceptionDuringSegment), index);
                return;
            }

            progressBar.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new UIupdater(MarkSegmentCompletion), index);
        }

        private void IniCalcArray()
        {
            if (ArrInProgress) return; // >>>>>>> >>>>>>>

            ArrInProgress = true;

            funcMin = double.PositiveInfinity;
            funcMax = double.NegativeInfinity;

            int segLength = AppStuff.ArgArrDosage;
            int segNum = (int)(AppStuff.ArgArrayLength / segLength);
            int addLength = (int)(AppStuff.ArgArrayLength % segLength);
            int prevNum = -1;

            segmentsNum = segNum;
            if (addLength != 0) segmentsNum++;

            if (outputSegments != null) prevNum = outputSegments.GetLength(0);

            if (segmentsNum != prevNum) // means that the range has been changed
            {
                outputSegments = new double[segmentsNum][];
                SegmentsData = new AnalysisData[segmentsNum];
            }

            AppStuff.Plotter.AddGraph(itemIndex, segmentsNum, new Rect()); // initialize graph in the Plotter

            argInd = AppStuff.ArgIndex;
            double step = AppStuff.ArgArrayStep;
            double limA = AppStuff.ArgArrayLimA;
            double limB = AppStuff.ArgArrayLimB;
            double segSpan = step * segLength;

            InterruptControl.ResetFlags();
            segmentsCompleted = 0;

            // ONE THREAD for each segment
            for (int i = 0; i < segNum; i++)
            {
                SegmentsData[i] = new AnalysisData(args, BYTE_SIZE, argInd, segLength, limA, double.NaN, step, new Point[segLength]);

                AppStuff.MarkWorkStart();
                ThreadPool.QueueUserWorkItem(new WaitCallback(CalcArray), i);

                limA += segSpan;
            }

            if (addLength != 0)
            {
                SegmentsData[segNum] = new AnalysisData(args, BYTE_SIZE, argInd, addLength, limA, limB, step, new Point[addLength]);

                AppStuff.MarkWorkStart();
                ThreadPool.QueueUserWorkItem(new WaitCallback(CalcArray), segNum);
            }
        }

        public void ONclick_bttFunc(object sender, RoutedEventArgs e)
        {
            // Disabling UI items
            funcBtt.IsEnabled = false;
            outputTextBox.IsEnabled = false;
             
            exceptionFlag = false;
            exceptionStr = "";

            // copying arguments values
            if (argsNum >= AppStuff.ArgumentsNumber)
            {
                Buffer.BlockCopy(AppStuff.Arguments, 0, args, 0, AppStuff.BYTE_SIZE);
            }
            else
            {
                Buffer.BlockCopy(AppStuff.Arguments, 0, args, 0, BYTE_SIZE);
            }

            // START Single Calculation
            AppStuff.MarkWorkStart();
            AppStuff.ChangeArgsLabels(argsLabels, SerialNumber);
            ThreadPool.QueueUserWorkItem(new WaitCallback(CalcSingle));

            // START Array Calculation
            if (PlottingActive && AppStuff.IsRangeActive && AppStuff.ArgIndex < argsNum)
            {
                // Disabling UI items
                funcBtt.IsEnabled = false;
                outputTextBox.IsEnabled = false;

                IniCalcArray();
            }

            e.Handled = true;
        }

        /// <summary>
        /// Is called whenever any argument or range are changed.
        /// </summary>
        /// <param name="argChanged">If true copies the arguments and performs single calculation</param>
        public void ONclick_bttFunc(bool argChanged = true, bool rangeChanged = true) 
        {
            exceptionFlag = false;
            exceptionStr = "";

            if (argChanged) // START Single Calculation
            {
                // Disabling UI items
                funcBtt.IsEnabled = false;
                outputTextBox.IsEnabled = false;

                // copying arguments values
                if (argsNum >= AppStuff.ArgumentsNumber)
                {
                    Buffer.BlockCopy(AppStuff.Arguments, 0, args, 0, AppStuff.BYTE_SIZE);
                }
                else
                {
                    Buffer.BlockCopy(AppStuff.Arguments, 0, args, 0, BYTE_SIZE);
                }

                AppStuff.MarkWorkStart();
                ThreadPool.QueueUserWorkItem(new WaitCallback(CalcSingle));
            }

            if (rangeChanged && PlottingActive && AppStuff.IsRangeActive) // START Array Calculation
            {
                // Disabling UI items
                funcBtt.IsEnabled = false;
                outputTextBox.IsEnabled = false;

                IniCalcArray();
            }
        }

        public void ONmouseDoubleClick_TextBox(object sender, RoutedEventArgs e)
        {
            if (AutoUpdate) // Deactivate
            {
                AutoUpdate = false;
                outputTextBox.Style = AppStuff.txtboxStyleOutput;
                funcBtt.Style = AppStuff.bttDefault;
            }
            else // Activate
            {
                AutoUpdate = true;
                outputTextBox.Style = AppStuff.txtboxActive;
                funcBtt.Style = AppStuff.bttActive;
                ONclick_bttFunc(sender, e);
            }
        }

        public void ONclick_bttX(object sender, RoutedEventArgs e)
        {
            AppStuff.RemoveFunctionItem(this);
            e.Handled = true;
        }
    }
    
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public string DLLsPath;
        public string SavesPath;
        public string IOexceptionStr = "";
        public bool IOexceptionFlag = false;

        void CheckPermission()
        {
            string AppPath = "";

            try
            {
                AppPath = Directory.GetCurrentDirectory();

                FileIOPermission permitRW = new FileIOPermission(FileIOPermissionAccess.Read | FileIOPermissionAccess.Write, AppPath);
                permitRW.Demand();
            }
            catch (SecurityException SE)
            {
                IOexceptionStr = SE.ToString();
                IOexceptionFlag = true;
            }
            catch (Exception E)
            {
                IOexceptionStr += "\n\r-------------------------------------\n\r";
                IOexceptionStr += E.ToString();
                IOexceptionFlag = true;
            }

            if (IOexceptionFlag)
            {
                IOexceptionStr = "Probably, you will not be able to open functions or save data due to:" + IOexceptionStr;
                MessageBox.Show(IOexceptionStr, "Problem with access", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
            else
            {
                DLLsPath = System.IO.Path.Combine(AppPath, "Functions");
                SavesPath = System.IO.Path.Combine(AppPath, "Output");

                if (!Directory.Exists(DLLsPath)) Directory.CreateDirectory(DLLsPath);
                if (!Directory.Exists(SavesPath)) Directory.CreateDirectory(SavesPath);
            }
        }

        void App_Startup(object sender, StartupEventArgs e)
        {
            MainWindow windowMain = new MainWindow();
            windowMain.Show();

            CheckPermission();

            AppStuff.PasteIOdata();
            
            AppStuff.testBoxArgs = windowMain.FindName("txtboxInput") as TextBox;
            AppStuff.testBoxFunc = windowMain.FindName("txtboxOutput") as TextBox;
            AppStuff.stackFunctions = windowMain.FindName("stackFunctions") as StackPanel;
            AppStuff.stackArguments = windowMain.FindName("stackArguments") as StackPanel;
            AppStuff.bttAddFunction = windowMain.FindName("bttAddFunction") as Button;
            AppStuff.bttAddArgument = windowMain.FindName("bttAddArgument") as Button;
            AppStuff.bttRemoveArgument = windowMain.FindName("bttRemoveArgument") as Button;

            AppStuff.txtboxStyleOutput = TryFindResource("TextBoxStyle_DigiOut") as Style;
            AppStuff.txtboxStyleInput = TryFindResource("TextBoxStyle_ArgInput") as Style;
            AppStuff.bttActive = TryFindResource("ButtonActive") as Style;
            AppStuff.bttDefault = TryFindResource(typeof(Button)) as Style;
            AppStuff.txtboxActive = TryFindResource("ActiveDial") as Style;

            AppStuff.ActiveCyan = TryFindResource("SolidColor_ActiveCyan") as SolidColorBrush;
            AppStuff.ForeGrey = TryFindResource("SolidColor_ForeGrey") as SolidColorBrush;

            AppStuff.bttRemoveArgument.IsEnabled = false;

            AppStuff.AddArgumentItem();

            Canvas plotCanvas = windowMain.FindName("MainCanvas") as Canvas;
            AppStuff.Plotter = new CoordinateGrid(windowMain, plotCanvas, AppStuff.FunctionsNumber);
            AppStuff.Plotter.FitIn();//fitSize:new Size(plotCanvas.ActualHeight, plotCanvas.ActualHeight);
            //AppStuff.Plotter.AddGraph(0, 5);

            // --- TEST STUFF ---------------------------------------------------------------------------------------
            TextBox digidial = windowMain.FindName("txtboxOutput") as TextBox;
            if (digidial != null)
            {
                digidial.Text = String.Format("{0}", 1024 * (AppStuff.testidx + 1));
            }
            Rect r = new Rect();
            bool rE = r.IsEmpty;
            myCanvas = windowMain.FindName("MainCanvas") as Canvas;
            
            TranslateTransform newTranslate = new TranslateTransform(0, 10);

            ScaleTransform newScaling = new ScaleTransform(0.2, 0.2, 100, 100);

            myMxTransform = new MatrixTransform(myMatrix);

            SWShapes.Path myGrid = new SWShapes.Path();
            myGrid.Stroke = Brushes.Coral;
            myGrid.StrokeThickness = 0.5;

            LineInd = 1;
            indLeft = 0;
            indRight = LineInd;
            testGridLines = new PathFigure[LineInd + 1];
            posLeft = 100;
            posRight = 140;
            testGridLines[0] = new PathFigure(new Point(posLeft, 0), new LineSegment[1] { new LineSegment(new Point(posLeft, 800), true) }, false);
            testGridLines[1] = new PathFigure(new Point(posRight, 0), new LineSegment[1] { new LineSegment(new Point(posRight, 800), true) }, false);

            testGrid = new PathGeometry(testGridLines);
            myGrid.Data = testGrid;
            Canvas.SetZIndex(myGrid, -100);

            //PathFigureCollection myPF = testGrid.Figures;
            //myPF[1] = new PathFigure(new Point(200, 0), new LineSegment[1] { new LineSegment(new Point(200, 800), true) }, false);

            SWShapes.Path myPath = new SWShapes.Path();
            myPath.Stroke = testBrush;
            myPath.StrokeThickness = 2;
            
            double ArgX = 0;
            double omega = (1.0/64)*Math.PI;
            double amp = 50;

            for (int i = 0; i < 400; i++)
            {
                PointsArr[i] = new Point(ArgX, amp * Math.Sin(omega * ArgX));
                ArgX += 0.5;
            }

            PolyLineSegment FuncSegments = new PolyLineSegment(PointsArr, true);
            PathFigure myPathFigure = new PathFigure(new Point(0, 0), new PathSegment[1] { FuncSegments }, false);
            myPathFigure.IsFilled = false;

            myPathGeom = new PathGeometry(new PathFigure[1] { myPathFigure } );
            myPath.Data = myPathGeom;
            myMxTransform.Matrix = myMatrix;
            myPathGeom.Transform = myMxTransform;

            myLabel = new Label();
            myLabel.Foreground = Brushes.AliceBlue;
            myLabel.Content = "0.0";
            Canvas.SetLeft(myLabel, 50);
            Canvas.SetTop(myLabel, 30);

            myLabelII = new Label();
            myLabelII.Foreground = Brushes.Chartreuse;
            myLabelII.Content = String.Format("{0}x{1}", SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
            Canvas.SetLeft(myLabelII, 50);
            Canvas.SetTop(myLabelII, 42);

            myLabelUV = new Label();
            myLabelUV.Foreground = Brushes.Bisque;
            myLabelUV.Content = "UV:";
            Canvas.SetLeft(myLabelUV, 210);
            Canvas.SetTop(myLabelUV, 30);

            myLabelXY = new Label();
            myLabelXY.Foreground = Brushes.Bisque;
            myLabelXY.Content = "XY:";
            Canvas.SetLeft(myLabelXY, 210);
            Canvas.SetTop(myLabelXY, 42);

            //myLabelUShift = new Label();
            //myLabelUShift.Foreground = Brushes.Fuchsia;
            //myLabelUShift.Content = "0";
            //Canvas.SetLeft(myLabelUShift, 50);
            //Canvas.SetTop(myLabelUShift, 120);
            //myCanvas.Children.Add(myLabelUShift);

            //myCanvas.Children.Add(myPath);
            //myCanvas.Children.Add(myGrid);
            //myCanvas.Children.Add(myLabel);
            //myCanvas.Children.Add(myLabelII);
            //myCanvas.Children.Add(myLabelUV);
            //myCanvas.Children.Add(myLabelXY);

            myCanvas.MouseWheel += MouseWheel_Canvas;
            myCanvas.MouseLeftButtonDown += MLBdown_Canvas;
            windowMain.PreviewMouseMove += MouseMove_Global;
            windowMain.PreviewMouseLeftButtonUp += MLBup;

            //myLabel.LayoutTransform = newScaling;
        }

        // --- TEST STUFF ----------------------------
        PathGeometry testGrid = null;
        PathFigure[] testGridLines;
        PathGeometry myPathGeom = null;
        Point[] PointsArr = new Point[400];
        MatrixTransform myMxTransform;
        Matrix myMatrix = new Matrix(1, 0, 0, -1, 0, 100);
        Matrix gridMatrix = new Matrix(1, 0, 0, 1, 0, 0);
        Point MouseIniPoint = new Point();
        Canvas myCanvas = null;
        Label myLabel = null;
        Label myLabelII = null;
        SolidColorBrush testBrush = new SolidColorBrush(Color.FromArgb(255, 170, 170, 170));
        public Label myLabelXY = null;
        public Label myLabelUV = null;
        public Label myLabelUShift = null;
        bool MLBdownAtCanvas = false;
        double Zooming = 1;
        double Xoffset = 0;
        double posLeft, posRight;
        int LineInd, indLeft = 0, indRight = 1;

        void MLBdown_Canvas(object sender, MouseButtonEventArgs e)
        {
            MLBdownAtCanvas = true;
            MouseIniPoint = e.GetPosition(myCanvas);

            App myApp = Application.Current as App;
            myApp.MainWindow.CaptureMouse();
        }

        void MLBup(object sender, MouseButtonEventArgs e)
        {
            MLBdownAtCanvas = false;

            App myApp = Application.Current as App;
            myApp.MainWindow.ReleaseMouseCapture();
        }

        void MouseMove_Global(object sender, MouseEventArgs e)
        {
            if (MLBdownAtCanvas)
            {
                Point currPoint = e.GetPosition(myCanvas);
                double dx = currPoint.X - MouseIniPoint.X;
                double dy = currPoint.Y - MouseIniPoint.Y;
                
                myMatrix.OffsetX += dx;
                myMatrix.OffsetY += dy;

                myMxTransform.Matrix = myMatrix;
                //myPathGeom.Transform = new MatrixTransform(myMatrix);

                Xoffset += dx;
                gridMatrix.OffsetX += dx;
                testGrid.Transform = new MatrixTransform(gridMatrix);

                if (Xoffset > 40)
                {
                    posLeft -= 40;
                    posRight -= 40;

                    testGrid.Figures[indRight] = new PathFigure(new Point(posLeft, 0), new LineSegment[1] { new LineSegment(new Point(posLeft, 800), true) }, false);

                    if (--indLeft < 0) indLeft = LineInd;
                    if (--indRight < 0) indRight = LineInd;

                    Xoffset = 0;
                }
                else if (Xoffset < -40)
                {
                    posLeft += 40;
                    posRight += 40;

                    testGrid.Figures[indLeft] = new PathFigure(new Point(posRight, 0), new LineSegment[1] { new LineSegment(new Point(posRight, 800), true) }, false);

                    if (++indLeft > LineInd) indLeft = 0;
                    if (++indRight > LineInd) indRight = 0;

                    Xoffset = 0;
                }

                myLabel.Content = String.Format("{0:F2},{1:F2}", dx, dy);
                //myLabelII.Content = String.Format("{0}", currPoint);

                MouseIniPoint = currPoint;
            }
        }

        void MouseWheel_Canvas(object sender, MouseWheelEventArgs e)
        {
            double zoomCoeff = Math.Pow(2, 0.125);
            int steps = e.Delta / 120;
            double zooming = Math.Pow(zoomCoeff, steps);

            //if (steps < 0) zooming = -1/zooming;

            Point zoomOrigin = e.GetPosition(myCanvas);

            myMatrix.M11 *= zooming;
            myMatrix.M22 *= zooming;
            myMatrix.OffsetX = zooming * (myMatrix.OffsetX - zoomOrigin.X) + zoomOrigin.X;
            myMatrix.OffsetY = zooming * (myMatrix.OffsetY - zoomOrigin.Y) + zoomOrigin.Y;

            myMxTransform.Matrix = myMatrix;
            //myPathGeom.Transform = new MatrixTransform(myMatrix);

            myLabelII.Content = String.Format("{0}", myMatrix.M11);
        }
    }
}
