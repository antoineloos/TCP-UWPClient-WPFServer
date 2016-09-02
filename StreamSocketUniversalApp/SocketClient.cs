using Prism.Mvvm;
using StreamSocketUniversalApp.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;

namespace StreamSocketUniversalApp
{
    public sealed class SocketClient : BindableBase
    {
        const int BUFFER_SIZE = 4096;

        const string OPEN = "[open]";
        const string EXIT = "[exit]";
        const string PUSH = "[push]";
        const string CTRL = "[ctrl]";
        const string LIST = "[list]";
        const string PULL = "[pull]";
        const string ACK = "[ack]";

        private StreamSocket _socket;

        private string numClt;
        public string NumClt
        {
            get { return numClt; }
            set { SetProperty(ref numClt, value); }
        }

        private string message;
        public string Message
        {
            get { return message; }
            set { SetProperty(ref message, value); }
        }

        private string received;
        public string Received
        {
            get { return received; }
            set { SetProperty(ref received, value); }
        }


        public delegate void Error(string message);
        public event Error OnError;

        public string Ip { get; }
        public int Port { get; }

        public SocketClient(string ip, int port)
        {
            Ip = ip;
            Port = port;

            NumClt = $"Client {new Random().Next(1000, 9000)}";
            Message = "";
            Received = "";

            CreateInputDbFolder();
        }



        private async void Listen()
        {
            using (DataReader reader = new DataReader(_socket.InputStream) { InputStreamOptions = InputStreamOptions.Partial, ByteOrder = ByteOrder.LittleEndian, UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8 })
            {
                string result = "";
                List<string> headers = null;
                StringBuilder headerBuilder = null;
                try
                {
                    await reader.LoadAsync(BUFFER_SIZE);
                    while (reader.UnconsumedBufferLength > 0)
                    {
                        result = reader.ReadString(reader.UnconsumedBufferLength);
                        
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            Received = result;
                        });

                        if (result.StartsWith(CTRL))
                        {
                            headerBuilder = new StringBuilder();
                            headers = new List<string>();
                            Send(LIST);
                        }

                        if (result.Contains(LIST))
                        {
                            headerBuilder.Append(result);
                        }

                        if (result.Contains(PULL))
                        {
                            var lists = headerBuilder.Replace("\r\n", "§").ToString().Split('§');
                            headers.AddRange(lists.Where(h => h.StartsWith(LIST)).Select(h => h.Remove(0, LIST.Length+1)));
                            headers.ForEach(async (header) => await Receive(header));
                        }

                        if (result.StartsWith(EXIT))
                            reader?.DetachStream();
                        else
                            await reader.LoadAsync(BUFFER_SIZE);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
        }

        public async void CreateInputDbFolder()
        {
            var folder = ApplicationData.Current.LocalFolder;
            await folder.CreateFolderAsync("InputDB", CreationCollisionOption.OpenIfExists);
        }

        public async void Connect()
        {
            try
            {
                var hostName = new HostName(Ip);
                _socket = new StreamSocket();
                await _socket.ConnectAsync(hostName, Port.ToString());

                await Task.Factory.StartNew(Listen);
                Send(OPEN);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message);
            }
        }

        public async Task Receive(string header)
        {
            await Task.Factory.StartNew(async () =>
            {
                string filename = "";
                try
                {
                    var socket = new StreamSocket();
                    await socket.ConnectAsync(new HostName(Ip), $"{Port + 2}");
                    var outputStream = socket.OutputStream;
                    var inputStream = socket.InputStream;
                    byte[] buffer = new byte[BUFFER_SIZE];
                    
                    filename = header.Split('|')[0];
                    var filesize = Convert.ToUInt64(header.Split('|')[1]);

                    // Envoi du nom du fichier à recevoir
                    var read = Encoding.UTF8.GetBytes(header, 0, header.Length, buffer, 0);
                    await outputStream.WriteAsync(buffer.AsBuffer(0, read));
                    await outputStream.FlushAsync();

                    // Creation du répertoire local
                    FileInfo fi = new FileInfo(Path.Combine(ApplicationData.Current.LocalFolder.Path, filename));
                    if (fi.DirectoryName != ApplicationData.Current.LocalFolder.Path && !Directory.Exists(fi.DirectoryName))
                    {
                        Stack<string> parents = new Stack<string>();
                        var currentDirectory = fi.Directory;
                        while (!Directory.Exists(currentDirectory.FullName))
                        {
                            parents.Push(currentDirectory.Name);
                            currentDirectory = currentDirectory.Parent;
                        }
                        var currentFolder = await StorageFolder.GetFolderFromPathAsync(currentDirectory.FullName);
                        while (currentFolder.Path != fi.DirectoryName)
                        {
                            currentFolder = await currentFolder.CreateFolderAsync(parents.Pop(), CreationCollisionOption.OpenIfExists);
                        }
                    }
                    var folder = await StorageFolder.GetFolderFromPathAsync(fi.DirectoryName);
                    var file = await folder.CreateFileAsync(fi.Name, CreationCollisionOption.ReplaceExisting);

                    // Envoi
                    int readCount = 0;
                    ulong readTotal = 0;
                    var pendingWrites = new List<IAsyncOperationWithProgress<uint, uint>>();

                    using (var writeStream = await file.OpenStreamForWriteAsync())
                    {
                        using (var readStream = inputStream.AsStreamForRead())
                        {
                            while (readTotal < filesize)
                            {
                                readTotal += (ulong)(readCount = readStream.Read(buffer, 0, BUFFER_SIZE));
                                writeStream.Write(buffer, 0, readCount);
                            }
                        } 
                    }
                    socket.InputStream.Dispose();
                    socket.OutputStream.Dispose();
                    socket.Dispose();

                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Received = $"Received [{readTotal}] {Received}";
                    });
                }
                catch (Exception e)
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Received = $"Error [{filename}] {e.Message}";
                    });
                }
            });
        }

        /// <summary>
        /// Protocole PUSH
        /// Envoi d'un fichier en binaire
        /// </summary>
        /// <param name="file"></param>
        public Task<string> Send(StorageFile file, string path = "")
        {
            return Task.Factory.StartNew(async () =>
            {
                try
                {
                    var socket = new StreamSocket();
                    await socket.ConnectAsync(new HostName(Ip), $"{Port + 1}");
                    var outputStream = socket.OutputStream;
                    byte[] buffer = new byte[BUFFER_SIZE];

                    // Envoi du nom du fichier et de la taille du fichier
                    var filename = !string.IsNullOrEmpty(path) ?
                                    Path.Combine(path, file.Name) :
                                    file.Name;
                    var fileProp = await file.GetBasicPropertiesAsync();
                    var header = $"{filename}|{fileProp.Size}";
                    var read = Encoding.UTF8.GetBytes(header, 0, header.Length, buffer, 0);
                    await outputStream.WriteAsync(buffer.AsBuffer(0, read));
                    await outputStream.FlushAsync();

                    // Envoi
                    int readCount = 0;
                    ulong readTotal = 0;
                    var pendingWrites = new List<IAsyncOperationWithProgress<uint, uint>>();

                    using (var readStream = await file.OpenStreamForReadAsync())
                    {
                        while (readTotal < fileProp.Size)
                        {
                            readTotal += (ulong)(readCount = readStream.Read(buffer, 0, BUFFER_SIZE));
                            pendingWrites.Add(outputStream.WriteAsync(buffer.AsBuffer(0, readCount)));
                        }
                        await outputStream.FlushAsync();
                    }
                    socket.OutputStream.Dispose();
                    socket.Dispose();

                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Received = $"Sent [{readTotal}] {Received}";
                    });

                    return filename;
                }
                catch (Exception e)
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Received = $"Error [{file.DisplayName}] {e.Message}";
                    });
                    return "";
                }
            }).Result;
        }

        public async void Send(string message)
        {
            try
            {
                StreamWriter writer = new StreamWriter(_socket.OutputStream.AsStreamForWrite());

                await writer.WriteLineAsync(message);
                await writer.FlushAsync();
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message);
            }
        }

        public async void Send()
        {
            await Task.Run(() => Send(Message));
        }

        public void Push()
        {
            Send(PUSH);
        }

        public void Control(List<string> files)
        {
            Send(CTRL);
            Send($"{files.Count}");
            foreach (var file in files)
            {
                Send(file);
            }
        }

        public void List(/*List<string> files*/)
        {
            Send(LIST);
            //Send($"{files.Count}");
            //foreach (var file in files)
            //{
            //    Send(file);
            //}
        }

        public void Close()
        {
            try
            {
                //IsConnected = false;
                //IsAlive = false;

                Send(EXIT);

                _socket.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }
    }
}
