using System;
using System.Collections;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace OneTouchDownloader.Devices
{
    public class OneTouchDownloader
    {
        private SerialPort _port;
        private bool _receiveParity;
        private bool _sendParity;

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
            SendCommandFrame(new byte[0], false, true);
            Console.WriteLine("Waiting for ACK...");
            ReadCommandFrame(true);
            Console.WriteLine("Device initialized.");
        }

        public string ReadSWVersion()
        {
            Console.WriteLine("Sending ReadSW command...");
            var toport = new byte[] {0x05, 0x0D, 0x02};
            SendCommandFrame(toport);
            Console.WriteLine("Waiting for ACK...");
            ReadCommandFrame(true);
            Console.WriteLine("Waiting for data...");
            var resp = ReadCommandFrame();
            Console.WriteLine("Sending ACK");
            SendCommandFrame(new byte[0], true);
            var version = Encoding.ASCII.GetString(resp.Skip(3).ToArray());
            Console.WriteLine("Got version info: {0}", version);

            return version;
        }

        public string ReadSerialNumber()
        {
            Console.WriteLine("Sending read-serial-number command...");
            var toport = new byte[] {0x05, 0x0B, 0x02, 0x00, 0x00, 0x00, 0x00, 0x84, 0x6A, 0xE8, 0x73, 0x00};
            SendCommandFrame(toport);
            Console.WriteLine("Waiting for ACK...");
            ReadCommandFrame(true);
            Console.WriteLine("Waiting for data...");
            var resp = ReadCommandFrame();
            Console.WriteLine("Sending ACK");
            SendCommandFrame(new byte[0], true);
            var serial = Encoding.ASCII.GetString(resp.Skip(2).ToArray());

            Console.WriteLine("Got serial number:{0}", serial);
            return serial;
        }

        //public string ReadSN()
        //{
        //}


        private byte[] ReadNthReading(int n)
        {
            Console.WriteLine("Sending read-value [{0}] command...", n);
            var high = (byte) (n/256);
            var low = (byte) (n%256);
            var toport = new byte[] {0x05, 0x1F, low, high};
            SendCommandFrame(toport);
            Console.WriteLine("Waiting for ACK...");
            ReadCommandFrame(true);
            Console.WriteLine("Waiting for data...");
            var resp = ReadCommandFrame();
            Console.WriteLine("Sending ACK");
            SendCommandFrame(new byte[0], true);
            return resp;
        }

        public int ReadNrOfReadings()
        {
            var resp = ReadNthReading(500);
            if (resp[1] != 0x0f)
            {
                Console.WriteLine("Invalid number of records");
                return 0;
            }
            var recs = resp[3]*256 + resp[2];
            Console.WriteLine("There should be {0} records in device", recs);
            return recs;
        }

        public Reading GetRecord(int rec_number)
        {
            var resp = ReadNthReading(rec_number);
            if (resp[1] != 0x06)
            {
                Console.WriteLine("Invalid number of records");
                return null;
            }
            var date = BitConverter.ToInt32(resp, 2);
            var glucose = BitConverter.ToInt32(resp, 6);
            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(date).ToLocalTime();
            var rec = new Reading {Glucose = glucose, ReadingDate = dtDateTime};
            Console.WriteLine("Read record {0} : {1}", rec_number, rec);
            return rec;
        }

        public void ClosePort()
        {
            _port.Close();
            _port = null;
        }

        public void StopDevice()
        {
        }

        #region Protocol methods

        private void SendCommandFrame(byte[] command, bool isAcknoledge = false, bool isDisconnect = false)
        {
            var result = new byte[command.Length + 6];
            result[0] = 2;
            result[1] = (byte) result.Length;
            result[result.Length - 3] = 3;
            command.CopyTo(result, 3);

            var lcb = new BitArray(8, false);
            lcb[3] = isDisconnect;
            lcb[2] = isAcknoledge;
            lcb[1] = _receiveParity;
            lcb[0] = _sendParity;

            lcb.CopyTo(result, 2);
            //result[2] = (byte)(((result[2] & 0xF0) >> 4) | ((result[2] & 0x0f) << 4));

            var crc = crc_calculate_crc(0xFFFF, result, result.Length - 2);
            result[result.Length - 2] = (byte) (crc & 0xFF);
            result[result.Length - 1] = (byte) ((crc & 0xFF00) >> 8);
            WriteToPort(result);
            if (command.Length > 0)
                _sendParity = !_sendParity;
        }

        private byte[] ReadCommandFrame(bool sholdBeAck = false)
        {
            var stx = (byte) _port.ReadByte();
            if (stx != 2)
                //throw new ProtocolException("Device answer is not in a correct format");
            {
                Console.WriteLine("Device answer is not in a correct format");
                return null;
            }
            var len = (byte) _port.ReadByte();
            var buffer = new byte[len];
            buffer[0] = stx;
            buffer[1] = len;
            for (var i = 2; i < len; i++)
                buffer[i] = (byte) _port.ReadByte();
            Console.WriteLine("Read from port: {0}", BitConverter.ToString(buffer));
            Thread.Sleep(100);
            var crc = crc_calculate_crc(0xFFFF, buffer, buffer.Length - 2);
            var source_crc = buffer[buffer.Length - 1]*256 + buffer[buffer.Length - 2];
            if (crc != source_crc)
                //throw new ProtocolException("Received CRC is not valid");
            {
                Console.WriteLine("Received CRC is not valid");
                return null;
            }

            var result = buffer.Skip(3).Take(buffer.Length - 6).ToArray();

            if (sholdBeAck)
            {
                if (result.Length > 0)
                    //throw new ProtocolException("Expected ACK package has data and it shouldn't");
                {
                    Console.WriteLine("Expected ACK package has data and it shouldn't");
                    return null;
                }
                if ((buffer[2] & 0x04) == 0)
                    //throw new ProtocolException("Expected ACK pachage has no ACK bit");
                {
                    Console.WriteLine("Expected ACK pachage has no ACK bit");
                    return null;
                }
            }

            if (result.Length > 0)
            {
                if (((buffer[2] & 0x02) != 0) != _receiveParity)
                    //throw new ProtocolException("Read parity is incorrect");
                    Console.WriteLine("Receive flag seems to be invalid");
                _receiveParity = !_receiveParity;
            }
            return result;
        }

        private void WriteToPort(byte[] buffer)
        {
            Console.WriteLine("Writing bytes: {0}", BitConverter.ToString(buffer));
            _port.Write(buffer, 0, buffer.Length);
            Thread.Sleep(100);
        }

        private ushort crc_calculate_crc(ushort initial_crc, byte[] buffer, int length)
        {
            ushort index = 0;
            var crc = initial_crc;

            if (buffer != null)
            {
                for (index = 0; index < length; index++)
                {
                    crc = (ushort) ((byte) (crc >> 8) | (ushort) (crc << 8));
                    crc ^= buffer[index];
                    crc ^= (ushort) ((crc & 0xff) >> 4);
                    crc ^= (ushort) ((ushort) (crc << 8) << 4);
                    crc ^= (ushort) ((ushort) ((crc & 0xff) << 4) << 1);
                }
            }

            return (crc);
        }

        #endregion
    }
}