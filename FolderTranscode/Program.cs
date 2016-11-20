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
                    bool TwoPass = true;
                    int CRF = 18;
                    FileTranscoder.H265Preset Preset = FileTranscoder.H265Preset.veryfast;

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
                                case "-1-PASS":
                                    TwoPass = false;
                                    break;
                                case "-PRESET":
                                    try
                                    {
                                       Preset =  (FileTranscoder.H265Preset)Enum.Parse(typeof(FileTranscoder.H265Preset), args[arg + 1].ToLowerInvariant()) ;                                            
                                    }
                                    catch(Exception)
                                    {
                                        Console.WriteLine("Invalid Transcoder Preset, using default VeryFast");
                                        Preset = FileTranscoder.H265Preset.veryfast;
                                    }
                                    break;

                                case "-CRF":
                                    try
                                    {
                                        string c = args[arg + 1];
                                        if(Int32.TryParse(c, out CRF) && CRF >= 0 && CRF <= 51 )
                                        {
                                            Console.WriteLine("Setting CRF to " + CRF.ToString());
                                        }
                                        else
                                        {
                                            Console.WriteLine("Invalid CRF, using default 18");
                                            CRF = 18;
                                        }
                                    }
                                    catch(Exception)
                                    {
                                        Console.WriteLine("Invalid CRF, using Default");
                                        CRF = 18;
                                    }
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
                    F.TwoPass = TwoPass;
                    F.Preset = Preset;
                    F.CRF = CRF;
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
