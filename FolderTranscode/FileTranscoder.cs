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

        public bool RemovePlayOnBanner = false;
        public bool RemoveAds = false;
        public bool DeleteOriginal = false;
        public bool ForcePlayOnMode = false;
        public bool h265Transcode = true;

        public FileTranscoder(string inFileName, string outFileName)
        {
            InputFile = new FileInfo(inFileName);
            OutputFile = new FileInfo(outFileName);
            MediaFile F = new MediaFile(InputFile.FullName);
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

        public bool Transcode()
        {
            bool exclusiveAccess = false;

            try
            {
                FileStream FileTest = InputFile.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                FileTest.Close();
                exclusiveAccess = true;
            }
            catch(Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Couldn't get exclusive access to " + InputFile.Name + ", Skipping") ;
                Console.ForegroundColor = ConsoleColor.Gray;
            }

            if (!OutputFile.Directory.Exists)
                OutputFile.Directory.Create();

            if (!OutputFile.Exists && exclusiveAccess)
            {
                bool RetVal = false;
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
                            string HandbrakePath = @"C:\Program Files\Handbrake\HandbrakeCLI.exe";

                            if (File.Exists(HandbrakePath))
                            {
                                MediaInfoDotNet.Models.VideoStream VS = F.Video[0];
                                Process Handbrake = new Process();
                                Handbrake.StartInfo.UseShellExecute = false;
                                Handbrake.StartInfo.RedirectStandardError = true;
                                Handbrake.StartInfo.RedirectStandardOutput = true;
                                Handbrake.OutputDataReceived += Handbrake_OutputDataReceived;
                                Handbrake.ErrorDataReceived += Handbrake_ErrorDataReceived;
                                Handbrake.StartInfo.FileName = HandbrakePath;
                                Handbrake.StartInfo.Arguments = @"-e x265 --encoder-preset veryfast -q 18 --two-pass --decomb ";
#if DEBUG
                            Handbrake.StartInfo.Arguments = @"-e x265 --encoder-preset ultrafast -q 40 ";
#endif

                                Handbrake.StartInfo.Arguments += "-P -U -N eng"
                                                                  + " --maxWidth " + VS.width.ToString() + " --maxHeight " + VS.height.ToString() + " -m "
                                                                 + " --strict-anamorphic --audio-copy-mask aac,ac3,dts,dtshd  ";
                                string AudioChannels = "";
                                Console.WriteLine(new String('-', Console.WindowWidth));
                                Console.WriteLine("Transcoding " + F.filePath);
                                Console.WriteLine("Stream " + VS.streamid.ToString() + ": " + VS.ToString());
                                Console.WriteLine("Codec: " + VS.CodecId.ToString() + " " + VS.codecCommonName);
                                Console.WriteLine("Resolution: " + VS.width.ToString() + "x" + VS.height.ToString());
                                Console.WriteLine("Length: " + Duration.ToString());

                                int ChannelCount = 0;

                                foreach (MediaInfoDotNet.Models.AudioStream A in F.Audio)
                                {
                                    if (ChannelCount == 0)
                                        Handbrake.StartInfo.Arguments += "-E ";
                                    else
                                        Handbrake.StartInfo.Arguments += ",";

                                    ChannelCount++;
                                    Handbrake.StartInfo.Arguments += "copy";
                                    Console.WriteLine("Audio Stream " + A.streamid + ": " + A.ToString());
                                    Console.WriteLine("Audio Codec: " + A.CodecId + " / " + A.CodecCommonName + " / " + A.codecCommonName + " / " + A.EncodedLibrary + " / " + A.encoderLibrary);

                                    Console.WriteLine("Channels: " + A.Channels);
                                    AudioChannels = A.Channels;

                                }
                                Handbrake.StartInfo.Arguments += " ";


                                foreach (MediaInfoDotNet.Models.TextStream T in F.Text)
                                {
                                    Console.WriteLine("Subtitle " + T.streamid + ": " + T.ToString());
                                }

                                int subtitles = 0;
                                if (F.Text.Count > 0)
                                    Handbrake.StartInfo.Arguments += "-s ";

                                while (subtitles < F.Text.Count)
                                {
                                    if (subtitles != 0)
                                        Handbrake.StartInfo.Arguments += ",";

                                    Handbrake.StartInfo.Arguments += (subtitles + 1).ToString();
                                    subtitles++;
                                }

                                if (F.Text.Count > 0)
                                    Handbrake.StartInfo.Arguments += ",scan ";

                                Handbrake.StartInfo.Arguments += "-i \"" + F.filePath + "\" ";
                                Handbrake.StartInfo.Arguments += "-o \"" + OutputFile.FullName + "\" ";

                                //Console.WriteLine(Handbrake.StartInfo.FileName + " " + Handbrake.StartInfo.Arguments);

                                if (!OutputFile.Directory.Exists)
                                    OutputFile.Directory.Create();

                                Handbrake.Start();
                                Handbrake.PriorityClass = ProcessPriorityClass.BelowNormal;
                                Console.CursorVisible = false;
                                Handbrake.BeginOutputReadLine();
                                Handbrake.BeginErrorReadLine();
                                Handbrake.WaitForExit();
                                Console.CursorVisible = true;
                                Console.WriteLine();
                                Console.WriteLine("HandbrakeCLI exited with code " + Handbrake.ExitCode.ToString());

                                if (Handbrake.ExitCode == 0)
                                    RetVal = true;

                                if (DeleteOriginal && Handbrake.ExitCode == 0)
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("Deleting " + InputFile.Name);
                                    InputFile.Delete();
                                    Console.ForegroundColor = ConsoleColor.Gray;
                                }

                                if (Handbrake.ExitCode != 0)
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine("Handbrake exited with code " + Handbrake.ExitCode.ToString() + " deleting partial output file " + OutputFile.Name);
                                    OutputFile.Delete();
                                    Console.ForegroundColor = ConsoleColor.Gray;
                                }


                            }
                            else
                                throw new FileNotFoundException("Unable to find HandbrakeCLI.", HandbrakePath);

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

                return RetVal;
            }
            else
                throw new InvalidOperationException(OutputFile.FullName + " already exists");
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
                            string f = OutputFile.Directory.FullName + "\\~part." + chapterCount.ToString() + "." + InputFile.Name.ToString().Replace(InputFile.Extension, ".ts");
                            Process ffm = new Process();
                            ffm.StartInfo.UseShellExecute = false;
                            ffm.StartInfo.RedirectStandardError = true;
                            ffm.StartInfo.RedirectStandardOutput = true;
                            ffm.ErrorDataReceived += Ff_ErrorDataReceived;
                            ffm.OutputDataReceived += Ff_OutputDataReceived;
                            ffm.StartInfo.FileName = ffmpeg;
                            ffm.StartInfo.Arguments = "-y -i \"" + InputFile.FullName + "\" -ss " + C.Start.TotalSeconds.ToString() + " -to " + C.End.TotalSeconds.ToString() + " -codec copy -f mpegts \"" + f + "\"";
                            // Console.WriteLine(ffm.StartInfo.FileName.ToString() + " " + ffm.StartInfo.Arguments.ToString());
                            ffm.Start();
                            ffm.BeginErrorReadLine();
                            ffm.BeginOutputReadLine();
                            ffm.WaitForExit();

                            if (ffm.ExitCode == 0 && File.Exists(f))
                                ChapterFiles.Add(f);

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

                    string OF = OutputFile.Directory.FullName + "\\~rc." + InputFile.Name;
                    arg += "\" -codec copy -bsf:1 aac_adtstoasc \"" + OF + "\"";

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

        string RemoveBanner(MediaFile F)
        {
            string retVal = null;
            string ffmpeg = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).Directory.FullName + "\\ffmpeg.exe";

            if (File.Exists(ffmpeg))
            {
                string arg = "-y -i \"" + F.filePath + "\" ";

                string OF = OutputFile.Directory.FullName + "\\~br." + InputFile.Name;
                decimal dd = (Convert.ToDecimal(F.Video[0].Duration) / 1000) - 10;
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

        private void Ff_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.CursorLeft = 0;
            // Console.WriteLine(e.Data);
        }

        private void Ff_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.CursorLeft = 0;
            //Console.WriteLine("E:" + e.Data);
        }

        private void Handbrake_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.CursorLeft = 0;
        }

        private void Handbrake_OutputDataReceived(object sender, DataReceivedEventArgs e)
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
    }
}
