using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace alpha_innotec_data_collector
{
  
    public partial class DataCollector : ServiceBase
    {
        bool doForEver = true;
        EventLog eventLog;
        Thread worker;
        public DataCollector()
        {
            InitializeComponent();
            eventLog = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists("HeatPumpDataCollectorService"))
            {
                System.Diagnostics.EventLog.CreateEventSource("HeatPumpDataCollectorService", "HeatPumpDataCollectorService");
            }
            eventLog.Source = "HeatPumpDataCollectorService";
            eventLog.Log = "HeatPumpDataCollectorService";
            worker = new Thread(new ThreadStart(StartClient));
        }

        protected override void OnStart(string[] args)
        {
            worker.Start(); 
        }

        protected override void OnStop()
        { 
            doForEver = false;
            worker.Join();
        }
        public void StartClient()
        {
            while (doForEver)
            {
                // Data buffer for incoming data.  
                byte[] bytes = new byte[1024];


                // Connect to a remote device.  
                try
                {
                    // Establish the remote endpoint for the socket.  
                    // This example uses port 11000 on the local computer.  
                    //IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                    IPAddress ipAddress = IPAddress.Parse("192.168.10.10");
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("192.168.10.100"), 8888);

                    // Create a TCP/IP  socket.  
                    Socket sender = new Socket(ipAddress.AddressFamily,
                        SocketType.Stream, ProtocolType.Tcp);

                    // Connect the socket to the remote endpoint. Catch any errors.  
                    try
                    {
                        sender.Connect(remoteEP);

                        //Console.WriteLine("Socket connected to {0}", sender.RemoteEndPoint.ToString());

                        // Encode the data string into a byte array.  
                        byte[] msg = new byte[8] { 0x00, 0x00, 0x0b, 0xbc, 0x00, 0x00, 0x00, 0x00 };

                        // Send the data through the socket.  
                        int bytesSent = sender.Send(msg);
                        //Console.WriteLine("{0} Sending {1} bytes", DateTime.Now.ToShortTimeString(), bytesSent);
                        //bytesSent = sender.Send(msg);

                        byte[] returnCommand = new byte[4];
                        byte[] returnStatus = new byte[4];
                        byte[] numCalulations = new byte[4];
                        // Receive the response from the remote device.  
                        sender.Receive(returnCommand, 4, SocketFlags.None);
                        sender.Receive(returnStatus, 4, SocketFlags.None);
                        sender.Receive(numCalulations, 4, SocketFlags.None);

                        int commandInt = Int32.Parse(ByteArrayToString(returnCommand), System.Globalization.NumberStyles.HexNumber);
                        int statusInt = Int32.Parse(ByteArrayToString(returnStatus), System.Globalization.NumberStyles.HexNumber);
                        int numCalculationsInt = Int32.Parse(ByteArrayToString(numCalulations), System.Globalization.NumberStyles.HexNumber);

                        //Console.WriteLine("\tReturned command: {0}, Status {1}, and {2} calculation", commandInt, statusInt, numCalculationsInt);

                        int[] rawCalculations = new int[numCalculationsInt];
                        float[] calculations = new float[numCalculationsInt];
                        //Console.WriteLine("\tReading {0} calulcation", numCalculationsInt);
                        for (int i = 0; i < rawCalculations.Length; i++)
                        {
                            byte[] calculation = new byte[4];
                            sender.Receive(calculation, 4, SocketFlags.None);
                            rawCalculations[i] = Int32.Parse(ByteArrayToString(calculation), System.Globalization.NumberStyles.HexNumber);
                            calculations[i] = rawCalculations[i] / 10f;
                        }
                        //Console.WriteLine("\tReading Complete...");

                        // Release the socket.  
                        sender.Shutdown(SocketShutdown.Both);
                        sender.Close();

                        WebClient webClient = new WebClient();
                        webClient.UseDefaultCredentials = true;
                        webClient.Credentials = new NetworkCredential("admin", "Jufa48te");
                        //Console.WriteLine("\tBuilding influx datapackage");
                        List<Tuple<int, string>> influxPackage = new List<Tuple<int, string>>();

                        influxPackage.Add(new Tuple<int, string>(rawCalculations[10], "flow"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[11], "return"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[12], "targetReturn"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[14], "hotGasTemp"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[15], "outsideTemp"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[17], "hotWaterActual"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[18], "hotWaterTarget"));

                        influxPackage.Add(new Tuple<int, string>(rawCalculations[80], "operatingStatus"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[117], "mainMenuStatus_Line1"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[118], "mainMenuStatus_Line2"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[119], "mainMenuStatus_Line3"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[120], "timeSince"));

                        influxPackage.Add(new Tuple<int, string>(rawCalculations[56], "compressorOpHours1"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[57], "compressorImpulses1"));

                        influxPackage.Add(new Tuple<int, string>(rawCalculations[63], "heatpumpOpHours"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[64], "heatingOpHours"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[65], "hotWaterOpHours"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[67], "heatPumpRunning"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[70], "powerOnDelay"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[71], "switchingLockOff"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[72], "switchingLockOn"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[73], "compressorServiceLife"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[74], "heatingControllerMoreTime"));
                        influxPackage.Add(new Tuple<int, string>(rawCalculations[75], "heatingControllerLessTime"));

                        influxPackage.Add(new Tuple<int, string>(rawCalculations[77], "lockHotWater"));

                        influxPackage.Add(new Tuple<int, string>(rawCalculations[141], "timeToDefrost"));



                        string dataPacket = MakeInfluxDatapackage(influxPackage);

                        //Console.WriteLine("\tSleeping 5 seconds");
                        Thread.Sleep(5000);
                        //Console.WriteLine("\tSending influx datapackage");
                        var respons = webClient.UploadString("http://fenris.lulz.no:8086/write?db=heatpump", dataPacket);
                        //Console.WriteLine("\tSending complete");

                    }
                    catch (ArgumentNullException ane)
                    {
                        eventLog.WriteEntry(String.Format("ArgumentNullException : {0}", ane.ToString()));
                    }
                    catch (SocketException se)
                    {
                        eventLog.WriteEntry(String.Format("SocketException : {0}", se.ToString()));
                    }
                    catch (Exception e)
                    {
                        eventLog.WriteEntry(String.Format("Unexpected exception : {0}", e.ToString()));
                    }

                }
                catch (Exception e)
                {
                    eventLog.WriteEntry(e.ToString());
                }
                Thread.Sleep(55000);
            }
        }
        public string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
        public byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
        private static string MakeInfluxDatapackage(List<Tuple<int, string>> packageDict)
        {
            string dataPacket = "";

            for (int i = 0; i < packageDict.Count; i++)
            {
                string line = packageDict[i].Item2 + ",pump=alpha value=" + packageDict[i].Item1;
                if (i != packageDict.Count - 1)
                {
                    line = line + "\n";
                }
                dataPacket = dataPacket + line;
            }

            return dataPacket;
        }

    }
}
