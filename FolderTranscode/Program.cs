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
                    bool Delete = false;

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
                            }
                            arg++;
                        }
                    }

                    FolderTranscoder F = new FolderTranscoder(args[0], args[1], Delete);
                    F.DeleteOriginal = Delete;
                    F.RemoveAds = true;
                    F.RemovePlayOnBanner = true;
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
