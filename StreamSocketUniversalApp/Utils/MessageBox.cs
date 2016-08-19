using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Popups;

namespace StreamSocketUniversalApp.Utils
{
    public class MessageBox
    {
        public static async void Show(string ex)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                var dlg = new MessageDialog(ex);
                await dlg.ShowAsync();
            });

        }
    }
}
