using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CefSharp;
using CefSharp.Wpf;
using System.IO;
using Newtonsoft.Json;
using System.ComponentModel;
using XamlAnimatedGif;

namespace RandomDash
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		string dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\RandomDash\\temp";
		public MainWindow()
		{
			InitializeComponent();

			var settings = new CefSettings();
			if (Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}
			settings.CachePath = dir;
			Cef.Initialize(settings);
			browser = new ChromiumWebBrowser("https://www.doordash.com/consumer/edit_profile/");
			browser.MenuHandler = new CustomMenuHandler();
			browser.HorizontalAlignment = HorizontalAlignment.Stretch;
			browser.VerticalAlignment = VerticalAlignment.Stretch;
			browser.Margin = new Thickness(0);
			browser.Width = double.NaN;
			browser.Height = double.NaN;
			browser.Visibility = Visibility.Hidden;
			browser.SetValue(Grid.RowProperty, 1);
			browser.SetValue(Grid.ColumnProperty, 0);
			browser.SetValue(Grid.ColumnSpanProperty, 2);
			Grid.Children.Add(browser);

			bgGetItems.DoWork += BgGetItems_DoWork;
			bgGetItems.ProgressChanged += BgGetItems_ProgressChanged;
			bgGetItems.RunWorkerCompleted += BgGetItems_RunWorkerCompleted;
			bgGetRests.DoWork += BgGetRests_DoWork;
			bgGetRests.ProgressChanged += BgGetRests_ProgressChanged;
			bgGetRests.RunWorkerCompleted += BgGetRests_RunWorkerCompleted;

			timer.Tick += Timer_Tick;
			timer.Start();

			timerStart.Tick += TimerStart_Tick;
			timerStart.Start();

			AnimationBehavior.SetSourceUri(imageLoading, new Uri("Loading red.gif", UriKind.Relative));
			AnimationBehavior.SetSourceUri(imageLoadingStart, new Uri("Loading red.gif", UriKind.Relative));

			if (File.Exists(dir + "\\prefs"))
			{
				DarkMode();
			}
		}

		private void TimerStart_Tick(object sender, EventArgs e)
		{
			if (browser != null)
			{
				if (!browser.IsLoading)
				{
					if (browser.Address.Contains("identity.doordash.com"))
					{
						browser.Visibility = Visibility.Visible;
						panelStart.Visibility = Visibility.Hidden;
					}
					else if (browser.Address.StartsWith("https://www.doordash.com/consumer/edit_profile"))
					{
						browser.Visibility = Visibility.Hidden;
						panelStart.Visibility = Visibility.Visible;
						labelStart.Content = "Let's get started.";
						buttonStart.Visibility = Visibility.Visible;
						imageLoadingStart.Visibility = Visibility.Collapsed;
						timerStart.Stop();
					}
				}
			}
		}

		public BackgroundWorker bgGetRests = new BackgroundWorker()
		{
			WorkerReportsProgress = true
		};
		public BackgroundWorker bgGetItems = new BackgroundWorker()
		{
			WorkerReportsProgress = true
		};

		public System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer()
		{
			Interval = 100,
		};

		public System.Windows.Forms.Timer timerStart = new System.Windows.Forms.Timer()
		{
			Interval = 1,
		};

		public ChromiumWebBrowser browser;
		public class CustomMenuHandler : IContextMenuHandler
		{
			public void OnBeforeContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model)
			{
				model.Clear();
			}
			public bool OnContextMenuCommand(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
			{
				return false;
			}
			public void OnContextMenuDismissed(IWebBrowser browserControl, IBrowser browser, IFrame frame) { }
			public bool RunContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
			{
				return false;
			}
		}

		Request timerRequest = new Request();
		Response timerResponse = new Response();
		private void Timer_Tick(object sender, EventArgs e)
		{
			switch (timerRequest.type)
			{
				case RequestType.Evaluate:
					timerResponse.java = browser.GetMainFrame().EvaluateScriptAsync(timerRequest.javascript).Result;
					timerResponse.lastEval = timerRequest.javascript;
					break;
				case RequestType.Execute:
					browser.GetMainFrame().ExecuteJavaScriptAsync(timerRequest.javascript);
					timerResponse.lastExec = timerRequest.javascript;
					break;
				case RequestType.GetHtml:
					timerResponse.html = browser.GetSourceAsync().Result;
					break;
				case RequestType.IsLoading:
					timerResponse.IsLoading = browser.IsLoading;
					break;
			}
			timerRequest.Clear();
		}

		void ExecBrowser(Request request)
		{
			timerRequest.javascript = request.javascript;
			timerRequest.type = request.type;
		}

		int totalRests = 1;
		int scrollTo = 0;
		List<Restaurant> restaurants = new List<Restaurant>();
		string getRestsOutput = "";
		private void BgGetRests_DoWork(object sender, DoWorkEventArgs e)
		{
			while (browser == null) { }
			ExecBrowser(new Request() { type = RequestType.IsLoading });
			while (!timerResponse.IsLoading.HasValue)
			{
				if (cancel) { return; }
				ExecBrowser(new Request() { type = RequestType.IsLoading });
			}
			while (!timerResponse.IsLoading.Value)
			{
				if (cancel) { return; }
				ExecBrowser(new Request() { type = RequestType.IsLoading });
			}
			timerResponse.Clear();
			while (totalRests > restaurants.Count)
			{
				if (cancel) { return; }
				restaurants.Clear();
				ExecBrowser(new Request() { type = RequestType.GetHtml, });
				while (timerResponse.html == null) { }
				var html = timerResponse.html.Replace("`", "").Replace("><", "`").Replace("<", "`").Replace(">", "`").Split('`');
				timerResponse.Clear();
				for (int i = 0; i < html.Length; i++)
				{
					if (cancel) { return; }
					if (html[i] == "span class=\"sc-ifAKCX kWBhob\" display=\"block\"")
					{
						totalRests = int.Parse(html[i + 1].Replace(" stores nearby", ""));
					}
					if (html[i].StartsWith("a class=\"sc-fxmata iGzBfu sc-qrIAp drGHNA\""))
					{
						var href = "https://doordash.com" + html[i].Split(new string[] { "href=\"" }, StringSplitOptions.None)[1].Split('"')[0];
						restaurants.Add(new Restaurant
						{
							url = href
						});
					}
					if (html[i] == "span class=\"sc-fxMfqs dvdcrb sc-ifAKCX fBFfSn\" display=\"block\"" && restaurants.Count > 0)
					{
						restaurants[restaurants.Count - 1].name = html[i + 1].Replace("&amp;", "&");
					}
				}

				if (restaurants.Count > 0)
				{
					getRestsOutput = $"Getting restaurants near you ({restaurants.Count}/{totalRests})...";
					bgGetRests.ReportProgress(0);
				}
				else
				{
					getRestsOutput = $"Getting restaurants near you...";
					bgGetRests.ReportProgress(0);
				}
				

				scrollTo += 2000;
				ExecBrowser(new Request()
				{
					type = RequestType.Execute,
					javascript = "scrollTo(0," + scrollTo + ");",
				});
				System.Threading.Thread.Sleep(1000);
			}
		}

		private void BgGetRests_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			labelStatus.Content = getRestsOutput;
		}

		private void BgGetRests_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if (!cancel)
			{
				File.WriteAllText(dir + "\\restaurants.json", JsonConvert.SerializeObject(restaurants));

				var index = rand.Next(0, restaurants.Count);
				var theRest = restaurants[index];
				browser.Load(theRest.url);
			
				bgGetItems.RunWorkerAsync();
			}
			cancel = false;
		}

		int itemsWanted = 1;
		string getItemsOutput = "";
		bool waitForLoad = true;
		private void BgGetItems_DoWork(object sender, DoWorkEventArgs e)
		{
			if (waitForLoad)
			{
				System.Threading.Thread.Sleep(2000);
			}
			waitForLoad = true;

			while (browser == null) { }
			ExecBrowser(new Request() { type = RequestType.IsLoading });
			while (!timerResponse.IsLoading.HasValue)
			{
				if (cancel) { return; }
				ExecBrowser(new Request() { type = RequestType.IsLoading });
			}
			while (timerResponse.IsLoading.Value == true)
			{
				if (cancel) { return; }
				ExecBrowser(new Request() { type = RequestType.IsLoading });
			}
			timerResponse.Clear();

			ExecBrowser(new Request()
			{
				type = RequestType.Evaluate,
				javascript = "document.getElementsByClassName('sc-kGsDXJ fvLYbn').length",
			});
			while (timerResponse.java == null)
			{
				if (cancel) { return; }
				ExecBrowser(new Request()
				{
					type = RequestType.Evaluate,
					javascript = "document.getElementsByClassName('sc-kGsDXJ fvLYbn').length",
				});
			}
			while (timerResponse.java.Message != null || !timerResponse.java.Success)
			{
				if (cancel) { return; }
				ExecBrowser(new Request()
				{
					type = RequestType.Evaluate,
					javascript = "document.getElementsByClassName('sc-kGsDXJ fvLYbn').length",
				});
			}
			while (timerResponse.java.Result.ToString() == "0")
			{
				if (cancel) { return; }
				ExecBrowser(new Request()
				{
					type = RequestType.Evaluate,
					javascript = "document.getElementsByClassName('sc-kGsDXJ fvLYbn').length",
				});
			}
			var length = 0;
			while (!int.TryParse(timerResponse.java.Result.ToString(), out length))
			{
				if (cancel) { return; }
				ExecBrowser(new Request()
				{
					type = RequestType.Evaluate,
					javascript = "document.getElementsByClassName('sc-kGsDXJ fvLYbn').length",
				});
			}
			var lengthResponse = timerResponse.java;
			timerResponse.Clear();
			var possibleItems = new List<int>();
			for (int j = 0; j < length; j++)
			{
				if (cancel) { return; }
				possibleItems.Add(j);
			}

			for (int j = 0; j < itemsWanted; j++)
			{
				if (cancel) { return; }
				getItemsOutput = $"Choosing a random item ({j + 1}/{itemsWanted})...";
				bgGetItems.ReportProgress(0);

				var listIndex = rand.Next(0, possibleItems.Count);
				var itemIndex = possibleItems[listIndex];
				possibleItems.RemoveAt(listIndex);
				var isItemButton = false;
				while (!isItemButton)
				{
					listIndex = rand.Next(0, possibleItems.Count);
					itemIndex = possibleItems[listIndex];
					possibleItems.RemoveAt(listIndex);

					if (cancel) { return; }
					ExecBrowser(new Request()
					{
						type = RequestType.Evaluate,
						javascript = "document.getElementsByClassName('sc-kGsDXJ fvLYbn')[" + itemIndex + "].innerHTML.includes('picture')",
					});
					while (timerResponse.java == null)
					{
						if (cancel) { return; }
						ExecBrowser(new Request()
						{
							type = RequestType.Evaluate,
							javascript = "document.getElementsByClassName('sc-kGsDXJ fvLYbn')[" + itemIndex + "].innerHTML.includes('sc-ifAKCX buPVMR')",
						});
					}
					while (timerResponse.java.Message != null || !timerResponse.java.Success)
					{
						if (cancel) { return; }
						ExecBrowser(new Request()
						{
							type = RequestType.Evaluate,
							javascript = "document.getElementsByClassName('sc-kGsDXJ fvLYbn')[" + itemIndex + "].innerHTML.includes('sc-ifAKCX buPVMR')",
						});
					}
					while (!bool.TryParse(timerResponse.java.Result.ToString(), out isItemButton))
					{
						if (cancel) { return; }
						ExecBrowser(new Request()
						{
							type = RequestType.Evaluate,
							javascript = "document.getElementsByClassName('sc-kGsDXJ fvLYbn')[" + itemIndex + "].innerHTML.includes('sc-ifAKCX buPVMR')",
						});
					}
					timerResponse.Clear();
					if (isItemButton)
					{
						break;
					}
				}

				//click item
				var clickItemCmd = $"document.getElementsByClassName('sc-kGsDXJ fvLYbn')[{itemIndex}].click()";
				ExecBrowser(new Request()
				{
					type = RequestType.Execute,
					javascript = clickItemCmd,
				});
				while (timerResponse.lastExec != clickItemCmd)
				{
					if (cancel) { return; }
					ExecBrowser(new Request()
					{
						type = RequestType.Execute,
						javascript = clickItemCmd,
					});
				}

				ExecBrowser(new Request()
				{
					type = RequestType.Evaluate,
					javascript = "document.getElementsByClassName('sc-hMqMXs fyyYZp').length",
				});
				while (timerResponse.java == null)
				{
					if (cancel) { return; }
					ExecBrowser(new Request()
					{
						type = RequestType.Evaluate,
						javascript = "document.getElementsByClassName('sc-hMqMXs fyyYZp').length",
					});
				}
				while (timerResponse.java.Message != null || !timerResponse.java.Success)
				{
					if (cancel) { return; }
					ExecBrowser(new Request()
					{
						type = RequestType.Evaluate,
						javascript = "document.getElementsByClassName('sc-hMqMXs fyyYZp').length",
					});
				}
				var length2 = 0;
				while (!int.TryParse(timerResponse.java.Result.ToString(), out length2))
				{
					if (cancel) { return; }
					ExecBrowser(new Request()
					{
						type = RequestType.Evaluate,
						javascript = "document.getElementsByClassName('sc-hMqMXs fyyYZp').length",
					});
				}
				while (length == 0)
				{
					if (cancel) { return; }
					ExecBrowser(new Request()
					{
						type = RequestType.Evaluate,
						javascript = "document.getElementsByClassName('sc-hMqMXs fyyYZp').length",
					});
				}
				timerResponse.Clear();

			/*	//click add to cart
				ExecBrowser(new Request()
				{
					type = RequestType.Execute,
					javascript = $"document.getElementsByClassName('sc-hMqMXs fyyYZp')[{length2 - 1}].click()",
				});
				System.Threading.Thread.Sleep(2000);*/

				/*getItemsOutput = $"Preparing for checkout...";
				bgGetItems.ReportProgress(0);

				//click cart button
				var clickCartCmd = "document.getElementsByClassName('sc-kGsDXJ ciCRAb')[0].click()";
				ExecBrowser(new Request()
				{
					type = RequestType.Execute,
					javascript = clickCartCmd,
				});
				System.Threading.Thread.Sleep(1000);
				while (timerResponse.lastExec != clickCartCmd)
				{
					ExecBrowser(new Request()
					{
						type = RequestType.Execute,
						javascript = clickCartCmd,
					});
				}

				//click checkout
				ExecBrowser(new Request()
				{
					type = RequestType.Execute,
					javascript = "document.getElementsByClassName('sc-fNFDGM hXQqwA sc-qrIAp drGHNA')[0].click()",
				});
				System.Threading.Thread.Sleep(1000);*/
			}
		}

		private void BgGetItems_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			labelStatus.Content = getItemsOutput;
		}

		private void BgGetItems_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if (!cancel)
			{
				browser.Visibility = Visibility.Visible;
				panelStatus.Visibility = Visibility.Hidden;
				panelFinish.Visibility = Visibility.Visible;
			}
			cancel = false;
		}

		Random rand = new Random();
		class Restaurant
		{
			public string url;
			public string name;
		}

		class Request
		{
			public string javascript;
			public RequestType type = RequestType.None;
			public void Clear()
			{
				javascript = null;
				type = RequestType.None;
			}
		}
		
		enum RequestType
		{
			GetHtml,
			Execute,
			Evaluate,
			IsLoading,
			None
		}
		
		class Response
		{
			public string html;
			public JavascriptResponse java;
			public bool? IsLoading;
			public string lastExec;
			public string lastEval;
			public void Clear()
			{
				html = null;
				java = null;
				IsLoading = null;
				lastExec = null;
				lastEval = null;
			}
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
		/*	var restaurants = JsonConvert.DeserializeObject<List<Restaurant>>(File.ReadAllText(@"C:\CR\restaurants.json"));
			var output = "";
			for (int i = 0; i <25; i++)
			{
				var index = rand.Next(0, restaurants.Count);
				var theRest = restaurants[index];
				output += theRest.name +"        ";
			}*/
		}

		bool cancel = false;
		private void buttonBack_Click(object sender, RoutedEventArgs e)
		{
			if (!checkboxHood.IsChecked.GetValueOrDefault())
			{
				browser.Visibility = Visibility.Hidden;
			}
			buttonBack.Visibility = Visibility.Hidden;
			panelRefresh.Visibility = Visibility.Hidden;
			panelStatus.Visibility = Visibility.Hidden;
			panelFinish.Visibility = Visibility.Hidden;

			panelStart.Visibility = Visibility.Visible;

			cancel = true;
		}

		private void buttonStart_Click(object sender, RoutedEventArgs e)
		{
			panelStart.Visibility = Visibility.Hidden;
			buttonBack.Visibility = Visibility.Visible;
			if (Directory.Exists(dir))
			{
				if (File.Exists(dir + "\\restaurants.json"))
				{
					var tempRest = JsonConvert.DeserializeObject<List<Restaurant>>(File.ReadAllText(dir + "\\restaurants.json"));
					panelRefresh.Visibility = Visibility.Visible;
					runRefreshCount.Text = $"(Last total near you was {tempRest.Count}.)";
					return;
				}
			}

			labelStatus.Content = "Getting all nearby restaurants...";
			panelStatus.Visibility = Visibility.Visible;
			browser.Load("https://www.doordash.com/filters/");
			bgGetRests.RunWorkerAsync();
		}

		private void buttonRefreshYes_Click(object sender, RoutedEventArgs e)
		{
			panelRefresh.Visibility = Visibility.Hidden;

			labelStatus.Content = "Getting all nearby restaurants...";
			panelStatus.Visibility = Visibility.Visible;
			browser.Load("https://www.doordash.com/filters/");
			bgGetRests.RunWorkerAsync();
		}

		private void buttonRefreshNo_Click(object sender, RoutedEventArgs e)
		{
			panelRefresh.Visibility = Visibility.Hidden;

			labelStatus.Content = "Choosing a random restaurant...";
			panelStatus.Visibility = Visibility.Visible;
			
			if (File.Exists(dir + "\\restaurants.json"))
			{
				restaurants = JsonConvert.DeserializeObject<List<Restaurant>>(File.ReadAllText(dir + "\\restaurants.json"));
				var index = rand.Next(0, restaurants.Count);
				var theRest = restaurants[index];
				browser.Load(theRest.url);
				bgGetItems.RunWorkerAsync();
			}
			else
			{
				labelStatus.Content = "Getting all nearby restaurants...";
				browser.Load("https://www.doordash.com/filters/");
				bgGetRests.RunWorkerAsync();
			}
		}

		private void checkboxHood_Click(object sender, RoutedEventArgs e)
		{
			if (checkboxHood.IsChecked.GetValueOrDefault())
			{
				browser.Visibility = Visibility.Visible;
			}
			else
			{
				browser.Visibility = Visibility.Hidden;
			}
		}

		private void buttonAgain_Click(object sender, RoutedEventArgs e)
		{
			panelFinish.Visibility = Visibility.Hidden;
			panelRefresh.Visibility = Visibility.Hidden;
			if (!checkboxHood.IsChecked.GetValueOrDefault())
			{
				browser.Visibility = Visibility.Hidden;
			}

			labelStatus.Content = "Choosing a random restaurant...";
			panelStatus.Visibility = Visibility.Visible;
			if (restaurants.Count == 0)
			{
				if (File.Exists(dir + "\\restaurants.json"))
				{
					restaurants = JsonConvert.DeserializeObject<List<Restaurant>>(File.ReadAllText(dir + "\\restaurants.json"));
				}
				else
				{
					labelStatus.Content = "Getting all nearby restaurants...";
					browser.Load("https://www.doordash.com/filters/");
					bgGetRests.RunWorkerAsync();
					return;
				}
			}
			var index = rand.Next(0, restaurants.Count);
			var theRest = restaurants[index];
			browser.Load(theRest.url);
			bgGetItems.RunWorkerAsync();
		}
		
		private void buttonNewItem_Click(object sender, RoutedEventArgs e)
		{
			panelFinish.Visibility = Visibility.Hidden;
			panelRefresh.Visibility = Visibility.Hidden;

			labelStatus.Content = "Choosing a random item...";
			panelStatus.Visibility = Visibility.Visible;
			waitForLoad = false;
			bgGetItems.RunWorkerAsync();
		}

		bool darkMode = false;
		private void imageLogo_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ClickCount >= 3)
			{
				if (!darkMode)
				{
					darkMode = true;
					if (Directory.Exists(dir))
					{
						File.WriteAllText(dir + "\\prefs", "darkMode:true");
					}
					DarkMode();
				}
				else
				{
					darkMode = false;
					if (File.Exists(dir + "\\prefs"))
					{
						File.Delete(dir + "\\prefs");
					}

					Grid.Background = new SolidColorBrush(Color.FromRgb(251, 251, 251));
					labelDark.Visibility = Visibility.Hidden;

					checkboxHood.Foreground = Brushes.Black;
					labelStart.Foreground = Brushes.Black;
					labelStatus.Foreground = Brushes.Black;
					labelDark.Foreground = Brushes.Black;
					textBlockRefresh.Foreground = Brushes.Black;
					browser.Foreground = Brushes.Black;

					AnimationBehavior.SetSourceUri(imageLoading, new Uri("Loading red.gif", UriKind.Relative));
					AnimationBehavior.SetSourceUri(imageLoadingStart, new Uri("Loading red.gif", UriKind.Relative));
				}
			}
		}

		void DarkMode()
		{
			Grid.Background = new SolidColorBrush(Color.FromRgb(43, 43, 43));
			labelDark.Visibility = Visibility.Visible;

			checkboxHood.Foreground = Brushes.White;
			labelStart.Foreground = Brushes.White;
			labelStatus.Foreground = Brushes.White;
			labelDark.Foreground = Brushes.White;
			textBlockRefresh.Foreground = Brushes.White;
			browser.Foreground = Brushes.White;

			AnimationBehavior.SetSourceUri(imageLoading, new Uri("Loading red dark.gif", UriKind.Relative));
			AnimationBehavior.SetSourceUri(imageLoadingStart, new Uri("Loading red dark.gif", UriKind.Relative));
		}
	}
}
