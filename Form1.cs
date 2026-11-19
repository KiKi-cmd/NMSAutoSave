using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static System.Net.WebRequestMethods;


namespace NMSAutoSave
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        private string CurrentDirectory= @"C:\Users\" + Environment.UserName + @"\AppData\Roaming\HelloGames\NMS";
        private void Form1_Load(object sender, EventArgs e)
        {           
            Process[] processes = Process.GetProcessesByName("NMS");
            if (processes.Length == 0)
            {
                Process.Start(@"steam://rungameid/275850");
            }
            DirectoryInfo[] dirs = new DirectoryInfo(CurrentDirectory).GetDirectories();
            var dir= dirs.OrderByDescending(f => f.LastWriteTime).First();
            CurrentDirectory = CurrentDirectory + @"\" + dir.Name;
         
            if (!Directory.Exists(CurrentDirectory + @"\BackUp"))
            {
                Directory.CreateDirectory(CurrentDirectory + @"\BackUp");
            }


            processes = Process.GetProcessesByName("NMS");
            if (processes.Length > 0)
            {
                AutoSave();
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            Process[] processes = Process.GetProcessesByName("NMS");
            AutoSave();
            if (processes.Length == 0)
            {
                Application.Exit();
            }
        }

        private void AutoSave()
        {
            string datetime_Now = DateTime.Now.ToString("yyyy-MM-dd_HH_mm_ss");
            Directory.CreateDirectory(CurrentDirectory + @"\BackUp\" + datetime_Now);
            FileInfo[] files = new DirectoryInfo(CurrentDirectory).GetFiles();
            var latestFile = files.OrderByDescending(f => f.LastWriteTime).First();
            if (latestFile.LastWriteTime < DateTime.Now.AddMinutes(-10))
            {
                return;
            }
            foreach (FileInfo file in files)
            {
                if (file.Name.Contains(".hg"))
                {
                    try
                    {
                        file.CopyTo(CurrentDirectory + @"\BackUp\" + datetime_Now + @"\" + file.Name);
                    }
                    catch { }
                }
            }
            richTextBox1.Text = @"你不是……孤独……一人
正在……自动备份……游戏……存档
上次备份时间:
" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
