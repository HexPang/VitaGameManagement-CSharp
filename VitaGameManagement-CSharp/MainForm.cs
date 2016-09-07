using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.FtpClient;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VitaGameManagement_CSharp
{
    public partial class MainForm : Form
    {
        private delegate void LibraryAsyncEventHandler();
        private delegate void drawGameListDelegate();
        private delegate void PatchingDelegate(string file, string patchFile, ListViewItem listViewItem);
        private delegate void PatchingMessage(string message);
        private delegate void UpdatePatchingMessageDelegate(ListViewItem item, string message);
        private delegate void LoadLibraryDelegate();

        private FTPManager manager;
        private List<VitaPackageHelper.VitaPackage> packages;
        private bool libraryLoading = false;
        public MainForm()
        {
            InitializeComponent();
        }

        private void chooseGameFolder_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                libraryPath.Text = fbd.SelectedPath;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ConfigurationManager.AddUpdateAppSettings("library", libraryPath.Text);
            ConfigurationManager.AddUpdateAppSettings("vita_ip", vita_ip.Text);
            ConfigurationManager.AddUpdateAppSettings("vita_port", vita_port.Text);
            ConfigurationManager.AddUpdateAppSettings("cma_path", cma_path.Text);
            this.initGameLibrary();
            this.reloadSetting();
        }


        private void loadGameLibrary()
        {
            packages = VitaPackageHelper.Helper.loadPackages(libraryPath.Text);
        }

        private void addGamesToListView()
        {
            if (packages.Count > 0)
            {
                GameListView.Items.Clear();
                iconImageList.Images.Clear();
                foreach (VitaPackageHelper.VitaPackage package in packages)
                {
                    long size = new FileInfo(package.fileName).Length;
                    string fileSize = String.Format(new FileSizeFormatProvider(), "{0:fs}", size);

                    ListViewItem item = new ListViewItem(new String[] { package.appId, package.sfoData["TITLE"], package.region, fileSize, package.fileName, "OK", package.sfoData["APP_VER"] });
                    String iconFile = package.sfoData["CONTENT_ID"].Length > 0 ? package.sfoData["CONTENT_ID"] : package.sfoData["TITLE"];
                    iconFile = "icons//" + iconFile + ".jpg";
                    Image image = Image.FromFile(iconFile);

                    if(image != null)
                    {
                        iconImageList.Images.Add(image);
                        item.ImageIndex = iconImageList.Images.Count - 1;
                    }
                    GameListView.Items.Add(item);
                }
            }
            libraryLoading = false;
            tabPage1.Text = "Games";
            tabControl1.Enabled = true;
        }

		private void gameLibraryCallback(IAsyncResult result)
		{
			((LibraryAsyncEventHandler)result.AsyncState).EndInvoke(result);
            drawGameListDelegate dgld = new drawGameListDelegate(this.addGamesToListView);
            this.BeginInvoke(dgld);
        }

		private void initGameLibrary()
		{
            tabPage1.Text = "Loading...";
            tabControl1.Enabled = false;
            libraryLoading = true;
            LibraryAsyncEventHandler libraryAsync = new LibraryAsyncEventHandler(this.loadGameLibrary);
			libraryAsync.BeginInvoke(new AsyncCallback(this.gameLibraryCallback), libraryAsync);
		}

        private void reloadSetting()
        {
            libraryPath.Text = ConfigurationManager.ReadSetting("library");
            vita_ip.Text = ConfigurationManager.ReadSetting("vita_ip");
            vita_port.Text = ConfigurationManager.ReadSetting("vita_port");
            cma_path.Text = ConfigurationManager.ReadSetting("cma_path");
            if (vita_ip.Text != "" && vita_port.Text != "")
            {
                manager = FTPManager.instance(vita_ip.Text, vita_port.Text);
                manager.StartFTPWorker();
                ftpStatus.Text = "Starting...";
            }
            else
            {
                ftpStatus.Text = "Need PSVita IP And Port";
            }
        }
        private void MainForm_Load(object sender, EventArgs e)
        {
            this.reloadSetting();
            if (libraryPath.Text != "")
            {
                this.initGameLibrary();
            }
        }

        private void uploadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            pSPSaveDataToolStripMenuItem.DropDownItems.Clear();
            if(cma_path.Text.Length > 0 && GameListView.SelectedItems.Count == 1)
            {
                pSPSaveDataToolStripMenuItem.Enabled = true;
                string folder = cma_path.Text;
                string[] users = Directory.GetDirectories(folder + "/PSAVEDATA");
                foreach(string user in users)
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(user);
                    ToolStripMenuItem item = new ToolStripMenuItem(dirInfo.Name);
                    pSPSaveDataToolStripMenuItem.DropDownItems.Add(item);
                    string[] games = Directory.GetDirectories(user);
                    foreach(string game in games)
                    {
                        DirectoryInfo gameInfo = new DirectoryInfo(game);
                        ToolStripMenuItem gameItem = new ToolStripMenuItem(gameInfo.Name);
                        item.DropDownItems.Add(gameItem);
                        gameItem.Tag = dirInfo.Name + "/" + gameInfo.Name;
                        gameItem.Click += GameItem_Click;
                        if (File.Exists(game + "/param.sfo"))
                        {
                            Dictionary<string, string> sfo = VitaPackageHelper.Helper.parserSFO(game + "/param.sfo");
                            if (sfo["TITLE"] != null)
                            {
                                gameItem.Text = sfo["TITLE"];
                            }
                            if(File.Exists(game + "/ICON0.PNG"))
                            {
                                gameItem.Image = Image.FromFile(game + "/ICON0.PNG");
                            }
                        }
                    }
                }
            }else
            {
                pSPSaveDataToolStripMenuItem.Enabled = false;
            }
        }
        private void BeginUpdateListItemState(ListViewItem item,string text)
        {
            UpdatePatchingMessageDelegate upmd = new UpdatePatchingMessageDelegate(UpdatePatchingMessage);
            this.BeginInvoke(upmd, item, text);
        }
        private delegate void CopyVPKToFolder(string source,string dest,ListViewItem item);
        private void CopyFileAsync(string source,string dest,ListViewItem item)
        {
            BeginUpdateListItemState(item, "Copying...");
            FileStream fs = File.OpenRead(source);
            if (File.Exists(dest))
            {
                File.Delete(dest);
            }
            FileStream fs1 = File.Create(dest);
            Byte[] buffer = new Byte[2048];
            int read = fs.Read(buffer, 0, 2048);
            long copied = 0;
            long total = new FileInfo(source).Length;
            while(read > 0)
            {
                fs1.Write(buffer, 0, read);
                copied += read;
                BeginUpdateListItemState(item, String.Format("{0:00.0}%", copied / total * 100));
                read = fs.Read(buffer, 0, 2048);
            }
            fs1.Flush();
            fs1.Close();
            fs.Close();
            BeginUpdateListItemState(item, "Done.");
            MessageBox.Show("File as been copied as GAME.BIN.");
        }
        private void GameItem_Click(object sender, EventArgs e)
        {
            ToolStripItem item = sender as ToolStripItem;
            if (item != null && GameListView.SelectedItems.Count == 1)
            {
                //item.tag
                String folderName = item.Tag as String;
                if(folderName != null)
                {
                    String fileName = GameListView.SelectedItems[0].SubItems[4].Text;
                    String Path = cma_path.Text + "/PSAVEDATA/" + folderName + "/GAME.BIN";
                    CopyVPKToFolder cvtf = new CopyVPKToFolder(this.CopyFileAsync);
                    cvtf.BeginInvoke(fileName,Path,GameListView.SelectedItems[0],null,null);
                }
            }
        }

        private void MainForm_Validating(object sender, CancelEventArgs e)
        {
            manager.StopFTPWorker();
        }

        private long last_uploaded;
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (manager != null)
            {
                if (manager.isWorking())
                {
                    if(manager.error != "" && manager.error != null)
                    {
                        ftpStatus.Text = manager.error;
                    }else
                    {
                        if (manager.getQueueCount() > 0)
                        {
                            FTPQueue queue = manager.currentQueue;
                            String text = "";
                            if(queue != null)
                            {
                                if(last_uploaded == 0)
                                {
                                    last_uploaded = queue.uploaded;
                                }
                                long speed = queue.uploaded - last_uploaded;
                                last_uploaded = queue.uploaded;
                                String speed_text = String.Format(new FileSizeFormatProvider(), "{0:fs}/s", speed);
                                double progress = (double)queue.uploaded / (double)queue.total * 100;
                                uploadProgress.Value = (int)progress > 100 ? 100 : (int)progress;
                                text = speed_text;
                                if (queue.obj != null)
                                {
                                    ListViewItem item = queue.obj as ListViewItem;
                                    if (item != null)
                                    {
                                        if(uploadProgress.Value == 100)
                                        {
                                            item.SubItems[5].Text = "Done";
                                        }
                                        else
                                        {
                                            item.SubItems[5].Text = uploadProgress.Value + "%";
                                        }
                                        
                                    }
                                }
                            }
                            ftpStatus.Text = String.Format("{0} Queue {1}", manager.getQueueCount(), text);

                        }
                        else
                        {
                            ftpStatus.Text = "Idle";
                        }
                    }
           
                }else
                {
                    ftpStatus.Text = "Stop";
                }
            }
        }

        private void folderTree_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            //Connect to FTP Server
            if (manager != null)
            {
                string path = "/";
                if (e.Node.Text.StartsWith("/"))
                {
                    path = e.Node.Text;
                    e.Node.Nodes.Clear();
                }else
                {
                    folderTree.Nodes.RemoveByKey("folder");
                }
                IEnumerable<string> folders = manager.listFolder(path);
                
                foreach (string folder in folders)
                {
                    if(path == "/")
                    {
                        folderTree.Nodes.Add("folder", folder);
                    }else
                    {
                        e.Node.Nodes.Add("folder", folder);
                    }
                    
                }
            }
        }

        private void folderTree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if(manager != null)
            {
                if(e.Node.Text.StartsWith("/"))
                {
                    IEnumerable<FtpListItem> files = manager.listFile(e.Node.Text);
                    fileListView.Items.Clear();
                    foreach(FtpListItem file in files)
                    {
                        ListViewItem item = new ListViewItem();
                        item.Text = file.Name;
                        item.ToolTipText = String.Format(new FileSizeFormatProvider(),"{0:fs}", file.Size);
                        item.ImageIndex = 0;
                        fileListView.Items.Add(item);
                    }
                }
            }
        }


        private void UpdatePatchingMessage(ListViewItem item,string message)
        {
            item.SubItems[5].Text = message;
        }

        private void PatchGameAsync(string file,string patchFile,ListViewItem item)
        {
            PatchingMessage pm = msg => {
                UpdatePatchingMessageDelegate upmd = new UpdatePatchingMessageDelegate(UpdatePatchingMessage);
                this.BeginInvoke(upmd, item, msg.ToString());
            };
            VitaPackageHelper.Helper.PATCH_RESULT succ = VitaPackageHelper.Helper.patchPackage(file, patchFile,pm);
            if (succ == VitaPackageHelper.Helper.PATCH_RESULT.SUCCESS)
            {
                LoadLibraryDelegate lld = new LoadLibraryDelegate(initGameLibrary);
                this.BeginInvoke(lld);
                MessageBox.Show("Patching Successful.");
            }
            else if (succ == VitaPackageHelper.Helper.PATCH_RESULT.SFO_NOT_MATCH)
            {
                MessageBox.Show("Can not Patch this game,CONTENT_ID not match.Please check CONTENT_ID in param.sfo.");
            }
            else if (succ == VitaPackageHelper.Helper.PATCH_RESULT.VERSION_SAME)
            {
                MessageBox.Show("Can not Patch this game.APP_VER same.might be already patched?");
            }
        }
        const long PATCHING_TAG = 0x160109;
        private void patchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            
            if (GameListView.SelectedItems.Count == 1)
            {
                ListViewItem item = GameListView.SelectedItems[0];
                String file = item.SubItems[4].Text;
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = "*.zip|*.zip|*.vpk|*.vpk";
                if(ofd.ShowDialog() == DialogResult.OK)
                {
                    DialogResult result = MessageBox.Show("Do you want create a back before patch?", "Patching", MessageBoxButtons.YesNoCancel);
                    if(result == DialogResult.Cancel)
                    {
                        return;
                    }
                    if (result == DialogResult.Yes)
                    {
                        File.Copy(file, file + ".bak", true);
                    }
                    String patchFile = ofd.FileName;
                    PatchingDelegate pd = new PatchingDelegate(this.PatchGameAsync);
                    pd.BeginInvoke(file, patchFile,item, null, null);
                }
            }
        }

        private void toolStripStatusLabel2_Click(object sender, EventArgs e)
        {
            
        }

        private void fTPToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void fullPakcageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (GameListView.SelectedItems.Count == 1)
            {
                ListViewItem item = GameListView.SelectedItems[0];
                String file = item.SubItems[4].Text;
                manager.addToQueue(file, item.SubItems[0].Text, item);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                if(Directory.Exists(fbd.SelectedPath + "/PSAVEDATA"))
                {
                    cma_path.Text = fbd.SelectedPath;
                }else
                {
                    MessageBox.Show("Can not find folder PSAVEDATA in this path.");
                }
                
            }
        }

        private void cMAToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private delegate void SplitPackageDelegate(string file,ListViewItem item);
        private void SplitPackageAsync(string file,ListViewItem item)
        {
            BeginUpdateListItemState(item, "Working...");
            string mini_file = VitaPackageHelper.Helper.splitPackage(item.SubItems[4].Text);
            BeginUpdateListItemState(item, "Uploading...");
            manager.addToQueue(mini_file, item.SubItems[0].Text, item);
        }
        private void splitTransferToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(GameListView.SelectedItems.Count == 1)
            {
                ListViewItem item = GameListView.SelectedItems[0];
                //splitPackage
                SplitPackageDelegate spd = new SplitPackageDelegate(SplitPackageAsync);
                spd.BeginInvoke(item.SubItems[4].Text, item, null, null);
            }
        }
    }
}
