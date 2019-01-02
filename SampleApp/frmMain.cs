using EasyTabs;
using SampleApp.Properties;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SampleApp
{
    public partial class frmMain : Form
    {
        public frmMain()
        {
            InitializeComponent();

            backButton.MouseEnter += (sender, e) => backButton.BackgroundImage = Resources.ButtonHoverBackground;
            backButton.MouseLeave += (sender, e) => backButton.BackgroundImage = null;
            backButton.Click += (sender, e) => webBrowser.GoBack();

            forwardButton.MouseEnter += (sender, e) => forwardButton.BackgroundImage = Resources.ButtonHoverBackground;
            forwardButton.MouseLeave += (sender, e) => forwardButton.BackgroundImage = null;
            forwardButton.Click += (sender, e) => webBrowser.GoForward();

            urlTextBox.KeyDown += UrlTextBoxKeyDown;

            webBrowser.Url = new Uri(urlTextBox.Text);
            webBrowser.DocumentCompleted += WebBrowserDocumentCompleted;
        }

        /// <summary>
        /// Gets the parent <see cref="EasyTabs.TitleBarTabs"/> of this window
        /// </summary>
        protected TitleBarTabs ParentTabs => (ParentForm as TitleBarTabs);

        /// <summary>
        /// Gets the website's favicon
        /// </summary>
        /// <param name="url">Url to query</param>
        /// <returns>Icon</returns>
        private Icon GetIconFromAddress(string url)
        {
            Icon icon = null;
            try
            {
                WebRequest webRequest = WebRequest.Create(url);
                WebResponse response = webRequest.GetResponse();
                Stream stream = response.GetResponseStream();
                if (stream != null)
                {
                    byte[] buffer = new byte[1024];
                    using (MemoryStream ms = new MemoryStream())
                    {
                        int read;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                            ms.Write(buffer, 0, read);
                        ms.Seek(0, SeekOrigin.Begin);
                        icon = new Icon(ms);
                        ParentTabs.UpdateThumbnailPreviewIcon(ParentTabs.Tabs.Single(t => t.Content == this));
                        ParentTabs.RedrawTabs();
                    }
                }
            }
            catch
            {
                return Resources.DefaultIcon;
            }
            return icon;
        }

        private void WebBrowserDocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (urlTextBox.Text != "about:blank")
            {
                Text = webBrowser.DocumentTitle;
                urlTextBox.Text = webBrowser.Url.ToString();
                if (webBrowser.Url.Scheme == "http" || webBrowser.Url.Scheme == "https")
                    Icon = GetIconFromAddress($"{webBrowser.Url.Scheme}://{webBrowser.Url.Host}/favicon.ico");
                Parent.Refresh();
            }
        }

        private void UrlTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                string fullUrl = urlTextBox.Text;
                if (!Regex.IsMatch(fullUrl, "^[a-zA-Z0-9]+\\://"))
                    fullUrl = "http://" + fullUrl;
                Uri uri = new Uri(fullUrl);
                webBrowser.Navigate(uri);
            }
        }
    }
}
