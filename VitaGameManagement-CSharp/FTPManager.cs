//#define TRACE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.FtpClient;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VitaGameManagement_CSharp
{
    class FTPQueue
    {
        public string file;
        public string tag;
        public object obj;
        public long total;
        public long uploaded;
    }
    class FTPManager
    {
        static FTPManager _instance;
        private static List<FTPQueue> queueList = new List<FTPQueue>();
        public string ip;
        public string port;
        private Boolean stop;
        public FTPQueue currentQueue { get; set; }
        private Thread currentThread;
        public String error;

        public bool isWorking()
        {
            return currentThread.IsAlive;
        }

        public int getQueueCount()
        {
            return queueList.Count;
        }

        public FTPManager(string ip, string port)
        {
            this.ip = ip;
            this.port = port;
        }

        public static FTPManager instance(string ip,string port)
        {
            if(_instance == null)
            {
                _instance = new FTPManager(ip,port);
            }
            return _instance;
        }

        public void addToQueue(String file,string tag = null,object obj = null)
        {
            foreach(FTPQueue queue in queueList)
            {
                if (queue.file.Equals(file))
                {
                    return;
                }
            }
            queueList.Add(new FTPQueue() { file = file ,tag = tag,obj = obj});
        }

        public void StartFTPWorker()
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
        private void DoQueue()
        {
            while (!stop)
            {
                if (queueList.Count > 0)
                {
                    try
                    {
                        FTPQueue queue = queueList[0];
                        FileInfo fileInf = new FileInfo(queue.file);
                        queue.total = fileInf.Length;
                        currentQueue = queue;
                        string file = String.Format("/ux0:/{0}",fileInf.Name);
                        //string url = String.Format("ftp://127.0.0.1:21/{2}", ip, port, fileInf.Name);


                        using (FtpClient conn = new FtpClient())
                        {
                            conn.Host = ip;
                            conn.Port = int.Parse(port);
                            conn.Credentials = new NetworkCredential("anonymous", "");

                            FileStream fs = fileInf.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            using (Stream strm = conn.OpenWrite(file))
                            {
                                try
                                {
                                    int buffLength = 8192;
                                    byte[] buff = new byte[buffLength];
                                    int contentLen;

                                    // Read from the file stream 2kb at a time 
                                    contentLen = fs.Read(buff, 0, buffLength);
                                    error = null;
                                    // Till Stream content ends 
                                    while (contentLen != 0)
                                    {
                                        // Write Content from the file stream to the FTP Upload Stream 
                                        queue.uploaded += contentLen;
                                        strm.Write(buff, 0, contentLen);
                                        contentLen = fs.Read(buff, 0, buffLength);
                                        Thread.Sleep(1);
                                    }

                                    // Close the file stream and the Request Stream 
                                    strm.Close();
                                    fs.Close();

                                }
                                finally
                                {
                                    strm.Close();
                                    fs.Close();
                                }
                            }


                        }
                        queueList.RemoveAt(0);
                    }
                    catch (WebException ex)
                    {
                        error = ex.Message;
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                    }
                }
                Thread.Sleep(500);
            }
        }
    }
}
