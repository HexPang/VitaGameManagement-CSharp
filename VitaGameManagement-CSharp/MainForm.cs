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

        private FTPManager manager;
        private List<VitaPackageHelper.VitaPackage> packages;
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
            this.initGameLibrary();
            this.reloadSetting();
        }


		private void loadGameLibrary()
		{
			packages = VitaPackageHelper.Helper.loadPackages(libraryPath.Text);
			if (packages.Count > 0)
			{
				GameListView.Items.Clear();
				foreach (VitaPackageHelper.VitaPackage package in packages)
				{
					long size = new FileInfo(package.fileName).Length;
					string fileSize = String.Format(new FileSizeFormatProvider(), "{0:fs}", size);

					ListViewItem item = new ListViewItem(new String[] { package.appId, package.sfoData["TITLE"], package.region, fileSize, package.fileName, "OK", package.sfoData["APP_VER"] });
					GameListView.Items.Add(item);
				}
			}
		}

		private void gameLibraryCallback(IAsyncResult result)
		{
			((LibraryAsyncEventHandler)result.AsyncState).EndInvoke(result);
		}

		private void initGameLibrary()
		{
			LibraryAsyncEventHandler libraryAsync = new LibraryAsyncEventHandler(this.loadGameLibrary);
			libraryAsync.BeginInvoke(new AsyncCallback(this.gameLibraryCallback), libraryAsync);

		}

        private void reloadSetting()
        {
            libraryPath.Text = ConfigurationManager.ReadSetting("library");
            vita_ip.Text = ConfigurationManager.ReadSetting("vita_ip");
            vita_port.Text = ConfigurationManager.ReadSetting("vita_port");

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
                packages = VitaPackageHelper.Helper.loadPackages(libraryPath.Text);
                this.initGameLibrary();
            }
        }

        private void uploadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(GameListView.SelectedItems.Count == 1)
            {
                ListViewItem item = GameListView.SelectedItems[0];
                String file = item.SubItems[4].Text;
                manager.addToQueue(file,item.SubItems[0].Text,item);
            }
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {

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
                                uploadProgress.Value = (int)progress;
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
    }
}
