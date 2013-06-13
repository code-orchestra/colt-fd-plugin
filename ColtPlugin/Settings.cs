using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Text;

namespace ColtPlugin
{
    [Serializable]
    public class Settings
    {
        public String SecurityToken;

        private String workingFolder = "colt";
        private Boolean autorun = true;
        private Boolean fullConfig = false;
        private Boolean interceptBuilds = false;
        
        /// <summary> 
        /// Get and sets colt folder
        /// </summary>
        [DisplayName("COLT Working Folder")]
        [Description("Path to COLT working folder."), DefaultValue("colt")]
        public String WorkingFolder 
        {
            get { return this.workingFolder; }
            set { this.workingFolder = value; }
        }

        /// <summary> 
        /// Get and sets full autorun flag
        /// </summary>
        [DisplayName("Automatically run COLT project")]
        [Description("Automatically compile and run COLT project after opening it in COLT."), DefaultValue(true)]
        public Boolean AutoRun
        {
            get { return this.autorun; }
            set { this.autorun = value; }
        }

        /// <summary> 
        /// Get and sets full config flag
        /// </summary>
        [DisplayName("Load Full FD Configuration")]
        [Description("Attempt to load full FD configuration in COLT. FD project must be built at least once first."), DefaultValue(false)]
        public Boolean FullConfig 
        {
            get { return this.fullConfig; }
            set { this.fullConfig = value; }
        }

        /// <summary> 
        /// Get and sets production builds flag
        /// </summary>
        [DisplayName("Use COLT for FD builds")]
        [Description("Use COLT fast compiler to build your FD projects."), DefaultValue(false)]
        public Boolean InterceptBuilds
        {
            get { return this.interceptBuilds; }
            set { this.interceptBuilds = value; }
        }
    }

}
