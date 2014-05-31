using System;

namespace OneTouchDownloader
{
    public class Reading
    {
        public int Glucose { get; set; }
        public DateTime ReadingDate { get; set; }

        public override string ToString()
        {
            return string.Format("Glucose: {0}, ReadingDate: {1:dd.MM.yyyy HH:mm:ss}", Glucose, ReadingDate);
        }
    }
}