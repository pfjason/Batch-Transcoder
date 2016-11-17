using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FolderTranscode
{
    partial class FileTranscoder
    {
        private class Chapter
        {
            public int Number { get; private set; }
            public string Marker { get; private set; }
            public string Title { get; private set; }
            public string TimeStamp { get; private set; }
            public TimeSpan Start { get; private set; }
            public TimeSpan Duration { get; internal set; }
            public TimeSpan End { get { return new TimeSpan(Start.Ticks + Duration.Ticks); } }

            public Chapter(string marker, int number)
            {
                Marker = marker;
                Number = number;
                InitMarker();
            }

            void InitMarker()
            {
                string Timestamp = Marker.Substring(0, 12);
                TimeStamp = Timestamp;
                string[] TimeParts = Timestamp.Split(':');
                TimeSpan S = new TimeSpan(0);
                int Hours, Minutes;
                double Seconds;
                if (Int32.TryParse(TimeParts[0], out Hours) && Int32.TryParse(TimeParts[1], out Minutes) && Double.TryParse(TimeParts[2], out Seconds))
                {
                    S = new TimeSpan(0, Hours, Minutes, 0, Convert.ToInt32(Seconds * 1000));
                }
                else
                    throw new Exception("Unable to parse " + Timestamp + " as a new TimeSpan object.");

                Start = S;

                Title = Marker.Replace(Timestamp, "").Trim().Substring(2);
            }

            public void SetDuration(TimeSpan NextChapterStart)
            {
                Duration = new TimeSpan(NextChapterStart.Ticks - Start.Ticks);
            }

            public override string ToString()
            {
                return Number.ToString() + ": " + Title + "(" + Start.ToString() + " - " + new TimeSpan(Start.Ticks + Duration.Ticks).ToString() + ")";
            }
        }
    }
}
