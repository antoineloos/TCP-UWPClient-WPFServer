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
                var client = (TcpClient)obj;
                Random rnd = new Random();
                StreamReader sReader = new StreamReader(client.GetStream());
                StreamWriter sWriter = new StreamWriter(client.GetStream());

                Console.WriteLine("client accepted on the thread " + Thread.CurrentThread.ManagedThreadId.ToString());

                String data = "";
                //string result = "";
                //bool IsFileTransferStarted = false;
                //string courantFileName = "";

                try
                {
                    while ((data = sReader.ReadLine()) != EXIT)
                    {
                        switch (data)
                        {
                            case OPEN:
                                Acquittement(data, sWriter, rnd);
                                break;
                            case PUSH:
                                // Ack PUSH
                                Acquittement(data, sWriter, rnd);
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

            private static void RecieveFile(object obj)
            {
                var client = (TcpClient)obj;
                Log("RecieveFile connection ...");
                using (var stream = client.GetStream())
                {
                    var buffer = new byte[BUFFER_SIZE];
                    stream.Read(buffer, 0, BUFFER_SIZE);
                    var header = Encoding.UTF8.GetString(buffer);
                    var filename = header.Split('|')[0];
                    var filesize = Convert.ToUInt64(header.Split('|')[1]);

                    Log($"Recieving {filename}({filesize} bytes)");

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
                    Log($"Recieved: {readTotal} bytes");
                }
                client.Close();
            }

            static void Log(string message)
            {
                Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] {message}");
            }

            static void Acquittement(string message, StreamWriter writer, Random rnd)
            {
                Log(message);
                writer.WriteLine($"[{rnd.Next(1000, 9000)}] {ACK} {message}");
                writer.Flush();
            }
        }
    }
}
