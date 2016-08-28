using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Streams;

namespace StreamSocketUniversalApp.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        private static readonly MainWindowViewModel instance = new MainWindowViewModel();

        public static MainWindowViewModel Instance => instance;

        private string port;
        public string Port
        {
            get { return port; }
            set { SetProperty(ref port, value); }
        }

        private string adresse;
        public string Adresse
        {
            get { return adresse; }
            set { SetProperty(ref adresse, value); }
        }

        private string connexionStatus;
        public string ConnexionStatus
        {
            get { return connexionStatus; }
            set { SetProperty(ref connexionStatus, value); }
        }

        private ObservableCollection<SocketClient> lstSocketClt;
        public ObservableCollection<SocketClient> LstSocketClt
        {
            get { return lstSocketClt; }
            set { SetProperty(ref lstSocketClt, value); }
        }

        public DelegateCommand<SocketClient> ConnectCommand { get; private set; }
        public DelegateCommand<SocketClient> SendCommand { get; private set; }
        public DelegateCommand<SocketClient> CloseCommand { get; private set; }
        public DelegateCommand AddCltCommand { get; private set; }
        public DelegateCommand<SocketClient> DeleteCommand { get; private set; }
        public DelegateCommand<SocketClient> OpenCommand { get; private set; }

        private MainWindowViewModel()
        {
            Port = "5555";
            //Adresse = "10.0.0.2";
            Adresse = "127.0.0.1";
            LstSocketClt = new ObservableCollection<SocketClient>();
            ConnectCommand = new DelegateCommand<SocketClient>(ConnectFunc);
            SendCommand = new DelegateCommand<SocketClient>(SendFunc);
            CloseCommand = new DelegateCommand<SocketClient>(CloseFunc);
            AddCltCommand = new DelegateCommand(AddCltFunc);
            DeleteCommand = new DelegateCommand<SocketClient>(DeleteFunc);
            OpenCommand = new DelegateCommand<SocketClient>(OpenFunc);
        }

        public async Task<StorageFolder> OpenDlgFolder()
        {
            var folderPicker = new Windows.Storage.Pickers.FolderPicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop
            };
            folderPicker.FileTypeFilter.Add(".json");
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {

                StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", folder);
                return folder;
            }
            else
            {
                Debug.WriteLine("Operation cancelled.");
                return null;
            }
        }

        private async void OpenFunc(SocketClient client)
        {
            var folder = await OpenDlgFolder();
            if (folder == null) return;

            //await Task.Factory.StartNew(() => SendFiles(client, folder));
            await SendRepository(client, folder);
        }

        private async Task<List<Tuple<StorageFolder, string>>> GetRepository(StorageFolder folder, string path = "")
        {
            var currentFolders = new List<Tuple<StorageFolder, string>>();
            currentFolders.Add(new Tuple<StorageFolder, string>(folder, path));

            var subFolders = await folder.GetFoldersAsync();

            if (subFolders.Any())
            {
                foreach (var subFolder in subFolders)
                {
                    currentFolders.AddRange(await GetRepository(
                        subFolder,
                        !string.IsNullOrEmpty(path) ?
                            Path.Combine(path, subFolder.DisplayName) :
                            subFolder.DisplayName
                    ));
                }
            }

            return currentFolders;
        }

        private async Task SendRepository(SocketClient client, StorageFolder folder)
        {
            await GetRepository(folder)
                .ContinueWith(async task =>
                {
                    foreach (var f in task.Result)
                    {
                        await SendFiles(client, f.Item1, f.Item2);
                    }
                });
        }

        private async Task SendFiles(SocketClient client, StorageFolder folder, string path = "")
        {
            var lstFiles = await folder.GetFilesAsync();
            foreach (var file in lstFiles)
            {
                await client.Send(file, path);
            }
        }

        public void CloseAllClt()
        {
            foreach (SocketClient elem in LstSocketClt)
            {
                CloseFunc(elem);
            }
        }

        private void DeleteFunc(SocketClient client)
        {
            if (client.IsAlive)
            {
                client.Close();
            }
            LstSocketClt.Remove(client);
        }

        private void AddCltFunc()
        {
            LstSocketClt.Add(new SocketClient(Adresse, Convert.ToInt32(Port)));
        }

        private void CloseFunc(SocketClient client)
        {
            if (client.IsAlive)
            {
                client.Close();
            }
        }

        private void SendFunc(SocketClient client)
        {
            client.Send();
        }

        private void ConnectFunc(SocketClient client)
        {
            client.Connect();
        }
    }
}
