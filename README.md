# zxCalculator
<p align="left">
  <img src="https://raw.githubusercontent.com/zubetto/zxCalculator/master/zxCalciconBraces.png" width="128"/>
</p> 

[1.0.0-beta for windows](https://github.com/zubetto/zxCalculator/releases/tag/1.0.0-beta)  

WPF project of the extensible calculator, intended to perform calculations of custom functions, that can be dynamically added from DLLs and for plotting them on the interactive coordinate grid; Wrapper classes for the custom functions must implement ICalculate interface defined in the *zxcalc.dll* (therefore, projects with custom functions must reference the *zxcalc.dll*).  
  
Array calculations are performed in the multithreading manner.  
  
The class *CoordinateGrid* represents coordinate grid stuff (grid lines, scale values and labels) and performs plotting. *CoordinateGrid* can easily be added to any WPF project by specifying the canvas on which you want to plot graphs.  
  
The class *PointsSampler* implements a filtering method for efficient representation of graphs consisting of a large number of points.  
  
Next steps are implementation of a calculation interruption (pausing and canceling) and more attractive interface;  

<p align="left">
  <img src="https://raw.githubusercontent.com/zubetto/zxCalculator/master/zxCalcGUI.png" width="1162" hieght="707"/>
  <img src="https://raw.githubusercontent.com/zubetto/zxCalculator/master/zxCalcGUI_02.png" width="1162" hieght="707"/>
</p>
