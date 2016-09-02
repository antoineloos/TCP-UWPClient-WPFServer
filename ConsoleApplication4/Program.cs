using System;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;
using System.Text;
using System.Threading.Tasks;

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
            const string CTRL = "[ctrl]";
            const string PULL = "[pull]";
            const string ACK = "[ack]";

            static readonly string RecieveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "outputDir");

            static readonly string ActiveData = @"C:\Users\nlepinay\Documents\Code";
            private static int _nbResponse = 0;

            static void Main(string[] args)
            {
                TcpListener listener = new TcpListener(IPAddress.Any, 5555);
                TcpListener listenerReceive = new TcpListener(IPAddress.Any, 5556);
                TcpListener listenerSend = new TcpListener(IPAddress.Any, 5557);

                listener.Start();
                Console.WriteLine("server started on the port 5555");
                listenerReceive.Start();
                Console.WriteLine("server started on the port 5556");
                listenerSend.Start();
                Console.WriteLine("server started on the port 5557");

                Task.WaitAll(
                    Task.Factory.StartNew(() =>
                    {
                        while (true)
                        {
                            TcpClient client = listener.AcceptTcpClient();
                            ThreadPool.QueueUserWorkItem(ThreadProc, client);
                        }
                    }),
                    Task.Factory.StartNew(() =>
                    {
                        while (true)
                        {
                            TcpClient clientFile = listenerReceive.AcceptTcpClient();
                            ThreadPool.QueueUserWorkItem(RecieveFile, clientFile);
                        }
                    }),
                    Task.Factory.StartNew(() =>
                    {
                        while (true)
                        {
                            TcpClient clientFile = listenerSend.AcceptTcpClient();
                            ThreadPool.QueueUserWorkItem(SendFile, clientFile);
                        }
                    })
                );
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
                                Acquittement(sWriter, data);
                                break;
                            case PUSH:
                                // Ack PUSH
                                Acquittement(sWriter, data);
                                break;
                            case CTRL:
                                Acquittement(sWriter, data);
                                var nbFile = Convert.ToInt32(sReader.ReadLine());
                                var isAllFilesPushed = true;
                                for (int i = 0; i < nbFile; i++)
                                {
                                    isAllFilesPushed &= File.Exists(Path.Combine(RecieveFolder, sReader.ReadLine()));
                                }
                                Send(sWriter, $"{CTRL} {(isAllFilesPushed ? "SUCCESS" : "FAILED")}");
                                break;
                            case LIST:
                                Acquittement(sWriter, data);
                                foreach (var file in Directory.GetFiles(ActiveData, "*.*", SearchOption.AllDirectories))
                                {
                                    FileInfo fi = new FileInfo(file);
                                    Send(sWriter, $"{LIST} {fi.FullName.Remove(0, ActiveData.Length+1)}|{fi.Length}");
                                }
                                Send(sWriter, PULL);
                                break;
                            default:
                                Acquittement(sWriter, $"Unknown data : {data}");
                                break;
                        }
                    }
                }
                finally
                {
                    Send(sWriter, data);
                    sReader.Close();
                    sWriter.Close();
                    client.Close();
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
                catch (Exception e)
                {
                    Log($"{e}");
                }
                finally
                {
                    client.Close();
                }
            }

            private static void SendFile(object obj)
            {
                var client = (TcpClient)obj;
                Log($"{PULL} SendFile connection ...");
                try
                {
                    using (var stream = client.GetStream())
                    {
                        var buffer = new byte[BUFFER_SIZE];
                        stream.Read(buffer, 0, BUFFER_SIZE);
                        var header = Encoding.UTF8.GetString(buffer);
                        var filename = header.Split('|')[0];
                        var filesize = Convert.ToUInt64(header.Split('|')[1]);

                        Log($"{PULL} Sending {filename}({filesize} bytes)");

                        var fi = new FileInfo(Path.Combine(ActiveData, filename));
                        // Read file stream and write socket stream
                        ulong readTotal = 0;
                        using (var fs = new FileStream(fi.FullName, FileMode.Open))
                        {
                            int readCount = 0;
                            while (readTotal < filesize)
                            {
                                readTotal += (ulong)(readCount = fs.Read(buffer, 0, BUFFER_SIZE));
                                stream.Write(buffer, 0, readCount);
                            }
                        }
                        Log($"{PULL} Sent: {readTotal} bytes");
                    }
                }
                catch (Exception e)
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

            private static void Send(StreamWriter writer, string message)
            {
                Log(message);
                writer.WriteLine($"{message}");
                writer.Flush();
            }
        }
    }
}
