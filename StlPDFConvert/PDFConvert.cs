using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Security.Permissions;
using System.Threading;
using System.Runtime.InteropServices;
using System.Configuration;

namespace StlPDFConvert
{
    public partial class PDFConvert : Form
    {
        public static string OutPath = "";
        public static string InPath = "";

        public static PDFConvert mainFrm;

        public static int iWidth = 300;

        public static FileSystemWatcher watcher = new FileSystemWatcher();
        public static Dictionary<string, string> files;
        AppSettingsReader appSettings = new AppSettingsReader();

        /// <summary>
        /// 打印线程队列
        /// </summary>
        Thread ConvertThread = null;

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public PDFConvert()
        {
            mainFrm = this;
            Control.CheckForIllegalCrossThreadCalls = false;
            InitializeComponent();

            SetStatus(false);

            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            InPath = config.AppSettings.Settings["pdfInputPath"].Value;
            OutPath = config.AppSettings.Settings["pngOutputPath"].Value;
            this.txtIn.Text = InPath;
            this.txtOut.Text = OutPath;
            //appSettings.GetValue("pngOutputPath", typeof(string)).ToString();
        }

        private void btnInFileView_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.txtIn.Text = fbd.SelectedPath;
                string InPath = fbd.SelectedPath;
                SetConfigValue("pdfInputPath", fbd.SelectedPath);
            }
        }

        private void btnOutFileView_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.txtOut.Text = fbd.SelectedPath;
                SetConfigValue("pngOutputPath", fbd.SelectedPath);
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            SetStatus(true);
            OutPath = this.txtOut.Text;
            InPath = this.txtIn.Text;
            files = new Dictionary<string, string>();
            Int32.TryParse(this.txtWidth.Text, out iWidth);

            if (string.IsNullOrEmpty(OutPath) || string.IsNullOrEmpty(InPath) || iWidth < 100)
            {
                MessageBox.Show("目录为空，单页宽度不能小于100!");
                return;
            }

            ConvertThread = new Thread(new ParameterizedThreadStart(Start));
            ConvertThread.SetApartmentState(ApartmentState.STA);//.ApartmentState = ApartmentState.STA;
            ConvertThread.Start(this.txtIn.Text);
        }


        private static void Start(object obj)
        {
            watcher.Path = obj.ToString();
            watcher.Filter = "*.pdf";
            watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
           | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            watcher.IncludeSubdirectories = true;

            watcher.Changed += new FileSystemEventHandler(OnChanged);
            watcher.Renamed += new RenamedEventHandler(OnChanged);
            watcher.EnableRaisingEvents = true;
        }

        // Define the event handlers.
        private static void OnChanged(object source, FileSystemEventArgs e)
        {
            //watcher.EnableRaisingEvents = false;
            // Specify what is done when a file is changed, created, or deleted.
            Console.WriteLine("File: " + e.FullPath + " " + e.ChangeType);
            //Convert cv = new Convert();
            //cv.ToConvert(e.FullPath, OutPath);
            if (e.ChangeType != WatcherChangeTypes.Deleted)
            {
                string filepath = e.FullPath; //mypath + "//" + cr.Name;
                FileInfo fi = new FileInfo(filepath);

                if (!fi.Exists)
                {
                    Console.WriteLine("file not exits!!");
                }

            HELLO: try
                {

                    fi.OpenRead().Close();
                }
                catch (IOException ex)
                {
                    Console.WriteLine(ex.Message);
                    Thread.Sleep(1000);
                    goto HELLO;
                }


                if (!files.ContainsKey(fi.Name))
                {
                    mainFrm.tsst.Text = "开始转换" + fi.Name + "！";
                    files.Add(fi.Name, fi.Name);
                    mainFrm.lstIn.Items.Add(fi.Name);
                    Thread staThread = new Thread(new ParameterizedThreadStart(ToConvert));
                    staThread.SetApartmentState(ApartmentState.STA);//.ApartmentState = ApartmentState.STA;
                    staThread.Start(e.FullPath);
                }
            }

            //watcher.EnableRaisingEvents = true;
        }

        private static void OnRenamed(object source, RenamedEventArgs e)
        {
            // Specify what is done when a file is renamed.
            Console.WriteLine("File: {0} renamed to {1}", e.OldFullPath, e.FullPath);

        }

        public static void ToConvert(object strInFilePath)
        {
            Acrobat.CAcroPDDoc pdfDoc;
            Acrobat.CAcroPDPage pdfPage;
            Acrobat.CAcroRect pdfRect;
            Acrobat.CAcroPoint pdfPoint;

            pdfDoc = (Acrobat.CAcroPDDoc)Microsoft.VisualBasic.Interaction.CreateObject("AcroExch.PDDoc", "");
            bool ret = pdfDoc.Open(strInFilePath.ToString());

            if (!ret)
            {
                return;
                //throw new FileNotFoundException();
            }

            string strOriFileName = strInFilePath.ToString().Substring(strInFilePath.ToString().LastIndexOf(@"\") + 1);
            string strFileName = strOriFileName.Replace(".pdf", ".png");
            string outputFile = OutPath + "\\" + strFileName;

            int pageCount = pdfDoc.GetNumPages();

            pdfPage = (Acrobat.CAcroPDPage)pdfDoc.AcquirePage(0);
            pdfPoint = (Acrobat.CAcroPoint)pdfPage.GetSize();

            pdfRect = (Acrobat.CAcroRect)Microsoft.VisualBasic.Interaction.CreateObject("AcroExch.Rect", "");

            pdfRect.Left = 0;
            pdfRect.right = pdfPoint.x;
            pdfRect.Top = 0;
            pdfRect.bottom = pdfPoint.y;

            int thumbnailWidth = iWidth;
            int thumbnailHeight = (int)(thumbnailWidth * (pdfPoint.y * 1.0) / pdfPoint.x);

            double rate = (pdfPoint.y * 1.0) / pdfPoint.x;
            int imgwidth = 3 * thumbnailWidth + 30;
            int imgHeight = (int)(Math.Ceiling((pageCount > 9 ? 9 : pageCount) / 3.0) * thumbnailHeight) + 30;

            Bitmap thumbnailBitmap = new Bitmap(imgwidth, imgHeight,
                                               System.Drawing.Imaging.PixelFormat.Format32bppArgb);


            for (int i = 0; i < pageCount && i < 9; i++)
            {
                // Get the first page
                pdfPage = (Acrobat.CAcroPDPage)pdfDoc.AcquirePage(i);
                // Render to clipboard, scaled by 100 percent (ie. original size)
                // Even though we want a smaller image, better for us to scale in .NET
                // than Acrobat as it would greek out small text
                // see http://www.adobe.com/support/techdocs/1dd72.htm
                pdfPage.CopyToClipboard(pdfRect, 0, 0, 100);

                IDataObject clipboardData = Clipboard.GetDataObject();

                if (clipboardData.GetDataPresent(DataFormats.Bitmap))
                {
                    Bitmap pdfBitmap = (Bitmap)clipboardData.GetData(DataFormats.Bitmap);

                    // Render to small image using the bitmap class
                    Image pdfImage = pdfBitmap.GetThumbnailImage(thumbnailWidth, thumbnailHeight,
                                                                 null, IntPtr.Zero);

                    using (Graphics thumbnailGraphics = Graphics.FromImage(thumbnailBitmap))
                    {
                        // Draw rendered pdf image to new blank bitmap
                        thumbnailGraphics.DrawImage(pdfImage, i % 3 * (thumbnailWidth + 2), i / 3 * (thumbnailHeight + 2), thumbnailWidth, thumbnailHeight);
                        thumbnailGraphics.Save();
                    }

                }

            }
            pdfDoc.Close();
            Marshal.ReleaseComObject(pdfPage);
            Marshal.ReleaseComObject(pdfRect);
            Marshal.ReleaseComObject(pdfDoc);
            thumbnailBitmap.Save(outputFile);
            thumbnailBitmap.Dispose();

            mainFrm.lstIn.Items.Remove(strOriFileName);
            mainFrm.lstOut.Items.Add(strFileName);
            if (files.ContainsKey(strOriFileName))
            {
                files.Remove(strOriFileName);
            }
            mainFrm.tsst.Text = strFileName + "  转换完成！";
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (ConvertThread.IsAlive)
            {
                ConvertThread.Join(100);
            }

            watcher.EnableRaisingEvents = false;

            SetStatus(false);
        }


        //private void AutoRun_CheckedChanged(object sender, EventArgs e)
        //{
        //    if (AutoRun.Checked)//开机自动启动
        //    {
        //        try
        //        {
        //            RegistryKey runKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
        //            runKey.SetValue("文件夹监测.exe", System.Windows.Forms.Application.ExecutablePath);
        //            runKey.Close();
        //        }
        //        catch (IOException)
        //        {
        //            return;
        //        }
        //    }
        //    else  //不开机自动启动注册表信息删除
        //    {
        //        RegistryKey software = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
        //        string[] aimnames = software.GetValueNames();
        //        foreach (string aimKey in aimnames)
        //        {
        //            if (aimKey.Equals("文件夹监测.exe"))
        //            {
        //                software.DeleteValue("文件夹监测.exe");
        //                software.Close();
        //                break;
        //            }
        //        }
        //    }
        //}


        private void SetStatus(bool isTrans)
        {
            this.txtIn.Enabled = !isTrans;
            this.txtOut.Enabled = !isTrans;
            this.txtWidth.Enabled = !isTrans;
            this.btnStart.Enabled = !isTrans;
            this.btnInFileView.Enabled = !isTrans;
            this.btnOutFileView.Enabled = !isTrans;
            this.btnStop.Enabled = isTrans;
        }


        /// <summary>
        /// 修改AppSettings中配置
        /// </summary>
        /// <param name="key">key值</param>
        /// <param name="value">相应值</param>
        public static bool SetConfigValue(string key, string value)
        {
            try
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                if (config.AppSettings.Settings[key] != null)
                    config.AppSettings.Settings[key].Value = value;
                else
                    config.AppSettings.Settings.Add(key, value);
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void PDFConvert_FormClosing(object sender, FormClosingEventArgs e)
        {

        }


    }
}
