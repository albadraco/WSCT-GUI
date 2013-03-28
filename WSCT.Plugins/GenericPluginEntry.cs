﻿using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace WSCT.GUI.Plugins
{
    /// <summary>
    /// Generic IPluginEntry implementation able to manage Mono Winforms implementation constraint: only one message loop thread is authorized (ie Application.Run()).
    /// On Mono platform all plugin's Form are created in main message loop thread.
    /// On other platforms (Microsoft .net is the only one known for now) each plugin in created in its own message loop thread.
    /// </summary>
    public class GenericPluginEntry<T> : IPlugin
        where T : Form, new()
    {
        #region >> IPlugin

        /// <inheritdoc />
        public void show()
        {
            if (Type.GetType("Mono.Runtime") != null)
            {
                startPlugin();
            }
            else
            {
                Thread pluginThread = new Thread(startPlugin);
                pluginThread.SetApartmentState(ApartmentState.STA);
                pluginThread.Start();
            }
        }

        private void startPlugin()
        {
            if (Type.GetType("Mono.Runtime") != null)
            {
                new T().Show();
            }
            else
            {
                // Use a named Mutex (available computer-wide) to check if the pluginDesc thread still exists
                using (var mutex = new Mutex(false, typeof(T).Assembly.FullName))
                {
                    // If the mutex is available, then get it and launch the pluginDesc GUI
                    if (mutex.WaitOne(TimeSpan.FromSeconds(0), false))
                    {
                        Application.Run(new T());
                    }
                    else
                    {
                        MessageBox.Show("Only one instance of the plugin is authorized", "Plugin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        #endregion
    }
}