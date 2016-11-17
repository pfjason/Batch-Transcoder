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
        public bool DeleteOriginal = false, RemovePlayOnBanner = false, RemoveAds = false;

        public FolderTranscoder(string inFolder, string outFolder, bool deleteOriginal)
        {
            InFolder = new DirectoryInfo(inFolder);
            OutFolder = new DirectoryInfo(outFolder);
            DeleteOriginal = deleteOriginal;
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
                        FileInfo OutFile = new FileInfo(F.FullName.Replace(InFolder.FullName, OutFolder.FullName).Replace(F.Extension, ".mkv"));
                        FileTranscoder FT = new FileTranscoder(F.FullName, OutFile.FullName);
                        FT.DeleteOriginal = DeleteOriginal;
                        FT.RemoveAds = RemoveAds;
                        FT.RemovePlayOnBanner = RemovePlayOnBanner;
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
