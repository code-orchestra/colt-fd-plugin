using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using System.Reflection;
using WeifenLuo.WinFormsUI.Docking;
using ColtPlugin.Resources;
using ColtPlugin.Rpc;
using PluginCore.Localization;
using PluginCore.Utilities;
using PluginCore.Managers;
using PluginCore.Helpers;
using PluginCore;
using ProjectManager.Projects.AS3;
using ASCompletion.Context;
using System.Text.RegularExpressions;
using ASCompletion.Model;

namespace ColtPlugin
{
	public class PluginMain : IPlugin
	{
        private String pluginName = "ColtPlugin";
        private String pluginGuid = "12600B5B-D185-4171-A362-25C5F73548C6";
        private String pluginHelp = "codeorchestra.zendesk.com/home/";
        private String pluginDesc = "COLT FD Plugin";
        private String pluginAuth = "Makc"; // as if
        private String settingFilename;
        private Settings settingObject;
        private ToolStripMenuItem menuItem;
        private ToolStripButton toolbarButton, toolbarButton2;
        private FileSystemWatcher watcher;
        private String pathToLog;
        private System.Timers.Timer timer;
        private Keys MakeItLiveKeys = Keys.Control | Keys.Shift | Keys.L;
        private Boolean allowBuildInterception = true;

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
                        if (toolbarButton2 != null) toolbarButton2.Enabled = as3projectIsOpen && (GetCOLTFile() != null);
                        // modified or new project - reconnect in any case
                        WatchErrorsLog();
                    }
                    else if (cmd == "ProjectManager.Menu")
                    {
                        Object menu = (e as DataEvent).Data;
                        CreateMenuItem(menu as ToolStripMenuItem);
                    }
                    else if (cmd == "ProjectManager.ToolBar")
                    {
                        Object toolStrip = (e as DataEvent).Data;
                        toolbarButton = CreateToolbarButton(toolStrip as ToolStrip, "colt_save.png", "Menu.ExportToCOLT", new EventHandler(OnClick));
                        toolbarButton2 = CreateToolbarButton(toolStrip as ToolStrip, "colt_run.png", "Menu.OpenInCOLT", new EventHandler(OnClick2));
                    }
                    else if ((cmd == "ProjectManager.BuildingProject") || (cmd == "ProjectManager.TestingProject"))
                    {
                        // todo: FD might send this for projects other than PluginBase.CurrentProject - figure out how to catch that
                        if (settingObject.InterceptBuilds && allowBuildInterception)
                        {
                            new AppStarter(ProductionBuild, cmd == "ProjectManager.TestingProject");

                            e.Handled = true;
                        }
                    }
                    break;
                
                case EventType.FileSave:
                    if (watcher.EnableRaisingEvents) ClearErrors();
                    break;

                case EventType.Keys: // shortcut pressed
                    KeyEvent ke = (KeyEvent)e;
                    if (ke.Value == MakeItLiveKeys)
                    {
                        ke.Handled = true;
                        MakeItLive();
                    }
                    break;

                case EventType.Shortcut: // shortcut changed
                    DataEvent de = (DataEvent)e;
                    if (de.Action == "ColtPlugin.MakeItLive")
                    {
                        MakeItLiveKeys = (Keys)de.Data;
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
        }

        /// <summary>
        /// Adds the required event handlers
        /// </summary> 
        public void AddEventHandlers()
        {
            EventManager.AddEventHandler(this, EventType.Command | EventType.FileSave | EventType.Keys | EventType.Shortcut);

            watcher = new FileSystemWatcher();
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Changed += new FileSystemEventHandler(OnFileChange);

            PluginBase.MainForm.RegisterShortcutItem("ColtPlugin.MakeItLive", MakeItLiveKeys);
        }

        #endregion

        #region Menu items stuff

        private void CreateMenuItem(ToolStripMenuItem projectMenu)
        {
            menuItem = new ToolStripMenuItem(LocaleHelper.GetString("Menu.ExportToCOLT"), GetImage("colt_save.png"), new EventHandler(OnClick), null);
            menuItem.Enabled = false;
            projectMenu.DropDownItems.Add(menuItem);
        }

        private ToolStripButton CreateToolbarButton(ToolStrip toolStrip, String image, String hint, EventHandler handler)
        {
            ToolStripButton button = new ToolStripButton();
            button.Image = GetImage(image);
            button.Text = LocaleHelper.GetString(hint);
            button.DisplayStyle = ToolStripItemDisplayStyle.Image;
            button.Click += handler;
            toolStrip.Items.Add(button);
            return button;
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
            if (settingObject.SecurityToken != null)
            {
                new AppStarter(ExportAndOpen, settingObject.AutoRun);
            }

            else
            {
                new AppStarter(GetSecurityToken, true);
            }
        }

        private void OnClick2(Object sender, System.EventArgs e)
        {
            if (settingObject.SecurityToken != null)
            {
                new AppStarter(FindAndOpen, settingObject.AutoRun);
            }

            else
            {
                new AppStarter(GetSecurityToken, true);
            }
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
// debug
//settingObject.SecurityToken = null;
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

        /// <summary>
        /// Watches for COLT compilation errors log (optionally creates COLT folder if it does not exist)
        /// </summary>
        private void WatchErrorsLog(Boolean createFolder = false)
        {
            // shut down errors log watcher and its timer
            watcher.EnableRaisingEvents = false;
            if (timer != null) { timer.Stop(); timer = null; }

            // create the folder and subscribe to errors log updates
            IProject project = PluginBase.CurrentProject;

            String coltFolderPath = project.GetAbsolutePath(settingObject.WorkingFolder);
            if (createFolder && !Directory.Exists(coltFolderPath)) Directory.CreateDirectory(coltFolderPath);

            if (Directory.Exists(coltFolderPath))
            {
                pathToLog = Path.Combine(coltFolderPath, "compile_errors.log");
                watcher.Path = coltFolderPath;
                watcher.EnableRaisingEvents = true;
            }
        }

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

            // send the log line by line
            String[] messageLines = message.Split(new Char[] {'\r', '\n'});
            bool hasErrors = false;
            foreach (String line in messageLines) if (line.Length > 0)
            {
                int errorLevel = -3;
                if (line.Contains(incremental))
                {
                    try
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
                                TraceManager.Add(line.Replace(incremental, sources[i]), errorLevel);
                                hasErrors = true;
                                break;
                            }
                        }
                    }

                    catch (Exception)
                    {
                        // unexpected format, send as is
                        TraceManager.Add(line, errorLevel);
                    }
                }
                else
                {
                    // send as is
                    TraceManager.Add(line, errorLevel);
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

        #region Meta tags

        /// <summary>
        /// Generate meta tags
        /// </summary>
        private void MakeItLive()
        {
            ScintillaNet.ScintillaControl sci = PluginBase.MainForm.CurrentDocument.SciControl;
            if (sci == null) 
                return;

            IASContext context = ASCompletion.Context.ASContext.Context;
            if (context.CurrentClass == null || context.CurrentClass.IsVoid() || context.CurrentClass.LineFrom == 0) 
                return;

            // make member live
            int originalPos = sci.CurrentPos;
            int pos;
            int line;
            string indent;
            MemberModel member = context.CurrentMember;
            FlagType mask = FlagType.Function | FlagType.Dynamic;
            if (member != null && (member.Flags & mask) == mask) 
            {
                line = context.CurrentMember.LineFrom;
                indent = LineIndentPosition(sci, line);
                pos = sci.PositionFromLine(line) + indent.Length;
                string insert = "[LiveCodeUpdateListener(method=\"" + member.Name + "\")]\n" + indent;
                sci.SetSel(pos, pos);
                sci.ReplaceSel(insert);
                originalPos += insert.Length;
            }

            // make class live
            if (!Regex.IsMatch(sci.Text, "\\[Live\\]"))
            {
                line = context.CurrentClass.LineFrom;
                indent = LineIndentPosition(sci, line);
                pos = sci.PositionFromLine(line) + indent.Length;
                string insert = "[Live]\n" + indent;
                sci.SetSel(pos, pos);
                sci.ReplaceSel(insert);
                originalPos += insert.Length;
            }

            sci.SetSel(originalPos, originalPos);
        }

        private string LineIndentPosition(ScintillaNet.ScintillaControl sci, int line)
        {
            string txt = sci.GetLine(line);
            for (int i = 0; i < txt.Length; i++)
                if (txt[i] > 32) return txt.Substring(0, i);
            return "";
        }

        #endregion

        private void GetSecurityToken(Boolean param)
        {
            JsonRpcClient client = new JsonRpcClient();

            try
            {
                // knock
                client.Invoke("requestShortCode", new Object[] { LocaleHelper.GetString("Info.Description").TrimEnd(new Char[] { '.' }) });

                // if still here, user needs to enter the code
                Forms.FirstTimeDialog dialog = new Forms.FirstTimeDialog(settingObject.InterceptBuilds, settingObject.AutoRun);
                dialog.ShowDialog();

                // regardless of the code, set boolean options
                settingObject.AutoRun = dialog.AutoRun;
                settingObject.InterceptBuilds = dialog.InterceptBuilds;

                if ((dialog.ShortCode != null) && (dialog.ShortCode.Length == 4))
                {
                    // short code looks right - request security token
                    settingObject.SecurityToken = client.Invoke("obtainAuthToken", new Object[] { dialog.ShortCode }).ToString();
                }
            }

            catch (Exception details)
            {
                HandleAuthenticationExceptions(details);
            }
        }

        /// <summary>
        /// Makes production build and optionally runs its output
        /// </summary>
        private void ProductionBuild(Boolean run)
        {
            // make sure the COLT project is open
            // todo: currently no way to know if this fails, check the state before running the build in the future
            if (toolbarButton2.Enabled) FindAndOpen(false); else ExportAndOpen(false);

            try
            {
                JsonRpcClient client = new JsonRpcClient();
                client.Invoke("runProductionCompilation", new Object[] { settingObject.SecurityToken, /*run*/false });

                // leverage FD launch mechanism
                if (run)
                {
                    EventManager.DispatchEvent(this, new DataEvent(EventType.Command, "ProjectManager.PlayOutput", null));
                }
            }
            catch (Exception details)
            {
                HandleAuthenticationExceptions(details);
            }
        }


        /// <summary>
        /// Opens the project in COLT and optionally runs live session
        /// </summary>
        private void FindAndOpen(Boolean run)
        {
            // Create COLT subfolder if does not exist yet
            // While at that, start listening for colt/compile_errors.log changes
            WatchErrorsLog(true);

            // Find COLT project to open
            String coltFileName = GetCOLTFile();

            // Open it with default app (COLT)
            if (coltFileName != null)
            {
                try
                {
                    JsonRpcClient client = new JsonRpcClient();
                    client.Invoke("loadProject", new Object[] { settingObject.SecurityToken, coltFileName });
                    if (run) client.Invoke("runBaseCompilation", new Object[] { settingObject.SecurityToken });
                }
                catch (Exception details)
                {
                    HandleAuthenticationExceptions(details);
                }
            }

            else
            {
                toolbarButton2.Enabled = false;
            }

        }

        /// <summary>
        /// Exports the project to COLT and optionally runs live session
        /// </summary>
        private void ExportAndOpen(Boolean run)
        {
            // Create COLT subfolder if does not exist yet
            // While at that, start listening for colt/compile_errors.log changes
            WatchErrorsLog(true);

            // Create COLT project in it
            COLTRemoteProject project = ExportCOLTProject();
            if (project != null)
            {
                try
                {
                    JsonRpcClient client = new JsonRpcClient();
                    client.Invoke("createProject", new Object[] { settingObject.SecurityToken, project });

                    // Enable "open" button
                    toolbarButton2.Enabled = true;

                    // Remove older *.colt files
                    foreach (String oldFile in Directory.GetFiles(Path.GetDirectoryName(project.path), "*.colt"))
                    {
                        if (!project.path.Contains(Path.GetFileName(oldFile)))
                        {
                            File.Delete(oldFile);
                        }
                    }

                    if (run) client.Invoke("runBaseCompilation", new Object[] { settingObject.SecurityToken });
                }
                catch (Exception details)
                {
                    HandleAuthenticationExceptions(details);
                }
            }
        }

        /// <summary>
        /// Handles possible authentication exceptions
        /// </summary>
        private void HandleAuthenticationExceptions(Exception exception)
        {
            JsonRpcException rpcException = exception as JsonRpcException;
            if (rpcException != null)
            {
                // if the exception comes from rpc, we have two special situations to handle:
                // 1 short code was wrong (might happen a lot)
                // 2 security token was wrong (should never happen)
                // in both cases, we need to request new security token
                if ((rpcException.TypeName == "codeOrchestra.lcs.rpc.security.InvalidShortCodeException") ||
                    (rpcException.TypeName == "codeOrchestra.lcs.rpc.security.InvalidAuthTokenException"))
                {
                    settingObject.SecurityToken = null;
                }
            }

            TraceManager.Add(exception.ToString(), -1);
        }

        /// <summary>
        /// Returns path to existing COLT project or null.
        /// </summary>
        private String GetCOLTFile()
        {
            IProject project = PluginBase.CurrentProject;

            try
            {
                String[] files = Directory.GetFiles(project.GetAbsolutePath(settingObject.WorkingFolder), "*.colt");
                if (files.Length > 0) return files[0];
            }

            catch (Exception)
            {
            }

            return null;
        }

        /// <summary>
        /// Exports FD project setting to COLTRemoteProject instance.
        /// </summary>
        /// <returns></returns>
        private COLTRemoteProject ExportCOLTProject()
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

                    try
                    {
                        allowBuildInterception = false;
                        EventManager.DispatchEvent(this, new DataEvent(EventType.Command, "ProjectManager.BuildProject", null));
                    }

                    finally
                    {
                        allowBuildInterception = true;
                    }

                    return null;
                }

                // Create config copy with <file-specs>...</file-specs> commented out
                configCopy = Path.Combine("obj", projectName + "ConfigCopy.xml");
                File.WriteAllText(project.GetAbsolutePath(configCopy),
                    File.ReadAllText(project.GetAbsolutePath(configFile))
                        .Replace("<file-specs", "<!-- file-specs")
                        .Replace("/file-specs>", "/file-specs -->"));
            }


            // Export COLT project
            COLTRemoteProject result = new COLTRemoteProject();

            result.path = project.GetAbsolutePath(Path.Combine(settingObject.WorkingFolder, System.Guid.NewGuid() + ".colt"));

            result.name = project.Name;

            String[] libraryPaths = project.CompilerOptions.LibraryPaths.Clone() as String[];
            for (int i=0; i<libraryPaths.Length; i++) libraryPaths[i] = project.GetAbsolutePath(libraryPaths[i]);
            result.libraries = libraryPaths;

            result.targetPlayerVersion = project.MovieOptions.Version + ".0";

            result.mainClass = project.GetAbsolutePath(project.CompileTargets[0]);

            result.flexSDKPath = project.CurrentSDK;

            if (settingObject.FullConfig)
            {
                result.customConfigPath = project.GetAbsolutePath(configCopy);
            }

            String outputPath = project.OutputPath;
            int lastSlash = outputPath.LastIndexOf(@"\");
            if (lastSlash > -1)
            {
                result.outputPath = project.GetAbsolutePath(outputPath.Substring(0, lastSlash));
                result.outputFileName = outputPath.Substring(lastSlash + 1);
            }

            else
            {
                result.outputFileName = outputPath;
            }

            String[] sourcePaths = project.SourcePaths.Clone() as String[];
            for (int i=0; i<sourcePaths.Length; i++) sourcePaths[i] = project.GetAbsolutePath(sourcePaths[i]);
            result.sources = sourcePaths;


            // size, frame rate and background color
            String[] coltAdditionalOptionsKeys = {
                "-default-size",
                "-default-frame-rate",
                "-default-background-color"
            };
            String[] coltAdditionalOptions = {
                coltAdditionalOptionsKeys[0] + " " + project.MovieOptions.Width + " " + project.MovieOptions.Height,
                coltAdditionalOptionsKeys[1] + " " + project.MovieOptions.Fps,
                coltAdditionalOptionsKeys[2] + " " + project.MovieOptions.BackgroundColorInt
            };

            String additionalOptions = "";
            foreach (String option in project.CompilerOptions.Additional)
            {
                for (int i = 0; i < coltAdditionalOptionsKeys.Length; i++)
                {
                    if (option.Contains(coltAdditionalOptionsKeys[i]))
                    {
                        coltAdditionalOptions[i] = "";
                    }
                }
                additionalOptions += option + " ";
            }

            foreach (String option in coltAdditionalOptions)
            {
                additionalOptions += option + " ";
            }

            result.compilerOptions = additionalOptions.Trim() + (project.TraceEnabled ? " -debug" : "");

            return result;
        }
	}
}
