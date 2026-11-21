using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;


namespace NMSAutoSave
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        /// <summary>存档根目录（%APPDATA%\HelloGames\NMS）；加载后会追加具体的 st_xxx 子目录。</summary>
        private string CurrentDirectory = @"C:\Users\" + Environment.UserName + @"\AppData\Roaming\HelloGames\NMS";

        /// <summary>备份根目录。</summary>
        private string BackUpRoot { get { return CurrentDirectory + @"\BackUp"; } }

        /// <summary>上一次成功备份的内容指纹，用于跳过完全重复的备份。</summary>
        private string _lastFingerprint;

        /// <summary>由本工具拉起游戏后的计时起点；MinValue 表示游戏不是本工具拉起的。</summary>
        private DateTime _launchWaitStart = DateTime.MinValue;

        /// <summary>本工具拉起游戏后，等待游戏进程出现的宽限时间（分钟）。</summary>
        private const int LaunchGraceMinutes = 15;

        /// <summary>备份目录保留份数上限，超出后删除最旧的。</summary>
        private const int MaxBackupCount = 100;

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                bool launchedByTool = false;
                Process[] processes = Process.GetProcessesByName("NMS");
                if (processes.Length == 0)
                {
                    try
                    {
                        Process.Start(@"steam://rungameid/275850");
                        launchedByTool = true;
                    }
                    catch (Exception ex)
                    {
                        // Steam 未安装或 steam:// 协议未注册时，不应该让整个工具崩溃
                        WriteLog("拉起游戏失败（Steam 未安装或协议未注册）：" + ex.Message);
                    }
                }

                string saveRoot = CurrentDirectory;
                if (!Directory.Exists(saveRoot))
                {
                    FailFast("找不到存档目录：\n" + saveRoot
                        + "\n\n请先启动《无人深空》并保存过一次存档，再运行本工具。");
                    return;
                }

                DirectoryInfo[] dirs = new DirectoryInfo(saveRoot).GetDirectories();
                if (dirs.Length == 0)
                {
                    FailFast("存档目录下没有任何存档文件夹：\n" + saveRoot
                        + "\n\n请先启动《无人深空》并保存过一次存档，再运行本工具。");
                    return;
                }

                var dir = dirs.OrderByDescending(f => f.LastWriteTime).First();
                CurrentDirectory = CurrentDirectory + @"\" + dir.Name;

                if (!Directory.Exists(BackUpRoot))
                {
                    Directory.CreateDirectory(BackUpRoot);
                }
                WriteLog("启动，存档目录：" + CurrentDirectory);

                if (launchedByTool)
                {
                    _launchWaitStart = DateTime.Now;
                    ShowStatus("已通过 Steam 启动游戏\n等待游戏进程出现……\n（最长等待 "
                        + LaunchGraceMinutes + " 分钟）");
                }

                processes = Process.GetProcessesByName("NMS");
                if (processes.Length > 0)
                {
                    AutoSave();
                }
            }
            catch (Exception ex)
            {
                WriteLog("初始化异常：" + ex);
                FailFast("初始化失败：\n" + ex.Message);
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                bool running = Process.GetProcessesByName("NMS").Length > 0;

                if (running)
                {
                    _launchWaitStart = DateTime.MinValue;
                    AutoSave();
                    return;
                }

                // 进程不在：若游戏是本工具刚刚拉起的，给足加载时间，不要立刻退出
                if (_launchWaitStart != DateTime.MinValue
                    && (DateTime.Now - _launchWaitStart).TotalMinutes < LaunchGraceMinutes)
                {
                    ShowStatus("等待游戏启动中……\n已等待 "
                        + (int)(DateTime.Now - _launchWaitStart).TotalMinutes + " 分钟"
                        + "（上限 " + LaunchGraceMinutes + " 分钟）");
                    return;
                }

                // 游戏确实已退出：做最后一次备份后结束
                AutoSave();
                WriteLog("游戏进程已退出，工具结束");
                Application.Exit();
            }
            catch (Exception ex)
            {
                // 定时回调中逃逸的异常会直接终止进程，必须在这里兜住
                WriteLog("定时任务异常：" + ex);
            }
        }

        private void AutoSave()
        {
            try
            {
                FileInfo[] files = new DirectoryInfo(CurrentDirectory).GetFiles();
                if (files.Length == 0)
                {
                    WriteLog("存档目录为空，跳过本次备份");
                    return;
                }

                // 目录可能被清空，用 FirstOrDefault 避免 InvalidOperationException
                var latestFile = files.OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
                if (latestFile == null || latestFile.LastWriteTime < DateTime.Now.AddMinutes(-10))
                {
                    // 10 分钟内没有任何写入，说明游戏没在存档（菜单/暂停），无需备份
                    return;
                }

                var targets = files.Where(f => f.Name.Contains(".hg")).ToArray();
                if (targets.Length == 0)
                {
                    WriteLog("没有找到 .hg 存档文件，跳过本次备份");
                    return;
                }

                // 内容没有变化就不必再存一份完全相同的副本
                string fingerprint = Fingerprint(targets);
                if (fingerprint == _lastFingerprint)
                {
                    return;
                }

                // 先确认要备份，再创建目录，避免留下一堆空目录
                string datetime_Now = DateTime.Now.ToString("yyyy-MM-dd_HH_mm_ss");
                string destDir = Path.Combine(BackUpRoot, datetime_Now);
                Directory.CreateDirectory(destDir);

                int ok = 0;
                int fail = 0;
                foreach (FileInfo file in targets)
                {
                    try
                    {
                        file.CopyTo(Path.Combine(destDir, file.Name));
                        ok++;
                    }
                    catch (Exception ex)
                    {
                        // 不再静默吞掉，记录下来并在界面上提示
                        fail++;
                        WriteLog("复制失败 " + file.Name + "：" + ex.Message);
                    }
                }

                if (ok == 0)
                {
                    // 一份都没成功，删掉刚建的空目录
                    try { Directory.Delete(destDir, true); } catch { }
                    WriteLog("本次备份全部失败");
                    return;
                }

                _lastFingerprint = fingerprint;
                PruneBackups();

                string text = @"你不是……孤独……一人
正在……自动备份……游戏……存档
上次备份时间:
" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                if (fail > 0)
                {
                    text += "\n⚠ 有 " + fail + " 个文件复制失败，详见 _autosave.log";
                }
                ShowStatus(text);
                WriteLog("备份完成：" + datetime_Now + "，成功 " + ok + " 个，失败 " + fail + " 个");
            }
            catch (Exception ex)
            {
                WriteLog("备份过程异常：" + ex);
            }
        }

        /// <summary>
        /// 备份内容指纹：文件名 + 大小 + 修改时间。任意一项变化即视为新内容。
        /// </summary>
        private static string Fingerprint(FileInfo[] files)
        {
            StringBuilder sb = new StringBuilder();
            foreach (FileInfo f in files.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append(f.Name).Append('|').Append(f.Length).Append('|')
                  .Append(f.LastWriteTimeUtc.Ticks).Append(';');
            }
            return sb.ToString();
        }

        /// <summary>
        /// 只保留最近 MaxBackupCount 份备份。
        /// 仅处理符合 yyyy-MM-dd_HH_mm_ss 命名的目录，不会碰用户手动放进来的文件夹。
        /// </summary>
        private void PruneBackups()
        {
            try
            {
                var dirs = new DirectoryInfo(BackUpRoot).GetDirectories()
                    .Where(d => IsBackupName(d.Name))
                    .OrderByDescending(d => d.Name, StringComparer.Ordinal)
                    .ToArray();

                for (int i = MaxBackupCount; i < dirs.Length; i++)
                {
                    try
                    {
                        dirs[i].Delete(true);
                    }
                    catch (Exception ex)
                    {
                        WriteLog("清理旧备份失败 " + dirs[i].Name + "：" + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog("清理旧备份异常：" + ex.Message);
            }
        }

        /// <summary>判断目录名是否为 yyyy-MM-dd_HH_mm_ss 形式的备份目录。</summary>
        private static bool IsBackupName(string name)
        {
            if (name == null || name.Length != 19)
            {
                return false;
            }
            if (name[4] != '-' || name[7] != '-' || name[10] != '_'
                || name[13] != '_' || name[16] != '_')
            {
                return false;
            }
            for (int i = 0; i < name.Length; i++)
            {
                if (i == 4 || i == 7 || i == 10 || i == 13 || i == 16)
                {
                    continue;
                }
                if (!char.IsDigit(name[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private void ShowStatus(string text)
        {
            richTextBox1.Text = text;
        }

        private void FailFast(string message)
        {
            MessageBox.Show(message, "无人深空自动存档", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Application.Exit();
        }

        private void WriteLog(string message)
        {
            try
            {
                // 日志路径按当前备份目录动态推导，不依赖 Form1_Load 的执行顺序
                string dir = BackUpRoot;
                string path;
                if (!string.IsNullOrEmpty(dir))
                {
                    try
                    {
                        Directory.CreateDirectory(dir);
                        path = Path.Combine(dir, "_autosave.log");
                    }
                    catch
                    {
                        path = Path.Combine(Path.GetTempPath(), "NMSAutoSave.log");
                    }
                }
                else
                {
                    path = Path.Combine(Path.GetTempPath(), "NMSAutoSave.log");
                }

                File.AppendAllText(path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch
            {
                // 记日志本身失败不应影响主流程
            }
        }
    }
}
