using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace VitaPackageHelper
{
    public static class Helper
    {
        /// <summary>
        /// Convert Byte[] to Image
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static Image BytesToImage(byte[] buffer)
        {
            if(buffer == null) { return null; }
            MemoryStream ms = new MemoryStream(buffer);
            Image image = System.Drawing.Image.FromStream(ms);
            return image;
        }
        private static void ResizeImageAndSave(MemoryStream ms,string id)
        {
            if (!Directory.Exists("icons"))
            {
                Directory.CreateDirectory("icons");
            }
            if (File.Exists("icons//" + id + ".jpg"))
            {
                ms.Close();
                return;
            }
            Image img = Image.FromStream(ms);
            Bitmap bmp = new Bitmap(48, 48);
            Graphics g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(img, new Rectangle(0, 0, 48, 48), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel);
            g.Dispose();

            img.Dispose();
            ms.Close();

            img = bmp;
          
            img.Save("icons/" + id + ".jpg");
        }

        public static List<VitaPackage> loadPackages(String path)
        {
            List<VitaPackage> packages = new List<VitaPackage>();
            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            if (directoryInfo.Exists)
            {
                FileSystemInfo[] files = directoryInfo.GetFileSystemInfos();
                foreach (FileSystemInfo file in files)
                {
                    FileInfo info = file as FileInfo;
                    if (info != null)
                    {
                        if (info.Extension.ToLower().Equals(".vpk"))
                        {
                            Dictionary<String, String> sfo = loadSFO(info.FullName);
                            if(sfo.Count > 0)
                            {
                                String contentId = sfo["CONTENT_ID"];
                                if(contentId.IndexOf("-") == -1)
                                {
                                    contentId = sfo["TITLE"];
                                }
                                else
                                {
                                    String[] split = contentId.Split('-');
                                    if (split[1].IndexOf('_') > 0)
                                    {
                                        split = split[1].Split('_');
                                        contentId = split[0];
                                    }
                                    else
                                    {
                                        contentId = split[1];
                                    }
                                }
                               
                                String Region = "";
                                //regionPrefix = @{@"PCSF":@"EU",@"PCSE":@"US",@"PCSG":@"JP",@"PCSH":@"HK",@"PCSD":@"CN",@"PCSB":@"AU"};
                                if (contentId.StartsWith("PCSF"))
                                {
                                    Region = "EU";
                                }
                                else if (contentId.StartsWith("PCSE"))
                                {
                                    Region = "US";
                                }
                                else if (contentId.StartsWith("PCSG"))
                                {
                                    Region = "JP";
                                }
                                else if (contentId.StartsWith("PCSH"))
                                {
                                    Region = "HK";
                                }
                                else if (contentId.StartsWith("PCSD"))
                                {
                                    Region = "CN";
                                }
                                else if (contentId.StartsWith("PCSB"))
                                {
                                    Region = "AU";
                                }
                                else
                                {
                                    Region = contentId.Substring(0, 4);
                                }
                                //loadIconFromPackage(info.FullName)
                                VitaPackage package = new VitaPackage() { fileName = info.FullName, appId = contentId, sfoData = sfo, region = Region,icon = null };
                                packages.Add(package);
                            }
                        }
                    }
                    else
                    {
                        packages.AddRange(loadPackages(file.FullName));
                    }
                }
            }
            GC.Collect();
            return packages;
        }

        public enum PATCH_RESULT
        {
            SUCCESS,
            SFO_NOT_MATCH,
            VERSION_SAME
        }

		public static PATCH_RESULT patchPackage(String sourceFile, String patchFile,Delegate callback = null)
		{
			Dictionary<string, string> sourceSFO = loadSFO(sourceFile);
			Dictionary<string, string> patchSFO = loadSFO(patchFile);
            if(sourceSFO.Count > 0 && patchSFO.Count > 0)
            {
                if (sourceSFO["CONTENT_ID"] != patchSFO["CONTENT_ID"])
                {
                    return PATCH_RESULT.SFO_NOT_MATCH;
                }
                if (sourceSFO["APP_VER"] == patchSFO["APP_VER"])
                {
                    return PATCH_RESULT.VERSION_SAME;
                }
            }
	
            callback?.DynamicInvoke("Cleaning...");

            string temp = sourceFile.Substring(0,sourceFile.LastIndexOf('\\')) + "\\temp\\";
            if(Directory.Exists(temp))
                Directory.Delete(temp,true);
            callback?.DynamicInvoke("Extracting...");
            ZipFile.ExtractToDirectory(sourceFile, temp);
            using (FileStream zipToOpen = new FileStream(patchFile, FileMode.Open))
            {
                using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                     if(entry.Name != "")
                        {
                            //Skip Directory
                            callback?.DynamicInvoke("Patching " + entry.Name);
                            entry.ExtractToFile(temp + entry.FullName, true);
                        }else
                        {
                            if(!Directory.Exists(temp + entry.FullName))
                            {
                                Directory.CreateDirectory(temp + entry.FullName);
                            }
                        }
                    }
                }
            }
            callback?.DynamicInvoke("Cleaning...");
            File.Delete(sourceFile);
            callback?.DynamicInvoke("Rebuilding...");

            ZipFile.CreateFromDirectory(temp, sourceFile);
            callback?.DynamicInvoke("Cleaning...");
            Directory.Delete(temp,true);
            callback?.DynamicInvoke("Done.");
            return PATCH_RESULT.SUCCESS;
		}

        public static Dictionary<string,string> parserSFO(String file)
        {
            Dictionary<string, string> sfo = new Dictionary<string, string>();
            using (BinaryReader reader = new BinaryReader(File.OpenRead(file)))
            {
                Int32 header = reader.ReadInt32();
                if (header == 0x46535000)
                {
                    Byte[] version = reader.ReadBytes(4);
                    Int32 key_table_start = reader.ReadInt32();
                    Int32 data_table_start = reader.ReadInt32();
                    Int32 tables_entries = reader.ReadInt32();
                    List<SFOFFSET> offsetTable = new List<SFOFFSET>();

                    for (int i = 0; i < tables_entries; i++)
                    {
                        Int16 key_offset = reader.ReadInt16();
                        Int16 data_format = reader.ReadInt16();
                        Int32 data_lenght = reader.ReadInt32();
                        Int32 data_max_lenght = reader.ReadInt32();
                        Int32 data_offset = reader.ReadInt32();
                        offsetTable.Add(new SFOFFSET() { key_offset = key_offset, data_format = data_format, data_lenght = data_lenght, data_max_lenght = data_max_lenght, data_offset = data_offset });
                    }
                    List<string> keyTable = new List<string>();
                    for (int i = 0; i < tables_entries; i++)
                    {
                        //Key Here
                        String key = "";
                        int keyByte = reader.ReadByte();
                        while (keyByte != 0)
                        {
                            key += ((char)keyByte).ToString();
                            keyByte = reader.ReadByte();
                        }
                        keyTable.Add(key);
                    }
                    for (int i = 0; i < tables_entries; i++)
                    {
                        //Key Here
                        SFOFFSET offset = offsetTable[i];
                        Byte[] buff;
                        buff = reader.ReadBytes(offset.data_max_lenght);
                        string data = System.Text.Encoding.UTF8.GetString(buff, 0, offset.data_max_lenght).Replace("\0", string.Empty);
                        sfo.Add(keyTable[i], data);
                    }
                }
            }
            return sfo;
        }

        public static string splitPackage(String file)
        {
            FileInfo fi = new FileInfo(file);
            Dictionary<String, String> sfo = loadSFO(file);

            String fileName = fi.Name;
            if(sfo.Count > 0)
            {
                if(sfo["CONTENT_ID"].Length > 0)
                {
                    fileName = sfo["CONTENT_ID"].Split('_')[0].Split('-')[1];

                }
            }else
            {
                fileName = fileName.Substring(0, fileName.Length - fi.Extension.Length);
            }
            
            String MINI_FILE = fi.Directory.FullName + "/" + fileName  + ".MINI.VPK";
            String SPLIT_DIR = fi.Directory.FullName + "/" + fileName + "_SPLIT";
            String MINI_DIR = fi.Directory.FullName + "/" + fileName + "_MINI";
            if (Directory.Exists(SPLIT_DIR))
            {
                Directory.Delete(SPLIT_DIR, true);
            }
            if (Directory.Exists(MINI_DIR))
            {
                Directory.Delete(MINI_DIR, true);
            }
            if (File.Exists(MINI_FILE))
            {
                File.Delete(MINI_FILE);
            }
            Directory.CreateDirectory(SPLIT_DIR);
            Directory.CreateDirectory(MINI_DIR);
            ZipFile.ExtractToDirectory(file, SPLIT_DIR);
            File.Move(SPLIT_DIR + "/eboot.bin", MINI_DIR + "/eboot.bin");
            Directory.Move(SPLIT_DIR + "/sce_sys", MINI_DIR+ "/sce_sys");
            Directory.Move(SPLIT_DIR + "/sce_module", MINI_DIR + "/sce_module");
            ZipFile.CreateFromDirectory(MINI_DIR, MINI_FILE);
            Directory.Delete(MINI_DIR,true);

            return MINI_FILE;
        }

        public static Dictionary<string, string> loadSFO(String file)
        {

            Dictionary<String, String> sfo = new Dictionary<string, string>();
            try
            {
				using (FileStream zipToOpen = new FileStream(file, FileMode.Open))
                {
					using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read))
                    {
                        ZipArchiveEntry entry = archive.GetEntry("sce_sys/param.sfo");
                        using (StreamReader sr = new StreamReader(entry.Open()))
                          
                        {
                            BinaryReader reader = new BinaryReader(sr.BaseStream);
                            Int32 header = reader.ReadInt32();
                            if (header == 0x46535000)
                            {
                                Byte[] version = reader.ReadBytes(4);
                                Int32 key_table_start = reader.ReadInt32();
                                Int32 data_table_start = reader.ReadInt32();
                                Int32 tables_entries = reader.ReadInt32();
                                List<SFOFFSET> offsetTable = new List<SFOFFSET>();

                                for (int i = 0; i < tables_entries; i++)
                                {
                                    Int16 key_offset = reader.ReadInt16();
                                    Int16 data_format = reader.ReadInt16();
                                    Int32 data_lenght = reader.ReadInt32();
                                    Int32 data_max_lenght = reader.ReadInt32();
                                    Int32 data_offset = reader.ReadInt32();
                                    offsetTable.Add(new SFOFFSET() { key_offset = key_offset, data_format = data_format, data_lenght = data_lenght, data_max_lenght = data_max_lenght, data_offset = data_offset });
                                }
                                List<string> keyTable = new List<string>();
                                for (int i = 0;i< tables_entries;i++)
                                {
                                    //Key Here
                                    String key = "";
                                    int keyByte = reader.ReadByte();
                                    while (keyByte != 0)
                                    {
                                        key += ((char)keyByte).ToString();
                                        keyByte = reader.ReadByte();
                                    }
                                    keyTable.Add(key);
                                }
                                for (int i = 0; i < tables_entries; i++)
                                {
                                    //Key Here
                                    SFOFFSET offset = offsetTable[i];
                                    Byte[] buff;
                                    buff = reader.ReadBytes(offset.data_max_lenght );
                                    string data = System.Text.Encoding.UTF8.GetString(buff,0,offset.data_max_lenght).Replace("\0",string.Empty);
                                    sfo.Add(keyTable[i], data);
                                }

                            }
                        }
                        String iconName = sfo["CONTENT_ID"].Length > 0 ? sfo["CONTENT_ID"] : sfo["TITLE"];
                        
                        if (!File.Exists(String.Format("icons/{0}.jpg",iconName)))
                        {
                            entry = archive.GetEntry("sce_sys/icon0.png");

                            using (BinaryReader reader = new BinaryReader(entry.Open()))
                            {

                                Byte[] buffer = reader.ReadBytes(2048);
                                MemoryStream ms = new MemoryStream();
                                while (buffer.Length > 0)
                                {
                                    ms.Write(buffer, 0, buffer.Length);
                                    buffer = reader.ReadBytes(2048);
                                }
                                ResizeImageAndSave(ms, sfo["CONTENT_ID"].Length > 0 ? sfo["CONTENT_ID"] : sfo["TITLE"]);
                            }
                        }
                       
                    }
                }
            }
            catch (Exception ex)
            {
                
            }
            
            return sfo;
        }
    }
}
