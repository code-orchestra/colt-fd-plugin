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
        public Boolean InterceptBuilds = false;
        public String ShortCode = null;

        public FirstTimeDialog()
        {
            InitializeComponent();
        }

        public FirstTimeDialog(Boolean interceptBuilds)
        {
            InitializeComponent();
            InterceptBuilds = checkBox1.Checked = interceptBuilds;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            InterceptBuilds = checkBox1.Checked;
            ShortCode = textBox1.Text;

            Close();
        }
    }
}
