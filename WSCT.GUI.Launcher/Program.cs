﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace WSCT.GUI.Launcher
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new WSCT.GUI.WinSCardGUI());

            // Force close all other existing Application.Run() 
            Application.Exit();
        }
    }
}