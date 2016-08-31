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
            private static int _nbResponse = 0;

            static void Main(string[] args)
            {
                TcpListener listener = new TcpListener(IPAddress.Any, 5555);
                TcpListener listenerFile = new TcpListener(IPAddress.Any, 5556);

                listener.Start();
                Console.WriteLine("server started on the port 5555");
                listenerFile.Start();
                Console.WriteLine("server started on the port 5556");

                var clientMgr = new Thread(new ThreadStart(() =>
                {
                    while (true)
                    {
                        TcpClient client = listener.AcceptTcpClient();
                        ThreadPool.QueueUserWorkItem(ThreadProc, client);
                    }
                }));
                var clienFiletMgr = new Thread(new ThreadStart(() =>
                {
                    while (true)
                    {
                        TcpClient clientFile = listenerFile.AcceptTcpClient();
                        ThreadPool.QueueUserWorkItem(RecieveFile, clientFile);
                    }
                }));
                clientMgr.Start();
                clienFiletMgr.Start();
            }
            private static void ThreadProc(object obj)
            {
                Console.WriteLine("client accepted on the thread " + Thread.CurrentThread.ManagedThreadId.ToString());
                var client = (TcpClient)obj;

                String data = "";
                var socketStream = client.GetStream();
                StreamReader sReader = new StreamReader(socketStream);
                StreamWriter sWriter = new StreamWriter(socketStream);
                
                try
                {
                    while ((data = sReader.ReadLine()) != EXIT)
                    {
                        switch (data)
                        {
                            case OPEN:
                                //Acquittement(data, sWriter, rnd);
                                Acquittement(sWriter, data);
                                break;
                            case PUSH:
                                // Ack PUSH
                                //Acquittement(data, sWriter, rnd);
                                Acquittement(sWriter, data);
                                break;
                            case LIST:
                                Acquittement(sWriter, data);
                                var nbFile = Convert.ToInt32(sReader.ReadLine());
                                var isAllFilesPushed = true;
                                for (int i = 0; i < nbFile; i++)
                                {
                                    isAllFilesPushed &= File.Exists(Path.Combine(RecieveFolder, sReader.ReadLine()));
                                }
                                Acquittement(sWriter, $"{LIST} {(isAllFilesPushed ? "SUCCESS" : "FAILED")}");
                                break;
                            case PULL:
                                //if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\" + "importDir"))
                                //{
                                //    var import = File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\importDir\\4Item.json"));
                                //    foreach (string line in import.Split('\n'))
                                //    {
                                //        sWriter.WriteLine(line + "\n");
                                //        sWriter.Flush();
                                //    }
                                //    Console.WriteLine("syncok");
                                //}
                                break;
                            default:
                                //Acquittement($"Unknown data : {data}", sWriter, rnd);
                                Acquittement(sWriter, $"Unknown data : {data}");
                                break;
                        }
                    }
                }
                finally
                {
                    //sWriter.WriteLine("OK:Exit\n");
                    //sWriter.Flush();
                    //Acquittement(data, sWriter, rnd);
                    Acquittement(sWriter, data);
                    sReader.Close();
                    sWriter.Close();
                    //Console.WriteLine("exit from clt on thread "+ Thread.CurrentThread.ManagedThreadId.ToString());
                }
            }

            private static void RecieveFile(object obj)
            {
                var client = (TcpClient)obj;
                Log($"{PUSH} RecieveFile connection ...");
                try
                {
                    using (var stream = client.GetStream())
                    {
                        var buffer = new byte[BUFFER_SIZE];
                        stream.Read(buffer, 0, BUFFER_SIZE);
                        var header = Encoding.UTF8.GetString(buffer);
                        var filename = header.Split('|')[0];
                        var filesize = Convert.ToUInt64(header.Split('|')[1]);

                        Log($"{PUSH} Recieving {filename}({filesize} bytes)");

                        var fi = new FileInfo(Path.Combine(RecieveFolder, filename));
                        if (!fi.Directory.Exists)
                            Directory.CreateDirectory(fi.DirectoryName);
                        // Read socket stream and write file stream
                        ulong readTotal = 0;
                        using (var fs = new FileStream(fi.FullName, FileMode.Create))
                        {
                            int readCount = 0;
                            while (readTotal < filesize)
                            {
                                readTotal += (ulong)(readCount = stream.Read(buffer, 0, BUFFER_SIZE));
                                fs.Write(buffer, 0, readCount);
                            }
                        }
                        Log($"{PUSH} Recieved: {readTotal} bytes");
                    }
                }
                catch(Exception e)
                {
                    Log($"{e}");
                }
                finally
                {
                    client.Close();
                }
            }

            static void Log(string message)
            {
                Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] {message}");
            }

            private static void Acquittement(StreamWriter writer, string message)
            {
                Log(message);
                writer.WriteLine($"{ACK}({_nbResponse++}) {message}");
                writer.Flush();
            }

            //static void Acquittement(string message, StreamWriter writer, Random rnd)
            //{
            //    Log(message);
            //    writer.WriteLine($"[{rnd.Next(1000, 9000)}] {ACK} {message}");
            //    writer.Flush();
            //}
        }
    }
}
