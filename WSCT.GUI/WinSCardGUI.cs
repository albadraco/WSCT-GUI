using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WSCT.GUI.Plugins;
using WSCT.Helpers;
using WSCT.Helpers.Linq;
using WSCT.Linq;
using WSCT.Stack;
using WSCT.Wrapper;
using WSCT.Wrapper.Desktop.Core;

namespace WSCT.GUI
{
    public partial class WinSCardGui : Form
    {
        #region >> Fields

        private readonly CardChannelStackDescription channelLayers;
        private readonly CardContextStackDescription contextLayers;
        private readonly PluginRepository pluginRepository;
        private readonly StatusChangeMonitor statusMonitor;
        private readonly CardObserver observer;

        #endregion

        #region >> Constructors

        /// <summary>
        /// 
        /// </summary>
        public WinSCardGui()
        {
            InitializeComponent();

            guiShareMode.DataSource = Enum.GetValues(typeof(ShareMode));
            guiShareMode.SelectedItem = ShareMode.Shared;

            guiProtocol.DataSource = Enum.GetValues(typeof(Protocol));
            guiProtocol.SelectedItem = Protocol.Any;

            guiDisposition.DataSource = Enum.GetValues(typeof(Disposition));
            guiDisposition.SelectedItem = Disposition.UnpowerCard;

            guiAttribute.DataSource = Enum.GetValues(typeof(Attrib));
            guiAttribute.SelectedItem = Attrib.AtrString;

            pluginRepository = new PluginRepository();
            // Read all independant plugins repository and aggregate them in <c>pluginRepository</c>
            foreach (var fileName in Directory.GetFiles(".", "Plugin*.xml"))
            {
                var pr = SerializedObject<PluginRepository>.LoadFromXml(fileName);
                pr.Plugins.DoForEach(d => pluginRepository.Add(d));
            }

            observer = new CardObserver(this);

            channelLayers = SerializedObject<CardChannelStackDescription>.LoadFromXml("Stack.Channel.xml");

            contextLayers = SerializedObject<CardContextStackDescription>.LoadFromXml("Stack.Context.xml");

            statusMonitor = new StatusChangeMonitor();

            #region >> Initialize plugins menu and tab

            foreach (var plugin in pluginRepository.Plugins)
            {
                var pluginMenuItem = new ToolStripMenuItem { Text = plugin.Name };
                pluginMenuItem.Click += guiPluginsMenu_Click;
                guiPluginsMenuItem.DropDownItems.Add(pluginMenuItem);

                guiLoadedPlugins.Items.Add(plugin);
            }

            guiLoadedPlugins.DisplayMember = "name";

            #endregion

            #region >> Initialize layers menu and tab

            channelLayers.LayerDescriptions.DoForEach(d => guiLoadedChannelLayers.Items.Add(d));
            guiLoadedChannelLayers.DisplayMember = "name";

            contextLayers.LayerDescriptions.DoForEach(d => guiLoadedContextLayers.Items.Add(d));
            guiLoadedContextLayers.DisplayMember = "name";

            #endregion
        }

        #endregion

        #region >> Methods

        private string getLastNamespaceOf(object instance)
        {
            var splittedNamespace = instance.GetType().Namespace.Split('.');
            return splittedNamespace[splittedNamespace.Length - 1];
        }

        #endregion

        #region >> createCard * Stack

        private void CreateCardChannelStack()
        {
            var layers = channelLayers.LayerDescriptions
                .Select(ld => channelLayers.CreateInstance(ld.Name))
                .ToObservableLayers()
                .ForEach(observer.ObserveChannel);

            var stack = new CardChannelStack(layers).ToObservableStack();

            stack.Attach(SharedData.CardContext, guiReaders.SelectedItem.ToString());

            SharedData.CardChannel = stack;
        }

        private void CreateCardContextStack()
        {
            var layers = contextLayers.LayerDescriptions
                .Select(ld => contextLayers.CreateInstance(ld.Name))
                .ToObservableLayers()
                .ForEach(observer.ObserveContext);

            SharedData.CardContext = new CardContextStack(layers).ToObservableStack();
        }

        #endregion

        #region >> *_Click

        private void guiContextEstablish_Click(object sender, EventArgs e)
        {
            statusMonitor.OnCardInsertionEvent = null;
            statusMonitor.OnCardRemovalEvent = null;
            CreateCardContextStack();

            var lastError = SharedData.CardContext.Establish();

            if (lastError == ErrorCode.Success)
            {
                if (SharedData.CardContext.ListReaderGroups() == ErrorCode.Success)
                {
                    guiReaderGroups.DataSource = SharedData.CardContext.Groups;
                    guiFoundReaderGroups.Text = String.Format("Reader groups descriptionFound: {0}", SharedData.CardContext.GroupsCount);

                    if (SharedData.CardContext.ListReaders(SharedData.CardContext.Groups[0]) == ErrorCode.Success)
                    {
                        guiReaders.DataSource = SharedData.CardContext.Readers;
                        guiFoundReaders.Text = String.Format("Readers descriptionFound: {0}", SharedData.CardContext.ReadersCount);

                        statusMonitor.Context = SharedData.CardContext;
                        statusMonitor.Start();
                    }
                }

                UpdateContextEstablished();
            }
            else
            {
                guiContextState.Text = "An error occured";
            }
        }

        private void guiContextRelease_Click(object sender, EventArgs e)
        {
            statusMonitor.Stop();

            var lastError = SharedData.CardContext.Release();
            if (lastError == ErrorCode.Success)
            {
                SharedData.CardContext = null;
                guiContextState.Text = "Released";
                UpdateChannelStatus(ChannelStatusType.Disconnected);

                UpdateContextReleased();
            }
        }

        private void guiCardConnect_Click(object sender, EventArgs e)
        {
            CreateCardChannelStack();

            var errorCode = SharedData.CardChannel.Connect(ShareMode.Shared, Protocol.Any);
            if (errorCode == ErrorCode.Success)
            {
                UpdateReaderInUse(guiReaders.SelectedItem.ToString());
            }
        }

        private void guiCardWarmReset_Click(object sender, EventArgs e)
        {
            SharedData.CardChannel.Reconnect(ShareMode.Shared, Protocol.Any, Disposition.ResetCard);
        }

        private void guiCardColdReset_Click(object sender, EventArgs e)
        {
            SharedData.CardChannel.Reconnect(ShareMode.Shared, Protocol.Any, Disposition.UnpowerCard);
        }

        private void guiCardUnpower_Click(object sender, EventArgs e)
        {
            SharedData.CardChannel.Disconnect(Disposition.UnpowerCard);
            UpdateReaderInUse("");
        }

        private void guiChannelConnect_Click(object sender, EventArgs e)
        {
            CreateCardChannelStack();

            var errorCode = SharedData.CardChannel.Connect((ShareMode)guiShareMode.SelectedItem, (Protocol)guiProtocol.SelectedItem);
            if (errorCode == ErrorCode.Success)
            {
                UpdateReaderInUse(guiReaders.SelectedItem.ToString());
            }
        }

        private void guiChannelReconnect_Click(object sender, EventArgs e)
        {
            SharedData.CardChannel.Reconnect((ShareMode)guiShareMode.SelectedItem, (Protocol)guiProtocol.SelectedItem, (Disposition)guiDisposition.SelectedItem);
        }

        private void guiChannelDisconnect_Click(object sender, EventArgs e)
        {
            SharedData.CardChannel.Disconnect((Disposition)guiDisposition.SelectedItem);
        }

        private void guiGetAttribute_Click(object sender, EventArgs e)
        {
            try
            {
                byte[] recvBuffer = null;
                var errorCode = SharedData.CardChannel.GetAttrib((Attrib)guiAttribute.SelectedItem, ref recvBuffer);
                if (errorCode == ErrorCode.Success)
                {
                    UpdateAttribute(recvBuffer);
                }
            }
            catch (Exception)
            {
            }
        }

        private void guiQuit_Click(object sender, EventArgs e)
        {
            Close();
            Dispose();
        }

        private void guiAboutWinSCardGUI_Click(object sender, EventArgs e)
        {
            Form about = new AboutWinSCardGui();
            about.Show();
        }

        private void guiPluginsMenu_Click(object sender, EventArgs e)
        {
            var menuItem = (ToolStripMenuItem)sender;
            try
            {
                var plugin = pluginRepository.CreateInstance(menuItem.Text);
                plugin.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error(s) occured when launching plugin '" + menuItem.Text + "'", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        #endregion

        #region >> update*

        /// <summary>
        /// 
        /// </summary>
        /// <param name="attribute"></param>
        public void UpdateAttribute(byte[] attribute)
        {
            guiRawAttribute.Text = attribute.ToHexa();
            guiStringAttribute.Text = attribute.ToAsciiString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="error"></param>
        public void UpdateLastError(ErrorCode error)
        {
            guiLastError.Text = String.Format("Last Error: {0}", error);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="status"></param>
        public void UpdateChannelStatus(ChannelStatusType status)
        {
            switch (status)
            {
                case ChannelStatusType.Connected:
                    guiChannelState.Text = "Connected";
                    break;
                case ChannelStatusType.Disconnected:
                    guiChannelState.Text = "Disconnected";
                    break;
                case ChannelStatusType.Error:
                    guiChannelState.Text = "An error occured";
                    break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="readerName"></param>
        public void UpdateReaderInUse(string readerName)
        {
            guiReaderInUse.Text = readerName;
        }

        /// <summary>
        /// Called when new context established: update GUI
        /// </summary>
        public void UpdateContextEstablished()
        {
            guiGroupCardConnection.Enabled = true;
            guiGroupCardAttributes.Enabled = true;
            guiGroupCardInformations.Enabled = true;

            guiContextState.Text = "Established";

            guiContextEstablish.Enabled = false;
            guiContextRelease.Enabled = true;
            guiCardConnect.Enabled = true;
            guiCardWarmReset.Enabled = true;
            guiCardColdReset.Enabled = true;
            guiCardUnpower.Enabled = true;
        }

        /// <summary>
        /// Called when context is released: updateGUI
        /// </summary>
        public void UpdateContextReleased()
        {
            guiGroupCardConnection.Enabled = false;
            guiGroupCardAttributes.Enabled = false;
            guiGroupCardInformations.Enabled = false;

            guiFoundReaderGroups.Text = String.Format("Reader groups descriptionFound: {0}", 0);
            guiReaderGroups.DataSource = null;
            guiFoundReaders.Text = String.Format("Readers descriptionFound: {0}", 0);
            guiReaders.DataSource = null;

            guiContextEstablish.Enabled = true;
            guiContextRelease.Enabled = false;
            guiCardConnect.Enabled = false;
            guiCardWarmReset.Enabled = false;
            guiCardColdReset.Enabled = false;
            guiCardUnpower.Enabled = false;
        }

        #endregion

        #region >> *_SelectedIndexChanged

        private void guiLoadedPlugins_SelectedIndexChanged(object sender, EventArgs e)
        {
            var listBox = (ListBox)sender;
            if (listBox.SelectedItem != null)
            {
                var plugin = (PluginDescription)listBox.SelectedItem;
                guiPluginName.Text = plugin.Name;
                guiPluginClassName.Text = plugin.ClassName;
                guiPluginDLL.Text = plugin.DllName;
                guiPluginPathToDll.Text = plugin.PathToDll;

                try
                {
                    var assembly = Assembly.LoadFrom(plugin.PathToDll + plugin.DllName);
                    var assemblyName = new AssemblyName(assembly.FullName);
                    guiPluginAssemblyVersion.Text = assemblyName.Version.ToString();
                    guiPluginAssemblyName.Text = assemblyName.Name;
                    var assemblyDescription = (AssemblyDescriptionAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyDescriptionAttribute));
                    guiPluginAssemblyDescription.Text = assemblyDescription.Description;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error(s) occured when accessing plugin '" + plugin.Name + "'", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void guiLoadedChannelLayers_SelectedIndexChanged(object sender, EventArgs e)
        {
            var listBox = (ListBox)sender;
            if (listBox.SelectedItem != null)
            {
                // Deselect the selected layer in context layers
                guiLoadedContextLayers.SetSelected(0, false);

                // Update assembly informations
                var layer = (CardChannelLayerDescription)listBox.SelectedItem;
                guiLayerName.Text = layer.Name;
                guiLayerClassName.Text = layer.ClassName;
                guiLayerDLL.Text = layer.DllName;
                guiLayerPathToDll.Text = layer.PathToDll;

                try
                {
                    var assembly = Assembly.LoadFrom(layer.PathToDll + layer.DllName);
                    var assemblyName = new AssemblyName(assembly.FullName);
                    guiLayerAssemblyVersion.Text = assemblyName.Version.ToString();
                    guiLayerAssemblyName.Text = assemblyName.Name;
                    var assemblyDescription = (AssemblyDescriptionAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyDescriptionAttribute));
                    guiLayerAssemblyDescription.Text = assemblyDescription.Description;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error(s) occured when accessing layer '" + layer.Name + "'", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void guiLoadedContextLayers_SelectedIndexChanged(object sender, EventArgs e)
        {
            var listBox = (ListBox)sender;
            if (listBox.SelectedItem != null)
            {
                // Deselect the selected layer in channel layers
                guiLoadedChannelLayers.SetSelected(0, false);

                // Update assembly informations
                var layer = (CardContextLayerDescription)listBox.SelectedItem;
                guiLayerName.Text = layer.Name;
                guiLayerClassName.Text = layer.ClassName;
                guiLayerDLL.Text = layer.DllName;
                guiLayerPathToDll.Text = layer.PathToDll;

                try
                {
                    var assembly = Assembly.LoadFrom(layer.PathToDll + layer.DllName);
                    var assemblyName = new AssemblyName(assembly.FullName);
                    guiLayerAssemblyVersion.Text = assemblyName.Version.ToString();
                    guiLayerAssemblyName.Text = assemblyName.Name;
                    var assemblyDescription = (AssemblyDescriptionAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyDescriptionAttribute));
                    guiLayerAssemblyDescription.Text = assemblyDescription.Description;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error(s) occured when accessing layer '" + layer.Name + "'", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        #endregion

        #region >> *_FormClosing

        private void WinSCardGUI_FormClosing(object sender, FormClosingEventArgs e)
        {
            statusMonitor.Stop();
        }

        #endregion
    }
}