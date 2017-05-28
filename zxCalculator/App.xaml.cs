using System;
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

        private static int funcItemsAdded = 0;
        private static int argsItemsAdded = 0;
        private static FunctionStuff[] funcItems = new FunctionStuff[FunctionsNumber];
        private static ArgumentStuff[] argsItems = new ArgumentStuff[ArgumentsNumber];
        public static FunctionStuff[] FuncItems { get { return funcItems; } }
        public static ArgumentStuff[] ArgsItems { get { return argsItems; } }
        
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
        public static int SerialNumber { get { return SNum++; } }

        private static double[] args = new double[ArgumentsNumber];
        public static double[] Arguments { get { return args; } }

        private static string[] argLabels = new string[ArgumentsNumber];
        public static string[] ArgsLabels { get { return argLabels; } }

        private static int OngoingWorksNum = 0;
        private static bool WorkIsDone = true;
        //private static object _locker_ = new Object(); // lock is not needed due to MarkWorkStart()/...End() can be performed in the same thread

        /// <summary>
        /// Disables the stackArguments
        /// </summary>
        public static void MarkWorkStart()
        {
            //lock(_locker_) 
            {
                OngoingWorksNum++;

                if (WorkIsDone)
                {
                    WorkIsDone = false;
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
                    WorkIsDone = true;
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

        public static void ArgumentChange(double Val, int index, bool increment = true)
        {
            if (index > -1 && index < ArgumentsNumber)
            {
                if (increment) args[index] += Val;
                else args[index] = Val;

                for (int i = 0; i < FunctionsNumber; i++)
                {
                    if (funcItems[i] != null && funcItems[i].AutoUpdate)
                    {
                        funcItems[i].ONclick_bttFunc();
                    }
                }

                argsLabelsSerial = -1; // for updating args labels on click of any function button
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

        private int itemIndex = -1;
        public int Index
        {
            get { return itemIndex; }
            set { if (itemIndex < 0) itemIndex = value; }
        }

        private double increment = 0.125; // for spinners

        private void InputText()
        {
            string strValue = "";
            string strStep = "";
            int splitInd = inputTextBox.Text.IndexOf(':');

            if (splitInd > -1)
            {
                strValue = inputTextBox.Text.Substring(0, splitInd);
                strStep = inputTextBox.Text.Substring(++splitInd);

                double newIncrement = 1;
                if (Double.TryParse(strStep, out newIncrement)) increment = newIncrement;
            }
            else
            {
                strValue = inputTextBox.Text;
            }

            double newValue = 0;
            if (Double.TryParse(strValue, out newValue))
            {
                // Performing calculations with new arguments values
                AppStuff.ArgumentChange(newValue, itemIndex, increment: false);
            }

            inputTextBox.Text = String.Format("{0}", AppStuff.Arguments[itemIndex]);
        }

        private void LostKeyFocus_inputTextBox(object sender, RoutedEventArgs e)
        {
            InputText();
            e.Handled = true;
        }

        private void KeyDown_inputTextBox(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                InputText();
                e.Handled = true;
            }
        }

        private void SpinnerIncrease(object sender, RoutedEventArgs e)
        {
            AppStuff.ArgumentChange(increment, itemIndex);
            inputTextBox.Text = String.Format("{0}", AppStuff.Arguments[itemIndex]);

            e.Handled = true;
        }

        private void SpinnerDecrease(object sender, RoutedEventArgs e)
        {
            AppStuff.ArgumentChange(-increment, itemIndex);
            inputTextBox.Text = String.Format("{0}", AppStuff.Arguments[itemIndex]);

            e.Handled = true;
        }

        public void Loaded_inputTextBox(object sender, RoutedEventArgs e)
        {
            ControlTemplate argInputTmpl = inputTextBox.Template;

            rebttIncrease = argInputTmpl.FindName("rebttIncrease", inputTextBox) as RepeatButton;
            if (rebttIncrease != null) rebttIncrease.Click += SpinnerIncrease; // Event handler for rebttIncrease

            rebttDecrease = argInputTmpl.FindName("rebttDecrease", inputTextBox) as RepeatButton;
            if (rebttDecrease != null) rebttDecrease.Click += SpinnerDecrease; // Event handler for rebttDecrease
        }

        public ArgumentStuff(int index = -1)
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
            inputTextBox.Text = "0";

            // <<<<<<< Style and Repeat Buttons >>>>>>>
            if (AppStuff.txtboxStyleInput != null)
            {
                inputTextBox.Style = AppStuff.txtboxStyleInput;

                inputTextBox.Loaded += Loaded_inputTextBox;
            }

            // <<<<<<< argLabel = new Label(); >>>>>>>
            argLabel = new Label();
            argLabel.Width = 50;
            argLabel.HorizontalContentAlignment = HorizontalAlignment.Center;
            argLabel.Foreground = new SolidColorBrush(Color.FromArgb(255, 150, 150, 150));
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

        public readonly Function Cfunc;
        public readonly TextBox outputTextBox;
        public readonly Button funcBtt;
        public readonly Button Xbtt;
        public readonly Button strokeBtt;
        public readonly DockPanel funcPanel;

        private delegate void UIupdater();

        public readonly int argsNum = 1;
        public int BYTE_SIZE { get { return sizeof(double) * argsNum; } }

        private string label;
        private string description;
        private string[] argsLabels;

        private double[] args = null;
        private double[] argArr = null;
        private int argInd = 0;
        private double outputValue = double.NaN;
        private double[] outputArr = null;
        private string exceptionStr = "";
        private bool exceptionFlag = false;

        public bool AutoUpdate = false;

        public FunctionStuff(ICalculate funcData)
        {
            SerialNumber = AppStuff.SerialNumber;

            Cfunc = funcData.Calculate;

            if (funcData.argsNum > 1) argsNum = funcData.argsNum;
            args = new double[argsNum];

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
            if (AppStuff.txtboxStyleOutput != null) outputTextBox.Style = AppStuff.txtboxStyleOutput;
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

        private void UIupdateTextBox()
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
            funcBtt.IsEnabled = true;
            AppStuff.MarkWorkEnd();
        }

        private void CalcSingle(object state)
        {
            if (argsNum >= AppStuff.ArgumentsNumber)
            {
                Buffer.BlockCopy(AppStuff.Arguments, 0, args, 0, AppStuff.BYTE_SIZE);
            }
            else
            {
                Buffer.BlockCopy(AppStuff.Arguments, 0, args, 0, BYTE_SIZE);
            }
            
            exceptionFlag = false;

            try
            {
                outputValue = Cfunc(args)[0];
            }
            catch (Exception e)
            {
                exceptionFlag = true;
                exceptionStr = e.Message;
            }

            outputTextBox.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new UIupdater(UIupdateTextBox));
        }

        public void ONclick_bttFunc(object sender, RoutedEventArgs e)
        {
            // Disabling UI items
            funcBtt.IsEnabled = false;
            AppStuff.MarkWorkStart();
            AppStuff.ChangeArgsLabels(argsLabels, SerialNumber);

            ThreadPool.QueueUserWorkItem(new WaitCallback(CalcSingle));

            e.Handled = true;
        }

        public void ONclick_bttFunc()
        {
            // Disabling UI items
            funcBtt.IsEnabled = false;
            AppStuff.MarkWorkStart();

            ThreadPool.QueueUserWorkItem(new WaitCallback(CalcSingle));
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
                DLLsPath = Path.Combine(AppPath, "Functions");
                SavesPath = Path.Combine(AppPath, "Output");

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

            // --- TEST STUFF ---------------------------------------------------------------------------------------
            TextBox digidial = windowMain.FindName("txtboxOutput") as TextBox;
            if (digidial != null)
            {
                digidial.Text = String.Format("{0}", 1024 * (AppStuff.testidx + 1));
            }  
        }
    }
}
