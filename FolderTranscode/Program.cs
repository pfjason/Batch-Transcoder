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
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (args.Length >= 2)
                {
                    bool RemoveAds = true;
                    bool RemoveBanner = true;
                    bool Transcode = true;
                    bool Delete = false;
                    bool AutoCrop = true;

                    if (args.Length > 2)
                    {
                        int arg = 2;
                        while (arg < args.Length)
                        {
                            switch (args[arg].ToUpperInvariant())
                            {
                                case "-DELETE":
                                    Delete = true;
                                    break;
                                case "-NOTRANSCODE":
                                    Transcode = false;
                                    break;
                                case "-NOADREMOVAL":
                                    RemoveAds = false;
                                    break;
                                case "-NOBANNERREMOVAL":
                                    RemoveBanner = false;
                                    break;
                                case "-NOAUTOCROP":
                                    AutoCrop = false;
                                    break;
                            }
                            arg++;
                        }
                    }

                    FolderTranscoder F = new FolderTranscoder(args[0], args[1]);
                    F.DeleteOriginal = Delete;
                    F.RemoveAds = RemoveAds;
                    F.RemovePlayOnBanner = RemoveBanner;
                    F.h265Transcode = Transcode;
                    F.AutoCrop = AutoCrop;
                    F.StartTranscode();
                }
                else
                {
                    Console.WriteLine("Usage: ");
                    Console.WriteLine(System.AppDomain.CurrentDomain.FriendlyName + " [input directory] [output directory]");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

    }

    
   
}
