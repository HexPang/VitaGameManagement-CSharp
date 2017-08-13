using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VitaGameManagement_CSharp
{
    class CopyQueue
    {
        public string file;
        public string tag;
        public object obj;
        public long total;
        public long uploaded;
    }
    class FileCopyManager
    {
        static FileCopyManager _instance;
        private static List<CopyQueue> queueList = new List<CopyQueue>();
        private Boolean stop;
        public CopyQueue currentQueue { get; set; }
        private Thread currentThread;
        public String error;
        private string usbDrive;

        public bool isWorking()
        {
            return currentThread.IsAlive;
        }

        public int getQueueCount()
        {
            return queueList.Count;
        }

        public static FileCopyManager instance(string usbDrive)
        {
            if (_instance == null)
            {
                _instance = new FileCopyManager();
            }
            _instance.usbDrive = usbDrive;
            return _instance;
        }

        public void StartCopyWorker()
        {
            stop = false;
            currentThread = new Thread(new ThreadStart(DoQueue));
            currentThread.IsBackground = true;
            currentThread.Start();
        }
        public void StopFTPWorker()
        {
            stop = true;
        }

        public void addToQueue(String file, string tag = null, object obj = null)
        {
            foreach (CopyQueue queue in queueList)
            {
                if (queue.file.Equals(file))
                {
                    return;
                }
            }
            queueList.Add(new CopyQueue() { file = file, tag = tag, obj = obj });
        }

        private void DoQueue()
        {
            while (!stop)
            {
                if (queueList.Count > 0)
                {
                    CopyQueue queue = queueList[0];
                    FileInfo fileInf = new FileInfo(queue.file);
                    queue.total = fileInf.Length;
                    currentQueue = queue;

                    string file = this.usbDrive + fileInf.Name;//String.Format("/ux0:/{0}", fileInf.Name);
                    FileStream fs = fileInf.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    FileStream target_fs = new FileStream(file, FileMode.Create);
                    byte[] buffer = new byte[20480];
                    int recv = fs.Read(buffer, 0, buffer.Length);
                    while(recv > 0)
                    {
                        target_fs.Write(buffer, 0, recv);
                        queue.uploaded += recv;
                        recv = fs.Read(buffer, 0, buffer.Length);
                    }
                    target_fs.Close();
                    fs.Close();
                    queueList.RemoveAt(0);
                }
                Thread.Sleep(1000);
            }
        }
    }
}
