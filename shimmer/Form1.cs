using NetDust;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace shimmer
{
    public partial class Form1 : Form
    {
        private NetDustEngine _engine;
        public Form1()
        {
            InitializeComponent();
            _engine = new NetDustEngine();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "Net Dust (*.rpp;*.nd)|*.rpp;*.nd|All files (*.*)|*.*";
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (dlg.ShowDialog() != DialogResult.OK) return;

                string path = dlg.FileName;
                string content = File.ReadAllText(path, Encoding.UTF8);

                var ctx = _engine.ExecuteScript(content);

                listBox1.Items.Clear();
                foreach (var line in ctx.Log)
                {
                    listBox1.Items.Add(line);
                }

                textBoxVars.Text = string.Join(Environment.NewLine, ctx.Vars.Select(kv => $"{kv.Key} = \"{kv.Value}\""));
                textBoxNums.Text = string.Join(Environment.NewLine, ctx.Nums.Select(kv => $"{kv.Key} = {kv.Value}"));
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}
