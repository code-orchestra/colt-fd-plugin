using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace ColtPlugin.Forms
{
    public partial class FirstTimeDialog : Form
    {
        public Boolean AutoRun = true;
        public Boolean InterceptBuilds = false;
        public String ShortCode = null;

        public FirstTimeDialog()
        {
            InitializeComponent();
        }

        public FirstTimeDialog(Boolean interceptBuilds, Boolean autorun)
        {
            InitializeComponent();
            InterceptBuilds = checkBox1.Checked = interceptBuilds;
            AutoRun = checkBox2.Checked = autorun;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            AutoRun = checkBox2.Checked;
            InterceptBuilds = checkBox1.Checked;
            ShortCode = textBox1.Text;

            Close();
        }
    }
}
