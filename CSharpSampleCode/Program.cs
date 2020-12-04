using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO.Ports;

namespace CSharpSampleCode
{
    class Program
    {
        static void Main(string[] args)
        {

#if DEBUG //For Debug only
            //args = new string[]{ "/l"};
            args = new string[] { "/c15" };
#endif
            SerialPort serialPort = new SerialPort();

            #region Arguments Decoding
            if (args.Length == 1 && args[0].IndexOf("/c") == 0)
            {
                try
                {
                    int portNum = int.Parse(args[0].Replace("/c", ""));
                    serialPort.PortName = "COM" + portNum;
                    serialPort.BaudRate = 9600;
                    serialPort.Open();
                }
                catch
                {
                    Console.WriteLine("Error when opening ComPort " + serialPort.PortName);
                    return;
                }
            }

            else if (args.Length == 1 && args[0] == "/l")
            {
                Console.WriteLine("* This computer have the following COM Ports: ");

                try
                {
                    foreach (string s in SerialPort.GetPortNames())
                    {
                        Console.WriteLine("* " + s);
                    }
                    return;
                }
                catch   // In case there is not even one serial port
                {
                    return;
                }
            }

            else
            {
                Console.WriteLine("*");
                if (args.Length != 1 || args[0] != "/h")
                {
                    Console.WriteLine("* ERROR: unrecognized or incomplete command line.");
                    Console.WriteLine("*");
                }
                Console.WriteLine("* NovAtel C# Sample Code");
                Console.WriteLine("* Usage: csharpsample [option]");
                Console.WriteLine("*      /c<port>       : COM Port #");
                Console.WriteLine("*      /l             : List COM Ports");
                Console.WriteLine("*      /h             : This help list");
                return;
            }
            #endregion

            #region Break Signal
            Console.WriteLine("Sending break signal to " + serialPort.PortName);
            // Call the break signal function, passing the serial port as a parameter
            sendBreakSignal(serialPort);
            Console.WriteLine("Break sent to " + serialPort.PortName);
            Console.WriteLine(serialPort.PortName + " must be at its default configuration now.");
            #endregion

            // Discard any data in the in and out buffers
            serialPort.DiscardInBuffer();
            serialPort.DiscardOutBuffer();
            Thread.Sleep(1000);

            #region BESTPOSB - Request and Decoding
            // Send a BestPosB log request
            serialPort.Write("\r\nLOG BESTPOSB ONCE\r\n");
            Thread.Sleep(1000);
            try
            {
                // Call the method that will swipe the serial port in search of a binary novatel message
                byte[] message = getNovAtelMessage(serialPort);

                // If a message was found, pass it to the decode method
                if (message != null)
                    decodeBinaryMessage(message);

                else
                    Console.WriteLine("Unable to retrieve message.");
            }
            catch (Exception ex)
            {
                if (ex is TimeoutException)
                    Console.WriteLine("Timeout to retrieve the message.");

                else
                {
                    Console.WriteLine("Unfortunately an " + ex.GetType().Name + " happened.");
                }
            }
            #endregion

            #region VERSIONB - Request and Decoding
            // Send a BestPosB log request
            serialPort.Write("\r\nLOG VERSIONB ONCE\r\n");
            Thread.Sleep(1000);
            try
            {
                // Call the method that will swipe the serial port in search of a binary novatel message
                byte[] message = getNovAtelMessage(serialPort);

                // If a message was found, pass it to the decode method
                if (message != null)
                    decodeBinaryMessage(message);

                else
                    Console.WriteLine("Unable to retrieve message.");
            }
            catch (Exception ex)
            {
                if (ex is TimeoutException)
                    Console.WriteLine("Timeout to retrieve the message.");

                else
                {
                    Console.WriteLine("Unfortunately an " + ex.GetType().Name + " happened.");
                }
            }
            #endregion
        }

        #region Methods

        /// <summary>
        /// The current COM port configuration can be reset to its default state at any time by sending it two hardware
        /// break signals of 250 milliseconds each, spaced by fifteen hundred milliseconds(1.5 seconds) with a pause of
        /// at least 250 milliseconds following the second break.
        /// </summary>
        /// <param name="sp">Serial Port to send the break to</param>
        public static void sendBreakSignal(SerialPort sp)
        {
            sp.BaudRate = 9600;
            int bytesPerSecond = sp.BaudRate / 8; // number or bytes sent in 1000 ms
            byte[] breakSignal = new byte[bytesPerSecond / 4]; // Create a byte array that will take 1/4 of a second (250 ms) to be sent

            // Populate the array
            for (int i = 0; i < breakSignal.Length; i++)
                breakSignal[i] = 0xFF;

            sp.Write(breakSignal, 0, breakSignal.Length); // Send the first hardware break signal
            Thread.Sleep(1500); // Wait 1500 milisseconds to send the next break signal
            sp.Write(breakSignal, 0, breakSignal.Length); // Send the second hardware break signal
            Thread.Sleep(250); // Wait 250 milisseconds after the second hardware break signal
        }

        /// <summary>
        /// Method that search for a NovAtel message in the serial port and return a byte[] or return a null object if the timeOut value is reached.
        /// Note that this method ignore any bytes that don't match with a NovAtel log. It will search straight for the binary message's sync bytes (0xAA, 0x44 and 0x12) 
        /// </summary>
        /// <param name="sp">Serial Port to read the message from</param>
        /// <param name="timeOut">Timeout in milisseconds. 10000 is the default</param>
        /// <returns></returns>
        public static byte[] getNovAtelMessage(SerialPort sp, int timeOut = 10000)
        {
            long timeOutLimit = DateTime.Now.Ticks + (TimeSpan.TicksPerMillisecond * timeOut);

            byte[] header = new byte[4] { 0x00, 0x00, 0x00, 0x00 }; // initially 3 bytes for the sync bytes + 1 for the header lenght

            sp.ReadTimeout = timeOut;
            bool readFirst = true;
            do
            {
                if (readFirst)
                    sp.Read(header, 0, 1); // Read the first byte

                if (header[0] == 0xAA) // Check the first sync byte
                {
                    sp.Read(header, 1, 1); // Read the second byte
                    if (header[1] == 0x44) // Check the second sync byte
                    {
                        sp.Read(header, 2, 1); // Read the third byte
                        if (header[2] == 0x12) // Check the third sync byte
                        {
                            // At this point we have the 3 sync bytes, so all that is needed is to load the rest of the message
                            sp.Read(header, 3, 1); // Read the header lenght in the byte of index 3

                            int headerLenght = header[3];

                            byte[] tmpBuffer = new byte[headerLenght];
                            header.CopyTo(tmpBuffer, 0); // Copy header to a temporary buffer
                            header = new byte[headerLenght];
                            tmpBuffer.CopyTo(header, 0); // Copy the header back into the header array after updating its size
                            tmpBuffer = null;

                            sp.Read(header, 4, headerLenght - 4); // Read the rest of the header

                            int messageLenght = BitConverter.ToUInt16(header, 8); // Convert the 8th and 9th bytes to a Ushort that is the body message

                            byte[] message = new byte[headerLenght + messageLenght]; // Create a buffer for the whole message

                            header.CopyTo(message, 0); // Copy the header to the message

                            sp.Read(message, headerLenght, messageLenght); // Read the whole message into the message buffer, using the headerLenght as offset

                            byte[] crc = new byte[4];
                            sp.Read(crc, 0, 4); // Create and populate the CRC byte array

                            ulong crc32 = BitConverter.ToUInt32(crc, 0);

                            if (crc32 == CalculateBlockCRC32(message)) //If the calculated CRC matches the received CRC, return the message
                                return message;
                            else
                                readFirst = true;
                        }
                        else if (header[2] == 0xAA) // If the byte found is 0xAA it will be used in the next loop
                            readFirst = false;
                        else
                            readFirst = true;
                    }
                    else if (header[1] == 0xAA) // If the byte found is 0xAA it will be used in the next loop
                        readFirst = false;
                    else
                        readFirst = true;
                }
                else
                    readFirst = true;
#if DEBUG
            } while (true);
#else
            } while (timeOutLimit > DateTime.Now.Ticks);
#endif
            return null;
        }

        #region Crc Calculations
        /// <summary>
        /// Calculate a CRC value to be used by CRC calculation functions.
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public static ulong CRC32Value(int i)
        {
            const ulong CRC32_POLYNOMIAL = 0xEDB88320L;
            int j;
            ulong ulCRC;
            ulCRC = (ulong)i;
            for (j = 8; j > 0; j--)
            {
                if ((ulCRC & 1) == 1)
                    ulCRC = (ulCRC >> 1) ^ CRC32_POLYNOMIAL;
                else
                    ulCRC >>= 1;
            }
            return ulCRC;
        }

        /// <summary>
        /// Calculates the CRC-32 of a block of data all at once
        /// </summary>
        /// <param name="buffer">byte[] to calculate the CRC-32</param>
        /// <returns></returns>
        public static ulong CalculateBlockCRC32(byte[] buffer)
        {
            ulong ulTemp1;
            ulong ulTemp2;
            ulong ulCRC = 0;

            for (int i = 0; i < buffer.Length; i++)
            {
                ulTemp1 = (ulCRC >> 8) & 0x00FFFFFFL;
                ulTemp2 = CRC32Value(((int)ulCRC ^ buffer[i]) & 0xff);
                ulCRC = ulTemp1 ^ ulTemp2;
            }
            return (ulCRC);
        }
        #endregion

        /// <summary>
        /// Decode a Binary message and output some data based on the Message ID
        /// </summary>
        /// <param name="message"></param>
        public static void decodeBinaryMessage(byte[] message)
        {
            int h = message[3]; //header size, for offset
            int logType = BitConverter.ToUInt16(message, 4);

            // Decode the log based on its type
            switch ((BINARY_LOG_TYPE)logType)
            {
                case BINARY_LOG_TYPE.BESTPOSB_LOG_TYPE:
                    Console.WriteLine("**** BESTPOS LOG DECODED:");
                    Console.WriteLine("* Message ID     : " + logType);

                    int solnStatus = BitConverter.ToInt32(message, h);
                    Console.WriteLine("* Solution Status: " + GetSolnStatusString(solnStatus));

                    int posType = BitConverter.ToInt32(message, h + 4);
                    Console.WriteLine("* Position Type  : " + GetPosTypeString(posType));

                    double lat = BitConverter.ToDouble(message, h + 8);
                    Console.WriteLine("* Latitude       : " + lat.ToString("F10"));

                    double lon = BitConverter.ToDouble(message, h + 16);
                    Console.WriteLine("* Longitude      : " + lon.ToString("F10"));

                    double hgt = BitConverter.ToDouble(message, h + 24);
                    Console.WriteLine("* Height         : " + hgt.ToString("F10"));

                    float undulation = BitConverter.ToSingle(message, h + 32);
                    Console.WriteLine("* Undulation     : " + undulation.ToString("F4"));

                    int datum = BitConverter.ToInt32(message, h + 36);
                    Console.WriteLine("* Datum          : " + GetDatumString(datum));

                    float latStdDev = BitConverter.ToSingle(message, h + 40);
                    Console.WriteLine("* Lat Std Dev    : " + latStdDev.ToString("F4"));

                    float lonStdDev = BitConverter.ToSingle(message, h + 44);
                    Console.WriteLine("* Lon Std Dev    : " + lonStdDev.ToString("F4"));

                    float hgtStdDev = BitConverter.ToSingle(message, h + 48);
                    Console.WriteLine("* Height Std Dev : " + hgtStdDev.ToString("F4"));


                    string baseId = new string(Encoding.ASCII.GetChars(message, h + 52, 4));
                    if (baseId.Contains('\0'))
                        baseId = baseId.Substring(0, baseId.IndexOf('\0'));
                    Console.WriteLine("* Base Station ID: " + baseId);

                    float diffAge = BitConverter.ToSingle(message, h + 56);
                    Console.WriteLine("* Diff Age       : " + diffAge.ToString("F2"));

                    float solAge = BitConverter.ToSingle(message, h + 60);
                    Console.WriteLine("* Solution Age   : " + solAge.ToString("F2"));

                    int satsTracked = message[h + 64];
                    Console.WriteLine("* Sat Tracked    : " + satsTracked);

                    int satSolution = message[h + 65];
                    Console.WriteLine("* Sat Used       : " + satSolution);

                    int satsL1E1B1 = message[h + 66];
                    Console.WriteLine("* Sat L1E1B1 Used: " + satsL1E1B1);

                    int satsMulti = message[h + 67];
                    Console.WriteLine("* Sat MultiF Used: " + satsMulti);

                    byte extSolnStatus = message[h + 69];
                    if (posType == (int)POSTYPE.SINGLE) //if the receiver have a PDP solution
                        if (IsFlagActive(extSolnStatus, EXTENDED_SOLN_STATUS.Glide))
                            Console.WriteLine("* Single Solution: GLIDE");
                    Console.WriteLine("* Klobuchar Model: " + IsFlagActive(extSolnStatus, EXTENDED_SOLN_STATUS.IonoCorrKlobuchar).ToString());
                    Console.WriteLine("* SBAS Broadcast : " + IsFlagActive(extSolnStatus, EXTENDED_SOLN_STATUS.IonoCorrSBAS).ToString());
                    Console.WriteLine("* Multi-Freq Comp: " + IsFlagActive(extSolnStatus, EXTENDED_SOLN_STATUS.IonoCorrMultiFreq).ToString());
                    Console.WriteLine("* PSRDiff Correct: " + IsFlagActive(extSolnStatus, EXTENDED_SOLN_STATUS.IonoCorrPSRDIFF).ToString());
                    Console.WriteLine("* NovAtel Iono   : " + IsFlagActive(extSolnStatus, EXTENDED_SOLN_STATUS.IonoCorrNovatelIono).ToString());
                    Console.WriteLine("* Antenna Warning: " + IsFlagActive(extSolnStatus, EXTENDED_SOLN_STATUS.AntennaWarning).ToString());

                    byte maskGalBei = message[h + 70];
                    Console.WriteLine("* Galileo E1     : " + IsFlagActive(maskGalBei, SIGNALS_USED_MASK.E1_Used).ToString());
                    Console.WriteLine("* BeiDou B1      : " + IsFlagActive(maskGalBei, SIGNALS_USED_MASK.BEIDOU_B1_Used).ToString());
                    Console.WriteLine("* BeiDou B2      : " + IsFlagActive(maskGalBei, SIGNALS_USED_MASK.BEIDOU_B2_Used).ToString());

                    byte maskGpsGlo = message[h + 71];
                    Console.WriteLine("* GPS L1         : " + IsFlagActive(maskGpsGlo, SIGNALS_USED_MASK.GPS_L1_Used).ToString());
                    Console.WriteLine("* GPS L2         : " + IsFlagActive(maskGpsGlo, SIGNALS_USED_MASK.GPS_L2_Used).ToString());
                    Console.WriteLine("* GPS L5         : " + IsFlagActive(maskGpsGlo, SIGNALS_USED_MASK.GPS_L5_Used).ToString());
                    Console.WriteLine("* Glonass L1     : " + IsFlagActive(maskGpsGlo, SIGNALS_USED_MASK.GLO_L1_Used).ToString());
                    Console.WriteLine("* Glonass L2     : " + IsFlagActive(maskGpsGlo, SIGNALS_USED_MASK.GLO_L2_Used).ToString());
                    Console.WriteLine();
                    break;

                case BINARY_LOG_TYPE.VERB_LOG_TYPE:
                    Console.WriteLine("**** VERSION LOG DECODED:");
                    Console.WriteLine("* Message ID     : " + logType);

                    int numberComp = BitConverter.ToInt32(message, h);
                    Console.WriteLine("* # of Components: " + numberComp);

                    for (int i = 0; i < numberComp; i++)
                    {
                        int offset = h + (i * 108);

                        int compType = BitConverter.ToInt32(message, offset + 4);
                        Console.WriteLine("* Component Type : " + GetCompTypeString(compType));


                        string model = new string(Encoding.ASCII.GetChars(message, offset + 8, 16));
                        if (model.Contains('\0'))
                            model = model.Substring(0, model.IndexOf('\0'));
                        Console.WriteLine("   * Comp Model  : " + model);

                        string psn = new string(Encoding.ASCII.GetChars(message, offset + 24, 16));
                        if (psn.Contains('\0'))
                            psn = psn.Substring(0, psn.IndexOf('\0'));
                        Console.WriteLine("   * Serial #    : " + psn);

                        string hwVersion = new string(Encoding.ASCII.GetChars(message, offset + 40, 16));
                        if (hwVersion.Contains('\0'))
                            hwVersion = hwVersion.Substring(0, hwVersion.IndexOf('\0'));
                        Console.WriteLine("   * Hw Version  : " + hwVersion);

                        string swVersion = new string(Encoding.ASCII.GetChars(message, offset + 56, 16));
                        if (swVersion.Contains('\0'))
                            swVersion = swVersion.Substring(0, swVersion.IndexOf('\0'));
                        Console.WriteLine("   * Sw Version  : " + swVersion);

                        string bootVersion = new string(Encoding.ASCII.GetChars(message, offset + 72, 16));
                        if (bootVersion.Contains('\0'))
                            bootVersion = bootVersion.Substring(0, bootVersion.IndexOf('\0'));
                        Console.WriteLine("   * Boot Version: " + bootVersion);

                        string compDate = new string(Encoding.ASCII.GetChars(message, offset + 88, 12));
                        if (compDate.Contains('\0'))
                            compDate = compDate.Substring(0, compDate.IndexOf('\0'));
                        Console.WriteLine("   * Fw Comp Date: " + compDate);

                        string compTime = new string(Encoding.ASCII.GetChars(message, offset + 100, 12));
                        if (compTime.Contains('\0'))
                            compTime = compTime.Substring(0, compTime.IndexOf('\0'));
                        Console.WriteLine("   * Fw Comp Time: " + compTime);
                    }
                    break;
            }

        }

        public enum BINARY_LOG_TYPE
        {
            VERB_LOG_TYPE = 37,
            BESTPOSB_LOG_TYPE = 42,
        }

        public static bool IsFlagActive(byte b, object e)
        {
            int flag = (int)e;
            return ((b & flag) == flag);
        }

        #region Solution Status
        public enum SOLN_STATUS
        {
            SOLN_STATUS_NOT_SET = -2,
            SOLN_STATUS_MIN = -1,
            SOLN_STATUS_SOLUTION_COMPUTED = 0,
            SOLN_STATUS_INSUFFICIENT_OBS = 1,
            SOLN_STATUS_NO_CONVERGENCE = 2,
            SOLN_STATUS_SINGULAR_AtPA_MATRIX = 3,
            SOLN_STATUS_BIG_COVARIANCE_TRACE = 4,
            SOLN_STATUS_BIG_TEST_DISTANCE = 5,
            SOLN_STATUS_COLD_START = 6,
            SOLN_STATUS_SPEED_OR_HEIGHT_LIMITS_EXCEEDED = 7,
            SOLN_STATUS_VARIANCE_EXCEEDS_LIMIT = 8,
            SOLN_STATUS_RESIDUALS_ARE_TOO_LARGE = 9,
            SOLN_STATUS_DELTA_POSITION_IS_TOO_LARGE = 10,
            SOLN_STATUS_NEGATIVE_VARIANCE = 11,
            SOLN_STATUS_OLD_SOLUTION = 12,
            SOLN_STATUS_INTEGRITY_WARNING = 13,
            SOLN_STATUS_INS_INACTIVE = 14,
            SOLN_STATUS_INS_ALIGNING = 15,
            SOLN_STATUS_INS_BAD = 16,
            SOLN_STATUS_IMU_UNPLUGGED = 17,
            SOLN_STATUS_PENDING = 18,
            SOLN_STATUS_INVALID_FIX = 19,
            SOLN_STATUS_UNAUTHORIZED = 20,
            SOLN_STATUS_ANTENNA_WARNING = 21,
            SOLN_STATUS_INVALID_RATE = 22,

            SOLN_STATUS_MAX
        };

        public static string GetSolnStatusString(int solnStatus)
        {
            switch ((SOLN_STATUS)solnStatus)
            {
                case SOLN_STATUS.SOLN_STATUS_NOT_SET:
                    return "Solution status not set";
                case SOLN_STATUS.SOLN_STATUS_SOLUTION_COMPUTED:
                    return "Solution computed";
                case SOLN_STATUS.SOLN_STATUS_INSUFFICIENT_OBS:
                    return "Insufficient observations";
                case SOLN_STATUS.SOLN_STATUS_NO_CONVERGENCE:
                    return "No convergence";
                case SOLN_STATUS.SOLN_STATUS_SINGULAR_AtPA_MATRIX:
                    return "Singular AtPA matrix";
                case SOLN_STATUS.SOLN_STATUS_BIG_COVARIANCE_TRACE:
                    return "Covariance trace exceeds maximum (trace > 1000 m)";
                case SOLN_STATUS.SOLN_STATUS_BIG_TEST_DISTANCE:
                    return "Test distance exceeded (maximum of 3 rejections if distance > 10 km)";
                case SOLN_STATUS.SOLN_STATUS_COLD_START:
                    return "Converging from cold start";
                case SOLN_STATUS.SOLN_STATUS_SPEED_OR_HEIGHT_LIMITS_EXCEEDED:
                    return "CoCom limits exceeded";
                case SOLN_STATUS.SOLN_STATUS_VARIANCE_EXCEEDS_LIMIT:
                    return "Variance exceeds limits";
                case SOLN_STATUS.SOLN_STATUS_RESIDUALS_ARE_TOO_LARGE:
                    return "Residuals are too large";
                case SOLN_STATUS.SOLN_STATUS_DELTA_POSITION_IS_TOO_LARGE:
                    return "Delta position is too large";
                case SOLN_STATUS.SOLN_STATUS_NEGATIVE_VARIANCE:
                    return "Negative variance";
                case SOLN_STATUS.SOLN_STATUS_OLD_SOLUTION:
                    return "The position is old";
                case SOLN_STATUS.SOLN_STATUS_INTEGRITY_WARNING:
                    return "Integrity warning";
                case SOLN_STATUS.SOLN_STATUS_INS_INACTIVE:
                    return "INS has not started yet";
                case SOLN_STATUS.SOLN_STATUS_INS_ALIGNING:
                    return "INS doing its coarse alignment";
                case SOLN_STATUS.SOLN_STATUS_INS_BAD:
                    return "INS position is bad";
                case SOLN_STATUS.SOLN_STATUS_IMU_UNPLUGGED:
                    return "No IMU detected";
                case SOLN_STATUS.SOLN_STATUS_PENDING:
                    return "Not enough satellites to verify FIX POSITION";
                case SOLN_STATUS.SOLN_STATUS_INVALID_FIX:
                    return "Fixed position is not valid";
                case SOLN_STATUS.SOLN_STATUS_UNAUTHORIZED:
                    return "Position type (HP or XP) not authorized";
                case SOLN_STATUS.SOLN_STATUS_ANTENNA_WARNING:
                    return "Selected RTK antenna mode not possible";
                case SOLN_STATUS.SOLN_STATUS_INVALID_RATE:
                    return "Logging rate not supported for this solution type";
            }
            return "Unknown solution status";
        }
        #endregion
        #region Position Type
        public enum POSTYPE
        {
            POSTYPE_NOTSET = -1,
            NONE = 0,
            FIXEDPOS = 1,
            FIXEDHEIGHT = 2,
            FIXEDVEL = 3,
            DOPPLER_VELOCITY = 8,
            SINGLE = 16,
            PSRDIFF = 17,
            WAAS = 18,
            PROPAGATED = 19,
            OMNISTAR = 20,
            L1_FLOAT = 32,
            IONOFREE_FLOAT = 33,
            NARROW_FLOAT = 34,
            L1_INT = 48,
            WIDE_INT = 49,
            NARROW_INT = 50,
            RTK_DIRECT_INS = 51,
            INS = 52,
            INS_PSRSP = 53,
            INS_PSRDIFF = 54,
            INS_RTKFLOAT = 55,
            INS_RTKFIXED = 56,
            INS_OMNISTAR = 57,
            INS_OMNISTAR_HP = 58,
            INS_OMNISTAR_XP = 59,
            OMNISTAR_HP = 64,
            OMNISTAR_XP = 65,
            CDGPS = 66,
            EXT_CONSTRAINED = 67,
            PPP_CONVERGING = 68,
            PPP = 69,
            OPERATIONAL = 70,
            WARNING = 71,
            OUT_OF_BOUNDS = 72,
            INS_PPP_CONVERGING = 73,
            INS_PPP = 74,
            PPP_BASIC_CONVERGING = 77,
            PPP_BASIC = 78,
            INS_PPP_BASIC_CONVERGING = 79,
            INS_PPP_BASIC = 80,
            MAX_POSTYPE
        }

        public static string GetPosTypeString(int posType)
        {
            string cpRetValue = "UNKNOWN";

            switch ((POSTYPE)posType)
            {
                case POSTYPE.POSTYPE_NOTSET: cpRetValue = "NOTSET"; break;
                case POSTYPE.NONE: cpRetValue = "NONE"; break;
                case POSTYPE.FIXEDPOS: cpRetValue = "FIXEDPOS"; break;
                case POSTYPE.FIXEDHEIGHT: cpRetValue = "FIXEDHEIGHT"; break;
                case POSTYPE.FIXEDVEL: cpRetValue = "FIXEDVEL"; break;
                case POSTYPE.DOPPLER_VELOCITY: cpRetValue = "DOPPLER_VELOCITY"; break;
                case POSTYPE.SINGLE: cpRetValue = "SINGLE"; break;
                case POSTYPE.PSRDIFF: cpRetValue = "PSRDIFF"; break;
                case POSTYPE.WAAS: cpRetValue = "WAAS"; break;
                case POSTYPE.PROPAGATED: cpRetValue = "PROPAGATED"; break;
                case POSTYPE.OMNISTAR: cpRetValue = "OMNISTAR"; break;
                case POSTYPE.L1_FLOAT: cpRetValue = "L1_FLOAT"; break;
                case POSTYPE.IONOFREE_FLOAT: cpRetValue = "IONOFREE_FLOAT"; break;
                case POSTYPE.NARROW_FLOAT: cpRetValue = "NARROW_FLOAT"; break;
                case POSTYPE.L1_INT: cpRetValue = "L1_INT"; break;
                case POSTYPE.WIDE_INT: cpRetValue = "WIDE_INT"; break;
                case POSTYPE.NARROW_INT: cpRetValue = "NARROW_INT"; break;
                case POSTYPE.RTK_DIRECT_INS: cpRetValue = "RTK_DIRECT_INS"; break;
                case POSTYPE.INS: cpRetValue = "INS"; break;
                case POSTYPE.INS_PSRSP: cpRetValue = "INS_PSRSP"; break;
                case POSTYPE.INS_PSRDIFF: cpRetValue = "INS_PSRDIFF"; break;
                case POSTYPE.INS_RTKFLOAT: cpRetValue = "INS_RTKFLOAT"; break;
                case POSTYPE.INS_OMNISTAR: cpRetValue = "INS_OMNISTAR"; break;
                case POSTYPE.INS_OMNISTAR_XP: cpRetValue = "INS_OMNISTAR_XP"; break;
                case POSTYPE.INS_OMNISTAR_HP: cpRetValue = "INS_OMNISTAR_HP"; break;
                case POSTYPE.INS_RTKFIXED: cpRetValue = "INS_RTKFIXED"; break;
                case POSTYPE.OMNISTAR_HP: cpRetValue = "OMNISTAR_HP"; break;
                case POSTYPE.OMNISTAR_XP: cpRetValue = "OMNISTAR_XP"; break;
                case POSTYPE.CDGPS: cpRetValue = "CDGPS"; break;
                case POSTYPE.EXT_CONSTRAINED: cpRetValue = "EXT_CONSTRAINED"; break;
                case POSTYPE.PPP_CONVERGING: cpRetValue = "PPP_CONVERGING"; break;
                case POSTYPE.PPP: cpRetValue = "PPP"; break;
                case POSTYPE.OPERATIONAL: cpRetValue = "OPERATIONAL"; break;
                case POSTYPE.WARNING: cpRetValue = "WARNING"; break;
                case POSTYPE.OUT_OF_BOUNDS: cpRetValue = "OUT_OF_BOUNDS"; break;
                case POSTYPE.INS_PPP_CONVERGING: cpRetValue = "INS_PPP_CONVERGING"; break;
                case POSTYPE.INS_PPP: cpRetValue = "INS_PPP"; break;
                case POSTYPE.PPP_BASIC: cpRetValue = "PPP_BASIC"; break;
                case POSTYPE.PPP_BASIC_CONVERGING: cpRetValue = "PPP_BASIC_CONVERGING"; break;
                case POSTYPE.INS_PPP_BASIC: cpRetValue = "INS_PPP_BASIC"; break;
                case POSTYPE.INS_PPP_BASIC_CONVERGING: cpRetValue = "INS_PPP_BASIC_CONVERGING"; break;
                default:
                    break;   // Absolutely impossible to reach here
            }

            return cpRetValue;
        }
        #endregion
        #region Datum
        public enum DATUM_ID
        {
            UNKNOWN_DATUM = -1,
            ADIND = 1,
            ARC50,
            ARC60,
            AGD66,
            AGD84,
            BUKIT,
            ASTRO,
            CHATM,
            CARTH,
            CAPE,
            DJAKA,
            EGYPT,
            ED50,
            ED79,
            GUNSG,
            GEO49,
            GRB36,
            GUAM,
            HAWAII,
            KAUAI,
            MAUI,
            OAHU,
            HERAT,
            HJORS,
            HONGK,
            HUTZU,
            INDIA,
            IRE65,
            KERTA,
            KANDA,
            LIBER,
            LUZON,
            MINDA,
            MERCH,
            NAHR,
            NAD83,
            CANADA,
            ALASKA,
            NAD27,
            CARIBB,
            MEXICO,
            CAMER,
            MINNA,
            OMAN,
            PUERTO,
            QORNO,
            ROME,
            CHUA,
            SAM56,
            SAM69,
            CAMPO,
            SACOR,
            YACAR,
            TANAN,
            TIMBA,
            TOKYO,
            TRIST,
            VITI,
            WAK60,
            WGS72,
            WGS84,
            ZANDE,
            USER,
            CSRS,
            ADIM,
            ARSM,
            ENW,
            HTN,
            INDB,
            INDI,
            IRL,
            LUZA,
            LUZB,
            NAHC,
            NASP,
            OGMB,
            OHAA,
            OHAB,
            OHAC,
            OHAD,
            OHIA,
            OHIB,
            OHIC,
            OHID,
            TIL,
            TOYM
        };

        public static string GetDatumString(int datum)
        {
            switch ((DATUM_ID)datum)
            {
                case DATUM_ID.UNKNOWN_DATUM:
                    return "UNKNOWN_DATUM";
                case DATUM_ID.ADIND:
                    return "ADIND";
                case DATUM_ID.ARC50:
                    return "ARC50";
                case DATUM_ID.ARC60:
                    return "ARC60";
                case DATUM_ID.AGD66:
                    return "AGD66";
                case DATUM_ID.AGD84:
                    return "AGD84";
                case DATUM_ID.BUKIT:
                    return "BUKIT";
                case DATUM_ID.ASTRO:
                    return "ASTRO";
                case DATUM_ID.CHATM:
                    return "CHATM";
                case DATUM_ID.CARTH:
                    return "CARTH";
                case DATUM_ID.CAPE:
                    return "CAPE";
                case DATUM_ID.DJAKA:
                    return "DJAKA";
                case DATUM_ID.EGYPT:
                    return "EGYPT";
                case DATUM_ID.ED50:
                    return "ED50";
                case DATUM_ID.ED79:
                    return "ED79";
                case DATUM_ID.GUNSG:
                    return "GUNSG";
                case DATUM_ID.GEO49:
                    return "GEO49";
                case DATUM_ID.GRB36:
                    return "GRB36";
                case DATUM_ID.GUAM:
                    return "GUAM";
                case DATUM_ID.HAWAII:
                    return "HAWAII";
                case DATUM_ID.KAUAI:
                    return "KAUAI";
                case DATUM_ID.MAUI:
                    return "MAUI";
                case DATUM_ID.OAHU:
                    return "OAHU";
                case DATUM_ID.HERAT:
                    return "HERAT";
                case DATUM_ID.HJORS:
                    return "HJORS";
                case DATUM_ID.HONGK:
                    return "HONGK";
                case DATUM_ID.HUTZU:
                    return "HUTZU";
                case DATUM_ID.INDIA:
                    return "INDIA";
                case DATUM_ID.IRE65:
                    return "IRE65";
                case DATUM_ID.KERTA:
                    return "KERTA";
                case DATUM_ID.KANDA:
                    return "KANDA";
                case DATUM_ID.LIBER:
                    return "LIBER";
                case DATUM_ID.LUZON:
                    return "LUZON";
                case DATUM_ID.MINDA:
                    return "MINDA";
                case DATUM_ID.MERCH:
                    return "MERCH";
                case DATUM_ID.NAHR:
                    return "NAHR";
                case DATUM_ID.NAD83:
                    return "NAD83";
                case DATUM_ID.CANADA:
                    return "CANADA";
                case DATUM_ID.ALASKA:
                    return "ALASKA";
                case DATUM_ID.NAD27:
                    return "NAD27";
                case DATUM_ID.CARIBB:
                    return "CARIBB";
                case DATUM_ID.MEXICO:
                    return "MEXICO";
                case DATUM_ID.CAMER:
                    return "CAMER";
                case DATUM_ID.MINNA:
                    return "MINNA";
                case DATUM_ID.OMAN:
                    return "OMAN";
                case DATUM_ID.PUERTO:
                    return "PUERTO";
                case DATUM_ID.QORNO:
                    return "QORNO";
                case DATUM_ID.ROME:
                    return "ROME";
                case DATUM_ID.CHUA:
                    return "CHUA";
                case DATUM_ID.SAM56:
                    return "SAM56";
                case DATUM_ID.SAM69:
                    return "SAM69";
                case DATUM_ID.CAMPO:
                    return "CAMPO";
                case DATUM_ID.SACOR:
                    return "SACOR";
                case DATUM_ID.YACAR:
                    return "YACAR";
                case DATUM_ID.TANAN:
                    return "TANAN";
                case DATUM_ID.TIMBA:
                    return "TIMBA";
                case DATUM_ID.TOKYO:
                    return "TOKYO";
                case DATUM_ID.TRIST:
                    return "TRIST";
                case DATUM_ID.VITI:
                    return "VITI";
                case DATUM_ID.WAK60:
                    return "WAK60";
                case DATUM_ID.WGS72:
                    return "WGS72";
                case DATUM_ID.WGS84:
                    return "WGS84";
                case DATUM_ID.ZANDE:
                    return "ZANDE";
                case DATUM_ID.USER:
                    return "USER";
                case DATUM_ID.CSRS:
                    return "CSRS";
                case DATUM_ID.ADIM:
                    return "ADIM";
                case DATUM_ID.ARSM:
                    return "ARSM";
                case DATUM_ID.ENW:
                    return "ENW";
                case DATUM_ID.HTN:
                    return "HTN";
                case DATUM_ID.INDB:
                    return "INDB";
                case DATUM_ID.INDI:
                    return "INDI";
                case DATUM_ID.IRL:
                    return "IRL";
                case DATUM_ID.LUZA:
                    return "LUZA";
                case DATUM_ID.LUZB:
                    return "LUZB";
                case DATUM_ID.NAHC:
                    return "NAHC";
                case DATUM_ID.NASP:
                    return "NASP";
                case DATUM_ID.OGMB:
                    return "OGMB";
                case DATUM_ID.OHAA:
                    return "OHAA";
                case DATUM_ID.OHAB:
                    return "OHAB";
                case DATUM_ID.OHAC:
                    return "OHAC";
                case DATUM_ID.OHAD:
                    return "OHAD";
                case DATUM_ID.OHIA:
                    return "OHIA";
                case DATUM_ID.OHIB:
                    return "OHIB";
                case DATUM_ID.OHIC:
                    return "OHIC";
                case DATUM_ID.OHID:
                    return "OHID";
                case DATUM_ID.TIL:
                    return "TIL";
                case DATUM_ID.TOYM:
                    return "TOYM";
            }
            return "UNKNOWN DATUM";
        }
        #endregion
        #region Extended Solution Status
        public enum EXTENDED_SOLN_STATUS
        {
            Glide = 0x01,

            IonoCorrKlobuchar = 0x02, // These bits are considered as part of the IonoCorrMask
            IonoCorrSBAS = 0x04, // These bits are considered as part of the IonoCorrMask
            IonoCorrMultiFreq = 0x06, // These bits are considered as part of the IonoCorrMask
            IonoCorrPSRDIFF = 0x08, // These bits are considered as part of the IonoCorrMask
            IonoCorrNovatelIono = 0x0A, // These bits are considered as part of the IonoCorrMask

            AntennaWarning = 0x20,
        };
        #endregion
        #region Signals Mask
        public enum SIGNALS_USED_MASK
        {
            GPS_L1_Used = 0x01,  // L1
            GPS_L2_Used = 0x02,  // L2
            GPS_L5_Used = 0x04,  // L5
            GLO_L1_Used = 0x10,  // G1
            GLO_L2_Used = 0x20,  // G2

            E1_Used = 0x0001,  // Galileo E1

            BEIDOU_B1_Used = 0x10,  // Beidou B1
            BEIDOU_B2_Used = 0x20,  // Beidou B2
        };
        #endregion
        #region Component Types
        enum COMPONENT_TYPE
        {
            UNKNOWN = 0,                 // Unknown component
            GPSCARD = 1,                 // GPSCard
            CONTROLLER = 2,                 // Reserved
            ENCLOSURE = 3,                 // OEM card enclosure
            USERINFO = 8,                 // App specific Information
            DB_HEIGHTMODEL = (0x3A7A0000 | 0),  //Height/track model data
            DB_USERAPP = (0x3A7A0000 | 1),  // User application firmware
            DB_USERAPPAUTO = (0x3A7A0000 | 5),  // Auto-starting user application firmware
            OEM6FPGA = 12,                // OEM638 FPGA version
            GPSCARD2 = 13,                // Second card in a ProPak6
            BLUETOOTH = 14,                // Bluetooth component in a ProPak6
            WIFI = 15,                // Wifi component in a ProPak6
            CELLULAR = 16,                // Cellular component in a ProPak6
        };

        public static string GetCompTypeString(int compType)
        {
            switch ((COMPONENT_TYPE)compType)
            {
                case COMPONENT_TYPE.GPSCARD: return "OEM family component";
                case COMPONENT_TYPE.CONTROLLER: return "Reserved";
                case COMPONENT_TYPE.ENCLOSURE: return "OEM card enclosure";
                case COMPONENT_TYPE.USERINFO: return "Application specific information";
                case COMPONENT_TYPE.DB_HEIGHTMODEL: return "Height/track model data";
                case COMPONENT_TYPE.DB_USERAPP: return "User application firmware";
                case COMPONENT_TYPE.DB_USERAPPAUTO: return "Auto-starting user application firmware";
                case COMPONENT_TYPE.OEM6FPGA: return "OEM638 FPGA version";
                case COMPONENT_TYPE.GPSCARD2: return "Second card in a ProPak6";
                case COMPONENT_TYPE.BLUETOOTH: return "Bluetooth component in a ProPak6";
                case COMPONENT_TYPE.WIFI: return "Wi-Fi component in a ProPak6";
                case COMPONENT_TYPE.CELLULAR: return "Cellular component in a ProPak6";
                default:
                    return "Unknown component";
            }
        }
        #endregion
        #endregion

    }
}
