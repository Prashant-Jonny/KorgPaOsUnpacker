using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZLibNet;

namespace KORG_OS
{
    #region PackageBase
    class PackageBase
    {
        protected byte Read1(FileStream f)
        {
            int fbyte = f.ReadByte();

            if (fbyte < 0)
                throw new EndOfStreamException("Error reading byte");

            return (byte)fbyte;
        }

        protected UInt16 Read2(FileStream f)
        {
            UInt16 result = 0;
            byte[] tmp = new byte[2];
            f.Read(tmp, 0, 2);
            for (UInt16 i = 0; i < 2; i++)
                result |= (UInt16)(tmp[i] << (i * 8));
            return result;
        }

        protected UInt32 Read4(FileStream f)
        {
            UInt32 result = 0;
            byte[] tmp = new byte[4];
            f.Read(tmp, 0, 4);
            for (int i = 0; i < 4; i++)
                result |= (UInt32)(tmp[i] << (i * 8));
            return result;
        }

        protected string ReadString(FileStream f)
        {
            StringBuilder str = new StringBuilder();

            do
            {
                int fbyte = f.ReadByte();

                if (fbyte < 0)
                    throw new EndOfStreamException("Error reading byte");

                char fchar = (char)fbyte;

                if (fchar != 0)
                    str.Append(fchar);
                else
                    break;
            } while (true);

            return str.ToString();
        }

        protected byte[] ReadArray(FileStream f, int size)
        {
            byte[] result = new byte[size];
            f.Read(result, 0, size);
            return result;
        }

        protected UInt32 Swap4(UInt32 d)
        {
            return (d & 0x000000FFU) << 24
                 | (d & 0x0000FF00U) << 8
                 | (d & 0x00FF0000U) >> 8
                 | (d & 0xFF000000U) >> 24;
        }

        protected string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder();
            foreach (byte b in ba)
                hex.AppendFormat("{0:X2}", b);
            return hex.ToString();
        }
    }
    #endregion

    class Package : PackageBase
    {
        public byte[] MD5Hash { get; set; }
        public LfoPkgHeader LfoPkgHeader { get; set; }
        public LfoPkgInstallKernel LfoPkgInstallKernel { get; set; }
        public LfoPkgInstallRamdisk LfoPkgInstallRamdisk { get; set; }
        public LfoPkgInstall LfoPkgInstall { get; set; }
        public LfoPkgInstallXml LfoPkgInstallXml { get; set; }
        public List<LfoPkgInstallSh> LfoPkgInstallShList { get; set; }
        public LfoPkgKernel LfoPkgKernel { get; set; }
        public List<LfoPkgDirectory> LfoPkgDirectoryList { get; set; }
        public List<LfoPkgFile> LfoPkgFileList { get; set; }
        public LfoPkgFileSystem LfoPkgFileSystem { get; set; }

        public Package(string path)
        {
            LfoPkgInstallShList = new List<LfoPkgInstallSh>();
            LfoPkgDirectoryList = new List<LfoPkgDirectory>();
            LfoPkgFileList = new List<LfoPkgFile>();

            FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            MD5Hash = ReadArray(file, 16);

            while (file.Position < file.Length)
            {
                UInt32 type = Read4(file);
                UInt32 size = Read4(file);
                long offset = file.Position - 8;

                switch (type)
                {
                    case LfoPkgHeader.Type:
                        LfoPkgHeader = new LfoPkgHeader(path, offset);
                        break;
                    case LfoPkgInstallKernel.Type:
                        LfoPkgInstallKernel = new LfoPkgInstallKernel(path, offset);
                        break;
                    case LfoPkgInstallRamdisk.Type:
                        LfoPkgInstallRamdisk = new LfoPkgInstallRamdisk(path, offset);
                        break;
                    case LfoPkgInstall.Type:
                        LfoPkgInstall = new LfoPkgInstall(path, offset);
                        break;
                    case LfoPkgInstallXml.Type:
                        LfoPkgInstallXml = new LfoPkgInstallXml(path, offset);
                        break;
                    case LfoPkgInstallSh.Type:
                        LfoPkgInstallShList.Add(new LfoPkgInstallSh(path, offset));
                        break;
                    case LfoPkgKernel.Type:
                        LfoPkgKernel = new LfoPkgKernel(path, offset);
                        break;
                    case LfoPkgDirectory.Type:
                        LfoPkgDirectoryList.Add(new LfoPkgDirectory(path, offset));
                        break;
                    case LfoPkgFile.Type:
                        LfoPkgFileList.Add(new LfoPkgFile(path, offset));
                        break;
                    case LfoPkgFileSystem.Type:
                        LfoPkgFileSystem = new LfoPkgFileSystem(path, offset);
                        break;
                    default:
                        Console.WriteLine("Unknown chunk,\nType: " + type + " Offset: " + offset);
                        break;
                }

                long skip = file.Position + size;
                if ((skip % 4) != 0)
                    skip += 4 - (skip % 4);
                file.Seek(skip, SeekOrigin.Begin);
            }

            SaveFiles(path.Remove(path.LastIndexOf('.')));
            CreateLog(path);
        }

        public void SaveFiles(string path)
        {
            LfoPkgHeader.SaveFile(path);
            LfoPkgInstallKernel.SaveFile(path + @"\lfo-pkg-install");
            LfoPkgInstallRamdisk.SaveFile(path + @"\lfo-pkg-install");
            LfoPkgInstall.SaveFile(path + @"\lfo-pkg-install");
            LfoPkgInstallXml.SaveFile(path + @"\lfo-pkg-install");
            foreach (LfoPkgInstallSh sh in LfoPkgInstallShList)
                sh.SaveFile(path + @"\lfo-pkg-install");
            LfoPkgKernel.SaveFile(path + @"\kernel");
            foreach (LfoPkgDirectory dir in LfoPkgDirectoryList)
                dir.CreateDirectory(path);
            foreach (LfoPkgFile fil in LfoPkgFileList)
                fil.SaveFile(path);
            LfoPkgFileSystem.SaveFile(path + @"\");
        }

        public void CreateLog(string path)
        {
            string p = path.Remove(path.LastIndexOf('.'));
            string n = path.Substring(path.LastIndexOf('\\') + 1);
            Directory.CreateDirectory(p);
            StreamWriter f = new StreamWriter(p + @"\log.txt");

            f.WriteLine("Summary of " + n);

            f.WriteLine();

            f.WriteLine("MD5 " + ByteArrayToString(MD5Hash));

            f.WriteLine();

            f.WriteLine("[LfoPkgHeader] " + "Id " + LfoPkgHeader.Type + " Size " + LfoPkgHeader.Size);
            f.WriteLine("[LfoPkgHeader] " + "0x" + ByteArrayToString(LfoPkgHeader.Unknown1));
            f.WriteLine("[LfoPkgHeader] " + LfoPkgHeader.SystemType1 + " " + LfoPkgHeader.SystemType2);
            f.WriteLine("[LfoPkgHeader] " + LfoPkgHeader.BuildSystem + " " + LfoPkgHeader.Date + " " + LfoPkgHeader.Time);
            f.WriteLine("[LfoPkgHeader] " + LfoPkgHeader.PackageType1 + " " + LfoPkgHeader.PackageType2);

            f.WriteLine();

            f.WriteLine("[LfoPkgInstallKernel] " + "Id " + LfoPkgInstallKernel.Type + " Size " + LfoPkgInstallKernel.Size);
            f.WriteLine("[LfoPkgInstallKernel] " + "MD5 " + ByteArrayToString(LfoPkgInstallKernel.MD5Hash));

            f.WriteLine();

            f.WriteLine("[LfoPkgInstallRamdisk] " + "Id " + LfoPkgInstallRamdisk.Type + " Size " + LfoPkgInstallRamdisk.Size);
            f.WriteLine("[LfoPkgInstallRamdisk] " + "MD5 " + ByteArrayToString(LfoPkgInstallRamdisk.MD5Hash));

            f.WriteLine();

            f.WriteLine("[LfoPkgInstall] " + "Id " + LfoPkgInstall.Type + " Size " + LfoPkgInstall.Size);
            f.WriteLine("[LfoPkgInstall] " + "MD5 " + ByteArrayToString(LfoPkgInstall.MD5Hash));

            f.WriteLine();

            f.WriteLine("[LfoPkgInstallXml] " + "Id " + LfoPkgInstallXml.Type + " Size " + LfoPkgInstallXml.Size);
            f.WriteLine("[LfoPkgInstallXml] " + "MD5 " + ByteArrayToString(LfoPkgInstallXml.MD5Hash));

            foreach (LfoPkgInstallSh sh in LfoPkgInstallShList)
            {
                f.WriteLine();

                f.WriteLine("[LfoPkgInstallSh] " + "Id " + LfoPkgInstallSh.Type + " Size " + sh.Size);
                f.WriteLine("[LfoPkgInstallSh] " + "MD5 " + ByteArrayToString(sh.MD5Hash));
                f.WriteLine("[LfoPkgInstallSh] " + "0x" + ByteArrayToString(sh.Unknown1) + " " + sh.Name);
            }

            f.WriteLine();

            f.WriteLine("[LfoPkgKernel] " + "Id " + LfoPkgKernel.Type + " Size " + LfoPkgKernel.Size);
            f.WriteLine("[LfoPkgKernel] " + "MD5 " + ByteArrayToString(LfoPkgKernel.MD5Hash));

            foreach (LfoPkgDirectory dir in LfoPkgDirectoryList)
            {
                f.WriteLine();

                f.WriteLine("[LfoPkgDirectory] " + "Id " + LfoPkgDirectory.Type + " Size " + dir.Size);
                f.WriteLine("[LfoPkgDirectory] " + "0x" + ByteArrayToString(dir.Unknown1) + " 0x" + ByteArrayToString(dir.Unknown2) + " 0x" + ByteArrayToString(dir.Unknown3));
                f.WriteLine("[LfoPkgDirectory] " + dir.Name);
            }

            foreach (LfoPkgFile fil in LfoPkgFileList)
            {
                f.WriteLine();

                f.WriteLine("[LfoPkgFile] " + "Id " + LfoPkgFile.Type + " Size " + fil.Size);
                f.WriteLine("[LfoPkgFile] " + "MD5 " + ByteArrayToString(fil.MD5Hash));
                f.WriteLine("[LfoPkgFile] " + fil.Unknown1 + " " + fil.Unknown2 + " " + fil.Unknown3 + " " + fil.UncompressedFileSize + " " + fil.Unknown4);
                f.WriteLine("[LfoPkgFile] " + fil.Date + " " + fil.Time + " " + fil.Name);
            }

            f.WriteLine();

            f.WriteLine("[LfoPkgFileSystem] " + "Id " + LfoPkgFileSystem.Type + " Size " + LfoPkgFileSystem.Size);
            f.WriteLine("[LfoPkgFileSystem] " + "MD5 " + ByteArrayToString(LfoPkgFileSystem.MD5Hash));
            f.WriteLine("[LfoPkgFileSystem] " + ByteArrayToString(LfoPkgFileSystem.Unknown1));
            f.WriteLine("[LfoPkgFileSystem] " + LfoPkgFileSystem.Name);

            f.Close();
        }
    }

    #region LfoPkgHeader
    class LfoPkgHeader : PackageBase
    {
        private long offset;
        private string pkgPath;

        public const UInt32 Type = 0x00000001;
        public UInt32 Size { get; set; }
        public byte[] Unknown1 { get; set; }
        public string SystemType1 { get; set; }
        public string SystemType2 { get; set; }
        public string BuildSystem { get; set; }
        public string Date { get; set; }
        public string Time { get; set; }
        public string PackageType1 { get; set; }
        public string PackageType2 { get; set; }

        public LfoPkgHeader(string path, long offset)
        {
            this.offset = offset;
            this.pkgPath = path;
            LoadInfo();
        }

        public void LoadInfo()
        {
            FileStream file = new FileStream(pkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            file.Seek(offset, SeekOrigin.Begin);
            if (Read4(file) != Type)
                throw new InvalidDataException("It's not a header chunk");
            Size = Read4(file);
            Unknown1 = ReadArray(file, 12);
            SystemType1 = ReadString(file);
            SystemType2 = ReadString(file);
            BuildSystem = ReadString(file);
            Date = ReadString(file);
            Time = ReadString(file);
            PackageType1 = ReadString(file);
            PackageType2 = ReadString(file);
            file.Close();
        }

        public void SaveFile(string p)
        {
            Directory.CreateDirectory(p);
            StreamWriter file = new StreamWriter(p + @"\header.txt");
            file.WriteLine("Package header:");
            file.WriteLine("--------------------------------------------------");
            file.WriteLine("Size:           0x{0:X8}", Size);
            file.WriteLine("Unknown1:       0x{0}", ByteArrayToString(Unknown1));
            file.WriteLine("System type 1:  {0}", SystemType1);
            file.WriteLine("System type 2:  {0}", SystemType2);
            file.WriteLine("Build system:   {0}", BuildSystem);
            file.WriteLine("Date:           {0}", Date);
            file.WriteLine("Time:           {0}", Time);
            file.WriteLine("Package type 1: {0}", PackageType1);
            file.WriteLine("Package type 2: {0}", PackageType2);
            file.Close();
        }
    }
    #endregion

    #region LfoPkgInstallKernel
    class LfoPkgInstallKernel : PackageBase
    {
        private long offset;
        private string pkgPath;
        private long dataOffset;
        private int dataSize;

        public const UInt32 Type = 0x00000002;
        public UInt32 Size { get; set; }
        public byte[] MD5Hash { get; set; }

        public LfoPkgInstallKernel(string path, long offset)
        {
            this.offset = offset;
            this.pkgPath = path;
            LoadInfo();
        }

        public void LoadInfo()
        {
            FileStream file = new FileStream(pkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            file.Seek(offset, SeekOrigin.Begin);
            if (Read4(file) != Type)
                throw new InvalidDataException("It's not a lfo-pkg-install kernel chunk");
            Size = Read4(file);
            MD5Hash = ReadArray(file, 16);
            dataOffset = file.Position;
            dataSize = (int)Size - 16;
            file.Close();
        }

        public void SaveFile(string p)
        {
            Directory.CreateDirectory(p);
            FileStream fin = new FileStream(pkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            FileStream fout = new FileStream(p + @"\uImage", FileMode.Create, FileAccess.Write);

            fin.Seek(dataOffset, SeekOrigin.Begin);
            fout.Write(ReadArray(fin, dataSize), 0, dataSize);

            fout.Close();
            fin.Close();
        }
    }
    #endregion

    #region LfoPkgInstallRamdisk
    class LfoPkgInstallRamdisk : PackageBase
    {
        private long offset;
        private string pkgPath;
        private long dataOffset;
        private int dataSize;

        public const UInt32 Type = 0x00000003;
        public UInt32 Size { get; set; }
        public byte[] MD5Hash { get; set; }

        public LfoPkgInstallRamdisk(string path, long offset)
        {
            this.offset = offset;
            this.pkgPath = path;
            LoadInfo();
        }

        public void LoadInfo()
        {
            FileStream file = new FileStream(pkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            file.Seek(offset, SeekOrigin.Begin);
            if (Read4(file) != Type)
                throw new InvalidDataException("It's not a lfo-pkg-install ramdisk chunk");
            Size = Read4(file);
            MD5Hash = ReadArray(file, 16);
            dataOffset = file.Position;
            dataSize = (int)Size - 16;
            file.Close();
        }

        public void SaveFile(string p)
        {
            Directory.CreateDirectory(p);
            FileStream fin = new FileStream(pkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            FileStream fout = new FileStream(p + @"\ramdisk.gz", FileMode.Create, FileAccess.Write);

            fin.Seek(dataOffset, SeekOrigin.Begin);
            fout.Write(ReadArray(fin, dataSize), 0, dataSize);

            fout.Close();
            fin.Close();
        }
    }
    #endregion

    #region LfoPkgInstall
    class LfoPkgInstall : PackageBase
    {
        private long offset;
        private string pkgPath;
        private long dataOffset;
        private int dataSize;

        public const UInt32 Type = 0x00000004;
        public UInt32 Size { get; set; }
        public byte[] MD5Hash { get; set; }

        public LfoPkgInstall(string path, long offset)
        {
            this.offset = offset;
            this.pkgPath = path;
            LoadInfo();
        }

        public void LoadInfo()
        {
            FileStream file = new FileStream(pkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            file.Seek(offset, SeekOrigin.Begin);
            if (Read4(file) != Type)
                throw new InvalidDataException("It's not a lfo-pkg-install elf chunk");
            Size = Read4(file);
            MD5Hash = ReadArray(file, 16);
            dataOffset = file.Position;
            dataSize = (int)Size - 16;
            file.Close();
        }

        public void SaveFile(string p)
        {
            Directory.CreateDirectory(p);
            FileStream fin = new FileStream(pkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            FileStream fout = new FileStream(p + @"\lfo-pkg-install", FileMode.Create, FileAccess.Write);

            fin.Seek(dataOffset, SeekOrigin.Begin);
            fout.Write(ReadArray(fin, dataSize), 0, dataSize);

            fout.Close();
            fin.Close();
        }
    }
    #endregion

    #region LfoPkgInstallXml
    class LfoPkgInstallXml : PackageBase
    {
        private long offset;
        private string pkgPath;
        private long dataOffset;
        private int dataSize;

        public const UInt32 Type = 0x00000005;
        public UInt32 Size { get; set; }
        public byte[] MD5Hash { get; set; }

        public LfoPkgInstallXml(string path, long offset)
        {
            this.offset = offset;
            this.pkgPath = path;
            LoadInfo();
        }

        public void LoadInfo()
        {
            FileStream file = new FileStream(pkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            file.Seek(offset, SeekOrigin.Begin);
            if (Read4(file) != Type)
                throw new InvalidDataException("It's not a lfo-pkg-install xml chunk");
            Size = Read4(file);
            MD5Hash = ReadArray(file, 16);
            dataOffset = file.Position;
            dataSize = (int)Size - 16;
            file.Close();
        }

        public void SaveFile(string p)
        {
            Directory.CreateDirectory(p);
            FileStream fin = new FileStream(pkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            FileStream fout = new FileStream(p + @"\lfo-pkg-install.xml", FileMode.Create, FileAccess.Write);

            fin.Seek(dataOffset, SeekOrigin.Begin);
            fout.Write(ReadArray(fin, dataSize), 0, dataSize);

            fout.Close();
            fin.Close();
        }
    }
    #endregion

    #region LfoPkgInstallSh
    class LfoPkgInstallSh : PackageBase
    {
        private long offset;
        private string pkgPath;
        private long dataOffset;
        private int dataSize;

        public const UInt32 Type = 0x0000000F;
        public UInt32 Size { get; set; }
        public byte[] MD5Hash { get; set; }
        public byte[] Unknown1 { get; set; }
        public string Name { get; set; }

        public LfoPkgInstallSh(string path, long offset)
        {
            this.offset = offset;
            this.pkgPath = path;
            LoadInfo();
        }

        public void LoadInfo()
        {
            FileStream file = new FileStream(pkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            file.Seek(offset, SeekOrigin.Begin);
            if (Read4(file) != Type)
                throw new InvalidDataException("It's not a lfo-pkg-install sh chunk");
            Size = Read4(file);
            MD5Hash = ReadArray(file, 16);
            Unknown1 = ReadArray(file, 2);
            Name = ReadString(file);
            dataOffset = file.Position;
            dataSize = (int)Size - (16 + 2 + Name.Length + 1);
            file.Close();
        }

        public void SaveFile(string p)
        {
            Directory.CreateDirectory(p);
            FileStream fin = new FileStream(pkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            FileStream fout = new FileStream(p + @"\" + Name, FileMode.Create, FileAccess.Write);

            fin.Seek(dataOffset, SeekOrigin.Begin);
            fout.Write(ReadArray(fin, dataSize), 0, dataSize);

            fout.Close();
            fin.Close();
        }
    }
    #endregion

    #region LfoPkgKernel
    class LfoPkgKernel : PackageBase
    {
        private long offset;
        private string pkgPath;
        private long dataOffset;
        private int dataSize;

        public const UInt32 Type = 0x0000000E;
        public UInt32 Size { get; set; }
        public byte[] MD5Hash { get; set; }

        public LfoPkgKernel(string path, long offset)
        {
            this.offset = offset;
            this.pkgPath = path;
            LoadInfo();
        }

        public void LoadInfo()
        {
            FileStream file = new FileStream(pkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            file.Seek(offset, SeekOrigin.Begin);
            if (Read4(file) != Type)
                throw new InvalidDataException("It's not a lfo kernel chunk");
            Size = Read4(file);
            MD5Hash = ReadArray(file, 16);
            dataOffset = file.Position;
            dataSize = (int)Size - 16;
            file.Close();
        }

        public void SaveFile(string p)
        {
            Directory.CreateDirectory(p);
            FileStream fin = new FileStream(pkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            FileStream fout = new FileStream(p + @"\uImage", FileMode.Create, FileAccess.Write);

            fin.Seek(dataOffset, SeekOrigin.Begin);
            fout.Write(ReadArray(fin, dataSize), 0, dataSize);

            fout.Close();
            fin.Close();
        }
    }
    #endregion

    #region LfoPkgDirectory
    class LfoPkgDirectory : PackageBase
    {
        private long offset;
        private string pkgPath;

        public const UInt32 Type = 0x00000010;
        public UInt32 Size { get; set; }
        public byte[] Unknown1 { get; set; }
        public byte[] Unknown2 { get; set; }
        public byte[] Unknown3 { get; set; }
        public string Name { get; set; }

        public LfoPkgDirectory(string path, long offset)
        {
            this.offset = offset;
            this.pkgPath = path;
            LoadInfo();
        }

        public void LoadInfo()
        {
            FileStream file = new FileStream(pkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            file.Seek(offset, SeekOrigin.Begin);
            if (Read4(file) != Type)
                throw new InvalidDataException("It's not a lfo directory chunk");
            Size = Read4(file);
            Unknown1 = ReadArray(file, 4);
            Unknown2 = ReadArray(file, 2);
            Unknown3 = ReadArray(file, 2);
            Name = ReadString(file);
            file.Close();
        }

        public void CreateDirectory(string p)
        {
            Directory.CreateDirectory(p + Name);
        }
    }
    #endregion

    #region LfoPkgFile
    class LfoPkgFile : PackageBase
    {
        private long offset;
        private string pkgPath;
        private long dataOffset;
        private int dataSize;

        public const UInt32 Type = 0x00000011;
        public UInt32 Size { get; set; }
        public byte[] MD5Hash { get; set; }
        public UInt32 Unknown1 { get; set; }
        public UInt16 Unknown2 { get; set; }
        public UInt16 Unknown3 { get; set; }
        public UInt32 UncompressedFileSize { get; set; }
        public byte Unknown4 { get; set; }
        public string Name { get; set; }
        public string Date { get; set; }
        public string Time { get; set; }

        public LfoPkgFile(string path, long offset)
        {
            this.offset = offset;
            this.pkgPath = path;
            LoadInfo();
        }

        public void LoadInfo()
        {
            FileStream file = new FileStream(pkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            file.Seek(offset, SeekOrigin.Begin);
            if (Read4(file) != Type)
                throw new InvalidDataException("It's not a lfo file chunk");
            Size = Read4(file);
            MD5Hash = ReadArray(file, 16);
            Unknown1 = Read4(file);
            Unknown2 = Read2(file);
            Unknown3 = Read2(file);
            UncompressedFileSize = Read4(file);
            Unknown4 = Read1(file);
            Name = ReadString(file);
            Date = ReadString(file);
            Time = ReadString(file);
            dataOffset = file.Position;
            dataSize = (int)Size - (16 + 4 + 2 + 2 + 4 + 1 + Name.Length + 1 + Date.Length + 1 + Time.Length + 1);
            file.Close();            
        }

        public void SaveFile(string p)
        {
            Directory.CreateDirectory(p);
            FileStream fin = new FileStream(pkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            FileStream fout = new FileStream(p + @"\" + Name, FileMode.Create, FileAccess.Write);

            fin.Seek(dataOffset, SeekOrigin.Begin);

            while (true)
            {
                UInt32 blockType = Read4(fin);
                if (blockType != 0x00000100)
                    break;
                UInt32 compressedBlockSize = Read4(fin);
                UInt32 uncompressedBlockSize = Swap4(Read4(fin));
                byte[] compressed = ReadArray(fin, (int)(compressedBlockSize - 4));
                byte[] uncompressed = ZLibCompressor.DeCompress(compressed);
                fout.Write(uncompressed, 0, uncompressed.Length);

                if ((compressedBlockSize % 4) != 0)
                    fin.Position += 4 - (compressedBlockSize % 4);
            }

            fout.Close();
            fin.Close();
        }
    }
    #endregion

    #region LfoPkgFileSystem
    class LfoPkgFileSystem : PackageBase
    {
        private long offset;
        private string pkgPath;
        private long dataOffset;
        private int dataSize;

        public const UInt32 Type = 0x00000013;
        public UInt32 Size { get; set; }
        public byte[] MD5Hash { get; set; }
        public byte[] Unknown1 { get; set; }
        public string Name { get; set; }

        public LfoPkgFileSystem(string path, long offset)
        {
            this.offset = offset;
            this.pkgPath = path;
            LoadInfo();
        }

        public void LoadInfo()
        {
            FileStream file = new FileStream(pkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            file.Seek(offset, SeekOrigin.Begin);
            if (Read4(file) != Type)
                throw new InvalidDataException("It's not a lfo file system chunk");
            Size = Read4(file);
            MD5Hash = ReadArray(file, 16);
            Unknown1 = ReadArray(file, 6);
            Name = ReadString(file);
            dataOffset = file.Position;
            dataSize = (int)Size - (16 + 6 + Name.Length + 1);
            file.Close();
        }

        public void SaveFile(string p)
        {
            Directory.CreateDirectory(p + Name.Remove(Name.LastIndexOf('/')));
            FileStream fin = new FileStream(pkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            FileStream fout = new FileStream(p + @"\" + Name, FileMode.Create, FileAccess.Write);

            fin.Seek(dataOffset, SeekOrigin.Begin);
            fout.Write(ReadArray(fin, dataSize), 0, dataSize);

            fout.Close();
            fin.Close();
        }
    }
    #endregion
}
