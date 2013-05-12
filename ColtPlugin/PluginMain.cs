using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using System.Reflection;
using WeifenLuo.WinFormsUI.Docking;
using ColtPlugin.Resources;
using PluginCore.Localization;
using PluginCore.Utilities;
using PluginCore.Managers;
using PluginCore.Helpers;
using PluginCore;
using ProjectManager.Projects.AS3;

namespace ColtPlugin
{
	public class PluginMain : IPlugin
	{
        private String pluginName = "ColtPlugin";
        private String pluginGuid = "12600B5B-D185-4171-A362-25C5F73548C6";
        private String pluginHelp = "makc3d.wordpress.com/about/";
        private String pluginDesc = "COLT FD Plugin";
        private String pluginAuth = "Makc"; // as if
        private String buttonText = "Open in COLT";
        private String settingFilename;
        private Settings settingObject;
        private ToolStripMenuItem menuItem;
        private ToolStripButton toolbarButton;
        private Boolean active = false;
        private FileSystemWatcher watcher;
        private String pathToLog;
        private System.Timers.Timer timer;

	    #region Required Properties

        /// <summary>
        /// Api level of the plugin
        /// </summary>
        public Int32 Api
        {
            get { return 1; }
        }

        /// <summary>
        /// Name of the plugin
        /// </summary> 
        public String Name
		{
			get { return pluginName; }
		}

        /// <summary>
        /// GUID of the plugin
        /// </summary>
        public String Guid
		{
			get { return pluginGuid; }
		}

        /// <summary>
        /// Author of the plugin
        /// </summary> 
        public String Author
		{
			get { return pluginAuth; }
		}

        /// <summary>
        /// Description of the plugin
        /// </summary> 
        public String Description
		{
			get { return pluginDesc; }
		}

        /// <summary>
        /// Web address for help
        /// </summary> 
        public String Help
		{
			get { return pluginHelp; }
		}

        /// <summary>
        /// Object that contains the settings
        /// </summary>
        [Browsable(false)]
        public Object Settings
        {
            get { return settingObject; }
        }
		
		#endregion
		
		#region Required Methods
		
		/// <summary>
		/// Initializes the plugin
		/// </summary>
		public void Initialize()
		{
            InitBasics();
            LoadSettings();
            InitLocalization();
            AddEventHandlers();
        }
		
		/// <summary>
		/// Disposes the plugin
		/// </summary>
		public void Dispose()
		{
            SaveSettings();
		}
		
		/// <summary>
		/// Handles the incoming events
		/// </summary>
		public void HandleEvent(Object sender, NotifyEvent e, HandlingPriority prority)
		{
            switch (e.Type)
            {
                case EventType.Command:
                    string cmd = (e as DataEvent).Action;
                    if (cmd == "ProjectManager.Project")
                    {
                        IProject project = PluginBase.CurrentProject;
                        Boolean as3projectIsOpen = (project != null) && (project.Language == "as3");
                        if (menuItem != null) menuItem.Enabled = as3projectIsOpen;
                        if (toolbarButton != null) toolbarButton.Enabled = as3projectIsOpen;
                        // deactivate
                        active = false;
                        if (watcher != null) watcher.EnableRaisingEvents = false;
                        if (timer != null) { timer.Stop(); timer = null; }
                    }
                    else if (cmd == "ASCompletion.ClassPath")
                    {
                        // apparently project setting changes; reopen already opened COLT project
                        if (active) OpenInCOLT();
                    }
                    else if (cmd == "ProjectManager.Menu")
                    {
                        Object menu = (e as DataEvent).Data;
                        CreateMenuItem(menu as ToolStripMenuItem);
                    }
                    else if (cmd == "ProjectManager.ToolBar")
                    {
                        Object toolStrip = (e as DataEvent).Data;
                        CreateToolbarButton(toolStrip as ToolStrip);
                    }
                    break;
                
                case EventType.FileSave:
                    if (active) ClearErrors();
                    break;
            }
		}

		#endregion

        #region Initialize() stuff

        /// <summary>
        /// Initializes important variables
        /// </summary>
        public void InitBasics()
        {
            String dataPath = Path.Combine(PathHelper.DataDir, "ColtPlugin");
            if (!Directory.Exists(dataPath)) Directory.CreateDirectory(dataPath);
            settingFilename = Path.Combine(dataPath, "Settings.fdb");
        }

        /// <summary>
        /// Initializes the localization of the plugin
        /// </summary>
        public void InitLocalization()
        {
            LocaleVersion locale = PluginBase.MainForm.Settings.LocaleVersion;
            switch (locale)
            {
                /*
                case LocaleVersion.fi_FI : 
                    // We have Finnish available... or not. :)
                    LocaleHelper.Initialize(LocaleVersion.fi_FI);
                    break;
                */
                default : 
                    // Plugins should default to English...
                    LocaleHelper.Initialize(LocaleVersion.en_US);
                    break;
            }
            pluginDesc = LocaleHelper.GetString("Info.Description");
            buttonText = LocaleHelper.GetString("Info.ButtonText");
        }

        /// <summary>
        /// Adds the required event handlers
        /// </summary> 
        public void AddEventHandlers()
        {
            EventManager.AddEventHandler(this, EventType.Command | EventType.FileSave);

            watcher = new FileSystemWatcher();
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Changed += new FileSystemEventHandler(OnFileChange);
        }

        #endregion

        #region Menu items stuff

        private void CreateMenuItem(ToolStripMenuItem projectMenu)
        {
            menuItem = new ToolStripMenuItem(buttonText, GetImage("colt.png"), new EventHandler(OnClick), null);
            projectMenu.DropDownItems.Add(menuItem);
        }

        private void CreateToolbarButton(ToolStrip toolStrip)
        {
            toolbarButton = new ToolStripButton();
            toolbarButton.Image = GetImage("colt.png");
            toolbarButton.Text = buttonText;
            toolbarButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolbarButton.Click += new EventHandler(OnClick);
            toolStrip.Items.Add(toolbarButton);
        }

        /// <summary>
        /// Gets embedded image from resources
        /// </summary>
        private static Image GetImage(String imageName)
        {
            imageName = "ColtPlugin.Resources." + imageName;
            Assembly assembly = Assembly.GetExecutingAssembly();
            return new Bitmap(assembly.GetManifestResourceStream(imageName));
        }

        private void OnClick(Object sender, System.EventArgs e)
        {
            active = true;
            OpenInCOLT();
        }

        #endregion

        #region Plugin settings stuff

        /// <summary>
        /// Loads the plugin settings
        /// </summary>
        public void LoadSettings()
        {
            settingObject = new Settings();
            if (!File.Exists(settingFilename)) SaveSettings();
            else
            {
                Object obj = ObjectSerializer.Deserialize(settingFilename, settingObject);
                settingObject = (Settings)obj;
            }
        }

        /// <summary>
        /// Saves the plugin settings
        /// </summary>
        public void SaveSettings()
        {
            ObjectSerializer.Serialize(settingFilename, settingObject);
        }

		#endregion

        #region Logging errors

        private void OnFileChange(Object sender, FileSystemEventArgs e)
        {
            if (e.FullPath.EndsWith("compile_errors.log"))
            {
                if (timer == null)
                {
                    timer = new System.Timers.Timer();
                    timer.SynchronizingObject = (Form)PluginBase.MainForm; // thread safe
                    timer.Interval = 200;
                    timer.Elapsed += OnTimerElapsed;
                    timer.Enabled = true;
                    timer.Start();
                }
            }
        }

        private void OnTimerElapsed(object sender, EventArgs e)
        {
            timer.Stop();
            timer = null;

            ClearErrors();

            String message = File.ReadAllText(pathToLog);

            // COLT copies sources to "incremental" folder, so let's try to find correct path and patch the output
            String incremental = "colt\\incremental";
            String[] sources = PluginBase.CurrentProject.SourcePaths;

            // [09.05.2013 17:26:54] Philippe Elsass: make sure you send the log line by line to the Output
            String[] messageLines = message.Split(new Char[] {'\r', '\n'});
            bool hasErrors = false;
            foreach (String line in messageLines) if (line.Length > 0)
            {
                // [08.05.2013 18:04:15] Philippe Elsass: you can also specify '-3' as 2nd parameter to the traces (error level)
                // [08.05.2013 18:05:02] Philippe Elsass: so it will appear in red in the output and have an error icon in the results panel
                if (line.Contains(incremental))
                {
                    // carefully take the file name out
                    String file = line.Substring(0, line.IndexOf("): col"));
                    file = file.Substring(0, file.LastIndexOf("("));
                    file = file.Substring(file.IndexOf(incremental) + incremental.Length + 1);

                    // look for it in all source folders
                    for (int i = 0; i < sources.Length; i++)
                    {
                        if (File.Exists(PluginBase.CurrentProject.GetAbsolutePath(Path.Combine(sources[i], file))))
                        {
                            TraceManager.Add(line.Replace(incremental, sources[i]), -3);
                            hasErrors = true;
                            break;
                        }
                    }
                }
                else
                {
                    // send as is
                    TraceManager.Add(line, -3);
                }
            }

            if (hasErrors) ShowErrors();
        }

        private void ClearErrors()
        {
            EventManager.DispatchEvent(this, new DataEvent(EventType.Command, "ResultsPanel.ClearResults", null));
        }

        private void ShowErrors()
        {
            // should be an option: if the panel was hidden it captures keyboard focus
            //EventManager.DispatchEvent(this, new DataEvent(EventType.Command, "ResultsPanel.ShowResults", null));
        }

        #endregion

        /// <summary>
        /// Opens the project in COLT
        /// </summary>
        private void OpenInCOLT()
        {
            // our options: parse project.ProjectPath (xml file) or use api
            AS3Project project = (AS3Project)PluginBase.CurrentProject;

            String configCopy = "";
            if (settingObject.FullConfig)
            {
                // Construct flex config file name (see AS3ProjectBuilder, line 140)
                String projectName = project.Name.Replace(" ", "");
                String configFile = Path.Combine("obj", projectName + "Config.xml");

                if (!File.Exists(project.GetAbsolutePath(configFile)))
                {
                    TraceManager.Add("Required file (" + projectName + "Config.xml) does not exist, project must be built first...", -1);

                    EventManager.DispatchEvent(this, new DataEvent(EventType.Command, "ProjectManager.BuildProject", null));

                    return;
                }

                // Create config copy with <file-specs>...</file-specs> commented out
                configCopy = Path.Combine("obj", projectName + "ConfigCopy.xml");
                File.WriteAllText(project.GetAbsolutePath(configCopy),
                    File.ReadAllText(project.GetAbsolutePath(configFile))
                        .Replace("<file-specs", "<!-- file-specs")
                        .Replace("/file-specs>", "/file-specs -->"));
            }
            

            // Create COLT subfolder if does not exist yet
            String coltFolderPath = project.GetAbsolutePath(settingObject.WorkingFolder);
            if (!Directory.Exists(coltFolderPath)) Directory.CreateDirectory(coltFolderPath);

            // While at that, start listening for colt/compile_errors.log changes
            pathToLog = Path.Combine(coltFolderPath, "compile_errors.log");
            watcher.Path = coltFolderPath;
            watcher.EnableRaisingEvents = true;

            // Create COLT project with random name (if we'd update same file - are there file locks? how to reopen in colt?)
            String coltFileName = project.GetAbsolutePath(Path.Combine(settingObject.WorkingFolder, System.Guid.NewGuid() + ".colt"));
            StreamWriter stream = File.CreateText(coltFileName);


            // Write current project settings there
            stream.WriteLine("#Generated by FD plugin");

            stream.WriteLine("name=" + project.Name);

            MxmlcOptions options = project.CompilerOptions;
            String libraryPaths = "";
            foreach (String libraryPath in options.LibraryPaths)
                libraryPaths += EscapeForCOLT(project.GetAbsolutePath(libraryPath)) + ";";
            stream.WriteLine("libraryPaths=" + libraryPaths);

            stream.WriteLine("clearMessages=true");
            
            stream.WriteLine("targetPlayerVersion=" + project.MovieOptions.Version + ".0");
            
            stream.WriteLine("mainClass=" + EscapeForCOLT(project.GetAbsolutePath(project.CompileTargets[0])));

            stream.WriteLine("maxLoopIterations=10000");

            stream.WriteLine("flexSDKPath=" + EscapeForCOLT(project.CurrentSDK));

            stream.WriteLine("liveMethods=annotated");

            if (settingObject.FullConfig)
            {
                stream.WriteLine("compilerOptions=-load-config+\\=\"" + EscapeForCOLT(project.GetAbsolutePath(configCopy)) + "\"");
            }
            
            stream.WriteLine("target=SWF"); // use project.MovieOptions.Platform switch ??

            String outputPath = project.OutputPath;
            // fixme: colt does not take paths atm
            int lastSlash = outputPath.LastIndexOf(@"\");
            if (lastSlash > -1) outputPath = outputPath.Substring(lastSlash + 1);
            stream.WriteLine("outputFileName=" + outputPath);

            stream.WriteLine("useDefaultSDKConfiguration=true");

            String sourcePaths = "";
            foreach (String sourcePath in project.SourcePaths)
                sourcePaths += EscapeForCOLT(project.GetAbsolutePath(sourcePath)) + ";";
            stream.WriteLine("sourcePaths=" + sourcePaths);

            stream.Close();

            // Open it with default app (COLT)
            Process.Start(coltFileName);

            // Remove older *.colt files
            foreach (String oldFile in Directory.GetFiles(coltFolderPath, "*.colt"))
            {
                if (!coltFileName.Contains(Path.GetFileName(oldFile)))
                {
                    File.Delete(oldFile);
                }
            }
        }

        private String EscapeForCOLT(String path)
        {
            // some standard escape ??
            return path.Replace(@"\", @"\\").Replace(":", @"\:").Replace("=", @"\=");
        }
	}
}
