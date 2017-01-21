using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaInfoDotNet;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.IO;
using System.Diagnostics;


namespace FolderTranscode
{
    class FolderTranscoder
    {
        DirectoryInfo InFolder, OutFolder;
        public bool DeleteOriginal = false, RemovePlayOnBanner = false, RemoveAds = false, h265Transcode = true, AutoCrop = true, TwoPass = true, NoCopyUnaltered = false;
        public FileTranscoder.H265Preset Preset = FileTranscoder.H265Preset.veryfast;
        private int _CRF = 18;
        public int CRF
        {
            get
            {
                return _CRF;
            }
            set
            {
                if (value >= 0 && value <= 51)
                    _CRF = value;
                else
                    throw new ArgumentOutOfRangeException("value", "CRF must be between 0 and 51");
            }
        }

        public FolderTranscoder(string inFolder, string outFolder)
        {
            InFolder = new DirectoryInfo(inFolder);
            OutFolder = new DirectoryInfo(outFolder);
        }

        public void StartTranscode()
        {
            if (InFolder.Exists)
            {
                if (!OutFolder.Exists)
                    OutFolder.Create();

                Console.WriteLine("Reading files from " + InFolder.FullName);
                if (DeleteOriginal)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("WARNING: DELETE ORIGINAL ON SUCCESSFUL TRANSCODE SET TO ON");
                    Console.ForegroundColor = ConsoleColor.Gray;
                }

                foreach (FileInfo F in InFolder.GetFiles("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        string OutFileName = F.FullName.Replace(InFolder.FullName, OutFolder.FullName);

                        if (h265Transcode)
                            OutFileName = OutFileName.Replace(F.Extension, ".mkv");

                        FileInfo OutFile = new FileInfo(OutFileName);
                        FileTranscoder FT = new FileTranscoder(F.FullName, OutFile.FullName);
                        FT.DeleteOriginal = DeleteOriginal;
                        FT.RemoveAds = RemoveAds;
                        FT.NoCopyUnalteredFiles = this.NoCopyUnaltered;
                        FT.RemovePlayOnBanner = RemovePlayOnBanner;
                        FT.h265Transcode = h265Transcode;
                        FT.AutoCrop = AutoCrop;
                        FT.Preset = Preset;
                        FT.CRF = CRF;
                        FT.twoPass = TwoPass;
                        FT.Transcode();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
            else
                throw new DirectoryNotFoundException("Input directory \"" + InFolder.FullName + "\" not found");
        }

      
        
      

       
    }

}
