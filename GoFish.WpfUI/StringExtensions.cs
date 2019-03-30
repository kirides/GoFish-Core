using GoFish.DataAccess;
using GoFish.DataAccess.VisualFoxPro;
using GoFish.DataAccess.VisualFoxPro.Search;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GoFish.WpfUI
{

    public static class StringExtensions
    {
        public static string GetLine(this string value, int line)
        {
            using (var sr = new StringReader(value))
            {
                for (int i = 0; i < line; i++)
                {
                    sr.ReadLine();
                }
                return sr.ReadLine();
            }
        }
    }
}