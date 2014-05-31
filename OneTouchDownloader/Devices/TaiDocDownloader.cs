using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace OneTouchDownloader.Devices
{
    internal class TaiDocDownloader
    {
        private SerialPort _port;

        public bool InitPort(int portNumber)
        {
            try
            {
                _port = new SerialPort("COM" + portNumber, 9600, Parity.None, 8, StopBits.One);
                _port.Open();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public void InitDevice()
        {
            Console.WriteLine("Sending initialization sequence...");
            SendCommandFrame(0x22, 0, 0, 0, 0);
            Console.WriteLine("Waiting for initiaiuzation response...");
            ReadCommandFrame();
            Console.WriteLine("Device initialized.");
        }


        public string ReadSerialNumber()
        {
            Console.WriteLine("Reading device serial number (1) ...");
            SendCommandFrame(0x27, 0, 0, 0, 0);
            Console.WriteLine("Waitinf for serial number answer from device (1) ...");
            var a = ReadCommandFrame();
            Console.WriteLine("Reading device serial number (2) ...");
            SendCommandFrame(0x28, 0, 0, 0, 0);
            Console.WriteLine("Waitinf for serial number answer from device (2) ...");
            var b = ReadCommandFrame();
            var sb = new StringBuilder();
            Array.Reverse(b);
            Array.Reverse(a);
            foreach (var x in b)
                sb.AppendFormat("{0:X}", x);
            foreach (var x in a)
                sb.AppendFormat("{0:X}", x);
            Console.WriteLine("Device serial number is {0}", sb);
            return sb.ToString();
        }

        public decimal ReadNrOfReadings()
        {
            Console.WriteLine("Reading number of records ...");
            SendCommandFrame(0x2B, 0, 0, 0, 0);
            Console.WriteLine("Waitinf for number of records answer from device ...");
            var a = ReadCommandFrame();
            var nr_recs = a[1]*256 + a[0];
            Console.WriteLine("Number of records is {0}", nr_recs);
            return nr_recs;
        }

        public Reading GetRecord(int n)
        {
            var high = (byte) (n/256);
            var low = (byte) (n%256);
            Console.WriteLine("Reading record {0} (1)...", n);
            SendCommandFrame(0x25, low, high, 0, 0);
            Console.WriteLine("Waiting for record (1)...");
            var a = ReadCommandFrame();
            Console.WriteLine("Reading record {0} (2)...", n);
            SendCommandFrame(0x26, low, high, 0, 0);
            Console.WriteLine("Waiting for record (2)...");
            var b = ReadCommandFrame();
            var h = a[3];
            var mi = a[2];
            var ymd = a[1]*256 + a[0];
            var d = ymd & 0x1F;
            var mo = (ymd & 0x1E0) >> 5;
            var y = ((ymd & 0xFE00) >> 9) + 2000;
            var recDate = new DateTime(y, mo, d, h, mi, 0);
            var glucose = b[1]*256 + b[0];
            var rec = new Reading {ReadingDate = recDate, Glucose = glucose};
            Console.WriteLine("Reading: {0}", rec);
            return rec;
        }

        public void StopDevice()
        {
            //throw new NotImplementedException();
        }

        public void ClosePort()
        {
            _port.Close();
            _port = null;
        }

        #region Internal methods

        private byte[] ReadCommandFrame()
        {
            var buff = new byte[8];
            _port.Read(buff, 0, 8);
            Console.WriteLine("We have read {0}", BitConverter.ToString(buff));
            if (buff[0] != 0x51 || buff[buff.Length - 2] != 0xA5)
            {
                Console.WriteLine("Invalid answer!");
                return null;
            }
            var crc = calculate_crc(buff.Take(7));
            if (crc == buff[7]) return buff.Skip(2).Take(4).ToArray();
            Console.WriteLine("Invalid CRC!");
            return null;
        }

        private void SendCommandFrame(params byte[] bytes)
        {
            var buff = new byte[bytes.Length + 3];
            buff[0] = 0x51;
            buff[buff.Length - 2] = 0xA3;
            bytes.CopyTo(buff, 1);
            buff[buff.Length - 1] = calculate_crc(buff);
            WriteToPort(buff);
        }

        private void WriteToPort(byte[] buffer)
        {
            Console.WriteLine("Writing bytes: {0}", BitConverter.ToString(buffer));
            _port.Write(buffer, 0, buffer.Length);
            Thread.Sleep(100);
        }

        private byte calculate_crc(IEnumerable<byte> buffer)
        {
            var crc = buffer.Sum(x => x);
            crc &= 0xFF;
            return (byte) crc;
        }

        #endregion
    }
}