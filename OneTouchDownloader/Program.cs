using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OneTouchDownloader.Devices;

namespace OneTouchDownloader
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            //int portNumber;
            //do
            //{
            //    Console.Write("Select serial port: ");
            //    var portNumberString = Console.ReadLine();
            //    int.TryParse(portNumberString, out portNumber);
            //} while (portNumber == 0);

            //GetOneTouchData(portNumber);

            GetTaiDocData(18);

        }

        private static void GetTaiDocData(int portNumber)
        {
            var d = new TaiDocDownloader();
            var ok = d.InitPort(portNumber);
            if (!ok)
            {
                Console.WriteLine("Cannot open serial port");
                Console.ReadKey();
                return;
            }
            d.InitDevice();
            var serial = d.ReadSerialNumber();
            var nr_of_readings = d.ReadNrOfReadings();
            var lista = new List<Reading>();
            for (var i = 0; i < nr_of_readings; i++)
                lista.Add(d.GetRecord(i));

            var sb = new StringBuilder();
            sb.AppendLine("insert into Table values");
            foreach (var rec in lista)
            {
                sb.AppendFormat("(user_id,'{0:yyyyMMdd}','{0:HH:mm:ss}','','before',{1}),\r\n", rec.ReadingDate, rec.Glucose);
            }
            File.WriteAllText("export.txt", sb.ToString());


            Console.ReadKey();
            d.StopDevice();
            d.ClosePort();
        }

        private static void GetOneTouchData(int portNumber)
        {
            try
            {
                var d = new Devices.OneTouchDownloader();
                var ok = d.InitPort(portNumber);
                if (!ok)
                {
                    Console.WriteLine("Cannot open serial port");
                    Console.ReadKey();
                    return;
                }
                d.InitDevice();
                var sw_ver = d.ReadSWVersion();
                var serial = d.ReadSerialNumber();
                var nr_of_readings = d.ReadNrOfReadings();
                var lista = new List<Reading>();
                for (var i = 0; i < nr_of_readings; i++)
                    lista.Add(d.GetRecord(i));

                var sb = new StringBuilder();
                sb.AppendLine("insert into Table values");
                foreach (var rec in lista)
                {
                    sb.AppendFormat("(user_id,'{0:yyyyMMdd}','{0:HH:mm:ss}','','before',{1}),\r\n", rec.ReadingDate, rec.Glucose);
                }
                File.WriteAllText("export.txt", sb.ToString());


                Console.ReadKey();
                d.StopDevice();
                d.ClosePort();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: {0}", ex);
                Console.ReadKey();
            }
        }
    }
}