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
    partial class FileTranscoder
    {
        public FileInfo InputFile { get; private set; }
        TimeSpan Duration;
        public FileInfo OutputFile { get; private set; }
        Dictionary<string, string> Metadata = new Dictionary<string, string>();

        public bool RemovePlayOnBanner = false;
        public bool RemoveAds = false;
        public bool DeleteOriginal = false;
        public bool ForcePlayOnMode = false;
        public bool h265Transcode = true;
        public bool twoPass = true;
        public bool AutoCrop = true;

        public FileTranscoder(string inFileName, string outFileName)
        {            
            InputFile = new FileInfo(inFileName);
            OutputFile = new FileInfo(outFileName);
            MediaFile F = new MediaFile(InputFile.FullName);
            Metadata = GetMetaData(F);
            MediaInfoDotNet.Models.VideoStream VS = F.Video[0];
            Duration = new TimeSpan(VS.duration * TimeSpan.TicksPerMillisecond);
        }

        public bool isPlayOnFile()
        {
            MediaFile F = new MediaFile(InputFile.FullName);
            return ForcePlayOnMode || F.Inform.ToUpperInvariant().Contains("PROVIDERNAME ") && F.Inform.ToUpperInvariant().Contains("BROWSEPATH ");
        }

        public bool hasAds()
        {
            bool retVal = false;

            if (isPlayOnFile())
            {
                MediaFile F = new MediaFile(InputFile.FullName);

                foreach (MediaInfoDotNet.Models.MenuStream m in F.Menu)
                {
                    string i = m.miInform();
                    if (i.Contains("Menu #2") && i.Contains(": Advertisement"))
                    {
                        retVal = true;
                        break;
                    }
                }
            }

            return retVal;
        }



        Dictionary<string, string> GetMetaData(MediaFile F)
        {
            Dictionary<string, string> retVal = new Dictionary<string, string>() { };

            StringReader sr = new StringReader(F.Inform);
            string line = null;

            do
            {
                line = sr.ReadLine();

                if (line != null && line.Contains(":"))
                {
                    string[] parts = line.Split(':');

                    if (parts.Length == 2)
                    {
                        try
                        {
                            string k, v;
                            k = parts[0].Trim().Replace("\"", "'");
                            v = parts[1].Trim().Replace("\"", "'");

                            if (k != null && v != null && !retVal.ContainsKey(k))
                                retVal.Add(k, v);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.ToString());
                        }

                    }
                }

            } while (line != null);

            return retVal;
        }

        public bool Transcode()
        {

            bool exclusiveAccess = false;
            bool RetVal = false;

            try
            {
                FileStream FileTest = InputFile.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                FileTest.Close();
                exclusiveAccess = true;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Couldn't get exclusive access to " + InputFile.Name + ", Skipping");
                Console.ForegroundColor = ConsoleColor.Gray;
            }

            if (!OutputFile.Directory.Exists)
                OutputFile.Directory.Create();

            if (!OutputFile.Exists && exclusiveAccess)
            {
                MediaFile F = new MediaFile(InputFile.FullName);

                if (F.HasStreams)
                {
                    if (F.Video.Count > 0)
                    {
                        string tempBannerRemove = null;
                        string tempAdremove = null;

                        if (isPlayOnFile())
                        {
                            Console.ForegroundColor = ConsoleColor.Blue;
                            Console.WriteLine(InputFile.Name + " is a PlayOn file.");
                            Console.ForegroundColor = ConsoleColor.Gray;


                            if (RemoveAds && hasAds())
                            {
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.WriteLine(F.filePath + " has PlayOn Detected Commercials. Removing...");
                                Console.ForegroundColor = ConsoleColor.Gray;

                                tempAdremove = RemoveCommercials(F);

                                if (tempAdremove != null)
                                {
                                    F = new MediaFile(tempAdremove);

                                    if (!RemovePlayOnBanner && !h265Transcode)
                                    {
                                        File.Move(F.filePath, OutputFile.FullName);

                                        if (DeleteOriginal)
                                        {
                                            Console.ForegroundColor = ConsoleColor.Yellow;
                                            Console.WriteLine("Deleting " + InputFile.Name);
                                            InputFile.Delete();
                                            Console.ForegroundColor = ConsoleColor.Gray;
                                        }

                                        return true;
                                    }
                                }

                            }

                            if (RemovePlayOnBanner)
                            {
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.WriteLine(F.filePath + " has PlayOn Banner. Removing...");
                                Console.ForegroundColor = ConsoleColor.Gray;
                                tempBannerRemove = RemoveBanner(F);

                                if (tempBannerRemove != null)
                                {
                                    F = new MediaFile(tempBannerRemove);

                                    if (tempAdremove != null)
                                        File.Delete(tempAdremove);

                                    if (!h265Transcode)
                                    {
                                        File.Move(F.filePath, OutputFile.FullName);

                                        if (DeleteOriginal)
                                        {
                                            Console.ForegroundColor = ConsoleColor.Yellow;
                                            Console.WriteLine("Deleting " + InputFile.Name);
                                            InputFile.Delete();
                                            Console.ForegroundColor = ConsoleColor.Gray;
                                        }
                                        return true;
                                    }
                                }
                            }
                        }

                        if (h265Transcode)
                        {


                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("Transcoding " + F.filePath + " to h.265 format...");
                            Console.ForegroundColor = ConsoleColor.Gray;
                            RetVal = (ffmpeg_h265_Transcode(F) != null);

                            if (tempAdremove != null && File.Exists(tempAdremove))
                                File.Delete(tempAdremove);

                            if (tempBannerRemove != null && File.Exists(tempBannerRemove))
                                File.Delete(tempBannerRemove);

                        }
                        else
                        {
                            RetVal = ProcessNonMediaFile();
                        }
                    }
                    else
                    {
                        RetVal = ProcessNonMediaFile();
                    }
                }


            }
            else
                throw new InvalidOperationException(OutputFile.FullName + " already exists");
            return RetVal;
        }

        bool ProcessNonMediaFile()
        {
            bool RetVal = false;

            Console.Write(InputFile.FullName + " is not a media file. ");

            if (!OutputFile.Exists)
            {
                if (!DeleteOriginal)
                {
                    Console.WriteLine("Copying...");
                    InputFile.CopyTo(OutputFile.FullName);
                    RetVal = true;
                }
                else
                {
                    Console.WriteLine("Moving...");
                    InputFile.MoveTo(OutputFile.FullName);
                    RetVal = true;
                }
            }

            return RetVal;
        }

        string RemoveCommercials(MediaFile F)
        {
            string retVal = null;
            string ffmpeg = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).Directory.FullName + "\\ffmpeg.exe";

            if (File.Exists(ffmpeg))
            {
                if (hasAds())
                {
                    Collection<Chapter> Chapters = new Collection<Chapter>();

                    foreach (MediaInfoDotNet.Models.MenuStream m in F.Menu)
                    {
                        string i = m.miInform();


                        StringReader R = new StringReader(i);

                        string line;

                        while ((line = R.ReadLine()) != "Menu #2")
                        { }

                        if (line == "Menu #2")
                        {
                            Console.ForegroundColor = ConsoleColor.Gray;

                            int ChapterCount = 1;
                            bool first1 = true;

                            while ((line = R.ReadLine()) != null)
                            {

                                if (line.ToUpperInvariant().Contains(": VIDEO".ToUpperInvariant()) || line.ToUpperInvariant().Contains(": ADVERTISEMENT".ToUpperInvariant()))
                                {
                                    Chapter CC = new Chapter(line, ChapterCount);
                                    Chapters.Add(CC);

                                    if (!first1)
                                    {
                                        Chapters[ChapterCount - 2].SetDuration(CC.Start);
                                    }

                                    first1 = false;
                                }

                                ChapterCount++;
                            }
                            Chapters[Chapters.Count - 1].SetDuration(Duration);

                            break;
                        }
                    }
                    Collection<string> ChapterFiles = new Collection<string>();
                    int chapterCount = 0;
                    foreach (Chapter C in Chapters)
                    {
                        //Console.WriteLine(C.ToString());


                        if (C.Title.ToUpperInvariant() != "ADVERTISEMENT".ToUpperInvariant())
                        {
                            string f = OutputFile.Directory.FullName + "\\~part." + chapterCount.ToString() + "." + InputFile.Name.ToString();
                            string f2 = OutputFile.Directory.FullName + "\\~part." + chapterCount.ToString() + "." + InputFile.Name.ToString().Replace(InputFile.Extension, ".int.ts");
                            Process ffm = new Process();
                            ffm.StartInfo.UseShellExecute = false;
                            ffm.StartInfo.RedirectStandardError = true;
                            ffm.StartInfo.RedirectStandardOutput = true;
                            ffm.ErrorDataReceived += Ff_ErrorDataReceived;
                            ffm.OutputDataReceived += Ff_OutputDataReceived;
                            ffm.StartInfo.FileName = ffmpeg;
                            ffm.StartInfo.Arguments = "-y -i \"" + InputFile.FullName + "\" -ss " + C.Start.TotalSeconds.ToString() + " -to " + C.End.TotalSeconds.ToString() + " -codec copy \"" + f + "\"";
                            // Console.WriteLine(ffm.StartInfo.FileName.ToString() + " " + ffm.StartInfo.Arguments.ToString());
                            ffm.Start();
                            ffm.BeginErrorReadLine();
                            ffm.BeginOutputReadLine();
                            ffm.WaitForExit();

                            Process ffm2 = new Process();
                            ffm2.StartInfo.UseShellExecute = false;
                            ffm2.StartInfo.RedirectStandardError = true;
                            ffm2.StartInfo.RedirectStandardOutput = true;
                            ffm2.ErrorDataReceived += Ff_ErrorDataReceived;
                            ffm2.OutputDataReceived += Ff_OutputDataReceived;
                            ffm2.StartInfo.FileName = ffmpeg;
                            ffm2.StartInfo.Arguments = "-y -i \"" + f + "\" -codec copy -bsf:v h264_mp4toannexb -f mpegts \"" + f2 + "\"";
                            // Console.WriteLine(ffm.StartInfo.FileName.ToString() + " " + ffm.StartInfo.Arguments.ToString());
                            ffm2.Start();
                            ffm2.BeginErrorReadLine();
                            ffm2.BeginOutputReadLine();
                            ffm2.WaitForExit();

                            if (ffm.ExitCode == 0 && ffm2.ExitCode == 0 && File.Exists(f2))
                            {
                                ChapterFiles.Add(f2);
                                File.Delete(f);
                            }

                            chapterCount++;
                        }
                    }

                    string arg = " -y -i \"concat:";
                    bool first = true;
                    foreach (string ChapterFile in ChapterFiles)
                    {
                        if (!first)
                            arg += "|";
                        arg += ChapterFile;
                        first = false;
                    }

                    arg += "\"";

                    string OF = OutputFile.Directory.FullName + "\\~rc." + InputFile.Name;

                    arg += GetFFMPegMetaDataArgs();
                    arg += " -codec copy -bsf:1 aac_adtstoasc \"" + OF + "\"";

                    Process ff = new Process();
                    ff.StartInfo.FileName = ffmpeg;
                    ff.StartInfo.Arguments = arg;
                    ff.StartInfo.UseShellExecute = false;
                    ff.StartInfo.RedirectStandardError = true;
                    ff.StartInfo.RedirectStandardOutput = true;
                    ff.ErrorDataReceived += Ff_ErrorDataReceived;
                    ff.OutputDataReceived += Ff_OutputDataReceived;
                    ff.Start();
                    ff.BeginErrorReadLine();
                    ff.BeginOutputReadLine();
                    ff.WaitForExit();

                    if (ff.ExitCode == 0)
                        retVal = OF;

                    foreach (string f in ChapterFiles)
                        File.Delete(f);
                }
            }
            else throw new FileNotFoundException("FFMPEG was not found", ffmpeg);

            return retVal;
        }


        string GetAutoCropValues(MediaFile F)
        {
            string retVal = null;
            string ffmpeg = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).Directory.FullName + "\\ffmpeg.exe";

            if (File.Exists(ffmpeg))
            {
                string arg = "-i \"" + F.filePath + "\" -ss 00:02:00  -vframes 100 -vf cropdetect=65:16:0 -f null NUL";
                Process ffcrop = new Process();
                ffcrop.StartInfo.FileName = ffmpeg;
                ffcrop.StartInfo.Arguments = arg;
                ffcrop.StartInfo.UseShellExecute = false;
                ffcrop.StartInfo.RedirectStandardError = true;
                ffcrop.StartInfo.RedirectStandardOutput = false;
                ffcrop.StartInfo.CreateNoWindow = true;
                ffcrop.Start();
                string E = ffcrop.StandardError.ReadToEnd();
                ffcrop.WaitForExit();
                string Val = null;
                StringReader R = new StringReader(E);
                string line = null;

                while ((line = R.ReadLine()) != null)
                {
                    if (line.Contains("crop=") && line.Contains("cropdetect"))
                    {
                        Val = line.Substring(line.LastIndexOf("crop=") + 5);
                    }
                }

                if (ffcrop.ExitCode == 0)
                    retVal = Val;
            }
            else throw new FileNotFoundException("FFMPEG was not found", ffmpeg);

            return retVal;

        }

        private void Ffcrop_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            try
            {
                Console.CursorLeft = 0;
                int Top = Console.CursorTop;
                Console.Write(e.Data.PadRight(Console.WindowWidth - 1));
                Console.CursorTop = Top;
            }
            catch { }
        }

        private void Ffcrop_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            try
            {
                Console.CursorLeft = 0;
                int Top = Console.CursorTop;
                Console.Write(e.Data.PadRight(Console.WindowWidth - 1));
                Console.CursorTop = Top;
            }
            catch { }
        }

        string RemoveBanner(MediaFile F)
        {
            string retVal = null;
            string ffmpeg = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).Directory.FullName + "\\ffmpeg.exe";

            if (File.Exists(ffmpeg))
            {
                string arg = "-y -i \"" + F.filePath + "\" ";

                string OF = OutputFile.Directory.FullName + "\\~br." + InputFile.Name;
                decimal dd = (Convert.ToDecimal(F.Video[0].Duration) / 1000) - 10;

                arg += GetFFMPegMetaDataArgs();

                arg += " -codec copy -ss 00:00:04 -to " + dd.ToString() + " \"" + OF + "\"";

                Process ff = new Process();
                ff.StartInfo.FileName = ffmpeg;
                ff.StartInfo.Arguments = arg;
                ff.StartInfo.UseShellExecute = false;
                ff.StartInfo.RedirectStandardError = true;
                ff.StartInfo.RedirectStandardOutput = true;
                ff.ErrorDataReceived += Ff_ErrorDataReceived;
                ff.OutputDataReceived += Ff_OutputDataReceived;

                ff.Start();
                ff.BeginErrorReadLine();
                ff.BeginOutputReadLine();
                ff.WaitForExit();

                if (ff.ExitCode == 0)
                    retVal = OF;
            }
            else throw new FileNotFoundException("FFMPEG was not found", ffmpeg);

            return retVal;

        }

        string GetFFMPegMetaDataArgs()
        {
            string retVal = "";

            if (Metadata.ContainsKey("Track name"))
            {
                retVal += " -metadata title=\"" + Metadata["Track name"] + "\"";
                retVal += " -metadata Track_Name=\"" + Metadata["Track name"] + "\"";
            }

            if (Metadata.ContainsKey("Collection"))
                retVal += " -metadata Collection=\"" + Metadata["Collection"] + "\"";

            if (Metadata.ContainsKey("Performer"))
                retVal += " -metadata Performer=\"" + Metadata["Performer"] + "\"";

            if (Metadata.ContainsKey("Season"))
                retVal += " -metadata Season=\"" + Metadata["Season"] + "\"";

            if (Metadata.ContainsKey("Part"))
                retVal += " -metadata Part=\"" + Metadata["Part"] + "\"";

            if (Metadata.ContainsKey("ContentType"))
                retVal += " -metadata ContentType=\"" + Metadata["ContentType"] + "\"";

            if (Metadata.ContainsKey("Collection"))
                retVal += " -metadata collection=\"" + Metadata["Collection"] + "\"";

            if (Metadata.ContainsKey("Comment"))
            {
                retVal += " -metadata description=\"" + Metadata["Comment"] + "\"";
                retVal += " -metadata Comment=\"" + Metadata["Comment"] + "\"";
            }

            System.Reflection.Assembly A = System.Reflection.Assembly.GetExecutingAssembly();

            retVal += " -metadata converter=\"" + A.GetName().Name + " " + A.GetName().Version.ToString() + "\"";


            return retVal;
        }

        string ffmpeg_h265_Transcode(MediaFile F)
        {
            string retVal = null;
            string ffmpeg = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).Directory.FullName + "\\ffmpeg.exe";

            if (File.Exists(ffmpeg))
            {
                string ac = "";

                if (AutoCrop)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("Getting AutoCrop Settings: ");
                    ac = GetAutoCropValues(F);
                    Console.WriteLine(ac);
                    Console.ForegroundColor = ConsoleColor.Gray;
                }

                string arg = "-y -i \"" + F.filePath + "\" ";
                arg += " -vf \"yadif=0:-1:0";

                if (AutoCrop)
                    arg += ", crop=" + ac;

                arg += "\" -c:a copy  ";

                string OF = OutputFile.Directory.FullName + "\\" + InputFile.Name.Replace(InputFile.Extension, ".mkv");

#if !DEBUG
                arg += " -c:v libx265 -preset ultrafast -crf 10 ";
#else
                arg += " -c:v libx265 -preset ultrafast -crf 50 ";
#endif

                string arg1 = arg;
                string arg2 = arg;
                arg1 += " -pass 1 -f matroska NUL";
                arg2 += GetFFMPegMetaDataArgs();

                if (twoPass)
                    arg2 += " -pass 2 ";

                arg2 += " \"" + OF + "\"";

                Process ff1 = new Process();

                if (twoPass)
                {
                    Console.Write("Pass 1: ");
                    ff1.StartInfo.FileName = ffmpeg;
                    ff1.StartInfo.Arguments = arg1;
                    ff1.StartInfo.UseShellExecute = false;
                    ff1.StartInfo.RedirectStandardError = true;
                    ff1.StartInfo.RedirectStandardOutput = false;
                    ff1.ErrorDataReceived += Ff_ErrorDataReceived;
                   // ff1.OutputDataReceived += Ff_OutputDataReceived;
                    //Console.WriteLine(ff1.StartInfo.FileName + " " + ff1.StartInfo.Arguments);
                    //Console.ReadKey();
                    ff1.Start();
                    ff1.BeginErrorReadLine();
                    //ff1.BeginOutputReadLine();
                    ff1.WaitForExit();
                    Console.CursorLeft = 0;
                }

                if (twoPass)
                    Console.Write("Pass 2: ");                

                Process ff2 = new Process();
                ff2.StartInfo.FileName = ffmpeg;
                ff2.StartInfo.Arguments = arg2;
                ff2.StartInfo.UseShellExecute = false;
                ff2.StartInfo.RedirectStandardError = true;
                ff2.StartInfo.RedirectStandardOutput = false;
                ff2.ErrorDataReceived += Ff_ErrorDataReceived;
               // ff2.OutputDataReceived += Ff_OutputDataReceived;
                //Console.WriteLine(ff2.StartInfo.FileName + " " + ff2.StartInfo.Arguments);
                //Console.ReadKey();
                ff2.Start();
                ff2.BeginErrorReadLine();
               // ff2.BeginOutputReadLine();
                ff2.WaitForExit();

                if ((twoPass && ff1.ExitCode == 0 && ff2.ExitCode == 0)
                    || !twoPass && ff2.ExitCode == 0)
                    retVal = OF;
            }
            else throw new FileNotFoundException("FFMPEG was not found", ffmpeg);

            return retVal;

        }

        private void Ff_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            try
            {
                // This needs to be here, (or something that doesn't get optimized out by the compiler) so output data is discarded without hanging ffmpeg.
                Console.CursorLeft = Console.CursorLeft; 
            }
            catch { }
        }

        private void Ff_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            try
            {
                int Left = Console.CursorLeft;
                int Top = Console.CursorTop;
                if (e.Data.StartsWith("frame="))
                {
                    Console.Write(e.Data.PadRight(Console.WindowWidth - Left - 1));
                    Console.CursorTop = Top;
                    Console.CursorLeft = Left;
                }
            }
            catch { }
        }
    }
}
