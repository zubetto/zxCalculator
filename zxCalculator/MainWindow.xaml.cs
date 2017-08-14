using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace zxCalculator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ONclick_bttAddFunction(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlgOpenFile = new Microsoft.Win32.OpenFileDialog();
            dlgOpenFile.Multiselect = true;
            dlgOpenFile.DefaultExt = ".dll";
            dlgOpenFile.Filter = "Application extension (.dll)|*.dll";

            string[] files = null;
            string errors = ""; // errors during loading of dlls; or number of suitable types exceeds max number of available function slots

            int indexAdd = 0;
            int indexErr = 0;
            bool loadFlag = false;
            bool fullFlag = false;
            bool dlgResult = false;

            ICalculate[] functionsData = new ICalculate[AppStuff.FunctionsNumber];

            try // [[[[[[[ Showing OpenFile Dilog ]]]]]]]
            {
                if (AppStuff.FirstOpening && AppStuff.IOexception == "") dlgOpenFile.InitialDirectory = AppStuff.OpenPath;
                
                if (dlgOpenFile.ShowDialog() == true)
                {
                    dlgResult = true;

                    files = dlgOpenFile.FileNames;

                    int num = files.Length;

                    Assembly calcDLL;
                    Type[] types;

                    // --- files loop ---------------------------------------------------------------------------
                    for (int i = 0; i < num; i++)
                    {
                        calcDLL = Assembly.LoadFrom(files[i]);
                        types = calcDLL.GetTypes();

                        loadFlag = false;

                        // --- types loop ------------------
                        foreach (Type tp in types)
                        {
                            if (indexAdd >= AppStuff.FunctionsNumber)
                            {
                                errors += "Perhaps, not all functions have been loaded due to absence of free slots!";
                                fullFlag = true;
                                break;
                            }
                               
                            if (tp.GetInterface("zxCalculator.ICalculate") != null)
                            {
                                functionsData[indexAdd] = calcDLL.CreateInstance(tp.FullName) as ICalculate;

                                if (functionsData[indexAdd] != null)
                                {
                                    loadFlag = true;
                                    indexAdd++;
                                }
                            }
                        } // --- end of types loop ---------

                        if (fullFlag) break;

                        if (!loadFlag)
                        {
                            errors += String.Format("{0}. {1}: The ICalculate NOT FOUND\n\n\r", indexErr++, System.IO.Path.GetFileName(files[i]) );
                        }
                    } // --- end of files loop -------------------------------------------------------------------
                }
            }
            catch (Exception exc)
            {
                errors = String.Format("{0}\n\n\r{1}", exc.ToString(), errors);
            }

            if (indexAdd > 0)
            {
                for (int i = 0; i < indexAdd; i++) AppStuff.AddFunctionItem(new FunctionStuff(functionsData[i]));

                if (AppStuff.FirstOpening) AppStuff.FirstOpening = false;
            }
            else if (errors != "")
            {
                MessageBox.Show(errors, "Issues during loading", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
            else if (dlgResult)
            {
                MessageBox.Show("Unexpected error!", "Issues during loading", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
            
            e.Handled = true;
        }
        
        private void bttAddArgument_Click(object sender, RoutedEventArgs e)
        {
            AppStuff.AddArgumentItem();
            e.Handled = true;
        }

        private void bttRemoveArgument_Click(object sender, RoutedEventArgs e)
        {
            AppStuff.RemoveArgumentItem();
            e.Handled = true;
        }

        private void bttAutoFit_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton tggBtt = e.Source as ToggleButton;

            if (tggBtt.IsChecked == true)
            {
                bttAutoFitW.IsChecked = false;
                bttAutoFitH.IsChecked = false;

                AppStuff.Plotter.AutoFit = CoordinateGrid.FitinModes.WH;
                AppStuff.Plotter.FitIn(CoordinateGrid.FitinModes.WH);
            } 
            else AppStuff.Plotter.AutoFit = CoordinateGrid.FitinModes.Off;
        }

        private void bttAutoFitW_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton tggBtt = e.Source as ToggleButton;

            if (tggBtt.IsChecked == true)
            {
                bttAutoFit.IsChecked = false;
                bttAutoFitH.IsChecked = false;

                AppStuff.Plotter.AutoFit = CoordinateGrid.FitinModes.W;
                AppStuff.Plotter.FitIn(CoordinateGrid.FitinModes.W);
            }
            else AppStuff.Plotter.AutoFit = CoordinateGrid.FitinModes.Off;
        }

        private void bttAutoFitH_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton tggBtt = e.Source as ToggleButton;

            if (tggBtt.IsChecked == true)
            {
                bttAutoFit.IsChecked = false;
                bttAutoFitW.IsChecked = false;

                AppStuff.Plotter.AutoFit = CoordinateGrid.FitinModes.H;
                AppStuff.Plotter.FitIn(CoordinateGrid.FitinModes.H);
            }
            else AppStuff.Plotter.AutoFit = CoordinateGrid.FitinModes.Off;
        }

        private void bttSquareGrid_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton tggBtt = e.Source as ToggleButton;

            if (tggBtt.IsChecked == true)
            {
                AppStuff.Plotter.SquareGrid = true;

                if (!AppStuff.Plotter.FitIn(AppStuff.Plotter.AutoFit)) AppStuff.Plotter.FitIn(CoordinateGrid.FitinModes.Update);
            }
            else AppStuff.Plotter.SquareGrid = false;
        }

        private void bttRectZoom_Click(object sender, RoutedEventArgs e)
        {
            if (bttRectZoom.IsChecked == true)
            {
                AppStuff.Plotter.RectZoomSwitch(true);
                AppStuff.Plotter.RectZoomingComplete += delegate (Object o, EventArgs ec) { bttRectZoom.IsChecked = false; };
            }
            else
            {
                AppStuff.Plotter.RectZoomSwitch(false);
            }
        }
    }
}
