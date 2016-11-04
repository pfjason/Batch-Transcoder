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

                    if(args.Length > 2)
                    {
                        int arg = 2;
                        while(arg < args.Length)
                        {                           
                            switch(args[arg].ToUpperInvariant())
                            {
                                case "-DELETE":
                                    Delete = true;
                                    break;
                            }
                            arg++;
                        }
                    }
                    
                    FolderTranscoder F = new FolderTranscoder(args[0], args[1], Delete);
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

    class FolderTranscoder
    {
        DirectoryInfo InFolder, OutFolder;
        bool DeleteOriginal;

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
                if(DeleteOriginal)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("WARNING: DELETE ORIGINAL ON SUCCESSFUL TRANSCODE SET TO ON");
                    Console.ForegroundColor = ConsoleColor.Gray;
                }

                foreach (FileInfo F in InFolder.GetFiles("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        TranscodeFile(F);
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
            else
                throw new DirectoryNotFoundException("Input directory \"" + InFolder.FullName + "\" not found");
        }

        void TranscodeFile(FileInfo _file, bool RemovePlayOnBanner = false)
        {

            MediaFile F = new MediaFile(_file.FullName);

            if (F.HasStreams)
            {
                if (F.Video.Count > 0)
                {
                    MediaInfoDotNet.Models.VideoStream VS = F.Video[0];
                    Process Handbrake = new Process();
                    Handbrake.StartInfo.UseShellExecute = false;
                    Handbrake.StartInfo.RedirectStandardError = true;
                    Handbrake.StartInfo.RedirectStandardOutput = true;
                    Handbrake.OutputDataReceived += Handbrake_OutputDataReceived;
                    Handbrake.ErrorDataReceived += Handbrake_ErrorDataReceived;
                    Handbrake.StartInfo.FileName = @"C:\Program Files\Handbrake\HandbrakeCLI.exe";
                    Handbrake.StartInfo.Arguments = @"-e x265 --encoder-preset veryfast -q 18 --two-pass --decomb -P -U -N eng"
                                                      + " --maxWidth " + VS.width.ToString() + " --maxHeight " + VS.height.ToString()
                                                     + " --strict-anamorphic --audio-copy-mask aac,ac3,dts,dtshd  ";

                    bool PlayOn = F.Inform.ToUpperInvariant().Contains("PROVIDERNAME ") && F.Inform.ToUpperInvariant().Contains("BROWSEPATH ");
                    string AudioChannels = "";
                    Console.WriteLine(new String('-', Console.WindowWidth));
                    Console.WriteLine("Transcoding " + _file.FullName);

                    TimeSpan Duration;

                    //   Console.WriteLine(F.Inform);
                    //   Console.WriteLine("Album: " + F.General.Album);
                    
                    Console.WriteLine("Stream " + VS.streamid.ToString() + ": " + VS.ToString());
                    Console.WriteLine("Codec: " + VS.CodecId.ToString() + " " + VS.codecCommonName);
                    Console.WriteLine("Resolution: " + VS.width.ToString() + "x" + VS.height.ToString());
                    Duration = new TimeSpan(VS.duration * TimeSpan.TicksPerMillisecond);
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

                    /*      switch (AudioChannels)
                          {
                              case "6":
                                  Handbrake.StartInfo.Arguments += "--mixdown 5point1 ";
                                  break;
                              case "7":
                                  Handbrake.StartInfo.Arguments += "--mixdown 6point1 ";
                                  break;
                              case "8":
                                  Handbrake.StartInfo.Arguments += "--mixdown 7point1 ";
                                  break;
                          }*/

#if !DEBUG

                    if (PlayOn)
                    {
                        //Remove first 4 seconds and last 6 seconds of video for PlayOn banner deletion.
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.WriteLine(_file.Name + " is a PlayOn file");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Handbrake.StartInfo.Arguments += "--start-at duration:4 "
                            + "--stop-at duration:" + (Duration.TotalSeconds - 10).ToString() + " ";
                    }
#else
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("DEBUG Mode: " + _file.Name + " Encoding first 10 seconds only");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Handbrake.StartInfo.Arguments += "--start-at duration:0 --stop-at duration:30 ";
#endif

                    Handbrake.StartInfo.Arguments += "-i \"" + _file.FullName + "\" ";

                    FileInfo OutFile = new FileInfo(_file.FullName.Replace(InFolder.FullName, OutFolder.FullName).Replace(_file.Extension, ".mkv"));

                    Handbrake.StartInfo.Arguments += "-o \"" + OutFile.FullName + "\" ";

                    if (!OutFile.Exists)
                    {
                        Console.WriteLine(Handbrake.StartInfo.FileName + " " + Handbrake.StartInfo.Arguments);

                        if (!OutFile.Directory.Exists)
                            OutFile.Directory.Create();

                        Handbrake.Start();
                        Handbrake.PriorityClass = ProcessPriorityClass.BelowNormal;
                        Console.CursorVisible = false;
                        Handbrake.BeginOutputReadLine();
                        Handbrake.BeginErrorReadLine();
                        Handbrake.WaitForExit();
                        Console.CursorVisible = true;
                        Console.WriteLine();
                        Console.WriteLine("HandbrakeCLI exited with code " + Handbrake.ExitCode.ToString());
                        if(DeleteOriginal)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Deleting " + _file.Name);
                            _file.Delete();
                            Console.ForegroundColor = ConsoleColor.Gray;
                        }

                    }
                    else
                        throw new InvalidOperationException(OutFile.FullName + " already exists");


                }
                else
                {
                    Console.Write(_file.FullName + " is not a video file. ");
                    if (!File.Exists(_file.FullName.Replace(InFolder.FullName, OutFolder.FullName)))
                    {
                        if (!DeleteOriginal)
                        {
                            Console.WriteLine("Copying...");
                            _file.CopyTo(_file.FullName.Replace(InFolder.FullName, OutFolder.FullName));
                        }
                        else
                        {
                            Console.WriteLine("Moving...");
                            _file.MoveTo(_file.FullName.Replace(InFolder.FullName, OutFolder.FullName));
                        }
                    }
                }

            }
            else
            {
                Console.Write(_file.FullName + " is not a media file. ");
                if (!File.Exists(_file.FullName.Replace(InFolder.FullName, OutFolder.FullName)))
                {
                    if (!DeleteOriginal)
                    {
                        Console.WriteLine("Copying...");
                        _file.CopyTo(_file.FullName.Replace(InFolder.FullName, OutFolder.FullName));
                    }
                    else
                    {
                        Console.WriteLine("Moving...");
                        _file.MoveTo(_file.FullName.Replace(InFolder.FullName, OutFolder.FullName));
                    }
                }
            }

        }

        private void Handbrake_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.CursorLeft = 0;
            //Console.Write(e.Data.PadRight(Console.WindowWidth));
        }

        private void Handbrake_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            try
            {
                Console.CursorLeft = 0;
                int Top = Console.CursorTop;
                Console.Write(e.Data.PadRight(Console.WindowWidth-1));
                Console.CursorTop = Top;
            }
            catch { }
        }
    }
}
