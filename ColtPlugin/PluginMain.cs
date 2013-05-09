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
        private String lastErrors = "";

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
			get { return this.pluginName; }
		}

        /// <summary>
        /// GUID of the plugin
        /// </summary>
        public String Guid
		{
			get { return this.pluginGuid; }
		}

        /// <summary>
        /// Author of the plugin
        /// </summary> 
        public String Author
		{
			get { return this.pluginAuth; }
		}

        /// <summary>
        /// Description of the plugin
        /// </summary> 
        public String Description
		{
			get { return this.pluginDesc; }
		}

        /// <summary>
        /// Web address for help
        /// </summary> 
        public String Help
		{
			get { return this.pluginHelp; }
		}

        /// <summary>
        /// Object that contains the settings
        /// </summary>
        [Browsable(false)]
        public Object Settings
        {
            get { return this.settingObject; }
        }
		
		#endregion
		
		#region Required Methods
		
		/// <summary>
		/// Initializes the plugin
		/// </summary>
		public void Initialize()
		{
            this.InitBasics();
            this.LoadSettings();
            this.InitLocalization();
            this.AddEventHandlers();
        }
		
		/// <summary>
		/// Disposes the plugin
		/// </summary>
		public void Dispose()
		{
            this.SaveSettings();
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
                        if (this.menuItem != null) this.menuItem.Enabled = as3projectIsOpen;
                        if (this.toolbarButton != null) this.toolbarButton.Enabled = as3projectIsOpen;
                        // deactivate if project is closed
                        active &= as3projectIsOpen;
                        if (watcher != null) watcher.EnableRaisingEvents &= as3projectIsOpen;
                    }
                    else if (cmd == "ASCompletion.ClassPath")
                    {
                        // apparently project setting changes; reopen already opened COLT project
                        if (active) OpenInCOLT();
                    }
                    else if (cmd == "ProjectManager.Menu")
                    {
                        Object menu = (e as DataEvent).Data;
                        this.CreateMenuItem(menu as ToolStripMenuItem);
                    }
                    else if (cmd == "ProjectManager.ToolBar")
                    {
                        Object toolStrip = (e as DataEvent).Data;
                        this.CreateToolbarButton(toolStrip as ToolStrip);
                    }
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
            this.settingFilename = Path.Combine(dataPath, "Settings.fdb");
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
            this.pluginDesc = LocaleHelper.GetString("Info.Description");
            this.buttonText = LocaleHelper.GetString("Info.ButtonText");
        }

        /// <summary>
        /// Adds the required event handlers
        /// </summary> 
        public void AddEventHandlers()
        {
            EventManager.AddEventHandler(this, EventType.Command);

            watcher = new FileSystemWatcher();
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Changed += new FileSystemEventHandler(OnFileChange);
        }

        #endregion

        #region Menu items stuff

        private void CreateMenuItem(ToolStripMenuItem projectMenu)
        {
            this.menuItem = new ToolStripMenuItem(buttonText, GetImage("colt.png"), new EventHandler(this.OnClick), null);
            projectMenu.DropDownItems.Add(this.menuItem);
        }

        private void CreateToolbarButton(ToolStrip toolStrip)
        {
            this.toolbarButton = new ToolStripButton();
            this.toolbarButton.Image = GetImage("colt.png");
            this.toolbarButton.Text = buttonText;
            this.toolbarButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            this.toolbarButton.Click += new EventHandler(this.OnClick);
            toolStrip.Items.Add(this.toolbarButton);
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
            this.settingObject = new Settings();
            if (!File.Exists(this.settingFilename)) this.SaveSettings();
            else
            {
                Object obj = ObjectSerializer.Deserialize(this.settingFilename, this.settingObject);
                this.settingObject = (Settings)obj;
            }
        }

        /// <summary>
        /// Saves the plugin settings
        /// </summary>
        public void SaveSettings()
        {
            ObjectSerializer.Serialize(this.settingFilename, this.settingObject);
        }

		#endregion

        private void OnFileChange(Object sender, FileSystemEventArgs e)
        {
            if (Path.GetFileName(e.FullPath).Contains("compile_errors.log"))
            {
/* This hangs FD :( now if someone could fix this...
 *
                StreamReader stream = File.OpenText(e.FullPath);
                String errors = stream.ReadToEnd(); stream.Close();

                String message = errors;
                if ((lastErrors.Length > 0) && (errors.IndexOf(lastErrors) == 0))
                {
                    message = errors.Substring(lastErrors.Length);
                }
                TraceManager.AddAsync(message, -3);

                lastErrors = errors;
 */
            }
        }

        /// <summary>
        /// Opens the project in COLT
        /// </summary>
        private void OpenInCOLT()
        {
            // our options: parse project.ProjectPath (xml file) or use api
            AS3Project project = (AS3Project)PluginBase.CurrentProject;

            String configCopy = "";
            if (this.settingObject.FullConfig)
            {
                // Construct flex config file name (see AS3ProjectBuilder, line 140)
                String projectName = project.Name.Replace(" ", "");
                String configFile = Path.Combine("obj", projectName + "Config.xml");

                if (!File.Exists(project.GetAbsolutePath(configFile)))
                {
                    TraceManager.AddAsync("Required file (" + projectName + "Config.xml) does not exist, project must be built first...", -1);

                    EventManager.DispatchEvent(this, new DataEvent(EventType.Command, "ProjectManager.BuildProject", null));

                    return;
                }

                // Create config copy with <file-specs>...</file-specs> commented out
                StreamReader src = File.OpenText(project.GetAbsolutePath(configFile));
                String config = src.ReadToEnd();
                src.Close();

                configCopy = Path.Combine("obj", projectName + "ConfigCopy.xml");
                StreamWriter dst = File.CreateText(project.GetAbsolutePath(configCopy));
                dst.Write(config.Replace("<file-specs", "<!-- file-specs").Replace("/file-specs>", "/file-specs -->"));
                dst.Close();
            }
            

            // Create COLT subfolder if does not exist yet
            String coltFolderPath = project.GetAbsolutePath(this.settingObject.WorkingFolder);
            if (!Directory.Exists(coltFolderPath)) Directory.CreateDirectory(coltFolderPath);

            // While at that, start listening for colt/compile_errors.log changes
            String pathToLog = Path.Combine(coltFolderPath, "compile_errors.log");
            if (File.Exists(pathToLog))
            {
                StreamReader errorsFile = File.OpenText(pathToLog);
                lastErrors = errorsFile.ReadToEnd(); errorsFile.Close();
            }
            watcher.Path = coltFolderPath;
            watcher.EnableRaisingEvents = true;

            // Create COLT project with random name (if we'd update same file - are there file locks? how to reopen in colt?)
            String coltFileName = project.GetAbsolutePath(Path.Combine(this.settingObject.WorkingFolder, System.Guid.NewGuid() + ".colt"));
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

            if (this.settingObject.FullConfig)
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
