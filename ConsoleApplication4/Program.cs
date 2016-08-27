using System;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;
using System.Text;

namespace ConsoleApplication4
{

    namespace port_listen
    {
        class MainClass
        {
            const int BUFFER_SIZE = 4096;

            const string OPEN = "[open]";
            const string EXIT = "[exit]";
            const string PUSH = "[push]";
            const string LIST = "[list]";
            const string PULL = "[pull]";
            const string ACK = "[ack]";

            static readonly string RecieveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "outputDir");

            static void Main(string[] args)
            {
                TcpListener listener = new TcpListener(IPAddress.Any, 5555);
                TcpClient client;
                
                Console.WriteLine("server started on the port 5555");
                listener.Start();

                while (true) 
                {
                    client = listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(ThreadProc, client);
                }
            }
            private static void ThreadProc(object obj)
            {
                var client = (TcpClient)obj;
                Random rnd = new Random();
                StreamReader sReader = new StreamReader(client.GetStream());
                StreamWriter sWriter = new StreamWriter(client.GetStream());

                Console.WriteLine("client accepted on the thread "+Thread.CurrentThread.ManagedThreadId.ToString());
                
                String data = "";
                //string result = "";
                //bool IsFileTransferStarted = false;
                //string courantFileName = "";

                try
                {
                    while ((data = sReader.ReadLine()) != EXIT )
                    {
                        switch(data)
                        {
                            case OPEN:
                                Acquittement(data, sWriter, rnd);
                                break;
                            case PUSH:
                                // Ack PUSH
                                Acquittement(data, sWriter, rnd);
                                // Read file name
                                var filename = sReader.ReadLine();
                                Acquittement(filename, sWriter, rnd);
                                // Read size name
                                var filesize = Convert.ToUInt64(sReader.ReadLine());
                                Acquittement($"Expected: {filesize} bytes", sWriter, rnd);
                                // Read socket stream and write file stream
                                using (var fs = new FileStream(Path.Combine(RecieveFolder, filename), FileMode.Create))
                                {
                                    var buffer = new byte[BUFFER_SIZE];
                                    int readCount = 0;
                                    ulong readTotal = 0;
                                    while (readTotal < filesize)
                                    {
                                        readTotal += (ulong)(readCount = sReader.BaseStream.Read(buffer, 0, BUFFER_SIZE));
                                        fs.Write(buffer, 0, readCount);
                                    }
                                    Acquittement($"Recieved: {readTotal} bytes", sWriter, rnd);
                                }
                                break;
                            case LIST:
                                break;
                            case PULL:
                                if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\" + "importDir"))
                                {
                                    var import = File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\importDir\\4Item.json"));
                                    foreach (string line in import.Split('\n'))
                                    {
                                        sWriter.WriteLine(line + "\n");
                                        sWriter.Flush();
                                    }
                                    Console.WriteLine("syncok");
                                }
                                break;
                            default:
                                Acquittement($"Unknown data : {data}", sWriter, rnd);
                                break;
                        }
                        //if (data.Contains("EndFile"))
                        //{
                        //    IsFileTransferStarted = false;
                        //    File.AppendAllText(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\" + "outputDir" + "\\" + courantFileName, result , Encoding.UTF8);
                        //    Console.WriteLine("File received");
                        //    result = "";
                        //    sWriter.WriteLine("Received:"+courantFileName);
                        //    sWriter.Flush();
                        //}

                        //else if (data.Contains("FileName:"))
                        //{
                        //    if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\" + "outputDir"))
                        //    {
                        //        Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\" + "outputDir");
                        //    }
                            
                        //    courantFileName = data.Substring(data.IndexOf(":") + 1) + ".json";
                        //    var stream = System.IO.File.CreateText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\outputDir\\", courantFileName));
                        //    stream.Close();
                        //    IsFileTransferStarted = true;
                        //}

                        //else if(IsFileTransferStarted == true)
                        //{
                        //    result += data+"\n";
                        //}

                        //else if(data.Contains("Import"))
                        //{
                        //    if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\" + "importDir"))
                        //    {
                        //        var import = File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\importDir\\4Item.json"));
                        //        foreach (string line in import.Split('\n'))
                        //        {
                        //            sWriter.WriteLine(line+"\n");
                        //            sWriter.Flush();
                        //        }
                        //        Console.WriteLine("syncok");
                        //    }   
                        //}
                        //else
                        //{
                        //    Console.WriteLine("from clt on thread "+ Thread.CurrentThread.ManagedThreadId.ToString() +" : "+ data);
                        //    sWriter.WriteLine("ok dac "+rnd.Next(1000,9000).ToString());
                        //    sWriter.Flush();
                        //}
                    }
                }
                finally
                {
                    //sWriter.WriteLine("OK:Exit\n");
                    //sWriter.Flush();
                    Acquittement(data, sWriter, rnd);
                    sReader.Close();
                    sWriter.Close();
                    //Console.WriteLine("exit from clt on thread "+ Thread.CurrentThread.ManagedThreadId.ToString());
                }
            }

            static void Acquittement(string message, StreamWriter writer, Random rnd)
            {
                Console.WriteLine($"from clt on thread {Thread.CurrentThread.ManagedThreadId} : {message}");
                writer.WriteLine($"[{rnd.Next(1000, 9000)}] {ACK} {message}");
                writer.Flush();
            }
        } 
    }
}
