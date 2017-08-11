using System;
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
using System.Windows.Shapes;

namespace zxCalculator
{
    /// <summary>
    /// Interaction logic for EditStrokeWin.xaml
    /// </summary>
    public partial class EditStrokeWin : Window
    {
        private double currentThickness = 2;

        public Color SelectedColor { get { return ColorControl.SelectedColor; } }
        public DoubleCollection SelectedDashes { get { return (combobxDashes.SelectedItem as DoubleCollection); } }
        public double SettedThickness { get { return currentThickness; } }
        public bool IsGraphActive { get { return chbxIsActive.IsChecked == true; } }

        public double SetThickness(double thick)
        {
            if (thick < 0) thick = 0;
            else if (thick > 10) thick = 10;

            currentThickness = thick;
            txtBoxThickness.Text = thick.ToString("g2");

            return thick;
        }

        public double SetThickness(string thickStr)
        {
            double thick;

            if (double.TryParse(thickStr, out thick))
            {
                if (thick < 0) thick = 0;
                else if (thick > 10) thick = 10;

                currentThickness = thick;
            } 

            txtBoxThickness.Text = currentThickness.ToString("g2");

            return currentThickness;
        }

        public EditStrokeWin()
        {
            InitializeComponent();
        }
    }
}
