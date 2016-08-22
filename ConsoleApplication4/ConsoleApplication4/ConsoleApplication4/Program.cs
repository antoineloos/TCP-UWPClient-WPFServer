using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApplication4
{
    using System;
    using System.Net.Sockets;
    using System.Net;
    using System.IO;
    using System.Threading;

    namespace port_listen
    {
        class MainClass
        {


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
                
                
                //int buffersize = 512;
                // // Buffer for reading data
                
                
                //char[] buffer = null;
                //bool flag = true;

                String data = "";
                string result = "";
                bool IsFileTransferStarted = false;
                string courantFileName = "";

                try
                {

                    while ((data = sReader.ReadLine()) != "exit")
                    {
                        if (data != null)
                        {
                            if (data.Contains("EndFile"))
                            {
                                IsFileTransferStarted = false;
                                File.AppendAllText(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\" + "outputDir" + "\\" + courantFileName, result, Encoding.UTF8);
                                Console.WriteLine("File received");
                                result = "";
                                sWriter.WriteLine("Received:" + courantFileName);
                                sWriter.Flush();

                            }

                            else if (data.Contains("FileName:"))
                            {
                                if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\" + "outputDir"))
                                {
                                    Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\" + "outputDir");
                                }

                                courantFileName = data.Substring(data.IndexOf(":") + 1) + ".json";
                                var stream = System.IO.File.CreateText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\outputDir\\", courantFileName));
                                stream.Close();
                                IsFileTransferStarted = true;
                            }

                            else if (IsFileTransferStarted == true)
                            {
                                result += data + "\n";
                            }

                            else if (data.Contains("Import"))
                            {
                                if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\" + "importDir"))
                                {
                                    var import = File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\importDir\\1User.json"));
                                    foreach (string line in import.Split('\n'))
                                    {

                                        sWriter.WriteLine(line + "\n");
                                        sWriter.Flush();
                                    }

                                    Console.WriteLine("syncok");
                                }

                            }

                            else
                            {
                                Console.WriteLine("from clt on thread " + Thread.CurrentThread.ManagedThreadId.ToString() + " : " + data);
                                sWriter.WriteLine("ok dac " + rnd.Next(1000, 9000).ToString());
                                sWriter.Flush();
                            }

                        }
                    }
                        



                }
                finally
                {


                    sWriter.WriteLine("OK:Exit\n");
                    sWriter.Flush();
                    sReader.Close();
                    sWriter.Close();
                    Console.WriteLine("exit from clt on thread " + Thread.CurrentThread.ManagedThreadId.ToString());


                }
            }
        }
      
    }


}
