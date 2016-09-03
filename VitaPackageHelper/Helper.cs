using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VitaPackageHelper
{
    public static class Helper
    {
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


                                VitaPackage package = new VitaPackage() { fileName = info.FullName, appId = contentId, sfoData = sfo, region = Region };
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
            return packages;
        }

        public static Dictionary<string, string> loadSFO(String file)
        {

            Dictionary<String, String> sfo = new Dictionary<string, string>();
            try
            {
                using (FileStream zipToOpen = new FileStream(file, FileMode.Open))
                {
                    using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            if (entry.FullName.Equals("sce_sys/param.sfo"))
                            {
                                using (BinaryReader reader = new BinaryReader(entry.Open()))
                                {
                                    Int32 header = reader.ReadInt32();
                                    if (header == 0x46535000)
                                    {
                                        Byte[] version = reader.ReadBytes(4);
                                        Int32 key_table_start = reader.ReadInt32();
                                        Int32 data_table_start = reader.ReadInt32();
                                        Int32 tables_entries = reader.ReadInt32();
                                        long streamPosition = reader.BaseStream.Position;
                                        for (int i = 0; i < tables_entries; i++)
                                        {
                                            Int16 key_offset = reader.ReadInt16();
                                            Int16 data_format = reader.ReadInt16();
                                            Int32 data_lenght = reader.ReadInt32();
                                            Int32 data_max_lenght = reader.ReadInt32();
                                            Int32 data_offset = reader.ReadInt32();
                                            streamPosition = reader.BaseStream.Position;

                                            reader.BaseStream.Position = key_offset + key_table_start;
                                            String key = "";
                                            int keyByte = reader.ReadByte();
                                            while (keyByte != 0)
                                            {
                                                key += ((char)keyByte).ToString();
                                                keyByte = reader.ReadByte();
                                            }
                                            reader.BaseStream.Position = data_offset + data_table_start;
                                            Byte[] buff;
                                            buff = reader.ReadBytes(data_lenght - 1);
                                            String data = System.Text.Encoding.UTF8.GetString(buff);
                                            reader.BaseStream.Position = streamPosition;
                                            sfo[key] = data;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                
            }
            
            return sfo;
        }
    }
}
