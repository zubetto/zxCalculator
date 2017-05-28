using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
            string errors = ""; // nothing was opened or number of suitable types exceeds max number of available function slots

            int indexAdd = 0;
            int indexErr = 0;
            bool loadFlag = false;
            bool dlgResult = false;

            ICalculate[] functionsData = new ICalculate[AppStuff.FunctionsNumber];

            try // [[[[[[[ Showing OpenFile Dilog ]]]]]]]
            {
                if (AppStuff.FirstOpening && AppStuff.IOexception == "") dlgOpenFile.InitialDirectory = AppStuff.OpenPath;
                
                if (dlgOpenFile.ShowDialog() == true)
                {
                    dlgResult = true;

                    files = dlgOpenFile.FileNames;

                    int num = files.GetLength(0);

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
                                errors += "Perhaps, not all functions have been loaded due to absence of free slots!";

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

                        if (!loadFlag)
                        {
                            errors += String.Format("{0}. {1}: The ICalculate NOT FOUND/n/n/r", indexErr++, System.IO.Path.GetFileName(files[i]) );
                        }
                    } // --- end of files loop -------------------------------------------------------------------
                }
            }
            catch (Exception exc)
            {
                errors = String.Format("{0}/n/n/r{1}", exc.ToString(), errors);
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

        private void LostKeyFocus_TESTargInput(object sender, RoutedEventArgs e)
        {
            TextBox txtboxArgs = e.Source as TextBox;
            if (txtboxArgs == null) txtboxArgs = AppStuff.testBoxArgs;

            double Arg = 0;

            if (Double.TryParse(txtboxArgs.Text, out Arg))
            {
                AppStuff.testBoxFunc.Text = String.Format("{0}", Arg);
            }
            else
            {
                AppStuff.testBoxFunc.Text = "0";
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
    }
}
